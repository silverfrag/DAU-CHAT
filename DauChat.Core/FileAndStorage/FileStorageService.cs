using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;

namespace DauChat.Core.FileAndStorage;

/// <summary>
/// Stub triển khai IFileStorageService cho TV5 (Hồng Quốc Tuấn - nhánh feature/tv5-file-storage-backup).
/// Chịu trách nhiệm Truyền tệp theo chunk 64KB, file tạm .part, kiểm tra SHA-256,
/// file_manifest.jsonl, dọn rác Oldest-first và Backup/Restore .zip.
/// Truyền gói chunk qua INetworkService (TV2).
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly INetworkService _networkService;

    public FileStorageService(INetworkService networkService)
    {
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
    }

    public event EventHandler<FileTransferProgressDto>? TransferProgressChanged;
    public event EventHandler<FileManifestDto>? FileReceived;

    public Task<OperationResult<FileManifestDto>> SendFileAsync(
        string senderId,
        string targetId,
        bool isRoom,
        string sourceFilePath,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_05_01): Đọc streaming 64KB/chunk, tính SHA-256 và gửi qua INetworkService.");

    public Task<OperationResult<FileManifestDto?>> ReceiveFileChunkAsync(
        string currentUserId,
        FileChunkDto chunk,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_05_02): Ghi chunk vào .part, kiểm tra SHA-256 và lưu vào LocalData/{UserId}/Files/.");

    public Task<IReadOnlyList<FileManifestDto>> GetConversationFilesAsync(
        string currentUserId,
        string roomOrPartnerId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_05_04): Lấy danh sách tệp trong hội thoại từ file_manifest.jsonl.");

    public Task<OperationResult> OpenLocalFileAsync(string localFilePath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_05_05): Mở tệp local bằng ứng dụng mặc định Windows.");

    public Task<OperationResult> DeleteLocalFileAsync(
        string currentUserId,
        string fileId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_05_05/MOD_07_04): Xóa tệp khỏi đĩa và cập nhật file_manifest.jsonl.");

    public Task<OperationResult> RegisterManifestEntryAsync(
        string currentUserId,
        FileManifestDto manifest,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_07_03): Ghi bản ghi mới vào file_manifest.jsonl.");

    public Task<IReadOnlyList<FileManifestDto>> GetAllLocalFilesAsync(
        string currentUserId,
        FileCategory? categoryFilter = null,
        string? searchKeyword = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_07_04): Liệt kê và lọc toàn bộ tệp local (CAD, PDF, ZIP, Ảnh...).");

    public Task<StorageStatsDto> GetStorageStatsAsync(string currentUserId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_07_05): Thống kê dung lượng lưu trữ theo từng phòng/cuộc trò chuyện.");

    public Task<OperationResult<StorageCleanupResultDto>> CleanupOldFilesAsync(
        string currentUserId,
        int? olderThanDays = null,
        long? targetMaxBytes = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_07_06): Dọn tệp cũ theo chính sách Oldest-first.");

    public Task<OperationResult<string>> ExportBackupZipAsync(
        string currentUserId,
        string destinationZipPath,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_07_07): Nén toàn bộ LocalData/{UserId} thành file .zip.");

    public Task<OperationResult> RestoreBackupZipAsync(
        string currentUserId,
        string sourceZipPath,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV5 - MOD_07_08): Giải nén .zip và khôi phục dữ liệu Local.");

    protected virtual void OnTransferProgressChanged(FileTransferProgressDto progress)
        => TransferProgressChanged?.Invoke(this, progress);

    protected virtual void OnFileReceived(FileManifestDto manifest)
        => FileReceived?.Invoke(this, manifest);
}

