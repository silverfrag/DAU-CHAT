namespace DauChat.Shared.Constants;

/// <summary>
/// Các hằng số giao thức mạng và cấu trúc lưu trữ Local-First dùng chung toàn hệ thống DAU Chat.
/// </summary>
public static class ProtocolConstants
{
    public const string DefaultServerHost = "127.0.0.1";
    public const int DefaultServerPort = 9050;

    /// <summary>
    /// Kích thước header độ dài gói tin TCP (4 byte big-endian - Length-prefix framing, MOD_02_01).
    /// </summary>
    public const int LengthPrefixSizeBytes = 4;

    /// <summary>
    /// Giới hạn kích thước payload tối đa của một gói tin TCP (16 MB).
    /// </summary>
    public const int MaxPacketPayloadBytes = 16 * 1024 * 1024;

    /// <summary>
    /// Kích thước mỗi chunk khi truyền file qua stream (64 KB/chunk, MOD_05_01).
    /// </summary>
    public const int DefaultFileChunkSizeBytes = 64 * 1024;

    /// <summary>
    /// Chu kỳ gửi gói Heartbeat (giây) để duy trì trạng thái Online (MOD_02_04).
    /// </summary>
    public const int DefaultHeartbeatIntervalSeconds = 15;

    /// <summary>
    /// Thời gian timeout đánh dấu Client Offline nếu mất tín hiệu (MOD_02_05).
    /// </summary>
    public const int DefaultClientTimeoutSeconds = 45;

    /// <summary>
    /// Thời gian sống (TTL) mặc định của gói tin trong PendingQueue trên Server: 7 ngày (MOD_02_06, MOD_02_07).
    /// </summary>
    public const int DefaultPendingQueueTtlSeconds = 7 * 24 * 3600;

    /// <summary>
    /// Số lượng tin nhắn mặc định cho mỗi trang Lazy-load lịch sử chat (MOD_03_05, MOD_04_12, MOD_07_02).
    /// </summary>
    public const int DefaultPageSize = 30;

    // Cấu trúc thư mục Local-First kiểu Zalo (TV4 & TV5)
    public const string LocalDataRootFolder = "LocalData";
    public const string MessagesFolderName = "Messages";
    public const string FilesFolderName = "Files";
    public const string FileManifestFileName = "file_manifest.jsonl";
    public const string MessageLogExtension = ".jsonl";
    public const string MessageIndexExtension = ".idx";
    public const string TempPartExtension = ".part";
}

