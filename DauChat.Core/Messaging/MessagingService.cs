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
        var result = new List<ConversationSummaryDto>();
        try
        {
            var messagesRoot = GetMessagesFolder(currentUserId);
            if (!Directory.Exists(messagesRoot))
                return result;

            // Mỗi thư mục con là một ConvId (partnerId hoặc roomId)
            var convDirs = Directory.GetDirectories(messagesRoot);

            foreach (var dir in convDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var convId = Path.GetFileName(dir);

                // Chỉ lấy 1-1 (partnerId): không phải RoomId (không bắt đầu bằng "room_")
                // Quy ước: RoomId bắt đầu "room_", direct convId là UserId đối tác
                if (convId.StartsWith("room_", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Đọc tin nhắn cuối từ log
                var history = await ReadLocalHistoryAsync(currentUserId, convId, 0, 1, cancellationToken)
                    .ConfigureAwait(false);
                var lastMsg = history.Count > 0 ? history[^1] : null;

                var partnerResult = await _authRoomService.GetUserProfileAsync(convId, cancellationToken)
                    .ConfigureAwait(false);

                result.Add(new ConversationSummaryDto
                {
                    PartnerId = convId,
                    PartnerName = partnerResult.Data?.FullName ?? convId,
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

            // Sắp xếp theo tin nhắn cuối mới nhất
            result.Sort((a, b) =>
                (b.LastMessageAtUtc ?? DateTime.MinValue).CompareTo(a.LastMessageAtUtc ?? DateTime.MinValue));
        }
        catch (OperationCanceledException) { /* trả về những gì đã có */ }
        catch (Exception)
        {
            // Log lỗi (không ném exception lên UI)
        }
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
            var folder = Path.Combine(GetMessagesFolder(currentUserId), convId);
            await DeleteLocalHistoryFolderAsync(folder, cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok($"Đã xóa lịch sử trò chuyện với {partnerId}.");
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

            var rooms = response.GetPayload<List<RoomDto>>()
                        ?? new List<RoomDto>();

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
            var folder = Path.Combine(GetMessagesFolder(currentUserId), roomId);
            await DeleteLocalHistoryFolderAsync(folder, cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok($"Đã xóa lịch sử phòng {roomId}.");
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
        if (message == null) return false;

        // ── Deduplication: kiểm tra RAM cache trước ──────────────────────────
        if (_seenMessageIds.ContainsKey(message.MessageId))
            return false;

        var convId = string.IsNullOrWhiteSpace(message.RoomId)
            ? GetDirectConvId(currentUserId, message.SenderId == currentUserId
                ? message.ReceiverId ?? message.ConversationId
                : message.SenderId)
            : message.RoomId;

        var logFilePath = GetLogFilePath(currentUserId, convId, message.SentAtClientUtc);
        var idxFilePath = GetIdxFilePath(currentUserId, convId, message.SentAtClientUtc);

        var sem = _fileLocks.GetOrAdd(logFilePath, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // ── Deduplication: kiểm tra file trước khi ghi (phòng crash) ────
            if (await MessageExistsInFileAsync(logFilePath, message.MessageId, cancellationToken)
                .ConfigureAwait(false))
            {
                // Thêm vào cache để lần sau nhanh hơn
                RegisterSeenId(message.MessageId);
                return false;
            }

            // ── Đảm bảo thư mục tồn tại ──────────────────────────────────────
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

            // ── Ghi vào .jsonl (FileMode.Append) ─────────────────────────────
            var jsonLine = JsonSerializer.Serialize(message, JsonOpts) + "\n";
            var jsonBytes = Encoding.UTF8.GetBytes(jsonLine);

            long byteOffset;
            await using (var fs = new FileStream(logFilePath, FileMode.Append, FileAccess.Write,
                FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                byteOffset = fs.Position; // vị trí trước khi ghi = offset của record mới
                await fs.WriteAsync(jsonBytes, cancellationToken).ConfigureAwait(false);
            }

            // ── Ghi vào .idx: "MessageId,ByteOffset\n" ────────────────────────
            var idxLine = $"{message.MessageId},{byteOffset}\n";
            var idxBytes = Encoding.UTF8.GetBytes(idxLine);
            await using (var idxFs = new FileStream(idxFilePath, FileMode.Append, FileAccess.Write,
                FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                await idxFs.WriteAsync(idxBytes, cancellationToken).ConfigureAwait(false);
            }

            // ── Cập nhật RAM cache ────────────────────────────────────────────
            RegisterSeenId(message.MessageId);
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
        if (packet == null) return;

        switch (packet.Type)
        {
            // ── Tin nhắn cá nhân đến (MOD_03_04) ──────────────────────────────
            case PacketType.DirectMessage:
            {
                var message = packet.GetPayload<MessageDto>();
                if (message == null) return;

                // Đảm bảo metadata chính xác
                message.ServerTimestampUtc = packet.TimestampUtc;
                message.IsRoomMessage = false;

                bool appended = await AppendMessageToLocalLogAsync(
                    currentUserId, message, cancellationToken).ConfigureAwait(false);

                if (appended)
                {
                    OnDirectMessageReceived(message);

                    // Gửi ACK về Server để xóa khỏi PendingQueue (MOD_02_07)
                    if (packet.RequiresAck)
                    {
                        try
                        {
                            await _networkService.SendAckAsync(currentUserId, packet.PacketId, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (NotImplementedException) { /* TV2 chưa triển khai — bỏ qua khi test */ }
                        catch (Exception) { /* Lỗi mạng — không ném ra ngoài */ }
                    }
                }
                break;
            }

            // ── Tin nhắn phòng đến (MOD_04_11) ────────────────────────────────
            case PacketType.GroupMessage:
            {
                var message = packet.GetPayload<MessageDto>();
                if (message == null) return;

                message.ServerTimestampUtc = packet.TimestampUtc;
                message.IsRoomMessage = true;

                bool appended = await AppendMessageToLocalLogAsync(
                    currentUserId, message, cancellationToken).ConfigureAwait(false);

                if (appended)
                {
                    OnRoomMessageReceived(message);

                    if (packet.RequiresAck)
                    {
                        try
                        {
                            await _networkService.SendAckAsync(currentUserId, packet.PacketId, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (NotImplementedException) { /* TV2 chưa triển khai — bỏ qua khi test */ }
                        catch (Exception) { /* Lỗi mạng — không ném ra ngoài */ }
                    }
                }
                break;
            }

            // ── Phát sóng ghim thông báo đến (MOD_04_14) ──────────────────────
            case PacketType.PinAnnouncementBroadcast:
            {
                var pinNotice = packet.GetPayload<PinnedNoticeDto>();
                if (pinNotice == null) return;

                // Lưu dưới dạng tin nhắn loại PinnedAnnouncement vào local log
                var pinnedMsg = new MessageDto
                {
                    MessageId = pinNotice.MessageId,
                    ConversationId = pinNotice.RoomId,
                    SenderId = pinNotice.PinnedByUserId,
                    SenderName = pinNotice.PinnedByName,
                    RoomId = pinNotice.RoomId,
                    Content = pinNotice.Content,
                    Type = MessageType.PinnedAnnouncement,
                    IsRoomMessage = true,
                    IsPinned = true,
                    SentAtClientUtc = pinNotice.PinnedAtUtc,
                    ServerTimestampUtc = packet.TimestampUtc
                };

                await AppendMessageToLocalLogAsync(currentUserId, pinnedMsg, cancellationToken)
                    .ConfigureAwait(false);

                OnPinnedAnnouncementChanged(pinNotice.RoomId, pinNotice);

                if (packet.RequiresAck)
                {
                    try
                    {
                        await _networkService.SendAckAsync(currentUserId, packet.PacketId, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (NotImplementedException) { /* TV2 chưa triển khai — bỏ qua khi test */ }
                    catch (Exception) { /* Lỗi mạng — không ném ra ngoài */ }
                }
                break;
            }

            // ── Bỏ ghim thông báo (MOD_04_15) ─────────────────────────────────
            case PacketType.UnpinAnnouncementBroadcast:
            {
                var payload = packet.GetPayload<PinnedNoticeDto>();
                var roomId = payload?.RoomId ?? packet.TargetId ?? string.Empty;
                OnPinnedAnnouncementChanged(roomId, null);

                if (packet.RequiresAck)
                {
                    try
                    {
                        await _networkService.SendAckAsync(currentUserId, packet.PacketId, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (NotImplementedException) { /* TV2 chưa triển khai — bỏ qua khi test */ }
                    catch (Exception) { /* Lỗi mạng — không ném ra ngoài */ }
                }
                break;
            }

            // Các loại gói khác (TV2, TV3, TV5 xử lý)
            default:
                break;
        }
    }

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
        var result = new List<MessageDto>();
        try
        {
            var messagesFolder = Path.Combine(GetMessagesFolder(userId), convId);
            if (!Directory.Exists(messagesFolder))
                return result;

            // Lấy tất cả file .jsonl theo thứ tự thời gian (YYYY-MM.jsonl)
            var logFiles = Directory.GetFiles(messagesFolder, $"*{ProtocolConstants.MessageLogExtension}")
                .OrderByDescending(f => f) // mới nhất trước
                .ToList();

            // Thu thập tổng danh sách các offset records từ .idx (toàn bộ các file)
            // để tính trang đúng. Với lượng lịch sử lớn chỉ load những file cần thiết.
            var allRecords = new List<(string LogFile, long Offset)>();

            foreach (var logFile in logFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var idxFile = Path.ChangeExtension(logFile, ProtocolConstants.MessageIndexExtension);
                if (!File.Exists(idxFile))
                {
                    // Fallback: scan toàn bộ .jsonl nếu không có .idx
                    var scanRecords = await ScanLogFileOffsetsAsync(logFile, cancellationToken)
                        .ConfigureAwait(false);
                    allRecords.AddRange(scanRecords.Select(o => (logFile, o)));
                }
                else
                {
                    var idxRecords = await ReadIdxOffsetsAsync(idxFile, cancellationToken)
                        .ConfigureAwait(false);
                    allRecords.AddRange(idxRecords.Select(o => (logFile, o)));
                }
            }

            // Phân trang từ cuối lên (tin mới nhất ở cuối file)
            int totalMessages = allRecords.Count;
            int skip = pageIndex * pageSize;
            int startFromEnd = skip; // bỏ qua skip record tính từ cuối
            int take = pageSize;

            // Lấy các record từ cuối (index từ totalMessages-1 trở về)
            int fromIndex = totalMessages - 1 - startFromEnd;
            if (fromIndex < 0) return result;

            var selectedRecords = new List<(string LogFile, long Offset)>();
            for (int i = fromIndex; i >= 0 && selectedRecords.Count < take; i--)
                selectedRecords.Add(allRecords[i]);

            // Đọc nội dung từ file theo offset
            // Group theo file để giảm số lần mở file
            var byFile = selectedRecords.GroupBy(r => r.LogFile);
            var tempMessages = new List<MessageDto>();

            foreach (var group in byFile)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var logFile = group.Key;
                var sem = _fileLocks.GetOrAdd(logFile, _ => new SemaphoreSlim(1, 1));
                await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
                    using var reader = new StreamReader(fs, Encoding.UTF8);

                    foreach (var (_, offset) in group)
                    {
                        fs.Seek(offset, SeekOrigin.Begin);
                        reader.DiscardBufferedData();
                        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var msg = JsonSerializer.Deserialize<MessageDto>(line, JsonOpts);
                        if (msg != null) tempMessages.Add(msg);
                    }
                }
                finally { sem.Release(); }
            }

            // Sắp xếp theo thời gian tăng dần (cũ → mới) để hiển thị đúng thứ tự
            tempMessages.Sort((a, b) => a.SentAtClientUtc.CompareTo(b.SentAtClientUtc));
            result.AddRange(tempMessages);
        }
        catch (OperationCanceledException) { /* trả về những gì đã có */ }
        catch (Exception)
        {
            // Log lỗi (không ném exception lên UI)
        }
        return result;
    }

    /// <summary>Xóa toàn bộ thư mục lịch sử (.jsonl + .idx) của một convId.</summary>
    private static Task DeleteLocalHistoryFolderAsync(string folder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Đọc các byte offset từ file .idx (mỗi dòng: "MessageId,ByteOffset").
    /// </summary>
    private static async Task<List<long>> ReadIdxOffsetsAsync(
        string idxFilePath, CancellationToken cancellationToken)
    {
        var offsets = new List<long>();
        try
        {
            await using var fs = new FileStream(idxFilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && long.TryParse(parts[1], out long offset))
                    offsets.Add(offset);
            }
        }
        catch { /* file có thể đang bị ghi */ }
        return offsets;
    }

    /// <summary>
    /// Fallback: quét .jsonl để tìm byte offsets cho từng dòng JSON (không có .idx).
    /// </summary>
    private static async Task<List<long>> ScanLogFileOffsetsAsync(
        string logFilePath, CancellationToken cancellationToken)
    {
        var offsets = new List<long>();
        try
        {
            await using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
            using var reader = new StreamReader(fs, Encoding.UTF8, leaveOpen: true);

            long pos = 0;
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                offsets.Add(pos);
                // Tính vị trí byte tiếp theo (UTF-8 + \n)
                pos += Encoding.UTF8.GetByteCount(line) + 1; // +1 cho '\n'
            }
        }
        catch { /* bỏ qua */ }
        return offsets;
    }

    /// <summary>
    /// Kiểm tra xem MessageId đã tồn tại trong .jsonl chưa (dùng lúc khởi động/crash recovery).
    /// Chỉ scan nếu cache không có.
    /// </summary>
    private static async Task<bool> MessageExistsInFileAsync(
        string logFilePath, string messageId, CancellationToken cancellationToken)
    {
        if (!File.Exists(logFilePath)) return false;
        try
        {
            await using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, bufferSize: 4096, useAsync: true);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                // Dùng kiểm tra chuỗi nhanh (không cần deserialize toàn bộ)
                if (line.Contains($"\"MessageId\":\"{messageId}\"", StringComparison.Ordinal) ||
                    line.Contains($"\"messageId\":\"{messageId}\"", StringComparison.Ordinal))
                    return true;
            }
        }
        catch { /* bỏ qua lỗi đọc */ }
        return false;
    }

    /// <summary>
    /// Event handler từ INetworkService.PacketReceived — chuyển tiếp đến ProcessIncomingPacketAsync.
    /// currentUserId được lấy từ INetworkService.CurrentUserId (TV2 thiết lập sau Login).
    /// </summary>
    private void OnPacketReceivedFromNetwork(object? sender, NetworkPacket packet)
    {
        var userId = _networkService.CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId)) return;

        // Fire-and-forget với CancellationToken.None (không thể inject CT ở đây)
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessIncomingPacketAsync(userId, packet, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Log lỗi nền (không ném ra ngoài)
            }
        });
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
        => Path.Combine(
            ProtocolConstants.LocalDataRootFolder,
            userId,
            ProtocolConstants.MessagesFolderName);

    /// <summary>
    /// Đường dẫn file .jsonl cho một tháng cụ thể:
    /// LocalData/{userId}/Messages/{convId}/YYYY-MM.jsonl
    /// </summary>
    private static string GetLogFilePath(string userId, string convId, DateTime date)
        => Path.Combine(
            GetMessagesFolder(userId),
            convId,
            $"{date:yyyy-MM}{ProtocolConstants.MessageLogExtension}");

    /// <summary>
    /// Đường dẫn file .idx tương ứng với file .jsonl:
    /// LocalData/{userId}/Messages/{convId}/YYYY-MM.idx
    /// </summary>
    private static string GetIdxFilePath(string userId, string convId, DateTime date)
        => Path.Combine(
            GetMessagesFolder(userId),
            convId,
            $"{date:yyyy-MM}{ProtocolConstants.MessageIndexExtension}");

    /// <summary>
    /// ConvId chuẩn hóa cho cuộc trò chuyện 1-1: sắp xếp 2 userId để cùng Id dù ai gửi/nhận.
    /// Đảm bảo cả hai phía đều lưu vào cùng một convId.
    /// </summary>
    private static string GetDirectConvId(string userIdA, string userIdB)
    {
        // Sắp xếp để đảm bảo tính nhất quán
        var ids = new[] { userIdA, userIdB };
        Array.Sort(ids, StringComparer.Ordinal);
        return $"dm_{ids[0]}_{ids[1]}";
    }

    /// <summary>Rút gọn nội dung preview tối đa 80 ký tự.</summary>
    private static string TruncatePreview(string content, int maxLength = 80)
        => content.Length <= maxLength ? content : content[..maxLength] + "…";

    /// <summary>Thêm MessageId vào RAM cache, giữ tối đa MaxDeduplicationCacheSize entries.</summary>
    private void RegisterSeenId(string messageId)
    {
        lock (_seenIdQueue)
        {
            if (_seenMessageIds.TryAdd(messageId, true))
            {
                _seenIdQueue.Enqueue(messageId);
                // Dọn dẹp khi vượt giới hạn
                while (_seenIdQueue.Count > MaxDeduplicationCacheSize)
                {
                    if (_seenIdQueue.TryDequeue(out var old))
                        _seenMessageIds.TryRemove(old, out _);
                }
            }
        }
    }
}
