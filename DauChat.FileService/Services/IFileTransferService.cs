using DauChat.FileService.Models;
using DauChat.Shared.Models;

namespace DauChat.FileService.Services;

public interface IFileTransferService
{
    // Hàm gửi file: Cần đường dẫn file, UserId người gửi, UserId/RoomId người nhận, và hàm báo cáo tiến độ (%)
    Task SendFileAsync(string filePath, string senderId, string receiverId, IProgress<TransferProgress> progress, CancellationToken ct = default);

    // Hàm nhận file: Cần thông tin file (tên, dung lượng, mã băm) và nơi lưu file trên máy
    Task ReceiveFileAsync(FileChunkDto metadata, string saveDirectory, IProgress<TransferProgress> progress, CancellationToken ct = default);
}