using DauChat.FileService.Models;
using DauChat.FileService.Utils;

namespace DauChat.FileService.Services;

public class FileTransferService : IFileTransferService
{
    public async Task SendFileAsync(string filePath, string receiverId, IProgress<TransferProgress> progress, CancellationToken ct = default)
    {
        // Tạm thời mình sẽ để trống phần gửi qua mạng ở đây
        // Vì mình cần TV1/TV2 cung cấp hàm INetworkClient
        await Task.CompletedTask;
    }

    public async Task ReceiveFileAsync(FileMetadata metadata, string saveDirectory, IProgress<TransferProgress> progress, CancellationToken ct = default)
    {
        // Tương tự, phần nhận qua mạng sẽ nối với TV2 sau
        await Task.CompletedTask;
    }
}