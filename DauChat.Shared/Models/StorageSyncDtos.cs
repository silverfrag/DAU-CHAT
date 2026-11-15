namespace DauChat.Shared.Models;

/// <summary>
/// Yêu cầu kiểm tra bản sao lưu đồng bộ trên Server LAN (MOD_07_09).
/// </summary>
public sealed class SyncCheckRequestDto
{
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// Phản hồi kiểm tra bản sao lưu đồng bộ trên Server LAN.
/// </summary>
public sealed class SyncCheckResponseDto
{
    public bool HasSnapshot { get; set; }
    public DateTime? LastSnapshotUtc { get; set; }
    public long SnapshotSizeBytes { get; set; }
    public string SnapshotSummary { get; set; } = string.Empty;
    public int TotalMessagesCount { get; set; }
    public int TotalFilesCount { get; set; }
}

/// <summary>
/// Gói tin Client đẩy bản sao lưu lên Server LAN Vault (MOD_07_07).
/// </summary>
public sealed class SyncPushRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string SnapshotZipBase64 { get; set; } = string.Empty;
    public long SnapshotSizeBytes { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Summary { get; set; } = string.Empty;
    public int MessagesCount { get; set; }
    public int FilesCount { get; set; }
}

/// <summary>
/// Kết quả Server tiếp nhận bản sao lưu vào Vault (MOD_07_07).
/// </summary>
public sealed class SyncPushResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public long StoredBytes { get; set; }
}

/// <summary>
/// Yêu cầu kéo bản sao lưu từ Server LAN về máy Client mới (MOD_07_08).
/// </summary>
public sealed class SyncPullRequestDto
{
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// Dữ liệu bản sao lưu gửi từ Server LAN về Client mới (MOD_07_08).
/// </summary>
public sealed class SyncPullResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SnapshotZipBase64 { get; set; }
    public long SnapshotSizeBytes { get; set; }
    public DateTime? TimestampUtc { get; set; }
    public int MessagesCount { get; set; }
    public int FilesCount { get; set; }
}
