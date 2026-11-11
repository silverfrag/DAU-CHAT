using System.Text;
using DauChat.Shared.Constants;
using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.Client.UI.Services;

internal sealed class DemoMessagingService : IMessagingService
{
    private readonly DemoDataStore _store;
    private readonly Func<UserDto?> _getCurrentUser;
    private readonly HashSet<string> _seenMessageIds = new();

    public DemoMessagingService(DemoDataStore store, Func<UserDto?> getCurrentUser)
    {
        _store = store;
        _getCurrentUser = getCurrentUser;

        lock (_store.SyncRoot)
        {
            foreach (var list in _store.MessagesByConversation.Values)
            {
                foreach (var m in list) _seenMessageIds.Add(m.MessageId);
            }
        }

        _store.ExternalSyncApplied += diff =>
        {
            var currentUser = _getCurrentUser();
            if (currentUser is null) return;

            foreach (var msg in diff.NewMessages)
            {
                if (!_seenMessageIds.Add(msg.MessageId)) continue;

                if (msg.IsRoomMessage)
                {
                    bool canSeeRoom;
                    DateTime cutoff;
                    lock (_store.SyncRoot)
                    {
                        var room = _store.Rooms.FirstOrDefault(r => r.RoomId == (msg.RoomId ?? msg.ConversationId));
                        canSeeRoom = room is not null
                            && (room.MemberIds.Contains(currentUser.UserId) || currentUser.Role == UserRole.Admin);
                        cutoff = _store.GetClearedCutoffUtc(currentUser.UserId, msg.RoomId ?? msg.ConversationId);
                    }

                    if (canSeeRoom && msg.ServerTimestampUtc > cutoff)
                    {
                        RoomMessageReceived?.Invoke(this, msg);
                    }
                }
                else
                {
                    if (msg.SenderId == currentUser.UserId || msg.ReceiverId == currentUser.UserId)
                    {
                        var partnerId = msg.SenderId == currentUser.UserId
                            ? (msg.ReceiverId ?? string.Empty)
                            : msg.SenderId;
                        var dmKey = DemoDataStore.GetDirectConversationKey(currentUser.UserId, partnerId);
                        DateTime cutoff;
                        lock (_store.SyncRoot)
                        {
                            cutoff = _store.GetClearedCutoffUtc(currentUser.UserId, dmKey);
                        }

                        if (msg.ServerTimestampUtc > cutoff)
                        {
                            DirectMessageReceived?.Invoke(this, msg);
                        }
                    }
                }
            }

            foreach (var (roomId, notice) in diff.PinnedChanges)
            {
                PinnedAnnouncementChanged?.Invoke(this, (roomId, notice));
            }
        };
    }

    public event EventHandler<MessageDto>? DirectMessageReceived;
    public event EventHandler<MessageDto>? RoomMessageReceived;
    public event EventHandler<(string RoomId, PinnedNoticeDto? PinnedNotice)>? PinnedAnnouncementChanged;

    public Task<OperationResult<ConversationSummaryDto>> OpenDirectConversationAsync(
        string currentUserId,
        string partnerId,
        CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var partner = _store.FindAccountById(partnerId)?.User;
            if (partner is null)
            {
                return Task.FromResult(OperationResult<ConversationSummaryDto>.Fail("Không tìm thấy người dùng."));
            }

            var key = DemoDataStore.GetDirectConversationKey(currentUserId, partnerId);
            _store.MessagesByConversation.TryGetValue(key, out var list);
            var cutoff = _store.GetClearedCutoffUtc(currentUserId, key);
            var last = list?.LastOrDefault(m => m.ServerTimestampUtc > cutoff);

            var summary = new ConversationSummaryDto
            {
                PartnerId = partner.UserId,
                PartnerName = partner.FullName,
                PartnerAvatarPath = partner.AvatarPath,
                PartnerPresence = partner.Presence,
                LastMessagePreview = last?.Content ?? "Bắt đầu cuộc trò chuyện nội bộ",
                LastMessageAtUtc = last?.ServerTimestampUtc ?? DateTime.UtcNow,
                UnreadCount = _store.UnreadByConversation.GetValueOrDefault(partnerId, 0)
            };
            return Task.FromResult(OperationResult<ConversationSummaryDto>.Ok(summary));
        }
    }

    public Task<IReadOnlyList<ConversationSummaryDto>> GetDirectConversationsAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var partners = _store.Accounts
                .Where(a => a.User.UserId != currentUserId
                         && a.User.Status == AccountStatus.Active
                         && a.User.Role != UserRole.Admin)
                .Select(a =>
                {
                    var key = DemoDataStore.GetDirectConversationKey(currentUserId, a.User.UserId);
                    _store.MessagesByConversation.TryGetValue(key, out var msgs);
                    var cutoff = _store.GetClearedCutoffUtc(currentUserId, key);
                    var last = msgs?.LastOrDefault(m => m.ServerTimestampUtc > cutoff);
                    return new ConversationSummaryDto
                    {
                        PartnerId = a.User.UserId,
                        PartnerName = a.User.FullName,
                        PartnerAvatarPath = a.User.AvatarPath,
                        PartnerPresence = a.User.Presence,
                        LastMessagePreview = last is null
                            ? $"{a.User.Username} • Sẵn sàng nhắn tin"
                            : (last.Type == MessageType.FileAttachment ? $"[Tệp] {last.AttachmentFileName}" : last.Content),
                        LastMessageAtUtc = last?.ServerTimestampUtc ?? a.User.LastSeenAtUtc,
                        UnreadCount = _store.UnreadByConversation.GetValueOrDefault(a.User.UserId, 0)
                    };
                })
                .OrderByDescending(c => c.LastMessageAtUtc ?? DateTime.MinValue)
                .ToList();

            return Task.FromResult<IReadOnlyList<ConversationSummaryDto>>(partners);
        }
    }

    public Task<OperationResult<MessageDto>> SendDirectMessageAsync(
        string senderId,
        string receiverId,
        string content,
        MessageType messageType = MessageType.Text,
        string? attachmentFileId = null,
        string? attachmentFileName = null,
        long? attachmentFileSize = null,
        CancellationToken cancellationToken = default)
    {
        var key = DemoDataStore.GetDirectConversationKey(senderId, receiverId);
        var msg = _store.MutateAndSave(() =>
        {
            var sender = _store.FindAccountById(senderId)?.User;
            var now = DateTime.UtcNow;
            var created = new MessageDto
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ConversationId = key,
                SenderId = senderId,
                SenderName = sender?.FullName ?? senderId,
                ReceiverId = receiverId,
                Content = content,
                Type = messageType,
                IsRoomMessage = false,
                AttachmentFileId = attachmentFileId,
                AttachmentFileName = attachmentFileName,
                AttachmentFileSize = attachmentFileSize,
                SentAtClientUtc = now,
                ServerTimestampUtc = now
            };

            if (!_store.MessagesByConversation.TryGetValue(key, out var list))
            {
                list = new List<MessageDto>();
                _store.MessagesByConversation[key] = list;
            }
            list.Add(created);
            _seenMessageIds.Add(created.MessageId);

            var payloadBytes = Math.Max(128, Encoding.UTF8.GetByteCount(content) + 160);
            _store.TotalPacketsSent++;
            _store.TotalPacketsReceived++;
            _store.TotalBytesSent += payloadBytes;
            _store.TotalBytesReceived += payloadBytes;

            return created;
        });

        DirectMessageReceived?.Invoke(this, msg);
        return Task.FromResult(OperationResult<MessageDto>.Ok(msg));
    }

    public Task<IReadOnlyList<MessageDto>> GetDirectHistoryAsync(
        string currentUserId,
        string partnerId,
        int pageIndex = 0,
        int pageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            _store.UnreadByConversation[partnerId] = 0;
            var key = DemoDataStore.GetDirectConversationKey(currentUserId, partnerId);
            if (!_store.MessagesByConversation.TryGetValue(key, out var list))
            {
                return Task.FromResult<IReadOnlyList<MessageDto>>(Array.Empty<MessageDto>());
            }

            var cutoff = _store.GetClearedCutoffUtc(currentUserId, key);
            var filtered = list.Where(m => m.ServerTimestampUtc > cutoff).ToList();
            var paged = filtered
                .Skip(Math.Max(0, filtered.Count - (pageIndex + 1) * pageSize))
                .Take(pageSize)
                .ToList();
            return Task.FromResult<IReadOnlyList<MessageDto>>(paged);
        }
    }

    public Task<OperationResult> DeleteDirectHistoryAsync(
        string currentUserId,
        string partnerId,
        CancellationToken cancellationToken = default)
    {
        var key = DemoDataStore.GetDirectConversationKey(currentUserId, partnerId);
        _store.MutateAndSave(() =>
        {
            if (!_store.ClearedBeforeUtc.TryGetValue(currentUserId, out var map))
            {
                map = new Dictionary<string, DateTime>();
                _store.ClearedBeforeUtc[currentUserId] = map;
            }
            map[key] = DateTime.UtcNow;
        });

        return Task.FromResult(OperationResult.Ok("Đã xóa file lịch sử tin nhắn trên máy trạm."));
    }

    public Task<OperationResult<IReadOnlyList<RoomDto>>> GetJoinedRoomsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var isAdmin = _store.FindAccountById(userId)?.User.Role == UserRole.Admin;
            var rooms = _store.Rooms
                .Where(r => r.MemberIds.Contains(userId) || isAdmin)
                .OrderByDescending(r => r.LastActivityAtUtc ?? r.CreatedAtUtc)
                .ToList();
            return Task.FromResult(OperationResult<IReadOnlyList<RoomDto>>.Ok((IReadOnlyList<RoomDto>)rooms));
        }
    }

    public Task<OperationResult<(RoomDto Room, IReadOnlyList<MessageDto> RecentMessages, PinnedNoticeDto? PinnedNotice)>> EnterRoomAsync(
        string currentUserId,
        string roomId,
        int initialPageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var room = _store.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room is null)
            {
                return Task.FromResult(OperationResult<(RoomDto, IReadOnlyList<MessageDto>, PinnedNoticeDto?)>.Fail("Không tìm thấy phòng."));
            }

            _store.UnreadByConversation[roomId] = 0;
            _store.MessagesByConversation.TryGetValue(roomId, out var list);
            var cutoff = _store.GetClearedCutoffUtc(currentUserId, roomId);
            IReadOnlyList<MessageDto> recent = list?
                .Where(m => m.ServerTimestampUtc > cutoff)
                .TakeLast(initialPageSize)
                .ToList() ?? new List<MessageDto>();
            return Task.FromResult(OperationResult<(RoomDto, IReadOnlyList<MessageDto>, PinnedNoticeDto?)>.Ok((room, recent, room.PinnedNotice)));
        }
    }

    public Task<OperationResult> LeaveRoomAsync(string currentUserId, string roomId, CancellationToken cancellationToken = default)
    {
        var ok = _store.MutateAndSave(() =>
        {
            var room = _store.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room is null) return false;
            room.MemberIds.Remove(currentUserId);
            var u = _store.FindAccountById(currentUserId)?.User;
            if (!_store.MessagesByConversation.TryGetValue(roomId, out var msgs))
            {
                msgs = new List<MessageDto>();
                _store.MessagesByConversation[roomId] = msgs;
            }
            msgs.Add(new MessageDto
            {
                MessageId = $"sys_{Guid.NewGuid():N}"[..12],
                ConversationId = roomId,
                RoomId = roomId,
                IsRoomMessage = true,
                SenderId = currentUserId,
                SenderName = "Hệ thống DAU Chat",
                Content = $"{u?.FullName ?? currentUserId} đã rời khỏi phòng.",
                Type = MessageType.SystemNotice,
                SentAtClientUtc = DateTime.UtcNow,
                ServerTimestampUtc = DateTime.UtcNow
            });
            return true;
        });

        return Task.FromResult(ok ? OperationResult.Ok("Đã rời khỏi phòng.") : OperationResult.Fail("Không tìm thấy phòng."));
    }

    public Task<OperationResult<MessageDto>> SendRoomMessageAsync(
        string senderId,
        string roomId,
        string content,
        MessageType messageType = MessageType.Text,
        string? attachmentFileId = null,
        string? attachmentFileName = null,
        long? attachmentFileSize = null,
        CancellationToken cancellationToken = default)
    {
        var msg = _store.MutateAndSave(() =>
        {
            var sender = _store.FindAccountById(senderId)?.User;
            var room = _store.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            var now = DateTime.UtcNow;
            var created = new MessageDto
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ConversationId = roomId,
                RoomId = roomId,
                SenderId = senderId,
                SenderName = sender?.FullName ?? senderId,
                Content = content,
                Type = messageType,
                IsRoomMessage = true,
                AttachmentFileId = attachmentFileId,
                AttachmentFileName = attachmentFileName,
                AttachmentFileSize = attachmentFileSize,
                SentAtClientUtc = now,
                ServerTimestampUtc = now
            };

            if (!_store.MessagesByConversation.TryGetValue(roomId, out var list))
            {
                list = new List<MessageDto>();
                _store.MessagesByConversation[roomId] = list;
            }
            list.Add(created);
            if (room is not null) room.LastActivityAtUtc = now;
            _seenMessageIds.Add(created.MessageId);

            var recipientCount = Math.Max(1, (room?.MemberIds.Count ?? 2) - 1);
            var payloadBytes = Math.Max(128, Encoding.UTF8.GetByteCount(content) + 160);
            _store.TotalPacketsSent += recipientCount;
            _store.TotalPacketsReceived++;
            _store.TotalBytesSent += payloadBytes * recipientCount;
            _store.TotalBytesReceived += payloadBytes;

            return created;
        });

        RoomMessageReceived?.Invoke(this, msg);
        return Task.FromResult(OperationResult<MessageDto>.Ok(msg));
    }

    public Task<IReadOnlyList<MessageDto>> GetRoomHistoryAsync(
        string currentUserId,
        string roomId,
        int pageIndex = 0,
        int pageSize = ProtocolConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            if (!_store.MessagesByConversation.TryGetValue(roomId, out var list))
            {
                return Task.FromResult<IReadOnlyList<MessageDto>>(Array.Empty<MessageDto>());
            }

            var cutoff = _store.GetClearedCutoffUtc(currentUserId, roomId);
            var filtered = list.Where(m => m.ServerTimestampUtc > cutoff).ToList();
            var paged = filtered
                .Skip(Math.Max(0, filtered.Count - (pageIndex + 1) * pageSize))
                .Take(pageSize)
                .ToList();
            return Task.FromResult<IReadOnlyList<MessageDto>>(paged);
        }
    }

    public Task<OperationResult> DeleteRoomHistoryAsync(string currentUserId, string roomId, CancellationToken cancellationToken = default)
    {
        _store.MutateAndSave(() =>
        {
            if (!_store.ClearedBeforeUtc.TryGetValue(currentUserId, out var map))
            {
                map = new Dictionary<string, DateTime>();
                _store.ClearedBeforeUtc[currentUserId] = map;
            }
            map[roomId] = DateTime.UtcNow;
        });

        return Task.FromResult(OperationResult.Ok("Đã xóa file lịch sử .jsonl trên thiết bị."));
    }

    public Task<OperationResult<PinnedNoticeDto>> PinRoomAnnouncementAsync(
        string roomId,
        string lecturerId,
        string contentOrMessageId,
        bool isMessageId = false,
        CancellationToken cancellationToken = default)
    {
        var notice = _store.MutateAndSave(() =>
        {
            var room = _store.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room is null) return null;

            var lecturer = _store.FindAccountById(lecturerId)?.User;
            string content = contentOrMessageId;
            if (isMessageId && _store.MessagesByConversation.TryGetValue(roomId, out var list))
            {
                var found = list.FirstOrDefault(m => m.MessageId == contentOrMessageId);
                if (found is not null)
                {
                    content = found.Content;
                    found.IsPinned = true;
                }
            }

            var n = new PinnedNoticeDto
            {
                MessageId = isMessageId ? contentOrMessageId : Guid.NewGuid().ToString("N"),
                RoomId = roomId,
                PinnedByUserId = lecturerId,
                PinnedByName = lecturer?.FullName ?? lecturerId,
                Content = content.Trim(),
                PinnedAtUtc = DateTime.UtcNow
            };
            room.PinnedNotice = n;
            return n;
        });

        if (notice is null)
        {
            return Task.FromResult(OperationResult<PinnedNoticeDto>.Fail("Không tìm thấy phòng."));
        }

        PinnedAnnouncementChanged?.Invoke(this, (roomId, notice));
        return Task.FromResult(OperationResult<PinnedNoticeDto>.Ok(notice, "Đã ghim thông báo lên đầu phòng."));
    }

    public Task<OperationResult> UnpinRoomAnnouncementAsync(
        string roomId,
        string lecturerId,
        string? messageId = null,
        CancellationToken cancellationToken = default)
    {
        var ok = _store.MutateAndSave(() =>
        {
            var room = _store.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room is null) return false;
            room.PinnedNotice = null;
            return true;
        });

        if (!ok) return Task.FromResult(OperationResult.Fail("Không tìm thấy phòng."));

        PinnedAnnouncementChanged?.Invoke(this, (roomId, null));
        return Task.FromResult(OperationResult.Ok("Đã bỏ ghim thông báo."));
    }

    public Task<bool> AppendMessageToLocalLogAsync(string currentUserId, MessageDto message, CancellationToken cancellationToken = default)
    {
        var appended = _store.MutateAndSave(() =>
        {
            if (!_seenMessageIds.Add(message.MessageId)) return false;
            if (!_store.MessagesByConversation.TryGetValue(message.ConversationId, out var list))
            {
                list = new List<MessageDto>();
                _store.MessagesByConversation[message.ConversationId] = list;
            }
            list.Add(message);
            return true;
        });
        return Task.FromResult(appended);
    }

    public Task ProcessIncomingPacketAsync(string currentUserId, NetworkPacket packet, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
