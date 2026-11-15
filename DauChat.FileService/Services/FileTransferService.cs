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

    // Biến lưu trữ tạm thời các file đang nhận (FileId -> FileStream)
    private static readonly Dictionary<string, FileStream> _receivingFiles = new();

    public async Task ReceiveFileAsync(FileChunkDto chunk, string saveDirectory, IProgress<TransferProgress> progress, CancellationToken ct = default)
    {
        // 1. Đảm bảo thư mục lưu tồn tại
        if (!Directory.Exists(saveDirectory))
            Directory.CreateDirectory(saveDirectory);

        string tempFilePath = Path.Combine(saveDirectory, $"{chunk.FileId}.part");

        // 2. Mở file tạm để ghi (nếu là khúc đầu tiên thì tạo mới, khúc sau thì ghi tiếp)
        if (!_receivingFiles.TryGetValue(chunk.FileId, out var fs))
        {
            fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            _receivingFiles[chunk.FileId] = fs;
        }

        // 3. Ghi dữ liệu của khúc này vào file
        await fs.WriteAsync(chunk.Data, ct);

        // Báo cáo tiến trình
        progress?.Report(new TransferProgress
        {
            FileId = chunk.FileId,
            TransferredBytes = fs.Length,
            TotalBytes = chunk.TotalFileSize
        });

        // 4. Nếu là khúc cuối cùng -> Đóng file, kiểm tra mã băm và đổi tên
        if (chunk.IsLastChunk)
        {
            await fs.DisposeAsync();
            _receivingFiles.Remove(chunk.FileId);

            // Kiểm tra toàn vẹn (SHA-256)
            string actualHash = await Sha256Helper.ComputeFileHashAsync(tempFilePath, ct);
            if (actualHash != chunk.Sha256Checksum)
            {
                File.Delete(tempFilePath);
                throw new Exception("File bị lỗi trong quá trình truyền (Sai mã SHA-256)!");
            }

            // Đổi tên thành file hoàn chỉnh
            string finalFilePath = Path.Combine(saveDirectory, chunk.FileName);
            if (File.Exists(finalFilePath)) File.Delete(finalFilePath); // Ghi đè nếu đã có
            File.Move(tempFilePath, finalFilePath);
        }
    }
}