using DauChat.FileService.Models;

namespace DauChat.FileService.Services;

public interface IFileTransferService
{
    // Hàm gửi file: Cần đường dẫn file, người nhận, và hàm để báo cáo tiến độ (%)
    Task SendFileAsync(string filePath, string receiverId, IProgress<TransferProgress> progress, CancellationToken ct = default);

    // Hàm nhận file: Cần thông tin file (tên, dung lượng, mã băm) và nơi lưu file trên máy
    Task ReceiveFileAsync(FileMetadata metadata, string saveDirectory, IProgress<TransferProgress> progress, CancellationToken ct = default);
}