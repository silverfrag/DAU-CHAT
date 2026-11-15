using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.Core.AuthAndRooms;

public sealed class AuthRoomService : IAuthRoomService
{
    private readonly INetworkService _networkService;

    public AuthRoomService(INetworkService networkService)
        => _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));

    public UserDto? CurrentUser { get; private set; }
    public event EventHandler<UserDto?>? CurrentUserChanged;
    public event EventHandler<RoomDto>? RoomUpdated;
    public event EventHandler<string>? RoomDeleted;

    public async Task<OperationResult<LoginResultDto>> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return OperationResult<LoginResultDto>.Fail("Tên đăng nhập và mật khẩu không được để trống.", "VALIDATION_ERROR");
        var result = await RequestAsync<LoginRequestDto, LoginResultDto>(PacketType.LoginRequest, PacketType.LoginResponse, new LoginRequestDto { Username = username, Password = password }, cancellationToken);
        if (result.Success && result.Data is not null) { CurrentUser = result.Data.User; OnCurrentUserChanged(CurrentUser); }
        return result;
    }

    public async Task<OperationResult> LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync<IdRequest>(PacketType.LogoutRequest, PacketType.LogoutResponse, new(userId), cancellationToken);
        if (result.Success && string.Equals(CurrentUser?.UserId, userId, StringComparison.Ordinal)) { CurrentUser = null; OnCurrentUserChanged(null); }
        return result;
    }

    public Task<OperationResult> ChangePasswordAsync(string userId, string oldPassword, string newPassword, CancellationToken cancellationToken = default)
        => RequestAsync<ChangePasswordRequestDto>(PacketType.ChangePasswordRequest, PacketType.ChangePasswordResponse, new() { UserId = userId, OldPassword = oldPassword, NewPassword = newPassword }, cancellationToken);

    public async Task<OperationResult<UserDto>> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default)
        => await RequestAsync<IdRequest, UserDto>(PacketType.GetProfileRequest, PacketType.GetProfileResponse, new(userId), cancellationToken);

    public async Task<OperationResult<UserDto>> UpdateUserProfileAsync(string userId, string fullName, string email, string? avatarPath = null, CancellationToken cancellationToken = default)
        => await RequestAsync<ProfileRequest, UserDto>(PacketType.UpdateProfileRequest, PacketType.UpdateProfileResponse, new ProfileRequest(userId, fullName, email, avatarPath), cancellationToken);

    public async Task<OperationResult<IReadOnlyList<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default)
        => await RequestAsync<EmptyRequest, IReadOnlyList<UserDto>>(PacketType.GetUsersRequest, PacketType.GetUsersResponse, new(), cancellationToken);

    public async Task<OperationResult<UserDto>> CreateUserAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default)
        => await RequestAsync<CreateUserRequestDto, UserDto>(PacketType.CreateUserRequest, PacketType.CreateUserResponse, request, cancellationToken);

    public async Task<OperationResult<UserDto>> UpdateUserAsync(UserDto user, CancellationToken cancellationToken = default)
        => await RequestAsync<UserDto, UserDto>(PacketType.UpdateUserRequest, PacketType.UpdateUserResponse, user, cancellationToken);

    public Task<OperationResult> LockUserAsync(string userId, string reason, CancellationToken cancellationToken = default)
        => RequestAsync<LockRequest>(PacketType.LockUserRequest, PacketType.LockUserResponse, new(userId, reason), cancellationToken);

    public Task<OperationResult> UnlockUserAsync(string userId, CancellationToken cancellationToken = default)
        => RequestAsync<IdRequest>(PacketType.UnlockUserRequest, PacketType.UnlockUserResponse, new(userId), cancellationToken);

    public async Task<OperationResult<string>> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default)
        => await RequestAsync<IdRequest, string>(PacketType.ResetPasswordRequest, PacketType.ResetPasswordResponse, new(userId), cancellationToken);

    public Task<OperationResult> AssignRoleAsync(string userId, UserRole role, CancellationToken cancellationToken = default)
        => RequestAsync(PacketType.AssignRoleRequest, PacketType.AssignRoleResponse, new RoleRequest(userId, role), cancellationToken);

    public async Task<OperationResult<RoomDto>> CreateRoomAsync(RoomDto room, IReadOnlyList<string>? initialMemberIds = null, CancellationToken cancellationToken = default)
        => await RequestAsync<RoomRequest, RoomDto>(PacketType.CreateRoomRequest, PacketType.CreateRoomResponse, new RoomRequest(room, initialMemberIds ?? Array.Empty<string>()), cancellationToken);

    public async Task<OperationResult<RoomDto>> UpdateRoomAsync(RoomDto room, CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync<RoomDto, RoomDto>(PacketType.UpdateRoomRequest, PacketType.UpdateRoomResponse, room, cancellationToken);
        if (result.Success && result.Data is not null) OnRoomUpdated(result.Data);
        return result;
    }

    public async Task<OperationResult> DeleteRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync<IdRequest>(PacketType.DeleteRoomRequest, PacketType.DeleteRoomResponse, new(roomId), cancellationToken);
        if (result.Success) OnRoomDeleted(roomId);
        return result;
    }

    public async Task<OperationResult<IReadOnlyList<UserDto>>> GetRoomMembersAsync(string roomId, CancellationToken cancellationToken = default)
        => await RequestAsync<IdRequest, IReadOnlyList<UserDto>>(PacketType.GetRoomMembersRequest, PacketType.GetRoomMembersResponse, new(roomId), cancellationToken);

    public Task<OperationResult> AddRoomMembersAsync(string roomId, IReadOnlyList<string> userIds, CancellationToken cancellationToken = default)
        => RequestAsync(PacketType.AddRoomMembersRequest, PacketType.AddRoomMembersResponse, new MembersRequest(roomId, userIds), cancellationToken);

    public Task<OperationResult> RemoveRoomMemberAsync(string roomId, string userId, CancellationToken cancellationToken = default)
        => RequestAsync<MembersRequest>(PacketType.RemoveRoomMemberRequest, PacketType.RemoveRoomMemberResponse, new(roomId, new[] { userId }), cancellationToken);

    public async Task<OperationResult<IReadOnlyList<ClassDto>>> GetClassesAsync(string? filterKeyword = null, CancellationToken cancellationToken = default)
        => await RequestAsync<ClassFilterRequest, IReadOnlyList<ClassDto>>(PacketType.GetClassesRequest, PacketType.GetClassesResponse, new ClassFilterRequest(filterKeyword), cancellationToken);

    public async Task<OperationResult<ClassDto>> CreateClassAsync(ClassDto classDto, CancellationToken cancellationToken = default)
        => await RequestAsync<ClassDto, ClassDto>(PacketType.CreateClassRequest, PacketType.CreateClassResponse, classDto, cancellationToken);

    public async Task<OperationResult<ClassDto>> UpdateClassAsync(ClassDto classDto, CancellationToken cancellationToken = default)
        => await RequestAsync<ClassDto, ClassDto>(PacketType.UpdateClassRequest, PacketType.UpdateClassResponse, classDto, cancellationToken);

    public Task<OperationResult> DeleteClassAsync(string classId, CancellationToken cancellationToken = default)
        => RequestAsync<IdRequest>(PacketType.DeleteClassRequest, PacketType.DeleteClassResponse, new(classId), cancellationToken);

    public async Task<OperationResult<IReadOnlyList<UserDto>>> GetClassMembersAsync(string classId, CancellationToken cancellationToken = default)
        => await RequestAsync<IdRequest, IReadOnlyList<UserDto>>(PacketType.GetClassMembersRequest, PacketType.GetClassMembersResponse, new(classId), cancellationToken);

    public Task<OperationResult> AddClassMembersAsync(string classId, IReadOnlyList<string> userIds, CancellationToken cancellationToken = default)
        => RequestAsync(PacketType.AddClassMembersRequest, PacketType.AddClassMembersResponse, new MembersRequest(classId, userIds), cancellationToken);

    public Task<OperationResult> RemoveClassMembersAsync(string classId, IReadOnlyList<string> userIds, CancellationToken cancellationToken = default)
        => RequestAsync(PacketType.RemoveClassMembersRequest, PacketType.RemoveClassMembersResponse, new MembersRequest(classId, userIds), cancellationToken);

    public async Task<OperationResult<RoomDto>> CreateOrLinkClassRoomAsync(string classId, string roomName, CancellationToken cancellationToken = default)
        => await RequestAsync<ClassRoomRequest, RoomDto>(PacketType.LinkClassRoomRequest, PacketType.LinkClassRoomResponse, new ClassRoomRequest(classId, roomName), cancellationToken);

    private async Task<OperationResult> RequestAsync<T>(PacketType requestType, PacketType responseType, T payload, CancellationToken cancellationToken)
    {
        var response = await SendAsync(requestType, responseType, payload, cancellationToken);
        return response?.GetPayload<OperationResult>() ?? OperationResult.Fail("Không nhận được phản hồi từ máy chủ.", "NO_RESPONSE");
    }

    private async Task<OperationResult<TResponse>> RequestAsync<TRequest, TResponse>(PacketType requestType, PacketType responseType, TRequest payload, CancellationToken cancellationToken)
    {
        var response = await SendAsync(requestType, responseType, payload, cancellationToken);
        return response?.GetPayload<OperationResult<TResponse>>() ?? OperationResult<TResponse>.Fail("Không nhận được phản hồi từ máy chủ.", "NO_RESPONSE");
    }

    private Task<NetworkPacket?> SendAsync<T>(PacketType requestType, PacketType responseType, T payload, CancellationToken cancellationToken)
    {
        var senderId = payload is LoginRequestDto login && !string.IsNullOrWhiteSpace(login.Username)
            ? login.Username
            : CurrentUser?.UserId ?? "CLIENT";
        var request = NetworkPacket.Create(requestType, senderId, payload, correlationId: Guid.NewGuid().ToString("N"));
        return SendAndValidateAsync(request, responseType, cancellationToken);
    }

    private async Task<NetworkPacket?> SendAndValidateAsync(NetworkPacket request, PacketType responseType, CancellationToken cancellationToken)
    {
        var response = await _networkService.SendAndWaitAsync(request, cancellationToken: cancellationToken);
        if (response is null || response.Type != responseType) return null;
        return response;
    }

    private void OnCurrentUserChanged(UserDto? user) => CurrentUserChanged?.Invoke(this, user);
    private void OnRoomUpdated(RoomDto room) => RoomUpdated?.Invoke(this, room);
    private void OnRoomDeleted(string roomId) => RoomDeleted?.Invoke(this, roomId);

    public sealed record EmptyRequest;
    public sealed record IdRequest(string Id);
    public sealed record ProfileRequest(string UserId, string FullName, string Email, string? AvatarPath);
    public sealed record LockRequest(string UserId, string Reason);
    public sealed record RoleRequest(string UserId, UserRole Role);
    public sealed record RoomRequest(RoomDto Room, IReadOnlyList<string> InitialMemberIds);
    public sealed record MembersRequest(string Id, IReadOnlyList<string> UserIds);
    public sealed record ClassFilterRequest(string? FilterKeyword);
    public sealed record ClassRoomRequest(string ClassId, string RoomName);
}

