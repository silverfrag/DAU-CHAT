using DauChat.Shared.Enums;

namespace DauChat.Shared.Models;

/// <summary>
/// DTO bản ghi chỉ mục tệp lưu trong file_manifest.jsonl (MOD_05_04, MOD_05_05, MOD_07_03, MOD_07_04).
/// </summary>
public sealed class FileManifestDto
{
    public string FileId { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public FileCategory Category { get; set; } = FileCategory.Other;
    public string RoomOrPartnerId { get; set; } = string.Empty;
    public bool IsRoom { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256Checksum { get; set; } = string.Empty;
    public string LocalRelativePath { get; set; } = string.Empty;
    public string LocalFullPath { get; set; } = string.Empty;
    public DateTime DownloadedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAtUtc { get; set; } = DateTime.UtcNow;
}

