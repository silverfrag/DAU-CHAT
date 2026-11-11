using System.Security.Cryptography;

namespace DauChat.FileService.Utils;

public static class Sha256Helper
{
    public static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        byte[] hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}