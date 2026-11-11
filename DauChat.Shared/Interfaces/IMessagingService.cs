using DauChat.Shared.Constants;
using DauChat.Shared.Enums;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.Shared.Interfaces;

/// <summary>
/// Hợp đồng Động cơ Tin nhắn &amp; Lưu trữ Lịch sử Local-First (.jsonl + .idx) do TV4 (Dương Thị Yến Nhi) phụ trách.
/// Bao phủ Phân hệ 3 (MOD_03_01 -> MOD_03_06), Nhánh 4.3–4.6 (MOD_04_07 -> MOD_04_15) và Nhánh 7.1 (MOD_07_01, MOD_07_02).
/// </summary>
public interface IMessagingService
{
    #region Phân hệ 3: Chat cá nhân 1-1 [GV][SV] (MOD_03_01 -> MOD_03_06)
    /// <summary>MOD_03_01: Mở/khởi tạo phiên trò chuyện 1-1 với người dùng đích.</summary>
    Task<OperationResult<ConversationSummaryDto>> OpenDirectConversationAsync(
        string currentUserId,
        string partnerId,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_03_02: Lấy danh sách các cuộc trò chuyện 1-1 gần đây từ dữ liệu Local.</summary>
    Task<IReadOnlyList<ConversationSummaryDto>> GetDirectConversationsAsync(
        string currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_03_03: Gán UUID, gửi tin nhắn cá nhân qua INetworkService và ghi nối vào .jsonl Local.</summary>
    Task<OperationResult<MessageDto>> SendDirectMessageAsync(
        string senderId,
        string receiverId,
        string content,
        MessageType messageType = MessageType.Text,
        string? attachmentFileId = null,
        string? attachmentFileName = null,
        long? attachmentFileSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_03_05, MOD_07_02: Đọc phân trang (Lazy-load từ dưới lên) lịch sử tin nhắn 1-1 từ file .jsonl/.idx.</summary>
    Task<IReadOnlyList<MessageDto>> GetDirectHistoryAsync(
        string currentUserId,
        string partnerId,
        int pageIndex = 0,
        int pageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_03_06, MOD_07_02: Xóa file lịch sử tin nhắn 1-1 trên máy Client.</summary>
    Task<OperationResult> DeleteDirectHistoryAsync(
        string currentUserId,
        string partnerId,
        CancellationToken cancellationToken = default);
    #endregion

    #region Phân hệ 4.3 -> 4.6: Tham gia phòng, Chat nhóm & Ghim thông báo [GV][SV] (MOD_04_07 -> MOD_04_15)
    /// <summary>MOD_04_07: Lấy danh sách các phòng chat mà người dùng đang tham gia.</summary>
    Task<OperationResult<IReadOnlyList<RoomDto>>> GetJoinedRoomsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_08: Vào phòng chat, tải thông báo ghim và trang lịch sử gần nhất.</summary>
    Task<OperationResult<(RoomDto Room, IReadOnlyList<MessageDto> RecentMessages, PinnedNoticeDto? PinnedNotice)>> EnterRoomAsync(
        string currentUserId,
        string roomId,
        int initialPageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_09: Chủ động rời khỏi phòng chat không bắt buộc.</summary>
    Task<OperationResult> LeaveRoomAsync(
        string currentUserId,
        string roomId,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_10: Gửi tin nhắn broadcast vào phòng (lấy danh sách thành viên từ IAuthRoomService).</summary>
    Task<OperationResult<MessageDto>> SendRoomMessageAsync(
        string senderId,
        string roomId,
        string content,
        MessageType messageType = MessageType.Text,
        string? attachmentFileId = null,
        string? attachmentFileName = null,
        long? attachmentFileSize = null,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_12, MOD_07_02: Đọc phân trang lịch sử tin nhắn phòng từ file .jsonl/.idx Local.</summary>
    Task<IReadOnlyList<MessageDto>> GetRoomHistoryAsync(
        string currentUserId,
        string roomId,
        int pageIndex = 0,
        int pageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_13, MOD_07_02: Xóa file lịch sử local của phòng trên máy Client.</summary>
    Task<OperationResult> DeleteRoomHistoryAsync(
        string currentUserId,
        string roomId,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_14: Giảng viên ghim thông báo quan trọng lên đầu phòng chat.</summary>
    Task<OperationResult<PinnedNoticeDto>> PinRoomAnnouncementAsync(
        string roomId,
        string lecturerId,
        string contentOrMessageId,
        bool isMessageId = false,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_15: Giảng viên bỏ ghim thông báo khỏi đầu phòng chat.</summary>
    Task<OperationResult> UnpinRoomAnnouncementAsync(
        string roomId,
        string lecturerId,
        string? messageId = null,
        CancellationToken cancellationToken = default);
    #endregion

    #region Phân hệ 7.1 & Xử lý gói tin đến (MOD_03_04, MOD_04_11, MOD_07_01, MOD_07_02)
    /// <summary>
    /// MOD_07_01: Ghi nối tiếp (Append-only, FileMode.Append) tin nhắn vào
    /// LocalData/{UserId}/Messages/{RoomOrPartnerId}/YYYY-MM.jsonl kèm chỉ mục .idx.
    /// Trả về false nếu trùng MessageId (Deduplication).
    /// </summary>
    Task<bool> AppendMessageToLocalLogAsync(
        string currentUserId,
        MessageDto message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MOD_03_04, MOD_04_11: Xử lý gói tin nhắn/ghim nhận từ INetworkService, chống trùng UUID, lưu Local và bắn Event.
    /// </summary>
    Task ProcessIncomingPacketAsync(
        string currentUserId,
        NetworkPacket packet,
        CancellationToken cancellationToken = default);
    #endregion

    #region Sự kiện tin nhắn thời gian thực cho UI
    /// <summary>MOD_03_04: Sự kiện khi nhận tin nhắn cá nhân 1-1 mới.</summary>
    event EventHandler<MessageDto>? DirectMessageReceived;

    /// <summary>MOD_04_11: Sự kiện khi nhận tin nhắn phòng mới.</summary>
    event EventHandler<MessageDto>? RoomMessageReceived;

    /// <summary>MOD_04_14, MOD_04_15: Sự kiện khi thông báo ghim của phòng thay đổi.</summary>
    event EventHandler<(string RoomId, PinnedNoticeDto? PinnedNotice)>? PinnedAnnouncementChanged;
    #endregion
}

