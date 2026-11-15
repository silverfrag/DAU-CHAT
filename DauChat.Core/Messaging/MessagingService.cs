using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using DauChat.Shared.Constants;
using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.Core.Messaging;

/// <summary>
/// Triển khai đầy đủ IMessagingService cho TV4 (Dương Thị Yến Nhi).
/// Phụ trách:
///   - Phân hệ 3: Chat cá nhân 1-1 (MOD_03_01 → MOD_03_06)
///   - Phân hệ 4.3→4.6: Phòng chat &amp; ghim thông báo (MOD_04_07 → MOD_04_15)
///   - Phân hệ 7.1: Lưu trữ Local-First Append-only .jsonl + .idx (MOD_07_01, MOD_07_02)
///
/// Kiến trúc lưu trữ (theo chuẩn Zalo):
///   LocalData/{UserId}/Messages/{ConvId}/YYYY-MM.jsonl  — append-only log
///   LocalData/{UserId}/Messages/{ConvId}/YYYY-MM.idx    — byte offsets (1 record/dòng)
///
/// Quy tắc bắt buộc:
///   - Mọi truyền tin đi qua INetworkService (TV2), không tạo TcpClient riêng.
///   - Lấy danh sách phòng/thành viên qua IAuthRoomService (TV3).
///   - Chống trùng UUID (Deduplication) bằng HashSet trong RAM + kiểm tra file.
/// </summary>
public class MessagingService : IMessagingService
{
    // ─── Dependencies (TV2 & TV3) ───────────────────────────────────────────
    private readonly INetworkService _networkService;
    private readonly IAuthRoomService _authRoomService;

    // ─── Deduplication cache (MessageId → bool) ─────────────────────────────
    // Giữ 2000 ID gần nhất trong bộ nhớ để chống trùng lặp real-time.
    private readonly ConcurrentDictionary<string, bool> _seenMessageIds = new();
    private readonly Queue<string> _seenIdQueue = new();
    private const int MaxDeduplicationCacheSize = 2000;

    // ─── JSON Options dùng chung ─────────────────────────────────────────────
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    // ─── Lock cho file I/O (tránh race condition khi nhiều luồng ghi cùng lúc) ─
    // Key: đường dẫn file .jsonl, Value: SemaphoreSlim(1,1)
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

    public MessagingService(INetworkService networkService, IAuthRoomService authRoomService)
    {
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        _authRoomService = authRoomService ?? throw new ArgumentNullException(nameof(authRoomService));

        // Lắng nghe gói tin đến từ INetworkService (TV2)
        _networkService.PacketReceived += OnPacketReceivedFromNetwork;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SỰ KIỆN (Events) cho UI / TV1
    // ═══════════════════════════════════════════════════════════════════════════
    public event EventHandler<MessageDto>? DirectMessageReceived;
    public event EventHandler<MessageDto>? RoomMessageReceived;
    public event EventHandler<(string RoomId, PinnedNoticeDto? PinnedNotice)>? PinnedAnnouncementChanged;

    // ═══════════════════════════════════════════════════════════════════════════
    // PHÂN HỆ 3: CHAT CÁ NHÂN 1-1 (MOD_03_01 → MOD_03_06)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// MOD_03_01: Mở/khởi tạo phiên trò chuyện 1-1 với người dùng đích.
    /// Đọc dữ liệu local để lấy tin nhắn cuối và số chưa đọc, trả về ConversationSummaryDto.
    /// </summary>
    public async Task<OperationResult<ConversationSummaryDto>> OpenDirectConversationAsync(
        string currentUserId,
        string partnerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
                return OperationResult<ConversationSummaryDto>.Fail("currentUserId không hợp lệ.");
            if (string.IsNullOrWhiteSpace(partnerId))
                return OperationResult<ConversationSummaryDto>.Fail("partnerId không hợp lệ.");

            // Lấy thông tin người dùng đích từ TV3
            var partnerResult = await _authRoomService.GetUserProfileAsync(partnerId, cancellationToken)
                .ConfigureAwait(false);

            var partnerName = partnerResult.Success && partnerResult.Data != null
                ? partnerResult.Data.FullName
                : partnerId;
            var partnerAvatar = partnerResult.Data?.AvatarPath;
            var partnerPresence = partnerResult.Data?.Presence ?? PresenceStatus.Offline;

            // Đọc tin nhắn cuối cùng từ local log
            var history = await GetDirectHistoryAsync(currentUserId, partnerId, 0, 1, cancellationToken)
                .ConfigureAwait(false);

            var lastMsg = history.Count > 0 ? history[^1] : null;
            var preview = lastMsg != null
                ? (lastMsg.Type == MessageType.FileAttachment
                    ? $"[Tệp] {lastMsg.AttachmentFileName}"
                    : TruncatePreview(lastMsg.Content))
                : string.Empty;

            var summary = new ConversationSummaryDto
            {
                PartnerId = partnerId,
                PartnerName = partnerName,
                PartnerAvatarPath = partnerAvatar,
                PartnerPresence = partnerPresence,
                LastMessagePreview = preview,
                LastMessageAtUtc = lastMsg?.SentAtClientUtc,
                UnreadCount = 0 // TV4 không theo dõi unread count ở lần này
            };

            return OperationResult<ConversationSummaryDto>.Ok(summary);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<ConversationSummaryDto>.Fail("Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult<ConversationSummaryDto>.Fail(
                $"Lỗi khi mở cuộc trò chuyện: {ex.Message}", "OPEN_CONV_ERROR");
        }
    }

    /// <summary>
    /// MOD_03_02: Lấy danh sách các cuộc trò chuyện 1-1 gần đây từ dữ liệu Local.
    /// Quét thư mục LocalData/{currentUserId}/Messages/ để tìm các ConvId không phải RoomId.
    /// </summary>
    public async Task<IReadOnlyList<ConversationSummaryDto>> GetDirectConversationsAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentUserId);
        var result = new List<ConversationSummaryDto>();
        var messagesRoot = GetMessagesFolder(currentUserId);
        if (!Directory.Exists(messagesRoot))
            return result;

        foreach (var dir in Directory.GetDirectories(messagesRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var convId = Path.GetFileName(dir);
            var history = await ReadLocalHistoryAsync(currentUserId, convId, 0, 1, cancellationToken)
                .ConfigureAwait(false);
            var lastMsg = history.Count > 0 ? history[^1] : null;

            if (convId.StartsWith("room_", StringComparison.OrdinalIgnoreCase) ||
                lastMsg?.IsRoomMessage == true || !string.IsNullOrWhiteSpace(lastMsg?.RoomId))
                continue;

            var partnerId = GetDirectPartnerId(currentUserId, convId, lastMsg);
            if (string.IsNullOrWhiteSpace(partnerId))
                continue;

            var partnerResult = await _authRoomService.GetUserProfileAsync(partnerId, cancellationToken)
                .ConfigureAwait(false);

            result.Add(new ConversationSummaryDto
            {
                PartnerId = partnerId,
                PartnerName = partnerResult.Data?.FullName ?? partnerId,
                PartnerAvatarPath = partnerResult.Data?.AvatarPath,
                PartnerPresence = partnerResult.Data?.Presence ?? PresenceStatus.Offline,
                LastMessagePreview = lastMsg != null
                    ? (lastMsg.Type == MessageType.FileAttachment
                        ? $"[Tệp] {lastMsg.AttachmentFileName}"
                        : TruncatePreview(lastMsg.Content))
                    : string.Empty,
                LastMessageAtUtc = lastMsg?.SentAtClientUtc,
                UnreadCount = 0
            });
        }

        result.Sort((a, b) =>
            (b.LastMessageAtUtc ?? DateTime.MinValue).CompareTo(a.LastMessageAtUtc ?? DateTime.MinValue));
        return result;
    }

    /// <summary>
    /// MOD_03_03: Gán UUID, gửi tin nhắn cá nhân qua INetworkService và ghi nối vào .jsonl Local.
    /// </summary>
    public async Task<OperationResult<MessageDto>> SendDirectMessageAsync(
        string senderId,
        string receiverId,
        string content,
        MessageType messageType = MessageType.Text,
        string? attachmentFileId = null,
        string? attachmentFileName = null,
        long? attachmentFileSize = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_networkService.IsConnected)
                return OperationResult<MessageDto>.Fail("Chưa kết nối đến Server.", "NOT_CONNECTED");

            if (string.IsNullOrWhiteSpace(content) && messageType == MessageType.Text)
                return OperationResult<MessageDto>.Fail("Nội dung tin nhắn không được rỗng.", "EMPTY_CONTENT");

            // Tạo DTO tin nhắn với UUID mới
            var message = new MessageDto
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ConversationId = GetDirectConvId(senderId, receiverId),
                SenderId = senderId,
                SenderName = _authRoomService.CurrentUser?.FullName ?? senderId,
                ReceiverId = receiverId,
                RoomId = null,
                Content = content,
                Type = messageType,
                IsRoomMessage = false,
                SentAtClientUtc = DateTime.UtcNow,
                ServerTimestampUtc = DateTime.UtcNow,
                AttachmentFileId = attachmentFileId,
                AttachmentFileName = attachmentFileName,
                AttachmentFileSize = attachmentFileSize
            };

            // Gửi qua mạng (TV2)
            var packet = NetworkPacket.Create(
                PacketType.DirectMessage,
                senderId: senderId,
                payload: message,
                targetId: receiverId,
                requiresAck: true);

            var sendResult = await _networkService.SendPacketAsync(packet, cancellationToken)
                .ConfigureAwait(false);

            if (!sendResult.Success)
                return OperationResult<MessageDto>.Fail(
                    $"Gửi thất bại: {sendResult.Message}", "SEND_FAILED");

            // Ghi nối vào local log của người gửi (phía mình)
            await AppendMessageToLocalLogAsync(senderId, message, cancellationToken)
                .ConfigureAwait(false);

            return OperationResult<MessageDto>.Ok(message);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<MessageDto>.Fail("Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult<MessageDto>.Fail(
                $"Lỗi khi gửi tin nhắn: {ex.Message}", "SEND_ERROR");
        }
    }

    /// <summary>
    /// MOD_03_05, MOD_07_02: Đọc phân trang (Lazy-load từ dưới lên) lịch sử tin nhắn 1-1
    /// từ file .jsonl/.idx.
    /// </summary>
    public Task<IReadOnlyList<MessageDto>> GetDirectHistoryAsync(
        string currentUserId,
        string partnerId,
        int pageIndex = 0,
        int pageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var convId = GetDirectConvId(currentUserId, partnerId);
        return ReadLocalHistoryAsync(currentUserId, convId, pageIndex, pageSize, cancellationToken);
    }

    /// <summary>
    /// MOD_03_06, MOD_07_02: Xóa file lịch sử tin nhắn 1-1 trên máy Client.
    /// </summary>
    public async Task<OperationResult> DeleteDirectHistoryAsync(
        string currentUserId,
        string partnerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var convId = GetDirectConvId(currentUserId, partnerId);
            var folder = GetConversationFolder(currentUserId, convId);
            await DeleteLocalHistoryFolderAsync(currentUserId, folder, cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok($"Đã xóa lịch sử trò chuyện với {partnerId}.");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail("Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Lỗi khi xóa lịch sử: {ex.Message}", "DELETE_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PHÂN HỆ 4.3→4.6: PHÒNG CHAT & GHIM THÔNG BÁO (MOD_04_07 → MOD_04_15)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// MOD_04_07: Lấy danh sách các phòng chat mà người dùng đang tham gia.
    /// Gửi yêu cầu tới Server qua INetworkService và nhận phản hồi.
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<RoomDto>>> GetJoinedRoomsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_networkService.IsConnected)
                return OperationResult<IReadOnlyList<RoomDto>>.Fail("Chưa kết nối đến Server.", "NOT_CONNECTED");

            var requestPacket = NetworkPacket.Create(
                PacketType.GetUserRoomsRequest,
                senderId: userId,
                payload: new { UserId = userId },
                targetId: "SERVER",
                correlationId: Guid.NewGuid().ToString("N"));

            var response = await _networkService.SendAndWaitAsync(
                requestPacket,
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (response == null)
                return OperationResult<IReadOnlyList<RoomDto>>.Fail("Server không phản hồi.", "TIMEOUT");

            if (response.Type == PacketType.ErrorResponse)
            {
                var err = response.GetPayload<OperationResult>();
                return OperationResult<IReadOnlyList<RoomDto>>.Fail(
                    err?.Message ?? "Lỗi không xác định từ Server.", err?.ErrorCode);
            }

            if (response.Type != PacketType.GetUserRoomsResponse)
                return OperationResult<IReadOnlyList<RoomDto>>.Fail(
                    $"Phản hồi không hợp lệ khi lấy danh sách phòng: {response.Type}.", "INVALID_RESPONSE");

            var rooms = response.GetPayload<List<RoomDto>>();
            if (rooms == null)
                return OperationResult<IReadOnlyList<RoomDto>>.Fail(
                    "Phản hồi danh sách phòng không chứa dữ liệu hợp lệ.", "INVALID_RESPONSE");

            return OperationResult<IReadOnlyList<RoomDto>>.Ok(rooms);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<IReadOnlyList<RoomDto>>.Fail("Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult<IReadOnlyList<RoomDto>>.Fail(
                $"Lỗi lấy danh sách phòng: {ex.Message}", "GET_ROOMS_ERROR");
        }
    }

    /// <summary>
    /// MOD_04_08: Vào phòng chat, tải thông báo ghim và trang lịch sử gần nhất.
    /// </summary>
    public async Task<OperationResult<(RoomDto Room, IReadOnlyList<MessageDto> RecentMessages, PinnedNoticeDto? PinnedNotice)>> EnterRoomAsync(
        string currentUserId,
        string roomId,
        int initialPageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_networkService.IsConnected)
                return OperationResult<(RoomDto, IReadOnlyList<MessageDto>, PinnedNoticeDto?)>.Fail(
                    "Chưa kết nối đến Server.", "NOT_CONNECTED");

            // Lấy thông tin phòng từ Server (thông qua TV3 đã lấy, dùng broadcast RoomChangedNotice
            // hoặc gửi GetUserRoomsRequest để lấy metadata phòng)
            var roomsResult = await GetJoinedRoomsAsync(currentUserId, cancellationToken)
                .ConfigureAwait(false);

            if (!roomsResult.Success)
                return OperationResult<(RoomDto, IReadOnlyList<MessageDto>, PinnedNoticeDto?)>.Fail(
                    roomsResult.Message, roomsResult.ErrorCode);

            var room = roomsResult.Data?.FirstOrDefault(r => r.RoomId == roomId);
            if (room == null)
                return OperationResult<(RoomDto, IReadOnlyList<MessageDto>, PinnedNoticeDto?)>.Fail(
                    $"Không tìm thấy phòng {roomId} hoặc bạn chưa là thành viên.", "ROOM_NOT_FOUND");

            // Đọc lịch sử local
            var recentMessages = await GetRoomHistoryAsync(
                currentUserId, roomId, 0, initialPageSize, cancellationToken)
                .ConfigureAwait(false);

            return OperationResult<(RoomDto, IReadOnlyList<MessageDto>, PinnedNoticeDto?)>.Ok(
                (room, recentMessages, room.PinnedNotice));
        }
        catch (OperationCanceledException)
        {
            return OperationResult<(RoomDto, IReadOnlyList<MessageDto>, PinnedNoticeDto?)>.Fail(
                "Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult<(RoomDto, IReadOnlyList<MessageDto>, PinnedNoticeDto?)>.Fail(
                $"Lỗi khi vào phòng: {ex.Message}", "ENTER_ROOM_ERROR");
        }
    }

    /// <summary>
    /// MOD_04_09: Chủ động rời khỏi phòng chat không bắt buộc.
    /// </summary>
    public async Task<OperationResult> LeaveRoomAsync(
        string currentUserId,
        string roomId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_networkService.IsConnected)
                return OperationResult.Fail("Chưa kết nối đến Server.", "NOT_CONNECTED");

            var packet = NetworkPacket.Create(
                PacketType.LeaveRoomRequest,
                senderId: currentUserId,
                payload: new { UserId = currentUserId, RoomId = roomId },
                targetId: "SERVER",
                correlationId: Guid.NewGuid().ToString("N"));

            var response = await _networkService.SendAndWaitAsync(
                packet, timeout: TimeSpan.FromSeconds(10), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (response == null)
                return OperationResult.Fail("Server không phản hồi.", "TIMEOUT");

            if (response.Type == PacketType.ErrorResponse)
            {
                var err = response.GetPayload<OperationResult>();
                return OperationResult.Fail(err?.Message ?? "Lỗi từ Server.", err?.ErrorCode);
            }

            return OperationResult.Ok($"Đã rời phòng {roomId}.");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail("Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Lỗi khi rời phòng: {ex.Message}", "LEAVE_ROOM_ERROR");
        }
    }

    /// <summary>
    /// MOD_04_10: Gửi tin nhắn broadcast vào phòng (lấy danh sách thành viên từ IAuthRoomService).
    /// </summary>
    public async Task<OperationResult<MessageDto>> SendRoomMessageAsync(
        string senderId,
        string roomId,
        string content,
        MessageType messageType = MessageType.Text,
        string? attachmentFileId = null,
        string? attachmentFileName = null,
        long? attachmentFileSize = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_networkService.IsConnected)
                return OperationResult<MessageDto>.Fail("Chưa kết nối đến Server.", "NOT_CONNECTED");

            if (string.IsNullOrWhiteSpace(content) && messageType == MessageType.Text)
                return OperationResult<MessageDto>.Fail("Nội dung tin nhắn không được rỗng.", "EMPTY_CONTENT");

            // Lấy danh sách thành viên từ TV3
            var membersResult = await _authRoomService.GetRoomMembersAsync(roomId, cancellationToken)
                .ConfigureAwait(false);

            if (!membersResult.Success)
                return OperationResult<MessageDto>.Fail(
                    $"Không lấy được danh sách thành viên: {membersResult.Message}", "MEMBERS_ERROR");

            // Tạo DTO tin nhắn
            var message = new MessageDto
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ConversationId = roomId,
                SenderId = senderId,
                SenderName = _authRoomService.CurrentUser?.FullName ?? senderId,
                ReceiverId = null,
                RoomId = roomId,
                Content = content,
                Type = messageType,
                IsRoomMessage = true,
                SentAtClientUtc = DateTime.UtcNow,
                ServerTimestampUtc = DateTime.UtcNow,
                AttachmentFileId = attachmentFileId,
                AttachmentFileName = attachmentFileName,
                AttachmentFileSize = attachmentFileSize
            };

            // Lấy danh sách targetUserId (trừ bản thân)
            var targetIds = membersResult.Data!
                .Where(u => u.UserId != senderId)
                .Select(u => u.UserId)
                .ToList();

            // Broadcast qua INetworkService (TV2 sẽ relay RAM-to-RAM hoặc PendingQueue)
            var groupPacket = NetworkPacket.Create(
                PacketType.GroupMessage,
                senderId: senderId,
                payload: message,
                targetId: roomId,
                requiresAck: true);

            var broadcastResult = await _networkService.BroadcastToUsersAsync(
                targetIds, groupPacket, cancellationToken)
                .ConfigureAwait(false);

            if (!broadcastResult.Success)
                return OperationResult<MessageDto>.Fail(
                    $"Broadcast thất bại: {broadcastResult.Message}", "BROADCAST_FAILED");

            // Ghi nối vào local log
            await AppendMessageToLocalLogAsync(senderId, message, cancellationToken)
                .ConfigureAwait(false);

            return OperationResult<MessageDto>.Ok(message);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<MessageDto>.Fail("Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult<MessageDto>.Fail(
                $"Lỗi khi gửi tin nhắn phòng: {ex.Message}", "SEND_ROOM_ERROR");
        }
    }

    /// <summary>
    /// MOD_04_12, MOD_07_02: Đọc phân trang lịch sử tin nhắn phòng từ file .jsonl/.idx Local.
    /// </summary>
    public Task<IReadOnlyList<MessageDto>> GetRoomHistoryAsync(
        string currentUserId,
        string roomId,
        int pageIndex = 0,
        int pageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => ReadLocalHistoryAsync(currentUserId, roomId, pageIndex, pageSize, cancellationToken);

    /// <summary>
    /// MOD_04_13, MOD_07_02: Xóa file lịch sử local của phòng trên máy Client.
    /// </summary>
    public async Task<OperationResult> DeleteRoomHistoryAsync(
        string currentUserId,
        string roomId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var folder = GetConversationFolder(currentUserId, roomId);
            await DeleteLocalHistoryFolderAsync(currentUserId, folder, cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok($"Đã xóa lịch sử phòng {roomId}.");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail("Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Lỗi khi xóa lịch sử phòng: {ex.Message}", "DELETE_ERROR");
        }
    }

    /// <summary>
    /// MOD_04_14: Giảng viên ghim thông báo quan trọng lên đầu phòng chat.
    /// </summary>
    public async Task<OperationResult<PinnedNoticeDto>> PinRoomAnnouncementAsync(
        string roomId,
        string lecturerId,
        string contentOrMessageId,
        bool isMessageId = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_networkService.IsConnected)
                return OperationResult<PinnedNoticeDto>.Fail("Chưa kết nối đến Server.", "NOT_CONNECTED");

            // Xây dựng PinnedNoticeDto
            var pinNotice = new PinnedNoticeDto
            {
                MessageId = isMessageId ? contentOrMessageId : Guid.NewGuid().ToString("N"),
                RoomId = roomId,
                PinnedByUserId = lecturerId,
                PinnedByName = _authRoomService.CurrentUser?.FullName ?? lecturerId,
                Content = isMessageId ? string.Empty : contentOrMessageId, // nội dung sẽ được Server điền nếu dùng MessageId
                PinnedAtUtc = DateTime.UtcNow
            };

            var payload = new
            {
                RoomId = roomId,
                LecturerId = lecturerId,
                ContentOrMessageId = contentOrMessageId,
                IsMessageId = isMessageId,
                PinnedNotice = pinNotice
            };

            var packet = NetworkPacket.Create(
                PacketType.PinAnnouncementRequest,
                senderId: lecturerId,
                payload: payload,
                targetId: roomId,
                correlationId: Guid.NewGuid().ToString("N"),
                requiresAck: true);

            var response = await _networkService.SendAndWaitAsync(
                packet, timeout: TimeSpan.FromSeconds(10), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (response == null)
                return OperationResult<PinnedNoticeDto>.Fail("Server không phản hồi.", "TIMEOUT");

            if (response.Type == PacketType.ErrorResponse)
            {
                var err = response.GetPayload<OperationResult>();
                return OperationResult<PinnedNoticeDto>.Fail(err?.Message ?? "Lỗi từ Server.", err?.ErrorCode);
            }

            // Server broadcast PinAnnouncementBroadcast cho tất cả thành viên
            // Ta cũng lưu tin nhắn ghim vào local log
            var pinnedMsg = new MessageDto
            {
                MessageId = pinNotice.MessageId,
                ConversationId = roomId,
                SenderId = lecturerId,
                SenderName = pinNotice.PinnedByName,
                RoomId = roomId,
                Content = pinNotice.Content,
                Type = MessageType.PinnedAnnouncement,
                IsRoomMessage = true,
                IsPinned = true,
                SentAtClientUtc = pinNotice.PinnedAtUtc,
                ServerTimestampUtc = pinNotice.PinnedAtUtc
            };

            await AppendMessageToLocalLogAsync(lecturerId, pinnedMsg, cancellationToken)
                .ConfigureAwait(false);

            // Phát event lên UI
            OnPinnedAnnouncementChanged(roomId, pinNotice);

            return OperationResult<PinnedNoticeDto>.Ok(pinNotice);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<PinnedNoticeDto>.Fail("Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult<PinnedNoticeDto>.Fail(
                $"Lỗi khi ghim thông báo: {ex.Message}", "PIN_ERROR");
        }
    }

    /// <summary>
    /// MOD_04_15: Giảng viên bỏ ghim thông báo khỏi đầu phòng chat.
    /// </summary>
    public async Task<OperationResult> UnpinRoomAnnouncementAsync(
        string roomId,
        string lecturerId,
        string? messageId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_networkService.IsConnected)
                return OperationResult.Fail("Chưa kết nối đến Server.", "NOT_CONNECTED");

            var payload = new { RoomId = roomId, LecturerId = lecturerId, MessageId = messageId };

            var packet = NetworkPacket.Create(
                PacketType.UnpinAnnouncementRequest,
                senderId: lecturerId,
                payload: payload,
                targetId: roomId,
                correlationId: Guid.NewGuid().ToString("N"),
                requiresAck: true);

            var response = await _networkService.SendAndWaitAsync(
                packet, timeout: TimeSpan.FromSeconds(10), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (response == null)
                return OperationResult.Fail("Server không phản hồi.", "TIMEOUT");

            if (response.Type == PacketType.ErrorResponse)
            {
                var err = response.GetPayload<OperationResult>();
                return OperationResult.Fail(err?.Message ?? "Lỗi từ Server.", err?.ErrorCode);
            }

            // Phát event lên UI (null = không còn ghim)
            OnPinnedAnnouncementChanged(roomId, null);

            return OperationResult.Ok($"Đã bỏ ghim thông báo khỏi phòng {roomId}.");
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Fail("Thao tác bị hủy.", "CANCELLED");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Lỗi khi bỏ ghim: {ex.Message}", "UNPIN_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PHÂN HỆ 7.1: LƯU TRỮ LOCAL-FIRST (MOD_07_01, MOD_07_02)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// MOD_07_01: Ghi nối tiếp (Append-only, FileMode.Append) tin nhắn vào
    /// LocalData/{UserId}/Messages/{ConvId}/YYYY-MM.jsonl kèm chỉ mục .idx.
    /// Trả về false nếu trùng MessageId (Deduplication).
    /// </summary>
    public async Task<bool> AppendMessageToLocalLogAsync(
        string currentUserId,
        MessageDto message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentUserId);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.MessageId);

        string convId;
        if (!string.IsNullOrWhiteSpace(message.RoomId) || message.IsRoomMessage)
        {
            convId = message.RoomId ?? message.ConversationId;
        }
        else
        {
            var partnerId = string.Equals(message.SenderId, currentUserId, StringComparison.Ordinal)
                ? message.ReceiverId
                : message.SenderId;
            if (string.IsNullOrWhiteSpace(partnerId))
                partnerId = message.ConversationId;
            convId = GetDirectConvId(currentUserId, partnerId);
        }

        var conversationFolder = GetConversationFolder(currentUserId, convId);
        var logFilePath = GetLogFilePath(currentUserId, convId, message.SentAtClientUtc);
        var idxFilePath = GetIdxFilePath(currentUserId, convId, message.SentAtClientUtc);
        var cacheKey = GetSeenMessageKey(currentUserId, message.MessageId);
        if (_seenMessageIds.ContainsKey(cacheKey))
            return false;

        var sem = _fileLocks.GetOrAdd(conversationFolder, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_seenMessageIds.ContainsKey(cacheKey))
                return false;

            Directory.CreateDirectory(conversationFolder);
            await EnsureIndexAsync(logFilePath, idxFilePath, cancellationToken).ConfigureAwait(false);

            foreach (var existingLog in Directory.GetFiles(
                         conversationFolder, $"*{ProtocolConstants.MessageLogExtension}"))
            {
                if (await MessageExistsInFileAsync(existingLog, message.MessageId, cancellationToken)
                        .ConfigureAwait(false))
                {
                    RegisterSeenId(cacheKey);
                    return false;
                }
            }

            await EnsureLogEndsWithNewlineAsync(logFilePath, cancellationToken).ConfigureAwait(false);
            var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOpts) + "\n");
            long byteOffset;
            await using (var fs = new FileStream(logFilePath, FileMode.Append, FileAccess.Write,
                FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                byteOffset = fs.Position;
                await fs.WriteAsync(jsonBytes, cancellationToken).ConfigureAwait(false);
            }

            var idxBytes = Encoding.UTF8.GetBytes($"{message.MessageId},{byteOffset}\n");
            await using (var idxFs = new FileStream(idxFilePath, FileMode.Append, FileAccess.Write,
                FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                await idxFs.WriteAsync(idxBytes, cancellationToken).ConfigureAwait(false);
            }

            RegisterSeenId(cacheKey);
            return true;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// MOD_03_04, MOD_04_11: Xử lý gói tin nhắn/ghim nhận từ INetworkService,
    /// chống trùng UUID, lưu Local và bắn Event lên UI.
    /// </summary>
    public async Task ProcessIncomingPacketAsync(
        string currentUserId,
        NetworkPacket packet,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentUserId);
        ArgumentNullException.ThrowIfNull(packet);

        switch (packet.Type)
        {
            case PacketType.DirectMessage:
            {
                var message = packet.GetPayload<MessageDto>();
                if (message == null)
                    throw new InvalidDataException("Gói DirectMessage không chứa MessageDto hợp lệ.");

                message.ServerTimestampUtc = packet.TimestampUtc;
                message.IsRoomMessage = false;
                var appended = await AppendMessageToLocalLogAsync(currentUserId, message, cancellationToken)
                    .ConfigureAwait(false);
                if (appended)
                    OnDirectMessageReceived(message);
                await AcknowledgePacketAsync(currentUserId, packet, cancellationToken).ConfigureAwait(false);
                break;
            }
            case PacketType.GroupMessage:
            {
                var message = packet.GetPayload<MessageDto>();
                if (message == null)
                    throw new InvalidDataException("Gói GroupMessage không chứa MessageDto hợp lệ.");

                message.ServerTimestampUtc = packet.TimestampUtc;
                message.IsRoomMessage = true;
                var appended = await AppendMessageToLocalLogAsync(currentUserId, message, cancellationToken)
                    .ConfigureAwait(false);
                if (appended)
                    OnRoomMessageReceived(message);
                await AcknowledgePacketAsync(currentUserId, packet, cancellationToken).ConfigureAwait(false);
                break;
            }
            case PacketType.PinAnnouncementBroadcast:
            {
                var notice = packet.GetPayload<PinnedNoticeDto>();
                if (notice == null)
                    throw new InvalidDataException("Gói PinAnnouncementBroadcast không chứa PinnedNoticeDto hợp lệ.");

                var pinnedMessage = new MessageDto
                {
                    MessageId = notice.MessageId,
                    ConversationId = notice.RoomId,
                    SenderId = notice.PinnedByUserId,
                    SenderName = notice.PinnedByName,
                    RoomId = notice.RoomId,
                    Content = notice.Content,
                    Type = MessageType.PinnedAnnouncement,
                    IsRoomMessage = true,
                    IsPinned = true,
                    SentAtClientUtc = notice.PinnedAtUtc,
                    ServerTimestampUtc = packet.TimestampUtc
                };

                await AppendMessageToLocalLogAsync(currentUserId, pinnedMessage, cancellationToken)
                    .ConfigureAwait(false);
                OnPinnedAnnouncementChanged(notice.RoomId, notice);
                await AcknowledgePacketAsync(currentUserId, packet, cancellationToken).ConfigureAwait(false);
                break;
            }
            case PacketType.UnpinAnnouncementBroadcast:
            {
                var notice = packet.GetPayload<PinnedNoticeDto>();
                var roomId = notice?.RoomId ?? packet.TargetId;
                if (string.IsNullOrWhiteSpace(roomId))
                    throw new InvalidDataException("Gói UnpinAnnouncementBroadcast thiếu RoomId.");

                OnPinnedAnnouncementChanged(roomId, null);
                await AcknowledgePacketAsync(currentUserId, packet, cancellationToken).ConfigureAwait(false);
                break;
            }
        }
    }

    private Task AcknowledgePacketAsync(
        string currentUserId,
        NetworkPacket packet,
        CancellationToken cancellationToken)
        => packet.RequiresAck
            ? _networkService.SendAckAsync(currentUserId, packet.PacketId, cancellationToken)
            : Task.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPER METHODS (nội bộ TV4)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Đọc phân trang lịch sử từ file .jsonl/.idx.
    /// Thuật toán: dùng .idx để xác định offset, seek ngược từ cuối (Lazy-load từ dưới lên).
    /// </summary>
    private async Task<IReadOnlyList<MessageDto>> ReadLocalHistoryAsync(
        string userId,
        string convId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ValidatePathSegment(convId, nameof(convId));
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        var result = new List<MessageDto>();
        var messagesFolder = GetConversationFolder(userId, convId);
        if (!Directory.Exists(messagesFolder))
            return result;

        var sem = _fileLocks.GetOrAdd(messagesFolder, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var logFiles = Directory.GetFiles(messagesFolder, $"*{ProtocolConstants.MessageLogExtension}")
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            var allRecords = new List<(string LogFile, long Offset)>();

            foreach (var logFile in logFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var idxFile = Path.ChangeExtension(logFile, ProtocolConstants.MessageIndexExtension);
                await EnsureIndexAsync(logFile, idxFile, cancellationToken).ConfigureAwait(false);
                var offsets = await ReadIdxOffsetsAsync(idxFile, cancellationToken).ConfigureAwait(false);
                allRecords.AddRange(offsets.Select(offset => (logFile, offset)));
            }

            long skip = (long)pageIndex * pageSize;
            long fromIndex = allRecords.Count - 1L - skip;
            if (fromIndex < 0)
                return result;

            var selectedRecords = new List<(string LogFile, long Offset)>();
            for (long i = fromIndex; i >= 0 && selectedRecords.Count < pageSize; i--)
                selectedRecords.Add(allRecords[(int)i]);
            selectedRecords.Reverse();

            foreach (var group in selectedRecords.GroupBy(record => record.LogFile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var fs = new FileStream(group.Key, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
                using var reader = new StreamReader(fs, Encoding.UTF8);

                foreach (var (_, offset) in group)
                {
                    if (offset < 0 || offset >= fs.Length)
                        continue;
                    fs.Seek(offset, SeekOrigin.Begin);
                    reader.DiscardBufferedData();
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var message = JsonSerializer.Deserialize<MessageDto>(line, JsonOpts);
                    if (message != null)
                        result.Add(message);
                }
            }
        }
        finally
        {
            sem.Release();
        }

        return result;
    }

    /// <summary>Xóa toàn bộ thư mục lịch sử (.jsonl + .idx) của một convId.</summary>
    private async Task DeleteLocalHistoryFolderAsync(
        string userId,
        string folder,
        CancellationToken cancellationToken)
    {
        var sem = _fileLocks.GetOrAdd(folder, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
            var cachePrefix = $"{userId}\0";
            foreach (var key in _seenMessageIds.Keys)
            {
                if (key.StartsWith(cachePrefix, StringComparison.Ordinal))
                    _seenMessageIds.TryRemove(key, out _);
            }
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Đọc các byte offset từ file .idx (mỗi dòng: "MessageId,ByteOffset").
    /// </summary>
    private static async Task<List<long>> ReadIdxOffsetsAsync(
        string idxFilePath, CancellationToken cancellationToken)
    {
        var offsets = new List<long>();
        await using var fs = new FileStream(idxFilePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            var separator = line.LastIndexOf(',');
            if (separator >= 0 && long.TryParse(line.AsSpan(separator + 1), out var offset) && offset >= 0)
                offsets.Add(offset);
        }
        return offsets;
    }

    private static async Task EnsureLogEndsWithNewlineAsync(
        string logFilePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(logFilePath))
            return;

        await using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.ReadWrite,
            FileShare.Read, bufferSize: 1, useAsync: true);
        if (fs.Length == 0)
            return;

        fs.Position = fs.Length - 1;
        var lastByte = new byte[1];
        await fs.ReadExactlyAsync(lastByte, cancellationToken).ConfigureAwait(false);
        if (lastByte[0] != (byte)'\n')
        {
            fs.Position = fs.Length;
            await fs.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken).ConfigureAwait(false);
            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsureIndexAsync(
        string logFilePath,
        string idxFilePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(logFilePath))
            return;

        var needsRebuild = !File.Exists(idxFilePath);
        if (!needsRebuild)
        {
            var offsets = await ReadIdxOffsetsAsync(idxFilePath, cancellationToken).ConfigureAwait(false);
            needsRebuild = offsets.Count == 0 || !await IndexCoversLogAsync(
                logFilePath, offsets[^1], cancellationToken).ConfigureAwait(false);
        }

        if (needsRebuild)
            await RebuildIndexAsync(logFilePath, idxFilePath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IndexCoversLogAsync(
        string logFilePath,
        long lastOffset,
        CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        if (lastOffset < 0 || lastOffset >= fs.Length)
            return false;

        fs.Position = lastOffset;
        var buffer = new byte[4096];
        while (true)
        {
            var bytesRead = await fs.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
                return false;

            var newlineIndex = Array.IndexOf(buffer, (byte)'\n', 0, bytesRead);
            if (newlineIndex >= 0)
                return fs.Position - bytesRead + newlineIndex + 1 == fs.Length;
        }
    }

    private static async Task RebuildIndexAsync(
        string logFilePath,
        string idxFilePath,
        CancellationToken cancellationToken)
    {
        var offsets = await ScanLogFileOffsetsAsync(logFilePath, cancellationToken).ConfigureAwait(false);
        await using var logStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        using var logReader = new StreamReader(logStream, Encoding.UTF8, leaveOpen: true);
        await using var indexStream = new FileStream(idxFilePath, FileMode.Create, FileAccess.Write,
            FileShare.Read, bufferSize: 4096, useAsync: true);
        await using var indexWriter = new StreamWriter(indexStream, Encoding.UTF8);

        foreach (var offset in offsets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logStream.Seek(offset, SeekOrigin.Begin);
            logReader.DiscardBufferedData();
            var line = await logReader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                var message = JsonSerializer.Deserialize<MessageDto>(line, JsonOpts);
                if (message != null)
                    await indexWriter.WriteLineAsync($"{message.MessageId},{offset}".AsMemory(), cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (JsonException)
            {
                // Ignore a torn/corrupt log line while rebuilding its index.
            }
        }
    }

    /// <summary>
    /// Fallback: quét .jsonl để tìm byte offsets cho từng dòng JSON (không có .idx).
    /// </summary>
    private static async Task<List<long>> ScanLogFileOffsetsAsync(
        string logFilePath, CancellationToken cancellationToken)
    {
        var offsets = new List<long>();
        await using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        var buffer = new byte[4096];
        long position = 0;
        long lineStart = 0;
        int bytesRead;

        while ((bytesRead = await fs.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (var index = 0; index < bytesRead; index++, position++)
            {
                if (buffer[index] != (byte)'\n')
                    continue;
                offsets.Add(lineStart);
                lineStart = position + 1;
            }
        }

        if (lineStart < position)
            offsets.Add(lineStart);
        return offsets;
    }

    /// <summary>
    /// Kiểm tra xem MessageId đã tồn tại trong .jsonl chưa (dùng lúc khởi động/crash recovery).
    /// Chỉ scan nếu cache không có.
    /// </summary>
    private static async Task<bool> MessageExistsInFileAsync(
        string logFilePath, string messageId, CancellationToken cancellationToken)
    {
        if (!File.Exists(logFilePath))
            return false;

        await using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(fs, Encoding.UTF8);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                var message = JsonSerializer.Deserialize<MessageDto>(line, JsonOpts);
                if (string.Equals(message?.MessageId, messageId, StringComparison.Ordinal))
                    return true;
            }
            catch (JsonException)
            {
                // Ignore a torn/corrupt log line; later valid records remain readable.
            }
        }
        return false;
    }

    /// <summary>
    /// Event handler từ INetworkService.PacketReceived — chuyển tiếp đến ProcessIncomingPacketAsync.
    /// currentUserId được lấy từ INetworkService.CurrentUserId (TV2 thiết lập sau Login).
    /// </summary>
    private void OnPacketReceivedFromNetwork(object? sender, NetworkPacket packet)
    {
        var userId = _networkService.CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            System.Diagnostics.Trace.TraceError("Received message packet without an authenticated user.");
            return;
        }

        _ = ProcessIncomingPacketFromNetworkAsync(userId, packet);
    }

    private async Task ProcessIncomingPacketFromNetworkAsync(string userId, NetworkPacket packet)
    {
        try
        {
            await ProcessIncomingPacketAsync(userId, packet, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Failed to process incoming packet {0}: {1}", packet.PacketId, ex);
        }
    }

    // ─── Các hàm phát Event ──────────────────────────────────────────────────
    protected virtual void OnDirectMessageReceived(MessageDto message)
        => DirectMessageReceived?.Invoke(this, message);

    protected virtual void OnRoomMessageReceived(MessageDto message)
        => RoomMessageReceived?.Invoke(this, message);

    protected virtual void OnPinnedAnnouncementChanged(string roomId, PinnedNoticeDto? notice)
        => PinnedAnnouncementChanged?.Invoke(this, (roomId, notice));

    // ─── Tiện ích đường dẫn file ─────────────────────────────────────────────

    /// <summary>
    /// Trả về thư mục gốc lưu tin nhắn của userId:
    /// LocalData/{userId}/Messages/
    /// </summary>
    private static string GetMessagesFolder(string userId)
    {
        ValidatePathSegment(userId, nameof(userId));
        return Path.Combine(
            ProtocolConstants.LocalDataRootFolder,
            userId,
            ProtocolConstants.MessagesFolderName);
    }

    private static string GetConversationFolder(string userId, string convId)
    {
        ValidatePathSegment(convId, nameof(convId));
        return Path.Combine(GetMessagesFolder(userId), convId);
    }

    /// <summary>
    /// Đường dẫn file .jsonl cho một tháng cụ thể:
    /// LocalData/{userId}/Messages/{convId}/YYYY-MM.jsonl
    /// </summary>
    private static string GetLogFilePath(string userId, string convId, DateTime date)
        => Path.Combine(
            GetConversationFolder(userId, convId),
            $"{date:yyyy-MM}{ProtocolConstants.MessageLogExtension}");

    /// <summary>
    /// Đường dẫn file .idx tương ứng với file .jsonl:
    /// LocalData/{userId}/Messages/{convId}/YYYY-MM.idx
    /// </summary>
    private static string GetIdxFilePath(string userId, string convId, DateTime date)
        => Path.Combine(
            GetConversationFolder(userId, convId),
            $"{date:yyyy-MM}{ProtocolConstants.MessageIndexExtension}");

    /// <summary>
    /// ConvId chuẩn hóa cho cuộc trò chuyện 1-1: sắp xếp 2 userId để cùng Id dù ai gửi/nhận.
    /// Đảm bảo cả hai phía đều lưu vào cùng một convId.
    /// </summary>
    private static string GetDirectConvId(string userIdA, string userIdB)
    {
        ValidatePathSegment(userIdA, nameof(userIdA));
        ValidatePathSegment(userIdB, nameof(userIdB));
        var ids = new[] { userIdA, userIdB };
        Array.Sort(ids, StringComparer.Ordinal);
        return $"dm_{ids[0]}_{ids[1]}";
    }

    private static string GetDirectPartnerId(string currentUserId, string convId, MessageDto? lastMessage)
    {
        if (lastMessage != null)
        {
            var partnerId = string.Equals(lastMessage.SenderId, currentUserId, StringComparison.Ordinal)
                ? lastMessage.ReceiverId
                : lastMessage.SenderId;
            if (!string.IsNullOrWhiteSpace(partnerId))
                return partnerId;
        }

        var prefix = $"dm_{currentUserId}_";
        if (convId.StartsWith(prefix, StringComparison.Ordinal))
            return convId[prefix.Length..];

        var suffix = $"_{currentUserId}";
        if (convId.StartsWith("dm_", StringComparison.Ordinal) &&
            convId.EndsWith(suffix, StringComparison.Ordinal))
            return convId[3..^suffix.Length];

        return convId.StartsWith("dm_", StringComparison.Ordinal) ? string.Empty : convId;
    }

    private static void ValidatePathSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." ||
            Path.IsPathRooted(value) || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException("Identifier must be a valid single path segment.", parameterName);
    }

    private static string GetSeenMessageKey(string userId, string messageId)
        => $"{userId}\0{messageId}";

    /// <summary>Rút gọn nội dung preview tối đa 80 ký tự.</summary>
    private static string TruncatePreview(string content, int maxLength = 80)
        => content.Length <= maxLength ? content : content[..maxLength] + "…";

    /// <summary>Thêm MessageId vào RAM cache, giữ tối đa MaxDeduplicationCacheSize entries.</summary>
    private void RegisterSeenId(string cacheKey)
    {
        lock (_seenIdQueue)
        {
            if (_seenMessageIds.TryAdd(cacheKey, true))
            {
                _seenIdQueue.Enqueue(cacheKey);
                while (_seenIdQueue.Count > MaxDeduplicationCacheSize)
                {
                    if (_seenIdQueue.TryDequeue(out var old))
                        _seenMessageIds.TryRemove(old, out _);
                }
            }
        }
    }
}
