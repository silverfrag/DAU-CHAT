using System.Text.Json;
using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.Server.Vault;

/// <summary>
/// Quản lý Tủ lưu trữ bản sao lưu mạng LAN (User Vault) và hạn mức đệm (Hot-Cache Quota) trên Server.
/// </summary>
public sealed class ServerVaultHandler
{
    private readonly string _vaultRoot;
    private readonly long _maxVaultSizeBytes;
    private readonly object _syncLock = new();

    public ServerVaultHandler(string? vaultRoot = null, long maxVaultSizeBytes = 1024L * 1024 * 1024) // 1GB trần an toàn demo
    {
        _vaultRoot = vaultRoot ?? Path.Combine(AppContext.BaseDirectory, "ServerData", "Vaults");
        _maxVaultSizeBytes = maxVaultSizeBytes;
        Directory.CreateDirectory(_vaultRoot);
    }

    public async void HandlePacket(object? sender, NetworkPacket packet)
    {
        if (packet.Type is not (PacketType.SyncCheckRequest or PacketType.SyncPushSnapshot or PacketType.SyncPullRequest))
        {
            return;
        }

        var response = await ProcessPacketAsync(packet);
        if (sender is INetworkService network && response is not null)
        {
            await network.SendToUserAsync(packet.SenderId, response);
        }
    }

    public Task<NetworkPacket?> ProcessPacketAsync(NetworkPacket packet)
    {
        return packet.Type switch
        {
            PacketType.SyncCheckRequest => Task.FromResult<NetworkPacket?>(HandleSyncCheck(packet)),
            PacketType.SyncPushSnapshot => Task.FromResult<NetworkPacket?>(HandleSyncPush(packet)),
            PacketType.SyncPullRequest => Task.FromResult<NetworkPacket?>(HandleSyncPull(packet)),
            _ => Task.FromResult<NetworkPacket?>(null)
        };
    }

    private NetworkPacket HandleSyncCheck(NetworkPacket packet)
    {
        var req = packet.GetPayload<SyncCheckRequestDto>();
        var userId = req?.UserId ?? packet.SenderId;
        var userVaultDir = Path.Combine(_vaultRoot, userId);
        var snapshotPath = Path.Combine(userVaultDir, "latest_snapshot.bin");
        var metaPath = Path.Combine(userVaultDir, "metadata.json");

        lock (_syncLock)
        {
            if (File.Exists(snapshotPath) && File.Exists(metaPath))
            {
                try
                {
                    var metaJson = File.ReadAllText(metaPath);
                    var meta = JsonSerializer.Deserialize<SyncCheckResponseDto>(metaJson);
                    if (meta is not null)
                    {
                        meta.HasSnapshot = true;
                        return NetworkPacket.Create(PacketType.SyncCheckResponse, "SERVER", OperationResult<SyncCheckResponseDto>.Ok(meta), packet.SenderId, packet.CorrelationId);
                    }
                }
                catch
                {
                    // Lỗi đọc metadata -> đọc trực tiếp file
                }

                var fi = new FileInfo(snapshotPath);
                var fallback = new SyncCheckResponseDto
                {
                    HasSnapshot = true,
                    LastSnapshotUtc = fi.LastWriteTimeUtc,
                    SnapshotSizeBytes = fi.Length,
                    SnapshotSummary = $"Bản sao lưu {fi.Length / 1024} KB"
                };
                return NetworkPacket.Create(PacketType.SyncCheckResponse, "SERVER", OperationResult<SyncCheckResponseDto>.Ok(fallback), packet.SenderId, packet.CorrelationId);
            }
        }

        var empty = new SyncCheckResponseDto { HasSnapshot = false };
        return NetworkPacket.Create(PacketType.SyncCheckResponse, "SERVER", OperationResult<SyncCheckResponseDto>.Ok(empty), packet.SenderId, packet.CorrelationId);
    }

    private NetworkPacket HandleSyncPush(NetworkPacket packet)
    {
        var req = packet.GetPayload<SyncPushRequestDto>();
        if (req is null || string.IsNullOrWhiteSpace(req.UserId) || string.IsNullOrWhiteSpace(req.SnapshotZipBase64))
        {
            return NetworkPacket.Create(PacketType.SyncPushResponse, "SERVER", OperationResult<SyncPushResponseDto>.Fail("Dữ liệu sao lưu không hợp lệ."), packet.SenderId, packet.CorrelationId);
        }

        lock (_syncLock)
        {
            try
            {
                var userVaultDir = Path.Combine(_vaultRoot, req.UserId);
                Directory.CreateDirectory(userVaultDir);

                var snapshotPath = Path.Combine(userVaultDir, "latest_snapshot.bin");
                var metaPath = Path.Combine(userVaultDir, "metadata.json");

                var bytes = Convert.FromBase64String(req.SnapshotZipBase64);
                File.WriteAllBytes(snapshotPath, bytes);

                var checkDto = new SyncCheckResponseDto
                {
                    HasSnapshot = true,
                    LastSnapshotUtc = req.TimestampUtc,
                    SnapshotSizeBytes = bytes.Length,
                    SnapshotSummary = string.IsNullOrWhiteSpace(req.Summary) ? $"{req.MessagesCount} tin nhắn • {req.FilesCount} tệp" : req.Summary,
                    TotalMessagesCount = req.MessagesCount,
                    TotalFilesCount = req.FilesCount
                };
                File.WriteAllText(metaPath, JsonSerializer.Serialize(checkDto));

                // Kiểm tra giới hạn Quota an toàn cho máy chủ laptop demo
                EnforceMaxVaultQuotaLocked();

                var res = new SyncPushResponseDto
                {
                    Success = true,
                    Message = $"Đã lưu bản sao lưu vào Server Vault ({bytes.Length / 1024} KB).",
                    StoredBytes = bytes.Length,
                    TimestampUtc = DateTime.UtcNow
                };
                return NetworkPacket.Create(PacketType.SyncPushResponse, "SERVER", OperationResult<SyncPushResponseDto>.Ok(res), packet.SenderId, packet.CorrelationId);
            }
            catch (Exception ex)
            {
                return NetworkPacket.Create(PacketType.SyncPushResponse, "SERVER", OperationResult<SyncPushResponseDto>.Fail($"Lỗi lưu trữ Vault: {ex.Message}"), packet.SenderId, packet.CorrelationId);
            }
        }
    }

    private NetworkPacket HandleSyncPull(NetworkPacket packet)
    {
        var req = packet.GetPayload<SyncPullRequestDto>();
        var userId = req?.UserId ?? packet.SenderId;
        var userVaultDir = Path.Combine(_vaultRoot, userId);
        var snapshotPath = Path.Combine(userVaultDir, "latest_snapshot.bin");
        var metaPath = Path.Combine(userVaultDir, "metadata.json");

        lock (_syncLock)
        {
            if (!File.Exists(snapshotPath))
            {
                return NetworkPacket.Create(PacketType.SyncPullResponse, "SERVER", OperationResult<SyncPullResponseDto>.Fail("Không tìm thấy bản sao lưu nào của tài khoản này trên Server LAN."), packet.SenderId, packet.CorrelationId);
            }

            try
            {
                var bytes = File.ReadAllBytes(snapshotPath);
                var meta = File.Exists(metaPath) ? JsonSerializer.Deserialize<SyncCheckResponseDto>(File.ReadAllText(metaPath)) : null;

                var res = new SyncPullResponseDto
                {
                    Success = true,
                    Message = "Tải bản sao lưu từ Server LAN thành công.",
                    SnapshotZipBase64 = Convert.ToBase64String(bytes),
                    SnapshotSizeBytes = bytes.Length,
                    TimestampUtc = meta?.LastSnapshotUtc ?? File.GetLastWriteTimeUtc(snapshotPath),
                    MessagesCount = meta?.TotalMessagesCount ?? 0,
                    FilesCount = meta?.TotalFilesCount ?? 0
                };
                return NetworkPacket.Create(PacketType.SyncPullResponse, "SERVER", OperationResult<SyncPullResponseDto>.Ok(res), packet.SenderId, packet.CorrelationId);
            }
            catch (Exception ex)
            {
                return NetworkPacket.Create(PacketType.SyncPullResponse, "SERVER", OperationResult<SyncPullResponseDto>.Fail($"Lỗi đọc bản sao lưu: {ex.Message}"), packet.SenderId, packet.CorrelationId);
            }
        }
    }

    private void EnforceMaxVaultQuotaLocked()
    {
        try
        {
            var userDirs = Directory.GetDirectories(_vaultRoot)
                .Select(d => new DirectoryInfo(d))
                .OrderBy(d => d.LastWriteTimeUtc)
                .ToList();

            long totalBytes = userDirs.Sum(d => d.GetFiles().Sum(f => f.Length));
            while (totalBytes > _maxVaultSizeBytes && userDirs.Count > 1)
            {
                var oldest = userDirs[0];
                userDirs.RemoveAt(0);
                oldest.Delete(recursive: true);
                totalBytes = userDirs.Sum(d => d.GetFiles().Sum(f => f.Length));
            }
        }
        catch
        {
            // Tránh văng ngoại lệ trong quá trình dọn dẹp
        }
    }
}
