using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;

namespace DauChat.Client.UI.Services;

internal sealed class DemoFileStorageService : IFileStorageService
{
    private readonly DemoDataStore _store;

    public DemoFileStorageService(DemoDataStore store)
    {
        _store = store;
    }

    public event EventHandler<FileTransferProgressDto>? TransferProgressChanged;
    public event EventHandler<FileManifestDto>? FileReceived;

    public async Task<OperationResult<FileManifestDto>> SendFileAsync(
        string senderId,
        string targetId,
        bool isRoom,
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            return OperationResult<FileManifestDto>.Fail("Không tìm thấy tệp nguồn trên ổ đĩa.");
        }

        var fileInfo = new FileInfo(sourceFilePath);
        var fileName = fileInfo.Name;
        var ext = fileInfo.Extension.ToLowerInvariant();
        var fileSize = Math.Max(fileInfo.Length, 1024L);
        var fileId = $"file_{Guid.NewGuid():N}"[..12];
        var transferId = $"tx_{Guid.NewGuid():N}"[..10];
        var storageKey = isRoom ? targetId : DemoDataStore.GetDirectConversationKey(senderId, targetId);

        string sha256;
        await using (var fs = File.OpenRead(sourceFilePath))
        {
            var hashBytes = await SHA256.HashDataAsync(fs, cancellationToken);
            sha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        const int steps = 5;
        for (var i = 1; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var transferred = (fileSize * i) / steps;
            TransferProgressChanged?.Invoke(this, new FileTransferProgressDto
            {
                TransferId = transferId,
                FileId = fileId,
                FileName = fileName,
                TargetId = targetId,
                IsRoom = isRoom,
                IsUpload = true,
                TotalBytes = fileSize,
                TransferredBytes = transferred,
                IsCompleted = i == steps,
                IsSuccess = i == steps
            });
            if (i < steps)
            {
                await Task.Delay(140, cancellationToken);
            }
        }

        var manifest = _store.MutateAndSave(() =>
        {
            var sender = _store.FindAccountById(senderId)?.User;
            var entry = new FileManifestDto
            {
                FileId = fileId,
                FileName = fileName,
                Extension = ext,
                Category = ResolveCategory(ext),
                RoomOrPartnerId = storageKey,
                IsRoom = isRoom,
                SenderId = senderId,
                SenderName = sender?.FullName ?? senderId,
                FileSizeBytes = fileInfo.Length,
                Sha256Checksum = sha256,
                LocalRelativePath = $"Files/{storageKey}/{fileName}",
                LocalFullPath = sourceFilePath,
                DownloadedAtUtc = DateTime.UtcNow,
                LastAccessedAtUtc = DateTime.UtcNow
            };
            _store.LocalFiles.Insert(0, entry);
            var chunkCount = Math.Max(1L, (fileInfo.Length + 65535) / 65536);
            _store.TotalPacketsSent += chunkCount;
            _store.TotalPacketsReceived += chunkCount;
            _store.TotalBytesSent += fileInfo.Length;
            _store.TotalBytesReceived += fileInfo.Length;
            return entry;
        });

        FileReceived?.Invoke(this, manifest);
        return OperationResult<FileManifestDto>.Ok(manifest, "Đã truyền tệp và xác thực SHA-256.");
    }

    public Task<OperationResult<FileManifestDto?>> ReceiveFileChunkAsync(
        string currentUserId,
        FileChunkDto chunk,
        CancellationToken cancellationToken = default)
        => Task.FromResult(OperationResult<FileManifestDto?>.Ok(null));

    public Task<IReadOnlyList<FileManifestDto>> GetConversationFilesAsync(
        string currentUserId,
        string roomOrPartnerId,
        CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var dmKey = DemoDataStore.GetDirectConversationKey(currentUserId, roomOrPartnerId);
            var list = _store.LocalFiles
                .Where(f => !_store.IsFileHiddenForUser(currentUserId, f.FileId)
                         && (f.RoomOrPartnerId == roomOrPartnerId || (!f.IsRoom && f.RoomOrPartnerId == dmKey)))
                .OrderByDescending(f => f.DownloadedAtUtc)
                .ToList();
            return Task.FromResult<IReadOnlyList<FileManifestDto>>(list);
        }
    }

    public Task<OperationResult> OpenLocalFileAsync(string localFilePath, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(localFilePath) && File.Exists(localFilePath))
        {
            Process.Start(new ProcessStartInfo(localFilePath) { UseShellExecute = true });
            return Task.FromResult(OperationResult.Ok("Đã mở tệp bằng ứng dụng Windows mặc định."));
        }

        return Task.FromResult(OperationResult.Ok("Tệp mẫu demo đã được xác thực SHA-256 trên danh mục Local."));
    }

    public Task<OperationResult> DeleteLocalFileAsync(
        string currentUserId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        _store.MutateAndSave(() =>
        {
            if (!_store.HiddenFileIdsByUser.TryGetValue(currentUserId, out var ids))
            {
                ids = new List<string>();
                _store.HiddenFileIdsByUser[currentUserId] = ids;
            }
            if (!ids.Contains(fileId)) ids.Add(fileId);
        });

        return Task.FromResult(OperationResult.Ok("Đã xóa tệp khỏi kho Local trên máy này."));
    }

    public Task<OperationResult> RegisterManifestEntryAsync(
        string currentUserId,
        FileManifestDto manifest,
        CancellationToken cancellationToken = default)
    {
        _store.MutateAndSave(() => _store.LocalFiles.Insert(0, manifest));
        return Task.FromResult(OperationResult.Ok());
    }

    public Task<IReadOnlyList<FileManifestDto>> GetAllLocalFilesAsync(
        string currentUserId,
        FileCategory? categoryFilter = null,
        string? searchKeyword = null,
        CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            IEnumerable<FileManifestDto> q = _store.LocalFiles
                .Where(f => !_store.IsFileHiddenForUser(currentUserId, f.FileId));

            if (categoryFilter.HasValue && categoryFilter.Value != FileCategory.All)
            {
                q = q.Where(f => f.Category == categoryFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var k = searchKeyword.Trim();
                q = q.Where(f => f.FileName.Contains(k, StringComparison.OrdinalIgnoreCase)
                              || f.SenderName.Contains(k, StringComparison.OrdinalIgnoreCase)
                              || f.Sha256Checksum.Contains(k, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult<IReadOnlyList<FileManifestDto>>(q.OrderByDescending(f => f.DownloadedAtUtc).ToList());
        }
    }

    public Task<StorageStatsDto> GetStorageStatsAsync(string currentUserId, CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var visibleFiles = _store.LocalFiles
                .Where(f => !_store.IsFileHiddenForUser(currentUserId, f.FileId))
                .ToList();

            var msgBytesByConv = _store.MessagesByConversation.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var cutoff = _store.GetClearedCutoffUtc(currentUserId, kvp.Key);
                    return (long)kvp.Value
                        .Where(m => m.ServerTimestampUtc > cutoff)
                        .Sum(m => Encoding.UTF8.GetByteCount(m.Content) + 180);
                });

            var allConvIds = _store.Rooms.Select(r => r.RoomId)
                .Concat(visibleFiles.Select(f => f.RoomOrPartnerId))
                .Concat(msgBytesByConv.Keys)
                .Distinct()
                .ToList();

            var breakdown = new List<ConversationStorageStatDto>();
            foreach (var convId in allConvIds)
            {
                var room = _store.Rooms.FirstOrDefault(r => r.RoomId == convId);
                string displayName = room?.RoomName ?? ResolveConvDisplayNameLocked(currentUserId, convId);
                var files = visibleFiles.Where(f => f.RoomOrPartnerId == convId).ToList();
                var mBytes = msgBytesByConv.GetValueOrDefault(convId, 0);
                var fBytes = files.Sum(f => f.FileSizeBytes);
                if (mBytes == 0 && fBytes == 0 && room is null) continue;

                breakdown.Add(new ConversationStorageStatDto
                {
                    RoomOrPartnerId = convId,
                    DisplayName = displayName,
                    IsRoom = room is not null,
                    MessagesBytes = mBytes,
                    FilesBytes = fBytes,
                    FileCount = files.Count
                });
            }

            var stats = new StorageStatsDto
            {
                UserId = currentUserId,
                TotalMessagesBytes = breakdown.Sum(b => b.MessagesBytes),
                TotalFilesBytes = breakdown.Sum(b => b.FilesBytes),
                TotalFilesCount = visibleFiles.Count,
                BreakdownByConversation = breakdown.OrderByDescending(b => b.FilesBytes + b.MessagesBytes).ToList()
            };
            return Task.FromResult(stats);
        }
    }

    public Task<OperationResult<StorageCleanupResultDto>> CleanupOldFilesAsync(
        string currentUserId,
        int? olderThanDays = null,
        long? targetMaxBytes = null,
        CancellationToken cancellationToken = default)
    {
        var result = _store.MutateAndSave(() =>
        {
            var visible = _store.LocalFiles
                .Where(f => !_store.IsFileHiddenForUser(currentUserId, f.FileId))
                .OrderBy(f => f.LastAccessedAtUtc)
                .ToList();
            var removed = new List<FileManifestDto>();

            if (olderThanDays.HasValue)
            {
                var cutoff = DateTime.UtcNow.AddDays(-olderThanDays.Value);
                foreach (var file in visible)
                {
                    if (file.LastAccessedAtUtc <= cutoff)
                    {
                        removed.Add(file);
                    }
                }
            }
            else if (targetMaxBytes.HasValue)
            {
                var currentTotal = visible.Sum(f => f.FileSizeBytes);
                foreach (var file in visible)
                {
                    if (currentTotal <= targetMaxBytes.Value) break;
                    removed.Add(file);
                    currentTotal -= file.FileSizeBytes;
                }
            }

            if (!_store.HiddenFileIdsByUser.TryGetValue(currentUserId, out var hidden))
            {
                hidden = new List<string>();
                _store.HiddenFileIdsByUser[currentUserId] = hidden;
            }
            foreach (var r in removed)
            {
                if (!hidden.Contains(r.FileId)) hidden.Add(r.FileId);
            }

            return new StorageCleanupResultDto
            {
                DeletedFilesCount = removed.Count,
                FreedBytes = removed.Sum(f => f.FileSizeBytes),
                DeletedFileIds = removed.Select(f => f.FileId).ToList()
            };
        });

        return Task.FromResult(OperationResult<StorageCleanupResultDto>.Ok(
            result,
            result.DeletedFilesCount > 0
                ? $"Đã dọn {result.DeletedFilesCount} tệp cũ (Oldest-first), giải phóng {FormatBytes(result.FreedBytes)}."
                : "Không có tệp nào vượt ngưỡng cần dọn."));
    }

    public async Task<OperationResult<string>> ExportBackupZipAsync(
        string currentUserId,
        string destinationZipPath,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dauchat_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string messagesJson;
            string manifestJson;
            lock (_store.SyncRoot)
            {
                messagesJson = JsonSerializer.Serialize(_store.MessagesByConversation, new JsonSerializerOptions { WriteIndented = true });
                var visible = _store.LocalFiles.Where(f => !_store.IsFileHiddenForUser(currentUserId, f.FileId)).ToList();
                manifestJson = JsonSerializer.Serialize(visible, new JsonSerializerOptions { WriteIndented = true });
            }

            await File.WriteAllTextAsync(Path.Combine(tempDir, "messages_snapshot.json"), messagesJson, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file_manifest.jsonl"), manifestJson, cancellationToken);

            if (File.Exists(destinationZipPath))
            {
                File.Delete(destinationZipPath);
            }

            ZipFile.CreateFromDirectory(tempDir, destinationZipPath);
            return OperationResult<string>.Ok(destinationZipPath, $"Đã đóng gói bản sao lưu .ZIP tại {Path.GetFileName(destinationZipPath)}.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    public async Task<OperationResult> RestoreBackupZipAsync(
        string currentUserId,
        string sourceZipPath,
        bool overwriteExisting = false,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceZipPath))
        {
            return OperationResult.Fail("Không tìm thấy tệp sao lưu .ZIP.");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"dauchat_restore_{Guid.NewGuid():N}");
        try
        {
            ZipFile.ExtractToDirectory(sourceZipPath, tempDir, overwriteFiles: true);
            var manifestPath = Path.Combine(tempDir, "file_manifest.jsonl");
            if (File.Exists(manifestPath))
            {
                var raw = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                var entries = JsonSerializer.Deserialize<List<FileManifestDto>>(raw);
                if (entries is not null)
                {
                    _store.MutateAndSave(() =>
                    {
                        if (overwriteExisting)
                        {
                            _store.HiddenFileIdsByUser.Remove(currentUserId);
                        }
                        foreach (var e in entries)
                        {
                            if (_store.LocalFiles.All(x => x.FileId != e.FileId))
                            {
                                _store.LocalFiles.Add(e);
                            }
                            if (_store.HiddenFileIdsByUser.TryGetValue(currentUserId, out var hidden))
                            {
                                hidden.Remove(e.FileId);
                            }
                        }
                    });
                }
            }

            return OperationResult.Ok("Đã khôi phục dữ liệu từ bản sao lưu .ZIP.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Tệp sao lưu không hợp lệ: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private string ResolveConvDisplayNameLocked(string currentUserId, string convId)
    {
        if (convId.StartsWith("dm:", StringComparison.Ordinal))
        {
            var parts = convId[3..].Split("::");
            var otherId = parts.FirstOrDefault(p => p != currentUserId) ?? convId;
            return _store.FindAccountById(otherId)?.User.FullName ?? otherId;
        }
        return _store.FindAccountById(convId)?.User.FullName ?? convId;
    }

    public static FileCategory ResolveCategory(string extension) => extension.ToLowerInvariant() switch
    {
        ".dwg" or ".dxf" or ".rvt" or ".skp" => FileCategory.CAD,
        ".pdf" => FileCategory.PDF,
        ".zip" or ".rar" or ".7z" => FileCategory.Archive,
        ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" => FileCategory.Image,
        ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" => FileCategory.Document,
        _ => FileCategory.Other
    };

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024d * 1024d):0.0} MB";
        return $"{bytes / (1024d * 1024d * 1024d):0.00} GB";
    }
}
