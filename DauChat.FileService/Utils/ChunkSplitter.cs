namespace DauChat.FileService.Utils;

public static class ChunkSplitter
{
    // Kích thước mỗi khối là 64KB (65536 bytes)
    public const int DefaultChunkSize = 64 * 1024;

    public static async IAsyncEnumerable<(int Index, byte[] Data, bool IsLast)> ReadChunksAsync(
        string filePath, int chunkSize = DefaultChunkSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, useAsync: true);
        byte[] buffer = new byte[chunkSize];
        int index = 0;
        int bytesRead;

        while ((bytesRead = await fs.ReadAsync(buffer, 0, chunkSize, ct)) > 0)
        {
            byte[] data = buffer[..bytesRead];
            bool isLast = fs.Position >= fs.Length;
            yield return (index++, data, isLast);
        }
    }
}