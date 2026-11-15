namespace DauChat.FileService.Models;

// Khuôn mẫu chứa thông tin cơ bản của File trước khi gửi
public class FileMetadata
{
    public string FileId { get; set; } = string.Empty; // Mã định danh file
    public string FileName { get; set; } = string.Empty; // Tên file (vd: DoAn.pdf)
    public long FileSize { get; set; } // Dung lượng file
    public string Sha256 { get; set; } = string.Empty; // Mã băm kiểm tra
    public string SenderId { get; set; } = string.Empty; // Người gửi
    public string ReceiverId { get; set; } = string.Empty; // Người nhận / Phòng
}

// Khuôn mẫu đại diện cho 1 khúc (chunk) file được gửi đi
public class FileChunk
{
    public string FileId { get; set; } = string.Empty; // Thuộc về file nào
    public int ChunkIndex { get; set; } // Khúc thứ mấy
    public byte[] Data { get; set; } = Array.Empty<byte>(); // Dữ liệu của khúc
    public bool IsLast { get; set; } // Có phải khúc cuối không
}

// Khuôn mẫu báo cáo tiến độ (để làm thanh Progress Bar)
public class TransferProgress
{
    public string FileId { get; set; } = string.Empty;
    public long TransferredBytes { get; set; }
    public long TotalBytes { get; set; }
    public double Percent => TotalBytes == 0 ? 0 : (double)TransferredBytes / TotalBytes * 100;
}