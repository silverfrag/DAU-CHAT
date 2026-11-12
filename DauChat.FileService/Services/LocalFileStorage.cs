using System.Text.Json;
using DauChat.Shared.Models;

namespace DauChat.FileService.Services;

public class LocalFileStorage : ILocalFileStorage
{
    private string GetDirectoryPath(string userId, string roomId)
    {
        string path = Path.Combine("LocalData", userId, "Files", roomId);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    public async Task SaveFileAsync(string filePath, string userId, string roomId)
    {
        string dirPath = GetDirectoryPath(userId, roomId);
        string fileName = Path.GetFileName(filePath);
        string destinationPath = Path.Combine(dirPath, fileName);

        if (File.Exists(destinationPath)) File.Delete(destinationPath);
        File.Move(filePath, destinationPath);

        var manifestPath = Path.Combine(dirPath, "file_manifest.jsonl");
        var fileInfo = new FileInfo(destinationPath);

        var manifestEntry = new FileManifestDto
        {
            FileId = Guid.NewGuid().ToString("N"),
            FileName = fileName,
            FileSizeBytes = fileInfo.Length,
            DownloadedAtUtc = DateTime.UtcNow,
            LocalFullPath = destinationPath // Đã sửa thành LocalFullPath
        };

        string jsonLine = JsonSerializer.Serialize(manifestEntry) + Environment.NewLine;
        await File.AppendAllTextAsync(manifestPath, jsonLine);
    }

    public async Task<List<FileManifestDto>> GetDownloadedFilesAsync(string userId)
    {
        var result = new List<FileManifestDto>();
        string baseDir = Path.Combine("LocalData", userId, "Files");

        if (!Directory.Exists(baseDir)) return result;

        var manifestFiles = Directory.GetFiles(baseDir, "file_manifest.jsonl", SearchOption.AllDirectories);

        foreach (var mFile in manifestFiles)
        {
            var lines = await File.ReadAllLinesAsync(mFile);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var dto = JsonSerializer.Deserialize<FileManifestDto>(line);
                    if (dto != null) result.Add(dto);
                }
                catch { }
            }
        }
        return result;
    }

    public Task DeleteLocalFileAsync(string userId, string fileId)
    {
        string baseDir = Path.Combine("LocalData", userId, "Files");
        if (!Directory.Exists(baseDir)) return Task.CompletedTask;

        var manifestFiles = Directory.GetFiles(baseDir, "file_manifest.jsonl", SearchOption.AllDirectories);

        foreach (var mFile in manifestFiles)
        {
            var lines = File.ReadAllLines(mFile).ToList();
            bool changed = false;

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var dto = JsonSerializer.Deserialize<FileManifestDto>(lines[i]);
                if (dto?.FileId == fileId)
                {
                    if (File.Exists(dto.LocalFullPath)) File.Delete(dto.LocalFullPath); // Đã sửa thành LocalFullPath
                    lines.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed) File.WriteAllLines(mFile, lines);
        }
        return Task.CompletedTask;
    }
}