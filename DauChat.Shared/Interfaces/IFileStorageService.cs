using DauChat.Shared.Enums;
using DauChat.Shared.Models;

namespace DauChat.Shared.Interfaces;

/// <summary>
/// Hợp đồng Truyền tệp theo luồng (Chunking 64KB + SHA-256), Kho tệp Local, Dọn dung lượng &amp; Backup ZIP
/// do TV5 (Hồng Quốc Tuấn) phụ trách.
/// Bao phủ Phân hệ 5 (MOD_05_01 -> MOD_05_05) và Nhánh 7.2–7.4 (MOD_07_03 -> MOD_07_08).
/// </summary>
public interface IFileStorageService
{
    #region Phân hệ 5: Truyền tệp theo luồng & Quản lý tệp trong hội thoại [GV][SV] (MOD_05_01 -> MOD_05_05)
    /// <summary>
    /// MOD_05_01: Đọc tệp theo luồng (64KB/chunk), tính mã băm SHA-256 và gửi từng FileChunk qua INetworkService.
    /// </summary>
    Task<OperationResult<FileManifestDto>> SendFileAsync(
        string senderId,
        string targetId,
        bool isRoom,
        string sourceFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MOD_05_02: Nhận từng FileChunk, ghi nối tiếp vào file tạm (.part), kiểm tra khớp SHA-256
    /// rồi đổi tên sang LocalData/{UserId}/Files/{RoomOrPartnerId}/ và cập nhật file_manifest.jsonl.
    /// </summary>
    Task<OperationResult<FileManifestDto?>> ReceiveFileChunkAsync(
        string currentUserId,
        FileChunkDto chunk,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_05_04: Liệt kê danh sách tệp đã chia sẻ trong một cuộc trò chuyện hoặc phòng học.</summary>
    Task<IReadOnlyList<FileManifestDto>> GetConversationFilesAsync(
        string currentUserId,
        string roomOrPartnerId,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_05_05: Mở trực tiếp tệp Local bằng ứng dụng mặc định của Windows.</summary>
    Task<OperationResult> OpenLocalFileAsync(string localFilePath, CancellationToken cancellationToken = default);

    /// <summary>MOD_05_05, MOD_07_04: Xóa tệp khỏi ổ cứng Local và cập nhật chỉ mục manifest.</summary>
    Task<OperationResult> DeleteLocalFileAsync(
        string currentUserId,
        string fileId,
        CancellationToken cancellationToken = default);
    #endregion

    #region Phân hệ 7.2 -> 7.4: Kho tệp Local, Thống kê, Dọn rác & Backup/Restore ZIP (MOD_07_03 -> MOD_07_08)
    /// <summary>MOD_07_03: Ghi nhận bản ghi siêu dữ liệu tệp hoàn chỉnh vào file_manifest.jsonl.</summary>
    Task<OperationResult> RegisterManifestEntryAsync(
        string currentUserId,
        FileManifestDto manifest,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_07_04: Xem danh sách toàn bộ tệp đã tải trên máy, hỗ trợ lọc theo loại (CAD, PDF, ZIP, Ảnh...).</summary>
    Task<IReadOnlyList<FileManifestDto>> GetAllLocalFilesAsync(
        string currentUserId,
        FileCategory? categoryFilter = null,
        string? searchKeyword = null,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_07_05: Thống kê dung lượng ổ cứng đã sử dụng theo từng phòng học / cuộc trò chuyện.</summary>
    Task<StorageStatsDto> GetStorageStatsAsync(string currentUserId, CancellationToken cancellationToken = default);

    /// <summary>MOD_07_06: Dọn bộ nhớ đệm thủ công hoặc tự động xóa tệp cũ quá hạn theo nguyên tắc Oldest-first.</summary>
    Task<OperationResult<StorageCleanupResultDto>> CleanupOldFilesAsync(
        string currentUserId,
        int? olderThanDays = null,
        long? targetMaxBytes = null,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_07_07: Đóng gói toàn bộ dữ liệu LocalData/{UserId} (tin nhắn .jsonl + tệp) thành file nén .zip.</summary>
    Task<OperationResult<string>> ExportBackupZipAsync(
        string currentUserId,
        string destinationZipPath,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_07_08: Giải nén file .zip sao lưu và khôi phục lịch sử chat/tài liệu trên thiết bị mới.</summary>
    Task<OperationResult> RestoreBackupZipAsync(
        string currentUserId,
        string sourceZipPath,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default);
    #endregion

    #region Sự kiện tiến trình truyền tệp & nhận tệp hoàn chỉnh (MOD_05_02, MOD_05_03)
    /// <summary>MOD_05_03: Cập nhật phần trăm (%) tiến trình gửi/nhận tệp theo thời gian thực.</summary>
    event EventHandler<FileTransferProgressDto>? TransferProgressChanged;

    /// <summary>MOD_05_02: Phát sự kiện khi nhận và xác thực SHA-256 thành công một tệp hoàn chỉnh.</summary>
    event EventHandler<FileManifestDto>? FileReceived;
    #endregion
}

