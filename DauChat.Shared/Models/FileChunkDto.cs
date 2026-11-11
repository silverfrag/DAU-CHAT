using DauChat.Shared.Constants;

namespace DauChat.Shared.Models;

/// <summary>
/// DTO từng mảnh tệp (64KB/chunk) truyền qua stream TCP (MOD_05_01 -> MOD_05_03).
/// </summary>
public sealed class FileChunkDto
{
    public string TransferId { get; set; } = Guid.NewGuid().ToString("N");
    public string FileId { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;

    /// <summary>ReceiverId (chat 1-1) hoặc RoomId (chat nhóm).</summary>
    public string TargetId { get; set; } = string.Empty;
    public bool IsRoom { get; set; }

    public long TotalFileSize { get; set; }
    public string Sha256Checksum { get; set; } = string.Empty;
    public int ChunkSize { get; set; } = ProtocolConstants.DefaultFileChunkSizeBytes;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public bool IsLastChunk { get; set; }
}

