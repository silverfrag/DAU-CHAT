namespace DauChat.FileService.Services;

public interface IBackupService
{
    // Nén toàn bộ thư mục LocalData thành file .zip
    Task CreateBackupZipAsync(string userId, string outputZipPath);

    // Giải nén file .zip để khôi phục dữ liệu
    Task RestoreBackupZipAsync(string zipFilePath, string restoreDirectory);
}