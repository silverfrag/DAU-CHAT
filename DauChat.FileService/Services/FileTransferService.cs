using DauChat.FileService.Models;
using DauChat.FileService.Utils;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;
using DauChat.Shared.Enums;
using System.IO;

namespace DauChat.FileService.Services;

public class FileTransferService : IFileTransferService
{
    private readonly INetworkService _networkService;

    // Nhận "ống dẫn mạng" từ TV2
    public FileTransferService(INetworkService networkService)
    {
        _networkService = networkService;
    }

    public async Task SendFileAsync(string filePath, string receiverId, IProgress<TransferProgress> progress, CancellationToken ct = default)
    {
        // 1. Chuẩn bị thông tin file
        var fileInfo = new FileInfo(filePath);
        long totalBytes = fileInfo.Length;
        string fileHash = await Sha256Helper.ComputeFileHashAsync(filePath, ct);
        string fileId = Guid.NewGuid().ToString("N");
        int totalChunks = (int)Math.Ceiling((double)totalBytes / ChunkSplitter.DefaultChunkSize);

        long sentBytes = 0;

        // 2. Đọc file và gửi từng khúc (Chunk)
        await foreach (var chunk in ChunkSplitter.ReadChunksAsync(filePath, ChunkSplitter.DefaultChunkSize, ct))
        {
            // Đóng gói dữ liệu theo khuôn mẫu của TV2
            var chunkDto = new FileChunkDto
            {
                TransferId = Guid.NewGuid().ToString("N"),
                FileId = fileId,
                FileName = fileInfo.Name,
                SenderId = "TV5_TEMP", // Sẽ lấy từ User đăng nhập sau này
                TargetId = receiverId,
                TotalFileSize = totalBytes,
                Sha256Checksum = fileHash,
                ChunkIndex = chunk.Index,
                TotalChunks = totalChunks,
                Data = chunk.Data,
                IsLastChunk = chunk.IsLast
            };

            // Tạo gói tin để gửi đi
            var packet = NetworkPacket.Create(
                PacketType.FileChunk, // Sẽ kiểm tra lại tên này
                senderId: chunkDto.SenderId,
                payload: chunkDto,
                targetId: receiverId
            );

            // Gọi hàm của TV2 để gửi qua mạng
            await _networkService.SendToUserAsync(receiverId, packet, ct);

            // Cập nhật tiến trình (%)
            sentBytes += chunk.Data.Length;
            progress?.Report(new TransferProgress
            {
                FileId = fileId,
                TransferredBytes = sentBytes,
                TotalBytes = totalBytes
            });
        }
    }

    public async Task ReceiveFileAsync(FileMetadata metadata, string saveDirectory, IProgress<TransferProgress> progress, CancellationToken ct = default)
    {
        // Tạm thời mình để trống hàm này, xíu nữa mình viết tiếp phần nhận
        await Task.CompletedTask;
    }
}