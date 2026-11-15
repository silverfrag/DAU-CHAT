using DauChat.Shared.Models;

namespace DauChat.FileService.Services;

public interface ILocalFileStorage
{
    Task SaveFileAsync(string filePath, string userId, string roomId);
    Task<List<FileManifestDto>> GetDownloadedFilesAsync(string userId);
    Task DeleteLocalFileAsync(string userId, string fileId);
}