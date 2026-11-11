using DauChat.Shared.Enums;

namespace DauChat.Shared.Models;

/// <summary>
/// DTO tin nhắn cá nhân 1-1 và phòng chat (MOD_03_03 -> MOD_03_05, MOD_04_10 -> MOD_04_12, MOD_07_01).
/// Được ghi nối tiếp vào LocalData/{UserId}/Messages/{RoomOrPartnerId}/YYYY-MM.jsonl.
/// </summary>
public sealed class MessageDto
{
    /// <summary>Định danh duy nhất (UUID/Guid) để chống trùng lặp khi nhận hoặc đồng bộ (Deduplication).</summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>ID hội thoại chuẩn hóa (RoomId nếu là nhóm, hoặc PartnerId nếu là 1-1).</summary>
    public string ConversationId { get; set; } = string.Empty;

    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string? ReceiverId { get; set; }
    public string? RoomId { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; } = MessageType.Text;
    public bool IsRoomMessage { get; set; }
    public bool IsPinned { get; set; }

    public DateTime SentAtClientUtc { get; set; } = DateTime.UtcNow;
    public DateTime ServerTimestampUtc { get; set; } = DateTime.UtcNow;

    // Siêu dữ liệu tệp đính kèm (nếu Type == MessageType.FileAttachment)
    public string? AttachmentFileId { get; set; }
    public string? AttachmentFileName { get; set; }
    public long? AttachmentFileSize { get; set; }
    public string? LocalFilePath { get; set; }
}

