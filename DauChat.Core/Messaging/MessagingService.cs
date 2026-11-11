using DauChat.Shared.Constants;
using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.Core.Messaging;

/// <summary>
/// Stub triển khai IMessagingService cho TV4 (Dương Thị Yến Nhi - nhánh feature/tv4-messaging-history).
/// Chịu trách nhiệm Chat cá nhân 1-1, Chat phòng, Ghim thông báo Giảng viên
/// và Lưu trữ lịch sử Local-First Append-only (.jsonl + .idx).
/// Sử dụng INetworkService (TV2) để truyền tin và IAuthRoomService (TV3) để tra cứu thành viên phòng.
/// </summary>
public class MessagingService : IMessagingService
{
    private readonly INetworkService _networkService;
    private readonly IAuthRoomService _authRoomService;

    public MessagingService(INetworkService networkService, IAuthRoomService authRoomService)
    {
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
        _authRoomService = authRoomService ?? throw new ArgumentNullException(nameof(authRoomService));
    }

    public event EventHandler<MessageDto>? DirectMessageReceived;
    public event EventHandler<MessageDto>? RoomMessageReceived;
    public event EventHandler<(string RoomId, PinnedNoticeDto? PinnedNotice)>? PinnedAnnouncementChanged;

    public Task<OperationResult<ConversationSummaryDto>> OpenDirectConversationAsync(
        string currentUserId,
        string partnerId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_03_01): Mở cuộc trò chuyện 1-1.");

    public Task<IReadOnlyList<ConversationSummaryDto>> GetDirectConversationsAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_03_02): Lấy danh sách cuộc trò chuyện 1-1 từ Local.");

    public Task<OperationResult<MessageDto>> SendDirectMessageAsync(
        string senderId,
        string receiverId,
        string content,
        MessageType messageType = MessageType.Text,
        string? attachmentFileId = null,
        string? attachmentFileName = null,
        long? attachmentFileSize = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_03_03): Gán UUID, gửi DirectMessage qua INetworkService và ghi .jsonl Local.");

    public Task<IReadOnlyList<MessageDto>> GetDirectHistoryAsync(
        string currentUserId,
        string partnerId,
        int pageIndex = 0,
        int pageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_03_05/MOD_07_02): Lazy-load lịch sử chat 1-1 từ .jsonl/.idx.");

    public Task<OperationResult> DeleteDirectHistoryAsync(
        string currentUserId,
        string partnerId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_03_06/MOD_07_02): Xóa file lịch sử chat 1-1 tại Local.");

    public Task<OperationResult<IReadOnlyList<RoomDto>>> GetJoinedRoomsAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_04_07): Lấy danh sách phòng đang tham gia.");

    public Task<OperationResult<(RoomDto Room, IReadOnlyList<MessageDto> RecentMessages, PinnedNoticeDto? PinnedNotice)>> EnterRoomAsync(
        string currentUserId,
        string roomId,
        int initialPageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_04_08): Vào phòng chat, tải thông báo ghim và lịch sử gần nhất.");

    public Task<OperationResult> LeaveRoomAsync(
        string currentUserId,
        string roomId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_04_09): Rời phòng chat.");

    public Task<OperationResult<MessageDto>> SendRoomMessageAsync(
        string senderId,
        string roomId,
        string content,
        MessageType messageType = MessageType.Text,
        string? attachmentFileId = null,
        string? attachmentFileName = null,
        long? attachmentFileSize = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_04_10): Broadcast tin nhắn phòng qua INetworkService và ghi .jsonl Local.");

    public Task<IReadOnlyList<MessageDto>> GetRoomHistoryAsync(
        string currentUserId,
        string roomId,
        int pageIndex = 0,
        int pageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_04_12/MOD_07_02): Lazy-load lịch sử phòng từ .jsonl/.idx.");

    public Task<OperationResult> DeleteRoomHistoryAsync(
        string currentUserId,
        string roomId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_04_13/MOD_07_02): Xóa file lịch sử phòng tại Local.");

    public Task<OperationResult<PinnedNoticeDto>> PinRoomAnnouncementAsync(
        string roomId,
        string lecturerId,
        string contentOrMessageId,
        bool isMessageId = false,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_04_14): Giảng viên ghim thông báo quan trọng trong phòng.");

    public Task<OperationResult> UnpinRoomAnnouncementAsync(
        string roomId,
        string lecturerId,
        string? messageId = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_04_15): Giảng viên bỏ ghim thông báo phòng.");

    public Task<bool> AppendMessageToLocalLogAsync(
        string currentUserId,
        MessageDto message,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_07_01): Ghi nối tiếp FileMode.Append vào YYYY-MM.jsonl + cập nhật .idx + chống trùng UUID.");

    public Task ProcessIncomingPacketAsync(
        string currentUserId,
        NetworkPacket packet,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV4 - MOD_03_04/MOD_04_11): Xử lý gói tin đến, lưu Local và phát Event lên UI.");

    protected virtual void OnDirectMessageReceived(MessageDto message)
        => DirectMessageReceived?.Invoke(this, message);

    protected virtual void OnRoomMessageReceived(MessageDto message)
        => RoomMessageReceived?.Invoke(this, message);

    protected virtual void OnPinnedAnnouncementChanged(string roomId, PinnedNoticeDto? notice)
        => PinnedAnnouncementChanged?.Invoke(this, (roomId, notice));
}

