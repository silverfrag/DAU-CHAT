namespace DauChat.Shared.Models;

/// <summary>
/// DTO báo cáo phần trăm (%) tiến trình gửi/nhận tệp theo thời gian thực (MOD_05_03).
/// </summary>
public sealed class FileTransferProgressDto
{
    public string TransferId { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public bool IsRoom { get; set; }
    public bool IsUpload { get; set; }
    public long TotalBytes { get; set; }
    public long TransferredBytes { get; set; }
    public double Percentage => TotalBytes <= 0 ? 0d : Math.Round((double)TransferredBytes * 100d / TotalBytes, 2);
    public bool IsCompleted { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

