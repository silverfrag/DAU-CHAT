using DauChat.Shared.Enums;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;
using Microsoft.Data.Sqlite;

namespace DauChat.Server.Metadata;

public sealed class MetadataRequestHandler
{
    private readonly string _databasePath;

    public MetadataRequestHandler(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
    }

    public async void HandlePacket(object? sender, NetworkPacket packet)
    {
        if (!IsMetadataRequest(packet.Type)) return;
        var response = await HandleAsync(packet);
        if (sender is DauChat.Shared.Interfaces.INetworkService network && response is not null)
        {
            await network.SendToUserAsync(packet.SenderId, response);
        }
    }

    public async Task<NetworkPacket?> HandleAsync(NetworkPacket packet, CancellationToken cancellationToken = default)
    {
        try
        {
            return packet.Type switch
            {
                PacketType.LoginRequest => await LoginAsync(packet, cancellationToken),
                PacketType.LogoutRequest => await LogoutAsync(packet, cancellationToken),
                PacketType.ChangePasswordRequest => await ChangePasswordAsync(packet, cancellationToken),
                PacketType.GetProfileRequest => await GetProfileAsync(packet, cancellationToken),
                PacketType.UpdateProfileRequest => await UpdateProfileAsync(packet, cancellationToken),
                PacketType.GetUsersRequest => await GetUsersAsync(packet, cancellationToken),
                PacketType.CreateUserRequest => await CreateUserAsync(packet, cancellationToken),
                PacketType.UpdateUserRequest => await UpdateUserAsync(packet, cancellationToken),
                PacketType.LockUserRequest => await LockUserAsync(packet, cancellationToken),
                PacketType.UnlockUserRequest => await UnlockUserAsync(packet, cancellationToken),
                PacketType.ResetPasswordRequest => await ResetPasswordAsync(packet, cancellationToken),
                PacketType.AssignRoleRequest => await AssignRoleAsync(packet, cancellationToken),
                PacketType.CreateRoomRequest => await CreateRoomAsync(packet, cancellationToken),
                PacketType.UpdateRoomRequest => await UpdateRoomAsync(packet, cancellationToken),
                PacketType.DeleteRoomRequest => await DeleteRoomAsync(packet, cancellationToken),
                PacketType.GetRoomMembersRequest => await GetRoomMembersAsync(packet, cancellationToken),
                PacketType.AddRoomMembersRequest => await AddRoomMembersAsync(packet, cancellationToken),
                PacketType.RemoveRoomMemberRequest => await RemoveRoomMemberAsync(packet, cancellationToken),
                PacketType.GetClassesRequest => await GetClassesAsync(packet, cancellationToken),
                PacketType.CreateClassRequest => await CreateClassAsync(packet, cancellationToken),
                PacketType.UpdateClassRequest => await UpdateClassAsync(packet, cancellationToken),
                PacketType.DeleteClassRequest => await DeleteClassAsync(packet, cancellationToken),
                PacketType.GetClassMembersRequest => await GetClassMembersAsync(packet, cancellationToken),
                PacketType.AddClassMembersRequest => await AddClassMembersAsync(packet, cancellationToken),
                PacketType.RemoveClassMembersRequest => await RemoveClassMembersAsync(packet, cancellationToken),
                PacketType.LinkClassRoomRequest => await LinkClassRoomAsync(packet, cancellationToken),
                _ => null
            };
        }
        catch (SqliteException ex)
        {
            return Error(packet, ResponseType(packet.Type), ex.Message, "METADATA_ERROR");
        }
    }

    private async Task<NetworkPacket> LoginAsync(NetworkPacket packet, CancellationToken ct)
    {
        var request = Payload<LoginRequestDto>(packet);
        if (request is null) return Error(packet, PacketType.LoginResponse, "Dữ liệu đăng nhập không hợp lệ.", "VALIDATION_ERROR");
        await using var db = Open();
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT UserId, Username, PasswordHash, FullName, Email, AvatarPath, Role, Status, ClassId, LockReason, CreatedAtUtc, LastSeenAtUtc FROM Users WHERE Username = $username";
        command.Parameters.AddWithValue("$username", request.Username.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return Error(packet, PacketType.LoginResponse, "Tên đăng nhập hoặc mật khẩu không đúng.", "INVALID_CREDENTIALS");
        var hash = reader.GetString(2);
        var status = (AccountStatus)reader.GetInt32(7);
        if (!MetadataStore.VerifyPassword(request.Password, hash)) return Error(packet, PacketType.LoginResponse, "Tên đăng nhập hoặc mật khẩu không đúng.", "INVALID_CREDENTIALS");
        if (status == AccountStatus.Locked) return Error(packet, PacketType.LoginResponse, "Tài khoản đã bị khóa.", "ACCOUNT_LOCKED");
        var user = ReadUser(reader);
        var now = DateTime.UtcNow;
        await reader.DisposeAsync();
        await using var update = db.CreateCommand();
        update.CommandText = "UPDATE Users SET LastSeenAtUtc = $lastSeen WHERE UserId = $id";
        update.Parameters.AddWithValue("$lastSeen", now.ToString("O"));
        update.Parameters.AddWithValue("$id", user.UserId);
        await update.ExecuteNonQueryAsync(ct);
        user.LastSeenAtUtc = now;
        user.Presence = PresenceStatus.Online;
        return Success(packet, PacketType.LoginResponse, new LoginResultDto { User = user, SessionToken = Guid.NewGuid().ToString("N"), LoggedInAtUtc = now });
    }

    private async Task<NetworkPacket> LogoutAsync(NetworkPacket p, CancellationToken ct)
    {
        var id = Payload<IdRequest>(p)?.Id;
        if (string.IsNullOrWhiteSpace(id)) return Error(p, PacketType.LogoutResponse, "UserId không hợp lệ.", "VALIDATION_ERROR");
        await using var db = Open();
        await using var c = db.CreateCommand(); c.CommandText = "UPDATE Users SET LastSeenAtUtc = $time WHERE UserId = $id"; c.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("O")); c.Parameters.AddWithValue("$id", id); await c.ExecuteNonQueryAsync(ct);
        return Success(p, PacketType.LogoutResponse, OperationResult.Ok("Đăng xuất thành công."));
    }

    private async Task<NetworkPacket> ChangePasswordAsync(NetworkPacket p, CancellationToken ct)
    {
        var r = Payload<ChangePasswordRequestDto>(p);
        if (r is null || string.IsNullOrWhiteSpace(r.UserId) || string.IsNullOrWhiteSpace(r.NewPassword)) return Error(p, PacketType.ChangePasswordResponse, "Dữ liệu đổi mật khẩu không hợp lệ.", "VALIDATION_ERROR");
        await using var db = Open();
        await using var c = db.CreateCommand(); c.CommandText = "SELECT PasswordHash FROM Users WHERE UserId = $id"; c.Parameters.AddWithValue("$id", r.UserId); var old = await c.ExecuteScalarAsync(ct) as string;
        if (old is null || !MetadataStore.VerifyPassword(r.OldPassword, old)) return Error(p, PacketType.ChangePasswordResponse, "Mật khẩu cũ không đúng.", "INVALID_PASSWORD");
        await using var u = db.CreateCommand(); u.CommandText = "UPDATE Users SET PasswordHash = $hash WHERE UserId = $id"; u.Parameters.AddWithValue("$hash", MetadataStore.HashPassword(r.NewPassword)); u.Parameters.AddWithValue("$id", r.UserId); await u.ExecuteNonQueryAsync(ct);
        return Success(p, PacketType.ChangePasswordResponse, OperationResult.Ok("Đổi mật khẩu thành công."));
    }

    private async Task<NetworkPacket> GetProfileAsync(NetworkPacket p, CancellationToken ct) => await UserResultAsync(p, PacketType.GetProfileResponse, ct);

    private async Task<NetworkPacket> UpdateProfileAsync(NetworkPacket p, CancellationToken ct)
    {
        var r = Payload<ProfileRequest>(p); if (r is null) return Error(p, PacketType.UpdateProfileResponse, "Dữ liệu profile không hợp lệ.", "VALIDATION_ERROR");
        await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "UPDATE Users SET FullName=$name, Email=$email, AvatarPath=$avatar WHERE UserId=$id"; c.Parameters.AddWithValue("$name", r.FullName); c.Parameters.AddWithValue("$email", r.Email); c.Parameters.AddWithValue("$avatar", (object?)r.AvatarPath ?? DBNull.Value); c.Parameters.AddWithValue("$id", r.UserId); var count = await c.ExecuteNonQueryAsync(ct);
        if (count == 0) return Error(p, PacketType.UpdateProfileResponse, "Không tìm thấy người dùng.", "NOT_FOUND"); return await UserResultAsync(p, PacketType.UpdateProfileResponse, ct, r.UserId);
    }

    private async Task<NetworkPacket> GetUsersAsync(NetworkPacket p, CancellationToken ct)
    {
        if (!await IsAdminAsync(p.SenderId, ct)) return Error(p, PacketType.GetUsersResponse, "Chỉ Admin được xem danh sách tài khoản.", "FORBIDDEN");
        await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "SELECT UserId, Username, FullName, Email, AvatarPath, Role, Status, ClassId, LockReason, CreatedAtUtc, LastSeenAtUtc FROM Users ORDER BY Username"; await using var r = await c.ExecuteReaderAsync(ct); var list = new List<UserDto>(); while (await r.ReadAsync(ct)) list.Add(ReadUser(r)); return Success(p, PacketType.GetUsersResponse, (IReadOnlyList<UserDto>)list);
    }

    private async Task<NetworkPacket> CreateUserAsync(NetworkPacket p, CancellationToken ct)
    {
        if (!await IsAdminAsync(p.SenderId, ct)) return Error(p, PacketType.CreateUserResponse, "Chỉ Admin được tạo tài khoản.", "FORBIDDEN"); var r = Payload<CreateUserRequestDto>(p); if (r is null || string.IsNullOrWhiteSpace(r.Username) || string.IsNullOrWhiteSpace(r.InitialPassword)) return Error(p, PacketType.CreateUserResponse, "Dữ liệu tài khoản không hợp lệ.", "VALIDATION_ERROR");
        var user = new UserDto { UserId = Guid.NewGuid().ToString("N"), Username = r.Username.Trim(), FullName = r.FullName.Trim(), Email = r.Email.Trim(), Role = r.Role, ClassId = r.ClassId, CreatedAtUtc = DateTime.UtcNow };
        await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "INSERT INTO Users(UserId,Username,PasswordHash,FullName,Email,Role,Status,ClassId,CreatedAtUtc) VALUES($id,$username,$hash,$name,$email,$role,$status,$class,$created)"; AddUser(c, user); c.Parameters.AddWithValue("$hash", MetadataStore.HashPassword(r.InitialPassword)); await c.ExecuteNonQueryAsync(ct); return Success(p, PacketType.CreateUserResponse, user);
    }

    private async Task<NetworkPacket> UpdateUserAsync(NetworkPacket p, CancellationToken ct)
    {
        if (!await IsAdminAsync(p.SenderId, ct)) return Error(p, PacketType.UpdateUserResponse, "Chỉ Admin được sửa tài khoản.", "FORBIDDEN"); var u = Payload<UserDto>(p); if (u is null) return Error(p, PacketType.UpdateUserResponse, "Dữ liệu tài khoản không hợp lệ.", "VALIDATION_ERROR"); await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "UPDATE Users SET Username=$username,FullName=$name,Email=$email,AvatarPath=$avatar,Role=$role,Status=$status,ClassId=$class,LockReason=$reason WHERE UserId=$id"; AddUserUpdate(c, u); var count = await c.ExecuteNonQueryAsync(ct); return count == 0 ? Error(p, PacketType.UpdateUserResponse, "Không tìm thấy người dùng.", "NOT_FOUND") : Success(p, PacketType.UpdateUserResponse, u);
    }

    private async Task<NetworkPacket> LockUserAsync(NetworkPacket p, CancellationToken ct) => await SetLockAsync(p, ct, true);
    private async Task<NetworkPacket> UnlockUserAsync(NetworkPacket p, CancellationToken ct) => await SetLockAsync(p, ct, false);
    private async Task<NetworkPacket> SetLockAsync(NetworkPacket p, CancellationToken ct, bool locked)
    {
        var type = locked ? PacketType.LockUserResponse : PacketType.UnlockUserResponse; if (!await IsAdminAsync(p.SenderId, ct)) return Error(p, type, "Chỉ Admin được thay đổi trạng thái tài khoản.", "FORBIDDEN"); var id = locked ? Payload<LockRequest>(p)?.UserId : Payload<IdRequest>(p)?.Id; var reason = locked ? Payload<LockRequest>(p)?.Reason : null; if (string.IsNullOrWhiteSpace(id)) return Error(p, type, "UserId không hợp lệ.", "VALIDATION_ERROR"); await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "UPDATE Users SET Status=$status, LockReason=$reason WHERE UserId=$id"; c.Parameters.AddWithValue("$status", locked ? (int)AccountStatus.Locked : (int)AccountStatus.Active); c.Parameters.AddWithValue("$reason", (object?)reason ?? DBNull.Value); c.Parameters.AddWithValue("$id", id); var count = await c.ExecuteNonQueryAsync(ct); return count == 0 ? Error(p, type, "Không tìm thấy người dùng.", "NOT_FOUND") : Success(p, type, OperationResult.Ok(locked ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản."));
    }

    private async Task<NetworkPacket> ResetPasswordAsync(NetworkPacket p, CancellationToken ct)
    {
        if (!await IsAdminAsync(p.SenderId, ct)) return Error(p, PacketType.ResetPasswordResponse, "Chỉ Admin được reset mật khẩu.", "FORBIDDEN"); var id = Payload<IdRequest>(p)?.Id; if (string.IsNullOrWhiteSpace(id)) return Error(p, PacketType.ResetPasswordResponse, "UserId không hợp lệ.", "VALIDATION_ERROR"); var password = $"Dau@{Random.Shared.Next(100000, 999999)}"; await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "UPDATE Users SET PasswordHash=$hash WHERE UserId=$id"; c.Parameters.AddWithValue("$hash", MetadataStore.HashPassword(password)); c.Parameters.AddWithValue("$id", id); var count = await c.ExecuteNonQueryAsync(ct); return count == 0 ? Error(p, PacketType.ResetPasswordResponse, "Không tìm thấy người dùng.", "NOT_FOUND") : Success(p, PacketType.ResetPasswordResponse, password);
    }

    private async Task<NetworkPacket> AssignRoleAsync(NetworkPacket p, CancellationToken ct)
    {
        if (!await IsAdminAsync(p.SenderId, ct)) return Error(p, PacketType.AssignRoleResponse, "Chỉ Admin được phân quyền.", "FORBIDDEN"); var r = Payload<RoleRequest>(p); if (r is null || r.Role == UserRole.System) return Error(p, PacketType.AssignRoleResponse, "Vai trò không hợp lệ.", "VALIDATION_ERROR"); await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "UPDATE Users SET Role=$role WHERE UserId=$id"; c.Parameters.AddWithValue("$role", (int)r.Role); c.Parameters.AddWithValue("$id", r.UserId); var count = await c.ExecuteNonQueryAsync(ct); return count == 0 ? Error(p, PacketType.AssignRoleResponse, "Không tìm thấy người dùng.", "NOT_FOUND") : Success(p, PacketType.AssignRoleResponse, OperationResult.Ok("Đã phân quyền."));
    }

    private async Task<NetworkPacket> CreateRoomAsync(NetworkPacket p, CancellationToken ct)
    {
        var r = Payload<RoomRequest>(p); if (r?.Room is null) return Error(p, PacketType.CreateRoomResponse, "Dữ liệu phòng không hợp lệ.", "VALIDATION_ERROR"); var room = r.Room; if (string.IsNullOrWhiteSpace(room.RoomId)) room.RoomId = Guid.NewGuid().ToString("N"); if (string.IsNullOrWhiteSpace(room.OwnerId)) room.OwnerId = p.SenderId; room.CreatedAtUtc = DateTime.UtcNow; await using var db = Open(); await using var tx = await db.BeginTransactionAsync(ct); await using var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx; c.CommandText = "INSERT INTO Rooms(RoomId,RoomName,Description,RoomType,ClassId,OwnerId,CreatedAtUtc,LastActivityAtUtc) VALUES($id,$name,$description,$type,$class,$owner,$created,$last)"; AddRoom(c, room); await c.ExecuteNonQueryAsync(ct); foreach (var userId in r.InitialMemberIds.Append(room.OwnerId).Distinct(StringComparer.Ordinal)) await AddMemberAsync(db, (SqliteTransaction)tx, "RoomMembers", "RoomId", room.RoomId, userId, ct); await tx.CommitAsync(ct); room.MemberIds = r.InitialMemberIds.Append(room.OwnerId).Distinct(StringComparer.Ordinal).ToList(); return Success(p, PacketType.CreateRoomResponse, room);
    }

    private async Task<NetworkPacket> UpdateRoomAsync(NetworkPacket p, CancellationToken ct)
    {
        var room = Payload<RoomDto>(p); if (room is null) return Error(p, PacketType.UpdateRoomResponse, "Dữ liệu phòng không hợp lệ.", "VALIDATION_ERROR"); await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "UPDATE Rooms SET RoomName=$name,Description=$description,RoomType=$type,ClassId=$class,LastActivityAtUtc=$last WHERE RoomId=$id"; AddRoomUpdate(c, room); var count = await c.ExecuteNonQueryAsync(ct); return count == 0 ? Error(p, PacketType.UpdateRoomResponse, "Không tìm thấy phòng.", "NOT_FOUND") : Success(p, PacketType.UpdateRoomResponse, room);
    }

    private async Task<NetworkPacket> DeleteRoomAsync(NetworkPacket p, CancellationToken ct) => await DeleteByIdAsync(p, PacketType.DeleteRoomResponse, "Rooms", "RoomId", "phòng", ct);

    private async Task<NetworkPacket> GetRoomMembersAsync(NetworkPacket p, CancellationToken ct) => await GetMembersAsync(p, PacketType.GetRoomMembersResponse, "RoomMembers", "RoomId", ct);
    private async Task<NetworkPacket> AddRoomMembersAsync(NetworkPacket p, CancellationToken ct) => await AddMembersRequestAsync(p, PacketType.AddRoomMembersResponse, "RoomMembers", "RoomId", ct);
    private async Task<NetworkPacket> RemoveRoomMemberAsync(NetworkPacket p, CancellationToken ct) => await RemoveMembersRequestAsync(p, PacketType.RemoveRoomMemberResponse, "RoomMembers", "RoomId", ct);

    private async Task<NetworkPacket> GetClassesAsync(NetworkPacket p, CancellationToken ct)
    {
        var filter = Payload<ClassFilterRequest>(p)?.FilterKeyword; await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "SELECT ClassId,ClassCode,ClassName,Department,AcademicYear,LecturerId,LinkedRoomId,CreatedAtUtc FROM Classes WHERE ($filter = '' OR ClassCode LIKE $like OR ClassName LIKE $like) ORDER BY ClassCode"; c.Parameters.AddWithValue("$filter", filter ?? string.Empty); c.Parameters.AddWithValue("$like", $"%{filter}%"); await using var r = await c.ExecuteReaderAsync(ct); var list = new List<ClassDto>(); while (await r.ReadAsync(ct)) list.Add(await ReadClassAsync(db, r, ct)); return Success(p, PacketType.GetClassesResponse, (IReadOnlyList<ClassDto>)list);
    }

    private async Task<NetworkPacket> CreateClassAsync(NetworkPacket p, CancellationToken ct) { if (!await IsAdminAsync(p.SenderId, ct)) return Error(p, PacketType.CreateClassResponse, "Chỉ Admin được tạo lớp.", "FORBIDDEN"); var x = Payload<ClassDto>(p); if (x is null) return Error(p, PacketType.CreateClassResponse, "Dữ liệu lớp không hợp lệ.", "VALIDATION_ERROR"); if (string.IsNullOrWhiteSpace(x.ClassId)) x.ClassId = Guid.NewGuid().ToString("N"); x.CreatedAtUtc = DateTime.UtcNow; await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "INSERT INTO Classes(ClassId,ClassCode,ClassName,Department,AcademicYear,LecturerId,LinkedRoomId,CreatedAtUtc) VALUES($id,$code,$name,$department,$year,$lecturer,$room,$created)"; AddClass(c, x); await c.ExecuteNonQueryAsync(ct); return Success(p, PacketType.CreateClassResponse, x); }
    private async Task<NetworkPacket> UpdateClassAsync(NetworkPacket p, CancellationToken ct) { if (!await IsAdminAsync(p.SenderId, ct)) return Error(p, PacketType.UpdateClassResponse, "Chỉ Admin được sửa lớp.", "FORBIDDEN"); var x = Payload<ClassDto>(p); if (x is null) return Error(p, PacketType.UpdateClassResponse, "Dữ liệu lớp không hợp lệ.", "VALIDATION_ERROR"); await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "UPDATE Classes SET ClassCode=$code,ClassName=$name,Department=$department,AcademicYear=$year,LecturerId=$lecturer,LinkedRoomId=$room WHERE ClassId=$id"; AddClassUpdate(c, x); var count = await c.ExecuteNonQueryAsync(ct); return count == 0 ? Error(p, PacketType.UpdateClassResponse, "Không tìm thấy lớp.", "NOT_FOUND") : Success(p, PacketType.UpdateClassResponse, x); }
    private async Task<NetworkPacket> DeleteClassAsync(NetworkPacket p, CancellationToken ct) => await DeleteByIdAsync(p, PacketType.DeleteClassResponse, "Classes", "ClassId", "lớp", ct);
    private async Task<NetworkPacket> GetClassMembersAsync(NetworkPacket p, CancellationToken ct) => await GetMembersAsync(p, PacketType.GetClassMembersResponse, "ClassMembers", "ClassId", ct);
    private async Task<NetworkPacket> AddClassMembersAsync(NetworkPacket p, CancellationToken ct) => await AddMembersRequestAsync(p, PacketType.AddClassMembersResponse, "ClassMembers", "ClassId", ct);
    private async Task<NetworkPacket> RemoveClassMembersAsync(NetworkPacket p, CancellationToken ct) => await RemoveMembersRequestAsync(p, PacketType.RemoveClassMembersResponse, "ClassMembers", "ClassId", ct);

    private async Task<NetworkPacket> LinkClassRoomAsync(NetworkPacket p, CancellationToken ct)
    {
        var r = Payload<ClassRoomRequest>(p); if (r is null) return Error(p, PacketType.LinkClassRoomResponse, "Dữ liệu liên kết không hợp lệ.", "VALIDATION_ERROR"); await using var db = Open(); await using var tx = await db.BeginTransactionAsync(ct); var room = new RoomDto { RoomId = Guid.NewGuid().ToString("N"), RoomName = r.RoomName, RoomType = RoomType.ClassRoom, ClassId = r.ClassId, OwnerId = p.SenderId, CreatedAtUtc = DateTime.UtcNow }; await using var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx; c.CommandText = "INSERT INTO Rooms(RoomId,RoomName,RoomType,ClassId,OwnerId,CreatedAtUtc) VALUES($id,$name,$type,$class,$owner,$created)"; AddRoom(c, room); await c.ExecuteNonQueryAsync(ct); await using var update = db.CreateCommand(); update.Transaction = (SqliteTransaction)tx; update.CommandText = "UPDATE Classes SET LinkedRoomId=$room WHERE ClassId=$class"; update.Parameters.AddWithValue("$room", room.RoomId); update.Parameters.AddWithValue("$class", r.ClassId); await update.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); return Success(p, PacketType.LinkClassRoomResponse, room);
    }

    private async Task<NetworkPacket> UserResultAsync(NetworkPacket p, PacketType response, CancellationToken ct, string? userId = null) { var id = userId ?? Payload<IdRequest>(p)?.Id; await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "SELECT UserId,Username,FullName,Email,AvatarPath,Role,Status,ClassId,LockReason,CreatedAtUtc,LastSeenAtUtc FROM Users WHERE UserId=$id"; c.Parameters.AddWithValue("$id", id ?? string.Empty); await using var r = await c.ExecuteReaderAsync(ct); return !await r.ReadAsync(ct) ? Error(p, response, "Không tìm thấy người dùng.", "NOT_FOUND") : Success(p, response, ReadUser(r)); }

    private async Task<NetworkPacket> GetMembersAsync(NetworkPacket p, PacketType response, string table, string key, CancellationToken ct) { var id = Payload<IdRequest>(p)?.Id; await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = $"SELECT u.UserId,u.Username,u.FullName,u.Email,u.AvatarPath,u.Role,u.Status,u.ClassId,u.LockReason,u.CreatedAtUtc,u.LastSeenAtUtc FROM Users u INNER JOIN {table} m ON m.UserId=u.UserId WHERE m.{key}=$id ORDER BY u.Username"; c.Parameters.AddWithValue("$id", id ?? string.Empty); await using var r = await c.ExecuteReaderAsync(ct); var list = new List<UserDto>(); while (await r.ReadAsync(ct)) list.Add(ReadUser(r)); return Success(p, response, (IReadOnlyList<UserDto>)list); }
    private async Task<NetworkPacket> AddMembersRequestAsync(NetworkPacket p, PacketType response, string table, string key, CancellationToken ct) { var x = Payload<MembersRequest>(p); if (x is null) return Error(p, response, "Dữ liệu thành viên không hợp lệ.", "VALIDATION_ERROR"); await using var db = Open(); foreach (var user in x.UserIds.Distinct()) { await using var c = db.CreateCommand(); c.CommandText = $"INSERT OR IGNORE INTO {table}({key},UserId,JoinedAtUtc) VALUES($id,$user,$time)"; c.Parameters.AddWithValue("$id", x.Id); c.Parameters.AddWithValue("$user", user); c.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("O")); await c.ExecuteNonQueryAsync(ct); } return Success(p, response, OperationResult.Ok("Đã thêm thành viên.")); }
    private async Task<NetworkPacket> RemoveMembersRequestAsync(NetworkPacket p, PacketType response, string table, string key, CancellationToken ct) { var x = Payload<MembersRequest>(p); if (x is null) return Error(p, response, "Dữ liệu thành viên không hợp lệ.", "VALIDATION_ERROR"); await using var db = Open(); foreach (var user in x.UserIds.Distinct()) { await using var c = db.CreateCommand(); c.CommandText = $"DELETE FROM {table} WHERE {key}=$id AND UserId=$user"; c.Parameters.AddWithValue("$id", x.Id); c.Parameters.AddWithValue("$user", user); await c.ExecuteNonQueryAsync(ct); } return Success(p, response, OperationResult.Ok("Đã xóa thành viên.")); }
    private async Task<NetworkPacket> DeleteByIdAsync(NetworkPacket p, PacketType response, string table, string key, string label, CancellationToken ct) { var id = Payload<IdRequest>(p)?.Id; await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = $"DELETE FROM {table} WHERE {key}=$id"; c.Parameters.AddWithValue("$id", id ?? string.Empty); var count = await c.ExecuteNonQueryAsync(ct); return count == 0 ? Error(p, response, $"Không tìm thấy {label}.", "NOT_FOUND") : Success(p, response, OperationResult.Ok($"Đã xóa {label}.")); }

    private async Task<bool> IsAdminAsync(string userId, CancellationToken ct) { await using var db = Open(); await using var c = db.CreateCommand(); c.CommandText = "SELECT Role FROM Users WHERE UserId=$id"; c.Parameters.AddWithValue("$id", userId); var value = await c.ExecuteScalarAsync(ct); return value is not null && Convert.ToInt32(value) == (int)UserRole.Admin; }
    private SqliteConnection Open() { var c = new SqliteConnection($"Data Source={_databasePath}"); c.Open(); return c; }
    private static bool IsMetadataRequest(PacketType t) => (int)t is >= 101 and <= 125 or >= 401 and <= 412 or >= 601 and <= 616;
    private static PacketType ResponseType(PacketType t) => t switch { PacketType.LoginRequest => PacketType.LoginResponse, PacketType.LogoutRequest => PacketType.LogoutResponse, PacketType.ChangePasswordRequest => PacketType.ChangePasswordResponse, PacketType.GetProfileRequest => PacketType.GetProfileResponse, PacketType.UpdateProfileRequest => PacketType.UpdateProfileResponse, PacketType.GetUsersRequest => PacketType.GetUsersResponse, PacketType.CreateUserRequest => PacketType.CreateUserResponse, PacketType.UpdateUserRequest => PacketType.UpdateUserResponse, PacketType.LockUserRequest => PacketType.LockUserResponse, PacketType.UnlockUserRequest => PacketType.UnlockUserResponse, PacketType.ResetPasswordRequest => PacketType.ResetPasswordResponse, PacketType.AssignRoleRequest => PacketType.AssignRoleResponse, PacketType.CreateRoomRequest => PacketType.CreateRoomResponse, PacketType.UpdateRoomRequest => PacketType.UpdateRoomResponse, PacketType.DeleteRoomRequest => PacketType.DeleteRoomResponse, PacketType.GetRoomMembersRequest => PacketType.GetRoomMembersResponse, PacketType.AddRoomMembersRequest => PacketType.AddRoomMembersResponse, PacketType.RemoveRoomMemberRequest => PacketType.RemoveRoomMemberResponse, PacketType.GetClassesRequest => PacketType.GetClassesResponse, PacketType.CreateClassRequest => PacketType.CreateClassResponse, PacketType.UpdateClassRequest => PacketType.UpdateClassResponse, PacketType.DeleteClassRequest => PacketType.DeleteClassResponse, PacketType.GetClassMembersRequest => PacketType.GetClassMembersResponse, PacketType.AddClassMembersRequest => PacketType.AddClassMembersResponse, PacketType.RemoveClassMembersRequest => PacketType.RemoveClassMembersResponse, PacketType.LinkClassRoomRequest => PacketType.LinkClassRoomResponse, _ => PacketType.ErrorResponse };
    private NetworkPacket Success<T>(NetworkPacket p, PacketType t, T data) => NetworkPacket.Create(t, "SERVER", OperationResult<T>.Ok(data), p.SenderId, p.CorrelationId);
    private NetworkPacket Error(NetworkPacket p, PacketType t, string message, string code) => NetworkPacket.Create(t, "SERVER", OperationResult.Fail(message, code), p.SenderId, p.CorrelationId);
    private static T? Payload<T>(NetworkPacket p) => p.GetPayload<T>();
    private static UserDto ReadUser(SqliteDataReader r) => new() { UserId = r.GetString(0), Username = r.GetString(1), FullName = r.GetString(2), Email = r.GetString(3), AvatarPath = r.IsDBNull(4) ? null : r.GetString(4), Role = (UserRole)r.GetInt32(5), Status = (AccountStatus)r.GetInt32(6), ClassId = r.IsDBNull(7) ? null : r.GetString(7), LockReason = r.IsDBNull(8) ? null : r.GetString(8), CreatedAtUtc = DateTime.Parse(r.GetString(9)), LastSeenAtUtc = r.IsDBNull(10) ? null : DateTime.Parse(r.GetString(10)) };
    private static void AddUser(SqliteCommand c, UserDto u) { c.Parameters.AddWithValue("$id", u.UserId); c.Parameters.AddWithValue("$username", u.Username); c.Parameters.AddWithValue("$name", u.FullName); c.Parameters.AddWithValue("$email", u.Email); c.Parameters.AddWithValue("$avatar", (object?)u.AvatarPath ?? DBNull.Value); c.Parameters.AddWithValue("$role", (int)u.Role); c.Parameters.AddWithValue("$status", (int)u.Status); c.Parameters.AddWithValue("$class", (object?)u.ClassId ?? DBNull.Value); c.Parameters.AddWithValue("$created", u.CreatedAtUtc.ToString("O")); }
    private static void AddUserUpdate(SqliteCommand c, UserDto u) { c.Parameters.AddWithValue("$id", u.UserId); c.Parameters.AddWithValue("$username", u.Username); c.Parameters.AddWithValue("$name", u.FullName); c.Parameters.AddWithValue("$email", u.Email); c.Parameters.AddWithValue("$avatar", (object?)u.AvatarPath ?? DBNull.Value); c.Parameters.AddWithValue("$role", (int)u.Role); c.Parameters.AddWithValue("$status", (int)u.Status); c.Parameters.AddWithValue("$class", (object?)u.ClassId ?? DBNull.Value); c.Parameters.AddWithValue("$reason", (object?)u.LockReason ?? DBNull.Value); }
    private static void AddRoom(SqliteCommand c, RoomDto r) { c.Parameters.AddWithValue("$id", r.RoomId); c.Parameters.AddWithValue("$name", r.RoomName); c.Parameters.AddWithValue("$description", (object?)r.Description ?? DBNull.Value); c.Parameters.AddWithValue("$type", (int)r.RoomType); c.Parameters.AddWithValue("$class", (object?)r.ClassId ?? DBNull.Value); c.Parameters.AddWithValue("$owner", r.OwnerId); c.Parameters.AddWithValue("$created", r.CreatedAtUtc.ToString("O")); c.Parameters.AddWithValue("$last", (object?)r.LastActivityAtUtc?.ToString("O") ?? DBNull.Value); }
    private static void AddRoomUpdate(SqliteCommand c, RoomDto r) { c.Parameters.AddWithValue("$id", r.RoomId); c.Parameters.AddWithValue("$name", r.RoomName); c.Parameters.AddWithValue("$description", (object?)r.Description ?? DBNull.Value); c.Parameters.AddWithValue("$type", (int)r.RoomType); c.Parameters.AddWithValue("$class", (object?)r.ClassId ?? DBNull.Value); c.Parameters.AddWithValue("$last", (object?)r.LastActivityAtUtc?.ToString("O") ?? DBNull.Value); }
    private static void AddClass(SqliteCommand c, ClassDto x) { c.Parameters.AddWithValue("$id", x.ClassId); c.Parameters.AddWithValue("$code", x.ClassCode); c.Parameters.AddWithValue("$name", x.ClassName); c.Parameters.AddWithValue("$department", x.Department); c.Parameters.AddWithValue("$year", x.AcademicYear); c.Parameters.AddWithValue("$lecturer", (object?)x.LecturerId ?? DBNull.Value); c.Parameters.AddWithValue("$room", (object?)x.LinkedRoomId ?? DBNull.Value); c.Parameters.AddWithValue("$created", x.CreatedAtUtc.ToString("O")); }
    private static void AddClassUpdate(SqliteCommand c, ClassDto x) { c.Parameters.AddWithValue("$id", x.ClassId); c.Parameters.AddWithValue("$code", x.ClassCode); c.Parameters.AddWithValue("$name", x.ClassName); c.Parameters.AddWithValue("$department", x.Department); c.Parameters.AddWithValue("$year", x.AcademicYear); c.Parameters.AddWithValue("$lecturer", (object?)x.LecturerId ?? DBNull.Value); c.Parameters.AddWithValue("$room", (object?)x.LinkedRoomId ?? DBNull.Value); }
    private static async Task AddMemberAsync(SqliteConnection db, SqliteTransaction tx, string table, string key, string id, string user, CancellationToken ct) { await using var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = $"INSERT OR IGNORE INTO {table}({key},UserId,JoinedAtUtc) VALUES($id,$user,$time)"; c.Parameters.AddWithValue("$id", id); c.Parameters.AddWithValue("$user", user); c.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("O")); await c.ExecuteNonQueryAsync(ct); }
    private static async Task<ClassDto> ReadClassAsync(SqliteConnection db, SqliteDataReader r, CancellationToken ct) { var x = new ClassDto { ClassId = r.GetString(0), ClassCode = r.GetString(1), ClassName = r.GetString(2), Department = r.GetString(3), AcademicYear = r.GetString(4), LecturerId = r.IsDBNull(5) ? null : r.GetString(5), LinkedRoomId = r.IsDBNull(6) ? null : r.GetString(6), CreatedAtUtc = DateTime.Parse(r.GetString(7)) }; await using var c = db.CreateCommand(); c.CommandText = "SELECT UserId FROM ClassMembers WHERE ClassId=$id"; c.Parameters.AddWithValue("$id", x.ClassId); await using var m = await c.ExecuteReaderAsync(ct); while (await m.ReadAsync(ct)) x.MemberIds.Add(m.GetString(0)); x.MemberCount = x.MemberIds.Count; return x; }

    public sealed class IdRequest { public string Id { get; set; } = string.Empty; }
    public sealed class LockRequest { public string UserId { get; set; } = string.Empty; public string Reason { get; set; } = string.Empty; }
    public sealed class RoleRequest { public string UserId { get; set; } = string.Empty; public UserRole Role { get; set; } }
    public sealed class ProfileRequest { public string UserId { get; set; } = string.Empty; public string FullName { get; set; } = string.Empty; public string Email { get; set; } = string.Empty; public string? AvatarPath { get; set; } }
    public sealed class RoomRequest { public RoomDto Room { get; set; } = new(); public IReadOnlyList<string> InitialMemberIds { get; set; } = Array.Empty<string>(); }
    public sealed class MembersRequest { public string Id { get; set; } = string.Empty; public IReadOnlyList<string> UserIds { get; set; } = Array.Empty<string>(); }
    public sealed class ClassFilterRequest { public string? FilterKeyword { get; set; } }
    public sealed class ClassRoomRequest { public string ClassId { get; set; } = string.Empty; public string RoomName { get; set; } = string.Empty; }
}
