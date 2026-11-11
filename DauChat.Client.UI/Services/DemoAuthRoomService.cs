using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;

namespace DauChat.Client.UI.Services;

internal sealed class DemoAuthRoomService : IAuthRoomService
{
    private readonly DemoDataStore _store;
    private readonly DemoNetworkService _network;

    public DemoAuthRoomService(DemoDataStore store, DemoNetworkService network)
    {
        _store = store;
        _network = network;

        _store.ExternalSyncApplied += diff =>
        {
            foreach (var room in diff.UpdatedRooms)
            {
                RoomUpdated?.Invoke(this, CloneRoom(room));
            }

            foreach (var deletedId in diff.DeletedRoomIds)
            {
                RoomDeleted?.Invoke(this, deletedId);
            }

            if (CurrentUser is { } cu)
            {
                DemoDataStore.DemoAccountRecord? acc;
                string? kickReason = null;
                lock (_store.SyncRoot)
                {
                    acc = _store.FindAccountById(cu.UserId);
                    if (_store.KickReasonsByUserId.TryGetValue(cu.UserId, out var r))
                    {
                        kickReason = r;
                    }
                }

                if (acc is null || acc.User.Status == AccountStatus.Locked || !string.IsNullOrEmpty(kickReason))
                {
                    var reason = acc?.User.Status == AccountStatus.Locked
                        ? (string.IsNullOrWhiteSpace(acc.User.LockReason)
                            ? "Tài khoản vừa bị Quản trị viên tạm khóa."
                            : $"Tài khoản bị khóa: {acc.User.LockReason}")
                        : (!string.IsNullOrEmpty(kickReason)
                            ? kickReason
                            : "Phiên đăng nhập đã bị ngắt kết nối bởi Quản trị viên.");

                    CurrentUser = null;
                    _network.SetCurrentUser(null);
                    _ = _network.DisconnectAsync();
                    CurrentUserChanged?.Invoke(this, null);
                    ForceLoggedOut?.Invoke(this, reason);
                    return;
                }

                var refreshed = CloneUser(acc.User);
                CurrentUser = refreshed;
                CurrentUserChanged?.Invoke(this, refreshed);
            }
        };
    }

    public UserDto? CurrentUser { get; private set; }

    public event EventHandler<UserDto?>? CurrentUserChanged;
    public event EventHandler<RoomDto>? RoomUpdated;
    public event EventHandler<string>? RoomDeleted;
    public event EventHandler<string>? ForceLoggedOut;

    public async Task<OperationResult<LoginResultDto>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(180, cancellationToken);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return OperationResult<LoginResultDto>.Fail("Vui lòng nhập đầy đủ mã tài khoản và mật khẩu.", "INVALID_INPUT");
        }

        var trimmedUser = username.Trim();
        var res = _store.MutateAndSave(() =>
        {
            var record = _store.FindAccountByUsername(trimmedUser);
            if (record is null || record.Password != password)
            {
                var deniedLog = new ConnectionLogDto
                {
                    TimestampUtc = DateTime.UtcNow,
                    EventType = "AUTH_FAILED",
                    UserId = trimmedUser,
                    IpEndPoint = $"{_store.ConnectedHost}:{_store.ConnectedPort}",
                    Description = $"Đăng nhập thất bại cho tài khoản '{trimmedUser}' (sai thông tin)."
                };
                _store.ConnectionLogs.Insert(0, deniedLog);
                return (Result: OperationResult<LoginResultDto>.Fail("Mã tài khoản hoặc mật khẩu demo không chính xác.", "INVALID_CREDENTIALS"), Log: deniedLog, LoggedUser: (UserDto?)null);
            }

            if (record.User.Status == AccountStatus.Locked)
            {
                var lockedLog = new ConnectionLogDto
                {
                    TimestampUtc = DateTime.UtcNow,
                    EventType = "AUTH_LOCKED",
                    UserId = record.User.UserId,
                    IpEndPoint = $"{_store.ConnectedHost}:{_store.ConnectedPort}",
                    Description = $"Từ chối đăng nhập {record.User.Username}: tài khoản đang bị khóa."
                };
                _store.ConnectionLogs.Insert(0, lockedLog);
                var reason = string.IsNullOrWhiteSpace(record.User.LockReason)
                    ? "Tài khoản đã bị Quản trị viên tạm khóa."
                    : record.User.LockReason;
                return (Result: OperationResult<LoginResultDto>.Fail($"Tài khoản đang bị khóa. Lý do: {reason}", "ACCOUNT_LOCKED"), Log: lockedLog, LoggedUser: (UserDto?)null);
            }

            _store.KickReasonsByUserId.Remove(record.User.UserId);
            record.User.Presence = PresenceStatus.Online;
            record.User.LastSeenAtUtc = DateTime.UtcNow;

            var cloned = CloneUser(record.User);
            var existingClient = _store.ConnectedClients.FirstOrDefault(c => c.UserId == record.User.UserId);
            if (existingClient is null)
            {
                _store.ConnectedClients.Insert(0, new ClientConnectionDto
                {
                    SessionId = $"sess_{Environment.ProcessId}",
                    UserId = record.User.UserId,
                    Username = record.User.Username,
                    IpEndPoint = $"127.0.0.1:{Random.Shared.Next(51000, 59999)}",
                    Status = PresenceStatus.Online,
                    ConnectedAtUtc = DateTime.UtcNow,
                    LastHeartbeatUtc = DateTime.UtcNow,
                    PacketsSent = 1,
                    PacketsReceived = 1
                });
            }
            else
            {
                existingClient.SessionId = $"sess_{Environment.ProcessId}";
                existingClient.Status = PresenceStatus.Online;
                existingClient.LastHeartbeatUtc = DateTime.UtcNow;
            }

            var okLog = new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow,
                EventType = "AUTH_LOGIN",
                UserId = record.User.UserId,
                IpEndPoint = $"{_store.ConnectedHost}:{_store.ConnectedPort}",
                Description = $"{record.User.Username} ({record.User.Role}) đăng nhập thành công."
            };
            _store.ConnectionLogs.Insert(0, okLog);

            var payload = new LoginResultDto
            {
                User = cloned,
                SessionToken = Guid.NewGuid().ToString("N"),
                LoggedInAtUtc = DateTime.UtcNow
            };
            return (Result: OperationResult<LoginResultDto>.Ok(payload, "Đăng nhập thành công"), Log: okLog, LoggedUser: cloned);
        });

        _network.EmitConnectionLog(res.Log);
        if (!res.Result.Success || res.LoggedUser is null)
        {
            return res.Result;
        }

        CurrentUser = res.LoggedUser;
        _network.SetCurrentUser(CurrentUser.UserId);
        _store.EnsurePollingStarted();
        _network.EmitPresence(CurrentUser.UserId, PresenceStatus.Online);
        CurrentUserChanged?.Invoke(this, CurrentUser);
        return res.Result;
    }

    public async Task<OperationResult> LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        var log = _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(userId);
            if (acc is not null)
            {
                acc.User.Presence = PresenceStatus.Offline;
                acc.User.LastSeenAtUtc = DateTime.UtcNow;
            }
            _store.ConnectedClients.RemoveAll(c => c.UserId == userId && c.SessionId == $"sess_{Environment.ProcessId}");
            var l = new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow,
                EventType = "AUTH_LOGOUT",
                UserId = userId,
                IpEndPoint = $"{_store.ConnectedHost}:{_store.ConnectedPort}",
                Description = $"Phiên của {userId} đã đăng xuất an toàn."
            };
            _store.ConnectionLogs.Insert(0, l);
            return l;
        });

        CurrentUser = null;
        await _network.DisconnectAsync(cancellationToken);
        _network.EmitConnectionLog(log);
        _network.EmitPresence(userId, PresenceStatus.Offline);
        CurrentUserChanged?.Invoke(this, null);
        return OperationResult.Ok("Đã đăng xuất");
    }

    public void CleanupSessionOnExit()
    {
        var uid = CurrentUser?.UserId;
        if (string.IsNullOrEmpty(uid)) return;

        try
        {
            _store.MutateAndSave(() =>
            {
                _store.ConnectedClients.RemoveAll(c => c.UserId == uid && c.SessionId == $"sess_{Environment.ProcessId}");
            });
        }
        catch
        {
            // Best-effort cleanup on exit
        }
    }

    public Task<OperationResult> ChangePasswordAsync(
        string userId,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return Task.FromResult(OperationResult.Fail("Mật khẩu mới phải có ít nhất 6 ký tự."));
        }

        var result = _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(userId);
            if (acc is null) return OperationResult.Fail("Không tìm thấy tài khoản.");
            if (acc.Password != oldPassword) return OperationResult.Fail("Mật khẩu hiện tại không đúng.");
            acc.Password = newPassword;
            acc.MustChangePassword = false;
            return OperationResult.Ok("Đổi mật khẩu thành công.");
        });

        return Task.FromResult(result);
    }

    public Task<OperationResult<UserDto>> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var acc = _store.FindAccountById(userId);
            return Task.FromResult(acc is null
                ? OperationResult<UserDto>.Fail("Không tìm thấy hồ sơ người dùng.")
                : OperationResult<UserDto>.Ok(CloneUser(acc.User)));
        }
    }

    public Task<OperationResult<UserDto>> UpdateUserProfileAsync(
        string userId,
        string fullName,
        string email,
        string? avatarPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Task.FromResult(OperationResult<UserDto>.Fail("Họ tên không được để trống."));
        }

        var res = _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(userId);
            if (acc is null) return OperationResult<UserDto>.Fail("Không tìm thấy người dùng.");

            acc.User.FullName = fullName.Trim();
            acc.User.Email = email.Trim();
            if (avatarPath is not null) acc.User.AvatarPath = avatarPath;

            var updated = CloneUser(acc.User);
            return OperationResult<UserDto>.Ok(updated, "Đã cập nhật hồ sơ cá nhân.");
        });

        if (res.Success && res.Data is not null && CurrentUser?.UserId == userId)
        {
            CurrentUser = res.Data;
            CurrentUserChanged?.Invoke(this, CurrentUser);
        }

        return Task.FromResult(res);
    }

    public Task<OperationResult<IReadOnlyList<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var list = _store.Accounts.Select(a => CloneUser(a.User)).ToList();
            return Task.FromResult<OperationResult<IReadOnlyList<UserDto>>>(OperationResult<IReadOnlyList<UserDto>>.Ok(list));
        }
    }

    public Task<OperationResult<UserDto>> CreateUserAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return Task.FromResult(OperationResult<UserDto>.Fail("Vui lòng nhập mã tài khoản và họ tên."));
        }

        var res = _store.MutateAndSave(() =>
        {
            var uname = request.Username.Trim();
            if (_store.FindAccountByUsername(uname) is not null)
            {
                return OperationResult<UserDto>.Fail("Mã tài khoản này đã tồn tại.");
            }

            var cls = _store.Classes.FirstOrDefault(c => c.ClassId == request.ClassId);
            var userId = $"u_{Guid.NewGuid():N}"[..10];
            var (bg, fg) = ResolveToneForRole(request.Role);

            var user = new UserDto
            {
                UserId = userId,
                Username = uname,
                FullName = request.FullName.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email)
                    ? $"{uname}@dau.edu.vn"
                    : request.Email.Trim(),
                Role = request.Role,
                Status = AccountStatus.Active,
                Presence = PresenceStatus.Offline,
                ClassId = cls?.ClassId,
                ClassName = cls is null ? null : $"{cls.ClassCode} • {cls.ClassName}",
                CreatedAtUtc = DateTime.UtcNow
            };

            _store.Accounts.Insert(0, new DemoDataStore.DemoAccountRecord
            {
                User = user,
                Password = string.IsNullOrWhiteSpace(request.InitialPassword) ? "123456" : request.InitialPassword,
                AvatarBgHex = bg,
                AvatarFgHex = fg,
                MustChangePassword = true
            });

            if (cls is not null && !cls.MemberIds.Contains(userId))
            {
                cls.MemberIds.Add(userId);
                cls.MemberCount = cls.MemberIds.Count;
                if (!string.IsNullOrEmpty(cls.LinkedRoomId))
                {
                    var linkedRoom = _store.Rooms.FirstOrDefault(r => r.RoomId == cls.LinkedRoomId);
                    if (linkedRoom is not null && !linkedRoom.MemberIds.Contains(userId))
                    {
                        linkedRoom.MemberIds.Add(userId);
                    }
                }
            }

            return OperationResult<UserDto>.Ok(CloneUser(user), $"Đã tạo tài khoản '{user.Username}'.");
        });

        return Task.FromResult(res);
    }

    public Task<OperationResult<UserDto>> UpdateUserAsync(UserDto user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.FullName))
        {
            return Task.FromResult(OperationResult<UserDto>.Fail("Họ tên không được để trống."));
        }

        var res = _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(user.UserId);
            if (acc is null) return OperationResult<UserDto>.Fail("Không tìm thấy tài khoản.");

            acc.User.FullName = user.FullName.Trim();
            acc.User.Email = user.Email?.Trim() ?? acc.User.Email;
            acc.User.Role = user.Role;

            var (bg, fg) = ResolveToneForRole(user.Role);
            acc.AvatarBgHex = bg;
            acc.AvatarFgHex = fg;

            if (!string.IsNullOrEmpty(user.ClassId))
            {
                var cls = _store.Classes.FirstOrDefault(c => c.ClassId == user.ClassId);
                acc.User.ClassId = cls?.ClassId ?? user.ClassId;
                acc.User.ClassName = cls is not null ? $"{cls.ClassCode} • {cls.ClassName}" : user.ClassName;
                if (cls is not null && !cls.MemberIds.Contains(acc.User.UserId))
                {
                    cls.MemberIds.Add(acc.User.UserId);
                    cls.MemberCount = cls.MemberIds.Count;
                    if (!string.IsNullOrEmpty(cls.LinkedRoomId))
                    {
                        var room = _store.Rooms.FirstOrDefault(r => r.RoomId == cls.LinkedRoomId);
                        if (room is not null && !room.MemberIds.Contains(acc.User.UserId))
                        {
                            room.MemberIds.Add(acc.User.UserId);
                        }
                    }
                }
            }
            else
            {
                acc.User.ClassId = null;
                acc.User.ClassName = null;
            }

            return OperationResult<UserDto>.Ok(CloneUser(acc.User), $"Đã cập nhật tài khoản '{acc.User.Username}'.");
        });

        if (res.Success && res.Data is not null && CurrentUser?.UserId == user.UserId)
        {
            CurrentUser = res.Data;
            CurrentUserChanged?.Invoke(this, CurrentUser);
        }

        return Task.FromResult(res);
    }

    public async Task<OperationResult> LockUserAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(userId);
            if (acc is null) return OperationResult.Fail("Không tìm thấy tài khoản.");
            acc.User.Status = AccountStatus.Locked;
            acc.User.LockReason = string.IsNullOrWhiteSpace(reason) ? "Quản trị viên tạm khóa" : reason.Trim();
            acc.User.Presence = PresenceStatus.Offline;
            return OperationResult.Ok("Đã khóa tài khoản và ngắt phiên kết nối.");
        });

        if (res.Success)
        {
            await _network.KickUserAsync(userId, reason, cancellationToken);
        }

        return res;
    }

    public Task<OperationResult> UnlockUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(userId);
            if (acc is null) return OperationResult.Fail("Không tìm thấy tài khoản.");
            acc.User.Status = AccountStatus.Active;
            acc.User.LockReason = null;
            _store.KickReasonsByUserId.Remove(userId);
            return OperationResult.Ok("Đã mở khóa tài khoản.");
        });

        return Task.FromResult(res);
    }

    public Task<OperationResult<string>> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default)
    {
        const string defaultPass = "Dau@123456";
        var res = _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(userId);
            if (acc is null) return OperationResult<string>.Fail("Không tìm thấy tài khoản.");
            acc.Password = defaultPass;
            acc.MustChangePassword = true;
            return OperationResult<string>.Ok(defaultPass, $"Đã đặt lại mật khẩu mặc định: {defaultPass}");
        });

        return Task.FromResult(res);
    }

    public Task<OperationResult> AssignRoleAsync(string userId, UserRole role, CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(userId);
            if (acc is null) return OperationResult.Fail("Không tìm thấy tài khoản.");
            acc.User.Role = role;
            var (bg, fg) = ResolveToneForRole(role);
            acc.AvatarBgHex = bg;
            acc.AvatarFgHex = fg;
            return OperationResult.Ok("Đã cập nhật vai trò RBAC.");
        });

        return Task.FromResult(res);
    }

    public Task<OperationResult<RoomDto>> CreateRoomAsync(
        RoomDto room,
        IReadOnlyList<string>? initialMemberIds = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(room.RoomName))
        {
            return Task.FromResult(OperationResult<RoomDto>.Fail("Tên phòng học không được để trống."));
        }

        var created = _store.MutateAndSave(() =>
        {
            var members = new HashSet<string>(initialMemberIds ?? Array.Empty<string>());
            if (!string.IsNullOrEmpty(room.OwnerId)) members.Add(room.OwnerId);
            if (CurrentUser is not null && CurrentUser.Role != UserRole.Admin)
            {
                members.Add(CurrentUser.UserId);
            }

            var linkedClass = !string.IsNullOrEmpty(room.ClassId)
                ? _store.Classes.FirstOrDefault(c => c.ClassId == room.ClassId)
                : null;

            var code = !string.IsNullOrWhiteSpace(room.JoinCode)
                ? room.JoinCode.Trim().ToUpperInvariant()
                : (linkedClass is not null && !string.IsNullOrWhiteSpace(linkedClass.JoinCode)
                    ? linkedClass.JoinCode
                    : DemoDataStore.GenerateJoinCode(
                        linkedClass?.ClassCode ?? "RM",
                        c => _store.Classes.Any(x => x.JoinCode.Equals(c, StringComparison.OrdinalIgnoreCase))
                          || _store.Rooms.Any(x => x.JoinCode.Equals(c, StringComparison.OrdinalIgnoreCase))));

            var newRoom = new RoomDto
            {
                RoomId = string.IsNullOrEmpty(room.RoomId) ? $"room_{Guid.NewGuid():N}"[..12] : room.RoomId,
                RoomName = room.RoomName.Trim(),
                Description = room.Description?.Trim(),
                RoomType = room.RoomType,
                ClassId = room.ClassId,
                JoinCode = code,
                OwnerId = string.IsNullOrEmpty(room.OwnerId) ? (CurrentUser?.UserId ?? "u_gv_demo01") : room.OwnerId,
                MemberIds = members.ToList(),
                CreatedAtUtc = DateTime.UtcNow,
                LastActivityAtUtc = DateTime.UtcNow
            };

            _store.Rooms.Insert(0, newRoom);
            if (linkedClass is not null && string.IsNullOrEmpty(linkedClass.LinkedRoomId))
            {
                linkedClass.LinkedRoomId = newRoom.RoomId;
            }

            _store.MessagesByConversation[newRoom.RoomId] = new List<MessageDto>
            {
                new()
                {
                    MessageId = $"sys_{Guid.NewGuid():N}"[..12],
                    ConversationId = newRoom.RoomId,
                    RoomId = newRoom.RoomId,
                    IsRoomMessage = true,
                    SenderId = newRoom.OwnerId,
                    SenderName = "Hệ thống DAU Chat",
                    Content = $"Phòng '{newRoom.RoomName}' đã được khởi tạo (Mã phòng: {newRoom.JoinCode}).",
                    Type = MessageType.SystemNotice,
                    SentAtClientUtc = DateTime.UtcNow,
                    ServerTimestampUtc = DateTime.UtcNow
                }
            };

            return CloneRoom(newRoom);
        });

        RoomUpdated?.Invoke(this, created);
        return Task.FromResult(OperationResult<RoomDto>.Ok(created, "Đã tạo phòng chat mới."));
    }

    public Task<OperationResult<RoomDto>> UpdateRoomAsync(RoomDto room, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(room.RoomName))
        {
            return Task.FromResult(OperationResult<RoomDto>.Fail("Tên phòng chat không được để trống."));
        }

        var res = _store.MutateAndSave(() =>
        {
            var existing = _store.Rooms.FirstOrDefault(r => r.RoomId == room.RoomId);
            if (existing is null) return OperationResult<RoomDto>.Fail("Không tìm thấy phòng.");
            existing.RoomName = room.RoomName.Trim();
            existing.Description = room.Description?.Trim();
            existing.RoomType = room.RoomType;
            return OperationResult<RoomDto>.Ok(CloneRoom(existing), "Đã cập nhật thông tin phòng.");
        });

        if (res.Success && res.Data is not null)
        {
            RoomUpdated?.Invoke(this, res.Data);
        }

        return Task.FromResult(res);
    }

    public Task<OperationResult> DeleteRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var deleted = _store.MutateAndSave(() =>
        {
            var count = _store.Rooms.RemoveAll(r => r.RoomId == roomId);
            foreach (var cls in _store.Classes.Where(c => c.LinkedRoomId == roomId))
            {
                cls.LinkedRoomId = null;
            }
            return count > 0;
        });

        if (deleted)
        {
            RoomDeleted?.Invoke(this, roomId);
        }

        return Task.FromResult(deleted
            ? OperationResult.Ok("Đã xóa phòng và giải tán kênh trao đổi.")
            : OperationResult.Fail("Không tìm thấy phòng cần xóa."));
    }

    public Task<OperationResult<IReadOnlyList<UserDto>>> GetRoomMembersAsync(string roomId, CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var room = _store.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room is null)
            {
                return Task.FromResult(OperationResult<IReadOnlyList<UserDto>>.Fail("Không tìm thấy phòng."));
            }

            var members = _store.Accounts
                .Where(a => room.MemberIds.Contains(a.User.UserId))
                .Select(a => CloneUser(a.User))
                .ToList();
            return Task.FromResult(OperationResult<IReadOnlyList<UserDto>>.Ok((IReadOnlyList<UserDto>)members));
        }
    }

    public Task<OperationResult> AddRoomMembersAsync(string roomId, IReadOnlyList<string> userIds, CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var room = _store.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room is null) return (Success: false, Room: (RoomDto?)null, AddedCount: 0);
            var added = 0;
            foreach (var id in userIds.Distinct())
            {
                if (!room.MemberIds.Contains(id))
                {
                    room.MemberIds.Add(id);
                    added++;
                }
            }

            if (added > 0)
            {
                AppendRoomSystemNoticeLocked(room, $"Đã thêm {added} thành viên mới vào phòng.");
            }

            return (Success: true, Room: CloneRoom(room), AddedCount: added);
        });

        if (!res.Success || res.Room is null) return Task.FromResult(OperationResult.Fail("Không tìm thấy phòng."));
        RoomUpdated?.Invoke(this, res.Room);
        return Task.FromResult(OperationResult.Ok($"Đã thêm {res.AddedCount} thành viên vào phòng."));
    }

    public Task<OperationResult> RemoveRoomMemberAsync(string roomId, string userId, CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var room = _store.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room is null) return (Ok: false, Message: "Không tìm thấy phòng.", Room: (RoomDto?)null);
            if (room.OwnerId == userId)
            {
                return (Ok: false, Message: "Không thể xóa giảng viên/chủ phòng khỏi phòng học.", Room: (RoomDto?)null);
            }
            var removed = room.MemberIds.Remove(userId);
            if (removed)
            {
                var u = _store.FindAccountById(userId)?.User;
                AppendRoomSystemNoticeLocked(room, $"{u?.FullName ?? userId} đã được mời ra khỏi phòng.");
            }
            return (Ok: true, Message: "Đã xóa thành viên khỏi phòng.", Room: CloneRoom(room));
        });

        if (!res.Ok || res.Room is null) return Task.FromResult(OperationResult.Fail(res.Message));
        RoomUpdated?.Invoke(this, res.Room);
        return Task.FromResult(OperationResult.Ok(res.Message));
    }

    public Task<OperationResult<IReadOnlyList<ClassDto>>> GetClassesAsync(string? filterKeyword = null, CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            IEnumerable<ClassDto> q = _store.Classes;
            if (!string.IsNullOrWhiteSpace(filterKeyword))
            {
                var k = filterKeyword.Trim();
                q = q.Where(c => c.ClassCode.Contains(k, StringComparison.OrdinalIgnoreCase)
                              || c.ClassName.Contains(k, StringComparison.OrdinalIgnoreCase)
                              || c.Department.Contains(k, StringComparison.OrdinalIgnoreCase)
                              || c.JoinCode.Contains(k, StringComparison.OrdinalIgnoreCase)
                              || (c.LecturerName?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            return Task.FromResult(OperationResult<IReadOnlyList<ClassDto>>.Ok((IReadOnlyList<ClassDto>)q.Select(CloneClass).ToList()));
        }
    }

    public Task<OperationResult<ClassDto>> CreateClassAsync(ClassDto classDto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(classDto.ClassCode) || string.IsNullOrWhiteSpace(classDto.ClassName))
        {
            return Task.FromResult(OperationResult<ClassDto>.Fail("Vui lòng nhập mã lớp và tên lớp."));
        }

        var created = _store.MutateAndSave(() =>
        {
            var codeNorm = classDto.ClassCode.Trim().ToUpperInvariant();
            var lecturer = _store.FindAccountById(classDto.LecturerId ?? "u_gv_demo01")?.User;
            var lecturerId = lecturer?.UserId ?? "u_gv_demo01";

            var joinCode = string.IsNullOrWhiteSpace(classDto.JoinCode)
                ? DemoDataStore.GenerateJoinCode(
                    codeNorm,
                    c => _store.Classes.Any(x => x.JoinCode.Equals(c, StringComparison.OrdinalIgnoreCase))
                      || _store.Rooms.Any(x => x.JoinCode.Equals(c, StringComparison.OrdinalIgnoreCase)))
                : classDto.JoinCode.Trim().ToUpperInvariant();

            var members = new List<string> { lecturerId };
            foreach (var id in classDto.MemberIds)
            {
                if (!members.Contains(id)) members.Add(id);
            }

            var cls = new ClassDto
            {
                ClassId = $"cls_{Guid.NewGuid():N}"[..10],
                ClassCode = codeNorm,
                ClassName = classDto.ClassName.Trim(),
                Department = string.IsNullOrWhiteSpace(classDto.Department) ? "Khoa Kiến trúc (DAU)" : classDto.Department.Trim(),
                AcademicYear = string.IsNullOrWhiteSpace(classDto.AcademicYear) ? "2024 - 2028" : classDto.AcademicYear.Trim(),
                LecturerId = lecturerId,
                LecturerName = lecturer?.FullName ?? "ThS. Nguyễn Văn Minh",
                JoinCode = joinCode,
                AllowStudentJoinByCode = classDto.AllowStudentJoinByCode,
                MemberIds = members,
                MemberCount = members.Count,
                CreatedAtUtc = DateTime.UtcNow
            };
            _store.Classes.Insert(0, cls);
            return CloneClass(cls);
        });

        return Task.FromResult(OperationResult<ClassDto>.Ok(created, $"Đã tạo lớp '{created.ClassCode}' (Mã tham gia: {created.JoinCode})."));
    }

    public Task<OperationResult<ClassDto>> UpdateClassAsync(ClassDto classDto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(classDto.ClassCode) || string.IsNullOrWhiteSpace(classDto.ClassName))
        {
            return Task.FromResult(OperationResult<ClassDto>.Fail("Mã lớp và tên lớp không được để trống."));
        }

        var res = _store.MutateAndSave(() =>
        {
            var existing = _store.Classes.FirstOrDefault(c => c.ClassId == classDto.ClassId);
            if (existing is null) return (Result: OperationResult<ClassDto>.Fail("Không tìm thấy lớp học."), UpdatedRoom: (RoomDto?)null);

            existing.ClassCode = classDto.ClassCode.Trim().ToUpperInvariant();
            existing.ClassName = classDto.ClassName.Trim();
            existing.Department = classDto.Department.Trim();
            if (!string.IsNullOrWhiteSpace(classDto.AcademicYear))
            {
                existing.AcademicYear = classDto.AcademicYear.Trim();
            }

            existing.AllowStudentJoinByCode = classDto.AllowStudentJoinByCode;

            var lecturer = !string.IsNullOrEmpty(classDto.LecturerId)
                ? _store.FindAccountById(classDto.LecturerId)?.User
                : null;
            if (lecturer is not null)
            {
                existing.LecturerId = lecturer.UserId;
                existing.LecturerName = lecturer.FullName;
                if (!existing.MemberIds.Contains(lecturer.UserId))
                {
                    existing.MemberIds.Insert(0, lecturer.UserId);
                    existing.MemberCount = existing.MemberIds.Count;
                }
            }

            RoomDto? linkedRoom = null;
            if (!string.IsNullOrEmpty(existing.LinkedRoomId))
            {
                linkedRoom = _store.Rooms.FirstOrDefault(r => r.RoomId == existing.LinkedRoomId);
                if (linkedRoom is not null && !string.IsNullOrEmpty(existing.LecturerId))
                {
                    linkedRoom.OwnerId = existing.LecturerId;
                    if (!linkedRoom.MemberIds.Contains(existing.LecturerId))
                    {
                        linkedRoom.MemberIds.Insert(0, existing.LecturerId);
                    }
                    linkedRoom = CloneRoom(linkedRoom);
                }
            }

            return (Result: OperationResult<ClassDto>.Ok(CloneClass(existing), $"Đã cập nhật lớp '{existing.ClassCode}'."), UpdatedRoom: linkedRoom);
        });

        if (res.UpdatedRoom is not null)
        {
            RoomUpdated?.Invoke(this, res.UpdatedRoom);
        }

        return Task.FromResult(res.Result);
    }

    public Task<OperationResult> DeleteClassAsync(string classId, CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var removed = _store.Classes.RemoveAll(c => c.ClassId == classId);
            return removed > 0
                ? OperationResult.Ok("Đã xóa lớp học.")
                : OperationResult.Fail("Không tìm thấy lớp học.");
        });

        return Task.FromResult(res);
    }

    public Task<OperationResult<IReadOnlyList<UserDto>>> GetClassMembersAsync(string classId, CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var cls = _store.Classes.FirstOrDefault(c => c.ClassId == classId);
            if (cls is null) return Task.FromResult(OperationResult<IReadOnlyList<UserDto>>.Fail("Không tìm thấy lớp."));
            var list = _store.Accounts
                .Where(a => cls.MemberIds.Contains(a.User.UserId))
                .OrderBy(a => a.User.Role == UserRole.GiangVien ? 0 : 1)
                .ThenBy(a => a.User.Username)
                .Select(a => CloneUser(a.User))
                .ToList();
            return Task.FromResult(OperationResult<IReadOnlyList<UserDto>>.Ok((IReadOnlyList<UserDto>)list));
        }
    }

    public Task<OperationResult> AddClassMembersAsync(string classId, IReadOnlyList<string> userIds, CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var cls = _store.Classes.FirstOrDefault(c => c.ClassId == classId);
            if (cls is null) return (Ok: false, Message: "Không tìm thấy lớp.", Room: (RoomDto?)null);

            var addedCount = 0;
            foreach (var id in userIds.Distinct())
            {
                if (!cls.MemberIds.Contains(id))
                {
                    cls.MemberIds.Add(id);
                    addedCount++;
                }
            }
            cls.MemberCount = cls.MemberIds.Count;

            RoomDto? syncedRoom = null;
            if (!string.IsNullOrEmpty(cls.LinkedRoomId))
            {
                var room = _store.Rooms.FirstOrDefault(r => r.RoomId == cls.LinkedRoomId);
                if (room is not null)
                {
                    foreach (var id in cls.MemberIds)
                    {
                        if (!room.MemberIds.Contains(id)) room.MemberIds.Add(id);
                    }
                    if (addedCount > 0)
                    {
                        AppendRoomSystemNoticeLocked(room, $"Đã thêm {addedCount} sinh viên vào danh sách lớp {cls.ClassCode} (tự động đồng bộ phòng).");
                    }
                    syncedRoom = CloneRoom(room);
                }
            }

            return (Ok: true, Message: addedCount > 0 ? $"Đã thêm {addedCount} thành viên vào lớp '{cls.ClassCode}'." : "Các thành viên này đã có trong lớp.", Room: syncedRoom);
        });

        if (res.Room is not null)
        {
            RoomUpdated?.Invoke(this, res.Room);
        }

        return Task.FromResult(res.Ok ? OperationResult.Ok(res.Message) : OperationResult.Fail(res.Message));
    }

    public Task<OperationResult> RemoveClassMembersAsync(string classId, IReadOnlyList<string> userIds, CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var cls = _store.Classes.FirstOrDefault(c => c.ClassId == classId);
            if (cls is null) return (Ok: false, Message: "Không tìm thấy lớp.", Room: (RoomDto?)null);

            var toRemove = userIds.Where(id => id != cls.LecturerId).ToHashSet();
            if (toRemove.Count == 0)
            {
                return (Ok: false, Message: "Không thể xóa giảng viên phụ trách khỏi lớp học.", Room: (RoomDto?)null);
            }

            var removed = cls.MemberIds.RemoveAll(toRemove.Contains);
            cls.MemberCount = cls.MemberIds.Count;

            RoomDto? syncedRoom = null;
            if (!string.IsNullOrEmpty(cls.LinkedRoomId))
            {
                var room = _store.Rooms.FirstOrDefault(r => r.RoomId == cls.LinkedRoomId);
                if (room is not null)
                {
                    room.MemberIds.RemoveAll(toRemove.Contains);
                    if (removed > 0)
                    {
                        AppendRoomSystemNoticeLocked(room, $"Đã cập nhật danh sách lớp {cls.ClassCode} (gỡ {removed} thành viên).");
                    }
                    syncedRoom = CloneRoom(room);
                }
            }

            return (Ok: true, Message: $"Đã xóa {removed} thành viên khỏi lớp '{cls.ClassCode}'.", Room: syncedRoom);
        });

        if (res.Room is not null)
        {
            RoomUpdated?.Invoke(this, res.Room);
        }

        return Task.FromResult(res.Ok ? OperationResult.Ok(res.Message) : OperationResult.Fail(res.Message));
    }

    public async Task<OperationResult<RoomDto>> CreateOrLinkClassRoomAsync(string classId, string roomName, CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var cls = _store.Classes.FirstOrDefault(c => c.ClassId == classId);
            if (cls is null) return (FoundExisting: false, Result: OperationResult<RoomDto>.Fail("Không tìm thấy lớp học."), ClsSnapshot: (ClassDto?)null);

            if (!string.IsNullOrEmpty(cls.LinkedRoomId))
            {
                var linked = _store.Rooms.FirstOrDefault(r => r.RoomId == cls.LinkedRoomId);
                if (linked is not null)
                {
                    foreach (var m in cls.MemberIds)
                    {
                        if (!linked.MemberIds.Contains(m)) linked.MemberIds.Add(m);
                    }
                    linked.JoinCode = cls.JoinCode;
                    AppendRoomSystemNoticeLocked(linked, $"Đã đồng bộ danh sách {cls.MemberIds.Count} thành viên từ lớp {cls.ClassCode}.");
                    return (FoundExisting: true, Result: OperationResult<RoomDto>.Ok(CloneRoom(linked), "Đã đồng bộ thành viên lớp với phòng chat liên kết."), ClsSnapshot: (ClassDto?)null);
                }
            }

            return (FoundExisting: false, Result: OperationResult<RoomDto>.Ok(new RoomDto()), ClsSnapshot: CloneClass(cls));
        });

        if (res.FoundExisting)
        {
            if (res.Result.Success && res.Result.Data is not null)
            {
                RoomUpdated?.Invoke(this, res.Result.Data);
            }
            return res.Result;
        }

        if (res.ClsSnapshot is null)
        {
            return res.Result;
        }

        var clsSnap = res.ClsSnapshot;
        var roomResult = await CreateRoomAsync(new RoomDto
        {
            RoomName = string.IsNullOrWhiteSpace(roomName) ? clsSnap.ClassName : roomName,
            Description = $"Phòng chat đồng bộ tự động từ lớp {clsSnap.ClassCode}",
            RoomType = RoomType.ClassRoom,
            ClassId = clsSnap.ClassId,
            JoinCode = clsSnap.JoinCode,
            OwnerId = clsSnap.LecturerId ?? "u_gv_demo01"
        }, clsSnap.MemberIds, cancellationToken);

        if (roomResult.Success && roomResult.Data is not null)
        {
            _store.MutateAndSave(() =>
            {
                var cls = _store.Classes.FirstOrDefault(c => c.ClassId == clsSnap.ClassId);
                if (cls is not null)
                {
                    cls.LinkedRoomId = roomResult.Data.RoomId;
                }
            });
        }

        return roomResult;
    }

    public async Task<OperationResult<ClassJoinResultDto>> JoinByCodeAsync(
        string userId,
        string joinCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return OperationResult<ClassJoinResultDto>.Fail("Vui lòng nhập mã lớp học (VD: 23XD1-KC8B).");
        }

        var code = joinCode.Trim().ToUpperInvariant();
        var outcome = _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(userId)?.User;
            if (acc is null)
            {
                return (NeedCreateRoomForClassId: (string?)null, Result: OperationResult<ClassJoinResultDto>.Fail("Không tìm thấy tài khoản hiện tại."));
            }

            var cls = _store.Classes.FirstOrDefault(c => c.JoinCode.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (cls is not null)
            {
                if (!cls.AllowStudentJoinByCode)
                {
                    return (NeedCreateRoomForClassId: (string?)null, Result: OperationResult<ClassJoinResultDto>.Fail($"Giảng viên đã tạm khóa tham gia bằng mã cho lớp '{cls.ClassCode}'."));
                }

                var alreadyInClass = cls.MemberIds.Contains(userId);
                if (!alreadyInClass)
                {
                    cls.MemberIds.Add(userId);
                    cls.MemberCount = cls.MemberIds.Count;
                }

                RoomDto? linkedRoom = !string.IsNullOrEmpty(cls.LinkedRoomId)
                    ? _store.Rooms.FirstOrDefault(r => r.RoomId == cls.LinkedRoomId)
                    : null;

                if (linkedRoom is not null)
                {
                    var addedToRoom = !linkedRoom.MemberIds.Contains(userId);
                    if (addedToRoom)
                    {
                        linkedRoom.MemberIds.Add(userId);
                        AppendRoomSystemNoticeLocked(linkedRoom, $"{acc.FullName} ({acc.Username}) đã tham gia lớp bằng mã {cls.JoinCode}.");
                    }

                    var msg = alreadyInClass && !addedToRoom
                        ? $"Bạn đã là thành viên của lớp '{cls.ClassName}'."
                        : $"Tham gia thành công lớp '{cls.ClassName}' ({cls.ClassCode})!";

                    return (NeedCreateRoomForClassId: (string?)null, Result: OperationResult<ClassJoinResultDto>.Ok(new ClassJoinResultDto
                    {
                        JoinedClass = CloneClass(cls),
                        JoinedRoom = CloneRoom(linkedRoom),
                        Summary = msg
                    }, msg));
                }

                return (NeedCreateRoomForClassId: cls.ClassId, Result: OperationResult<ClassJoinResultDto>.Ok(new ClassJoinResultDto
                {
                    JoinedClass = CloneClass(cls),
                    Summary = $"Đã tham gia lớp '{cls.ClassName}' ({cls.ClassCode})!"
                }, $"Đã tham gia lớp '{cls.ClassName}' ({cls.ClassCode})!"));
            }

            // Fallback: tìm trực tiếp theo JoinCode của Room nếu là phòng lẻ
            var room = _store.Rooms.FirstOrDefault(r => r.JoinCode.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (room is not null)
            {
                var added = !room.MemberIds.Contains(userId);
                if (added)
                {
                    room.MemberIds.Add(userId);
                    AppendRoomSystemNoticeLocked(room, $"{acc.FullName} ({acc.Username}) đã tham gia phòng bằng mã {room.JoinCode}.");
                }

                var msg = added
                    ? $"Đã tham gia phòng '{room.RoomName}'!"
                    : $"Bạn đã là thành viên của phòng '{room.RoomName}'.";

                return (NeedCreateRoomForClassId: (string?)null, Result: OperationResult<ClassJoinResultDto>.Ok(new ClassJoinResultDto
                {
                    JoinedRoom = CloneRoom(room),
                    Summary = msg
                }, msg));
            }

            return (NeedCreateRoomForClassId: (string?)null, Result: OperationResult<ClassJoinResultDto>.Fail($"Không tìm thấy lớp học hoặc phòng nào có mã '{code}'."));
        });

        if (outcome.NeedCreateRoomForClassId is { } cid && outcome.Result.Data?.JoinedClass is { } jCls)
        {
            var linkedRes = await CreateOrLinkClassRoomAsync(cid, jCls.ClassName, cancellationToken);
            if (linkedRes.Success && linkedRes.Data is not null)
            {
                outcome.Result.Data.JoinedRoom = linkedRes.Data;
            }
        }
        else if (outcome.Result.Success && outcome.Result.Data?.JoinedRoom is { } jRoom)
        {
            RoomUpdated?.Invoke(this, jRoom);
        }

        return outcome.Result;
    }

    public Task<OperationResult<string>> RegenerateJoinCodeAsync(
        string classId,
        CancellationToken cancellationToken = default)
    {
        var res = _store.MutateAndSave(() =>
        {
            var cls = _store.Classes.FirstOrDefault(c => c.ClassId == classId);
            if (cls is null) return (Result: OperationResult<string>.Fail("Không tìm thấy lớp học."), UpdatedRoom: (RoomDto?)null);

            var nextCode = DemoDataStore.GenerateJoinCode(
                cls.ClassCode,
                c => _store.Classes.Any(x => x.JoinCode.Equals(c, StringComparison.OrdinalIgnoreCase))
                  || _store.Rooms.Any(x => x.JoinCode.Equals(c, StringComparison.OrdinalIgnoreCase)));

            cls.JoinCode = nextCode;

            RoomDto? linkedRoom = null;
            if (!string.IsNullOrEmpty(cls.LinkedRoomId))
            {
                var room = _store.Rooms.FirstOrDefault(r => r.RoomId == cls.LinkedRoomId);
                if (room is not null)
                {
                    room.JoinCode = nextCode;
                    linkedRoom = CloneRoom(room);
                }
            }

            return (Result: OperationResult<string>.Ok(nextCode, $"Đã cấp lại mã lớp mới: {nextCode}"), UpdatedRoom: linkedRoom);
        });

        if (res.UpdatedRoom is not null)
        {
            RoomUpdated?.Invoke(this, res.UpdatedRoom);
        }

        return Task.FromResult(res.Result);
    }

    public void UpdateUserPresence(string userId, PresenceStatus status)
    {
        _store.MutateAndSave(() =>
        {
            var acc = _store.FindAccountById(userId);
            if (acc is not null) acc.User.Presence = status;
            var conn = _store.ConnectedClients.FirstOrDefault(c => c.UserId == userId);
            if (conn is not null) conn.Status = status;
        });

        if (CurrentUser?.UserId == userId) CurrentUser.Presence = status;
        _network.EmitPresence(userId, status);
        CurrentUserChanged?.Invoke(this, CurrentUser);
    }

    private void AppendRoomSystemNoticeLocked(RoomDto room, string content)
    {
        if (!_store.MessagesByConversation.TryGetValue(room.RoomId, out var list))
        {
            list = new List<MessageDto>();
            _store.MessagesByConversation[room.RoomId] = list;
        }

        list.Add(new MessageDto
        {
            MessageId = $"sys_{Guid.NewGuid():N}"[..12],
            ConversationId = room.RoomId,
            RoomId = room.RoomId,
            IsRoomMessage = true,
            SenderId = room.OwnerId,
            SenderName = "Hệ thống DAU Chat",
            Content = content,
            Type = MessageType.SystemNotice,
            SentAtClientUtc = DateTime.UtcNow,
            ServerTimestampUtc = DateTime.UtcNow
        });
        room.LastActivityAtUtc = DateTime.UtcNow;
    }

    private static (string Bg, string Fg) ResolveToneForRole(UserRole role) => role switch
    {
        UserRole.Admin => ("#FEF2F2", "#CB0914"),
        UserRole.GiangVien => ("#1E293B", "#FFFFFF"),
        _ => ("#EFF6FF", "#1D4ED8")
    };

    private static UserDto CloneUser(UserDto u) => new()
    {
        UserId = u.UserId,
        Username = u.Username,
        FullName = u.FullName,
        Email = u.Email,
        AvatarPath = u.AvatarPath,
        Role = u.Role,
        Status = u.Status,
        Presence = u.Presence,
        ClassId = u.ClassId,
        ClassName = u.ClassName,
        LockReason = u.LockReason,
        CreatedAtUtc = u.CreatedAtUtc,
        LastSeenAtUtc = u.LastSeenAtUtc
    };

    private static ClassDto CloneClass(ClassDto c) => new()
    {
        ClassId = c.ClassId,
        ClassCode = c.ClassCode,
        ClassName = c.ClassName,
        Department = c.Department,
        AcademicYear = c.AcademicYear,
        LecturerId = c.LecturerId,
        LecturerName = c.LecturerName,
        LinkedRoomId = c.LinkedRoomId,
        JoinCode = c.JoinCode,
        AllowStudentJoinByCode = c.AllowStudentJoinByCode,
        MemberCount = c.MemberIds.Count,
        MemberIds = new List<string>(c.MemberIds),
        CreatedAtUtc = c.CreatedAtUtc
    };

    private static RoomDto CloneRoom(RoomDto r) => new()
    {
        RoomId = r.RoomId,
        RoomName = r.RoomName,
        Description = r.Description,
        RoomType = r.RoomType,
        ClassId = r.ClassId,
        JoinCode = r.JoinCode,
        OwnerId = r.OwnerId,
        MemberIds = new List<string>(r.MemberIds),
        PinnedNotice = r.PinnedNotice is null
            ? null
            : new PinnedNoticeDto
            {
                MessageId = r.PinnedNotice.MessageId,
                RoomId = r.PinnedNotice.RoomId,
                PinnedByUserId = r.PinnedNotice.PinnedByUserId,
                PinnedByName = r.PinnedNotice.PinnedByName,
                Content = r.PinnedNotice.Content,
                PinnedAtUtc = r.PinnedNotice.PinnedAtUtc
            },
        CreatedAtUtc = r.CreatedAtUtc,
        LastActivityAtUtc = r.LastActivityAtUtc
    };
}
