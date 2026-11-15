using System.IO.Compression;

namespace DauChat.FileService.Services;

public class BackupService : IBackupService
{
    public Task CreateBackupZipAsync(string userId, string outputZipPath)
    {
        // Đường dẫn thư mục chứa dữ liệu của user
        string sourceDir = Path.Combine("LocalData", userId);

        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException("Không tìm thấy dữ liệu của người dùng để sao lưu.");

        // Nếu file zip đã tồn tại thì xóa đi để tạo mới
        if (File.Exists(outputZipPath))
            File.Delete(outputZipPath);

        // Nén toàn bộ thư mục thành file .zip
        ZipFile.CreateFromDirectory(sourceDir, outputZipPath, CompressionLevel.Optimal, false);

        return Task.CompletedTask;
    }

    public Task RestoreBackupZipAsync(string zipFilePath, string restoreDirectory)
    {
        if (!File.Exists(zipFilePath))
            throw new FileNotFoundException("Không tìm thấy file sao lưu .zip.");

        // Đảm bảo thư mục khôi phục tồn tại
        if (!Directory.Exists(restoreDirectory))
            Directory.CreateDirectory(restoreDirectory);

        // Giải nén đè lên thư mục cũ
        ZipFile.ExtractToDirectory(zipFilePath, restoreDirectory, true);

        return Task.CompletedTask;
    }
}