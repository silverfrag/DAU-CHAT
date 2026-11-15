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

        const long maxZaloFileSizeBytes = 1024L * 1024 * 1024; // 1 GB (Chuẩn Zalo)
        if (fileInfo.Length > maxZaloFileSizeBytes)
        {
            return OperationResult<FileManifestDto>.Fail($"Kích thước tệp '{fileName}' ({FormatBytes(fileInfo.Length)}) vượt quá giới hạn tối đa 1GB theo chuẩn Zalo.");
        }

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
                        .Where(m => m.ServerTimestampUtc > cutoff && !_store.IsMessageHiddenForUser(currentUserId, m.MessageId))
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
                if (room is not null && !room.MemberIds.Contains(currentUserId))
                {
                    continue;
                }

                if (convId.StartsWith("dm:", StringComparison.Ordinal)
                    && !convId[3..].Split("::").Contains(currentUserId))
                {
                    continue;
                }

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
        bool includeFiles = false,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dauchat_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string messagesJson;
            string manifestJson;
            string htmlContent;
            List<FileManifestDto> visibleFiles;
            lock (_store.SyncRoot)
            {
                messagesJson = JsonSerializer.Serialize(_store.MessagesByConversation, new JsonSerializerOptions { WriteIndented = true });
                visibleFiles = _store.LocalFiles.Where(f => !_store.IsFileHiddenForUser(currentUserId, f.FileId)).ToList();
                manifestJson = JsonSerializer.Serialize(visibleFiles, new JsonSerializerOptions { WriteIndented = true });
                htmlContent = GenerateHtmlTranscriptLocked(currentUserId, includeFiles);
            }

            await File.WriteAllTextAsync(Path.Combine(tempDir, "messages_snapshot.json"), messagesJson, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file_manifest.jsonl"), manifestJson, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "LichSuTroChuyen.html"), htmlContent, System.Text.Encoding.UTF8, cancellationToken);

            int packagedFilesCount = 0;
            long packagedBytes = 0;
            if (includeFiles)
            {
                var filesDir = Path.Combine(tempDir, "files");
                Directory.CreateDirectory(filesDir);
                foreach (var f in visibleFiles)
                {
                    if (!string.IsNullOrWhiteSpace(f.LocalFullPath) && File.Exists(f.LocalFullPath))
                    {
                        try
                        {
                            var targetSubDir = Path.Combine(filesDir, f.FileId);
                            Directory.CreateDirectory(targetSubDir);
                            var targetFilePath = Path.Combine(targetSubDir, f.FileName);
                            File.Copy(f.LocalFullPath, targetFilePath, overwrite: true);
                            packagedFilesCount++;
                            packagedBytes += new FileInfo(targetFilePath).Length;
                        }
                        catch
                        {
                            // Bỏ qua nếu tệp bị khóa IO
                        }
                    }
                }
            }

            var guideText =
@"========================================================================
   HƯỚNG DẪN XEM LỊCH SỬ TRÒ CHUYỆN & TÀI LIỆU DAU CHAT (DÀNH CHO NGƯỜI DÙNG)
========================================================================

1. CÁCH XEM TRỰC TIẾP TRÊN TRÌNH DUYỆT (DỄ DÙNG NHẤT):
   - Bạn chỉ cần nhấp đúp chuột (Double click) vào tệp:
     --> 'LichSuTroChuyen.html'
   - Tệp sẽ tự động mở trên trình duyệt Web (Google Chrome, Microsoft Edge,
     Cốc Cốc, Safari, Firefox...) mà KHÔNG CẦN cài đặt bất kỳ phần mềm chuyên
     môn hay công cụ lập trình nào.
   - Giao diện trực quan theo chuẩn DAU Chat: hiển thị đầy đủ phòng chat,
     người gửi, vai trò (Giảng viên, Sinh viên, Quản trị), thời gian,
     tin nhắn trích dẫn (reply) và danh mục tài liệu kèm mã băm SHA-256.
   - Nếu bản sao lưu được chọn có đính kèm toàn bộ tệp, bạn có thể nhấp trực
     tiếp vào tệp đính kèm trong trang HTML để mở tệp ngay, hoặc truy cập
     thư mục 'files/' để xem toàn bộ tài liệu và hình ảnh đã lưu.

2. CÁC TỆP DÀNH CHO PHỤC HỒI DỮ LIỆU VÀO PHẦN MỀM (RESTORE):
   - 'messages_snapshot.json': Lưu cấu trúc tin nhắn để ứng dụng DAU Chat
     có thể phục hồi toàn bộ cuộc hội thoại khi bạn chuyển máy tính mới.
   - 'file_manifest.jsonl': Danh mục kiểm tra tính toàn vẹn của tệp đính kèm.
   - Thư mục 'files/': Chứa toàn bộ tệp đính kèm vật lý đã được đóng gói.

------------------------------------------------------------------------
DAU Chat • Trường Đại học Kiến trúc Đà Nẵng
========================================================================";

            await File.WriteAllTextAsync(Path.Combine(tempDir, "HuongDan_DocLichSu.txt"), guideText, System.Text.Encoding.UTF8, cancellationToken);

            if (File.Exists(destinationZipPath))
            {
                File.Delete(destinationZipPath);
            }

            ZipFile.CreateFromDirectory(tempDir, destinationZipPath);
            var note = includeFiles
                ? $"Đã đóng gói bản sao lưu .ZIP đầy đủ (kèm {packagedFilesCount} tệp • {FormatBytes(packagedBytes)}) tại {Path.GetFileName(destinationZipPath)}."
                : $"Đã đóng gói bản sao lưu .ZIP (tin nhắn & HTML) tại {Path.GetFileName(destinationZipPath)}.";
            return OperationResult<string>.Ok(destinationZipPath, note);
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
            var messagesPath = Path.Combine(tempDir, "messages_snapshot.json");
            Dictionary<string, List<MessageDto>>? restoredMessages = null;
            if (File.Exists(messagesPath))
            {
                var rawMsgs = await File.ReadAllTextAsync(messagesPath, cancellationToken);
                restoredMessages = JsonSerializer.Deserialize<Dictionary<string, List<MessageDto>>>(rawMsgs);
            }

            List<FileManifestDto>? restoredManifests = null;
            var manifestPath = Path.Combine(tempDir, "file_manifest.jsonl");
            if (File.Exists(manifestPath))
            {
                var raw = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                restoredManifests = JsonSerializer.Deserialize<List<FileManifestDto>>(raw);
            }

            int restoredPhysicalFiles = 0;
            var filesDir = Path.Combine(tempDir, "files");
            if (Directory.Exists(filesDir) && restoredManifests is not null)
            {
                var localUserFilesDir = Path.Combine(AppContext.BaseDirectory, "LocalData", currentUserId, "Files");
                Directory.CreateDirectory(localUserFilesDir);

                foreach (var e in restoredManifests)
                {
                    var packagedFile = Path.Combine(filesDir, e.FileId, e.FileName);
                    if (File.Exists(packagedFile))
                    {
                        try
                        {
                            var destSubDir = Path.Combine(localUserFilesDir, string.IsNullOrWhiteSpace(e.RoomOrPartnerId) ? "General" : e.RoomOrPartnerId);
                            Directory.CreateDirectory(destSubDir);
                            var destFile = Path.Combine(destSubDir, e.FileName);
                            File.Copy(packagedFile, destFile, overwrite: true);
                            e.LocalFullPath = destFile;
                            e.LocalRelativePath = $"Files/{e.RoomOrPartnerId}/{e.FileName}";
                            e.LastAccessedAtUtc = DateTime.UtcNow;
                            restoredPhysicalFiles++;
                        }
                        catch
                        {
                            // Bỏ qua nếu lỗi chép tệp
                        }
                    }
                }
            }

            _store.MutateAndSave(() =>
            {
                _store.ClearedBeforeUtc.Remove(currentUserId);
                _store.HiddenMessageIdsByUser.Remove(currentUserId);
                if (overwriteExisting)
                {
                    _store.HiddenFileIdsByUser.Remove(currentUserId);
                }

                if (restoredMessages is not null)
                {
                    foreach (var kvp in restoredMessages)
                    {
                        if (!_store.MessagesByConversation.TryGetValue(kvp.Key, out var list))
                        {
                            list = new List<MessageDto>();
                            _store.MessagesByConversation[kvp.Key] = list;
                        }

                        var existingIds = list.Select(m => m.MessageId).ToHashSet();
                        foreach (var m in kvp.Value)
                        {
                            if (_store.RecalledMessageIds.Contains(m.MessageId))
                            {
                                continue;
                            }
                            if (existingIds.Add(m.MessageId))
                            {
                                list.Add(m);
                            }
                        }
                        list.Sort((a, b) => a.ServerTimestampUtc.CompareTo(b.ServerTimestampUtc));
                    }
                }

                if (restoredManifests is not null)
                {
                    foreach (var e in restoredManifests)
                    {
                        var existing = _store.LocalFiles.FirstOrDefault(x => x.FileId == e.FileId);
                        if (existing is null)
                        {
                            _store.LocalFiles.Add(e);
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(e.LocalFullPath) && File.Exists(e.LocalFullPath))
                            {
                                existing.LocalFullPath = e.LocalFullPath;
                            }
                        }
                        if (_store.HiddenFileIdsByUser.TryGetValue(currentUserId, out var hidden))
                        {
                            hidden.Remove(e.FileId);
                        }
                    }
                }
            });

            var msg = restoredPhysicalFiles > 0
                ? $"Đã khôi phục tin nhắn và {restoredPhysicalFiles} tệp đính kèm vật lý từ bản sao lưu .ZIP."
                : "Đã khôi phục tin nhắn & danh mục tệp từ bản sao lưu .ZIP.";
            return OperationResult.Ok(msg);
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

    private string GenerateHtmlTranscriptLocked(string currentUserId, bool includeFiles = false)
    {
        var sb = new System.Text.StringBuilder();
        var user = _store.FindAccountById(currentUserId)?.User;
        var userName = user?.FullName ?? currentUserId;
        var now = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"vi\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>DAU Chat • Bản sao lưu lịch sử trò chuyện</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    :root { --dau-red: #CB0914; --dau-red-soft: #FEF2F2; --text-primary: #0F172A; --text-muted: #64748B; --bg: #F8FAFC; --card: #FFFFFF; --border: #E2E8F0; }");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }");
        sb.AppendLine("    body { background-color: var(--bg); color: var(--text-primary); line-height: 1.5; padding: 24px; }");
        sb.AppendLine("    .container { max-width: 900px; margin: 0 auto; background: var(--card); border: 1px solid var(--border); border-radius: 16px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.04); }");
        sb.AppendLine("    .header { background: var(--dau-red); color: white; padding: 28px 32px; border-bottom: 3px solid #991B1B; }");
        sb.AppendLine("    .header h1 { font-size: 22px; font-weight: 800; letter-spacing: -0.5px; }");
        sb.AppendLine("    .header p { font-size: 13px; opacity: 0.9; margin-top: 4px; }");
        sb.AppendLine("    .meta-bar { background: #FFF1F2; padding: 12px 32px; font-size: 12.5px; color: #9F1239; border-bottom: 1px solid var(--border); display: flex; justify-content: space-between; flex-wrap: wrap; gap: 8px; }");
        sb.AppendLine("    .content { padding: 24px 32px; }");
        sb.AppendLine("    .conv-section { margin-bottom: 36px; border: 1px solid var(--border); border-radius: 12px; overflow: hidden; }");
        sb.AppendLine("    .conv-header { background: #F1F5F9; padding: 12px 18px; font-size: 15px; font-weight: 700; color: #1E293B; border-bottom: 1px solid var(--border); display: flex; justify-content: space-between; align-items: center; }");
        sb.AppendLine("    .conv-badge { font-size: 11px; padding: 3px 8px; border-radius: 6px; background: white; border: 1px solid var(--border); color: var(--text-muted); font-weight: 600; }");
        sb.AppendLine("    .messages-list { padding: 18px; display: flex; flex-direction: column; gap: 14px; background: #FFFFFF; }");
        sb.AppendLine("    .msg-row { display: flex; flex-direction: column; max-width: 82%; }");
        sb.AppendLine("    .msg-row.out { align-self: flex-end; }");
        sb.AppendLine("    .msg-row.in { align-self: flex-start; }");
        sb.AppendLine("    .msg-meta { font-size: 11px; color: var(--text-muted); margin-bottom: 3px; display: flex; align-items: center; gap: 6px; }");
        sb.AppendLine("    .msg-meta .sender { font-weight: 600; color: #334155; }");
        sb.AppendLine("    .msg-bubble { padding: 10px 14px; border-radius: 14px; font-size: 13.5px; line-height: 1.45; word-break: break-word; }");
        sb.AppendLine("    .msg-row.out .msg-bubble { background: var(--dau-red); color: white; border-bottom-right-radius: 4px; }");
        sb.AppendLine("    .msg-row.in .msg-bubble { background: #F8FAFC; color: #1E293B; border: 1px solid var(--border); border-bottom-left-radius: 4px; }");
        sb.AppendLine("    .quote-box { background: rgba(0,0,0,0.06); border-left: 3px solid #991B1B; padding: 6px 10px; border-radius: 4px; margin-bottom: 8px; font-size: 12px; }");
        sb.AppendLine("    .msg-row.out .quote-box { background: rgba(255,255,255,0.18); border-left-color: white; color: #FFF1F2; }");
        sb.AppendLine("    .file-card { display: flex; align-items: center; gap: 10px; background: rgba(0,0,0,0.04); padding: 8px 12px; border-radius: 8px; margin-top: 4px; transition: background 0.15s ease; }");
        sb.AppendLine("    .file-card:hover { background: rgba(0,0,0,0.08); }");
        sb.AppendLine("    .msg-row.out .file-card { background: rgba(255,255,255,0.15); }");
        sb.AppendLine("    .msg-row.out .file-card:hover { background: rgba(255,255,255,0.25); }");
        sb.AppendLine("    .file-icon { font-weight: bold; font-size: 11px; padding: 4px 8px; border-radius: 4px; background: white; color: var(--dau-red); }");
        sb.AppendLine("    .recalled { font-style: italic; opacity: 0.7; }");
        sb.AppendLine("    .footer { text-align: center; padding: 20px; font-size: 12px; color: var(--text-muted); border-top: 1px solid var(--border); }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine("    <div class=\"header\">");
        sb.AppendLine("      <h1>DAU CHAT • LỊCH SỬ TRÒ CHUYỆN VÀ TÀI LIỆU</h1>");
        sb.AppendLine("      <p>Trường Đại học Kiến trúc Đà Nẵng — Bản sao lưu giao diện trực quan cho người dùng</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine($"    <div class=\"meta-bar\"><span>👤 <strong>Chủ tài khoản:</strong> {System.Net.WebUtility.HtmlEncode(userName)} ({System.Net.WebUtility.HtmlEncode(user?.Username ?? currentUserId)})</span><span>🕒 <strong>Xuất lúc:</strong> {now}</span></div>");
        sb.AppendLine("    <div class=\"content\">");

        var allConvKeys = _store.MessagesByConversation.Keys.ToList();
        if (allConvKeys.Count == 0)
        {
            sb.AppendLine("      <p style=\"color: var(--text-muted); text-align: center; padding: 40px;\">Không có tin nhắn nào trong lịch sử.</p>");
        }

        foreach (var key in allConvKeys)
        {
            var isRoom = !key.StartsWith("dm:", StringComparison.Ordinal);
            if (!isRoom)
            {
                var parts = key[3..].Split("::");
                if (!parts.Contains(currentUserId)) continue;
            }
            else
            {
                var room = _store.Rooms.FirstOrDefault(r => r.RoomId == key);
                if (room is not null && !room.MemberIds.Contains(currentUserId) && user?.Role != UserRole.Admin)
                {
                    continue;
                }
            }

            var title = isRoom
                ? (_store.Rooms.FirstOrDefault(r => r.RoomId == key)?.RoomName ?? key)
                : ($"Trò chuyện trực tiếp: {ResolveConvDisplayNameLocked(currentUserId, key)}");

            var msgs = _store.MessagesByConversation[key]
                .Where(m => !_store.IsMessageHiddenForUser(currentUserId, m.MessageId))
                .OrderBy(m => m.ServerTimestampUtc)
                .ToList();

            if (msgs.Count == 0) continue;

            sb.AppendLine("      <div class=\"conv-section\">");
            sb.AppendLine($"        <div class=\"conv-header\"><span>💬 {System.Net.WebUtility.HtmlEncode(title)}</span><span class=\"conv-badge\">{msgs.Count} tin nhắn</span></div>");
            sb.AppendLine("        <div class=\"messages-list\">");

            foreach (var m in msgs)
            {
                var isOut = m.SenderId == currentUserId;
                var rowClass = isOut ? "msg-row out" : "msg-row in";
                var timeStr = m.ServerTimestampUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                var senderDisplay = isOut ? "Bạn" : m.SenderName;

                sb.AppendLine($"          <div class=\"{rowClass}\">");
                sb.AppendLine($"            <div class=\"msg-meta\"><span class=\"sender\">{System.Net.WebUtility.HtmlEncode(senderDisplay)}</span><span>•</span><span>{timeStr}</span></div>");
                sb.AppendLine("            <div class=\"msg-bubble\">");

                if (m.Content == MessageQuoteCodec.RecalledMarker)
                {
                    sb.AppendLine("              <p class=\"recalled\">Tin nhắn đã được thu hồi</p>");
                }
                else
                {
                    if (MessageQuoteCodec.TryDecode(m.Content, out var quoteAuthor, out var quoteSnippet, out var body))
                    {
                        sb.AppendLine($"              <div class=\"quote-box\"><strong>{System.Net.WebUtility.HtmlEncode(quoteAuthor)}:</strong> {System.Net.WebUtility.HtmlEncode(quoteSnippet)}</div>");
                        sb.AppendLine($"              <p>{System.Net.WebUtility.HtmlEncode(body)}</p>");
                    }
                    else if (m.Type == MessageType.FileAttachment)
                    {
                        var ext = System.IO.Path.GetExtension(m.AttachmentFileName ?? "").ToUpperInvariant().TrimStart('.');
                        var sz = m.AttachmentFileSize.HasValue ? FormatBytes(m.AttachmentFileSize.Value) : "Tệp";
                        if (includeFiles && !string.IsNullOrWhiteSpace(m.AttachmentFileId))
                        {
                            var safeName = System.Net.WebUtility.UrlEncode(m.AttachmentFileName ?? "file");
                            var href = $"files/{m.AttachmentFileId}/{safeName}";
                            sb.AppendLine($"              <a href=\"{href}\" target=\"_blank\" style=\"text-decoration:none; color:inherit;\"><div class=\"file-card\"><span class=\"file-icon\">{System.Net.WebUtility.HtmlEncode(ext)}</span><div style=\"flex:1;\"><strong>{System.Net.WebUtility.HtmlEncode(m.AttachmentFileName ?? "Tệp")}</strong><div style=\"font-size: 11px; opacity: 0.85;\">{sz} • Xác thực SHA-256 (Nhấp để mở)</div></div><span style=\"font-size: 12px; font-weight:bold; color:var(--dau-red);\">↗</span></div></a>");
                        }
                        else
                        {
                            sb.AppendLine($"              <div class=\"file-card\"><span class=\"file-icon\">{System.Net.WebUtility.HtmlEncode(ext)}</span><div><strong>{System.Net.WebUtility.HtmlEncode(m.AttachmentFileName ?? "Tệp")}</strong><div style=\"font-size: 11px; opacity: 0.85;\">{sz} • Xác thực SHA-256</div></div></div>");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"              <p>{System.Net.WebUtility.HtmlEncode(m.Content)}</p>");
                    }
                }

                sb.AppendLine("            </div>");
                sb.AppendLine("          </div>");
            }

            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");
        }

        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"footer\">DAU Chat — Hệ thống nhắn tin & cộng tác học tập nội bộ • Đại học Kiến trúc Đà Nẵng</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public Task<SyncCheckResponseDto> CheckLanSyncAvailableAsync(string currentUserId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.CheckLanVaultSnapshot(currentUserId));
    }

    public async Task<OperationResult<SyncPushResponseDto>> PushSnapshotToLanServerAsync(
        string currentUserId,
        bool includeFiles = false,
        CancellationToken cancellationToken = default)
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"dauchat_lansync_push_{Guid.NewGuid():N}.zip");
        try
        {
            var exportRes = await ExportBackupZipAsync(currentUserId, tempZip, includeFiles, cancellationToken);
            if (!exportRes.Success || !File.Exists(tempZip))
            {
                return OperationResult<SyncPushResponseDto>.Fail("Không thể tạo bản đóng gói snapshot.");
            }

            var bytes = await File.ReadAllBytesAsync(tempZip, cancellationToken);
            var base64 = Convert.ToBase64String(bytes);

            int msgCount = 0;
            int fileCount = 0;
            lock (_store.SyncRoot)
            {
                msgCount = _store.MessagesByConversation.Values.Sum(l => l.Count);
                fileCount = _store.LocalFiles.Count(f => !_store.IsFileHiddenForUser(currentUserId, f.FileId));
            }

            var snap = new SyncPushRequestDto
            {
                UserId = currentUserId,
                SnapshotZipBase64 = base64,
                SnapshotSizeBytes = bytes.Length,
                TimestampUtc = DateTime.UtcNow,
                MessagesCount = msgCount,
                FilesCount = fileCount,
                Summary = $"{msgCount} tin nhắn • {fileCount} tệp • {FormatBytes(bytes.Length)}"
            };

            _store.SaveLanVaultSnapshot(snap);

            // Lưu kèm vào ServerData/Vaults nếu có
            try
            {
                var serverVault = Path.Combine(AppContext.BaseDirectory, "ServerData", "Vaults", currentUserId);
                Directory.CreateDirectory(serverVault);
                await File.WriteAllBytesAsync(Path.Combine(serverVault, "latest_snapshot.bin"), bytes, cancellationToken);
                await File.WriteAllTextAsync(Path.Combine(serverVault, "metadata.json"), JsonSerializer.Serialize(new SyncCheckResponseDto
                {
                    HasSnapshot = true,
                    LastSnapshotUtc = snap.TimestampUtc,
                    SnapshotSizeBytes = bytes.Length,
                    SnapshotSummary = snap.Summary,
                    TotalMessagesCount = msgCount,
                    TotalFilesCount = fileCount
                }), cancellationToken);
            }
            catch
            {
                // Bỏ qua lỗi IO phụ
            }

            return OperationResult<SyncPushResponseDto>.Ok(new SyncPushResponseDto
            {
                Success = true,
                Message = $"Đã đẩy bản sao lưu lên Server LAN ({FormatBytes(bytes.Length)}).",
                StoredBytes = bytes.Length,
                TimestampUtc = snap.TimestampUtc
            });
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
    }

    public async Task<OperationResult> PullSnapshotFromLanServerAsync(string currentUserId, CancellationToken cancellationToken = default)
    {
        var snap = _store.GetLanVaultSnapshot(currentUserId);
        byte[]? bytes = null;

        if (snap is not null && !string.IsNullOrWhiteSpace(snap.SnapshotZipBase64))
        {
            bytes = Convert.FromBase64String(snap.SnapshotZipBase64);
        }
        else
        {
            var serverVault = Path.Combine(AppContext.BaseDirectory, "ServerData", "Vaults", currentUserId, "latest_snapshot.bin");
            if (File.Exists(serverVault))
            {
                bytes = await File.ReadAllBytesAsync(serverVault, cancellationToken);
            }
        }

        if (bytes is null || bytes.Length == 0)
        {
            return OperationResult.Fail("Không tìm thấy bản sao lưu nào của bạn trên Server LAN.");
        }

        var tempZip = Path.Combine(Path.GetTempPath(), $"dauchat_lansync_pull_{Guid.NewGuid():N}.zip");
        try
        {
            await File.WriteAllBytesAsync(tempZip, bytes, cancellationToken);
            var restoreRes = await RestoreBackupZipAsync(currentUserId, tempZip, overwriteExisting: true, cancellationToken);
            if (restoreRes.Success)
            {
                return OperationResult.Ok($"Đồng bộ thành công từ Server LAN ({FormatBytes(bytes.Length)})!");
            }
            return restoreRes;
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
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
