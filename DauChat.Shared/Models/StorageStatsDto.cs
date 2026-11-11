namespace DauChat.Shared.Models;

/// <summary>
/// Thống kê dung lượng lưu trữ Local theo từng phòng/cuộc trò chuyện (MOD_07_05).
/// </summary>
public sealed class StorageStatsDto
{
    public string UserId { get; set; } = string.Empty;
    public long TotalMessagesBytes { get; set; }
    public long TotalFilesBytes { get; set; }
    public long TotalUsedBytes => TotalMessagesBytes + TotalFilesBytes;
    public int TotalFilesCount { get; set; }
    public List<ConversationStorageStatDto> BreakdownByConversation { get; set; } = new();
}

/// <summary>
/// Chi tiết dung lượng đã dùng của một phòng học hoặc cuộc trò chuyện 1-1 (MOD_07_05).
/// </summary>
public sealed class ConversationStorageStatDto
{
    public string RoomOrPartnerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRoom { get; set; }
    public long MessagesBytes { get; set; }
    public long FilesBytes { get; set; }
    public int FileCount { get; set; }
}

/// <summary>
/// Kết quả dọn bộ nhớ đệm / tệp cũ quá hạn Oldest-first (MOD_07_06).
/// </summary>
public sealed class StorageCleanupResultDto
{
    public int DeletedFilesCount { get; set; }
    public long FreedBytes { get; set; }
    public List<string> DeletedFileIds { get; set; } = new();
}

