using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;

namespace DauChat.Core.AuthAndRooms;

/// <summary>
/// Stub triển khai IAuthRoomService cho TV3 (Nguyễn Thành Long - nhánh feature/tv3-auth-room-class).
/// Chịu trách nhiệm Tài khoản, RBAC, Phòng chat, Lớp học và kết nối qua INetworkService (TV2).
/// </summary>
public class AuthRoomService : IAuthRoomService
{
    private readonly INetworkService _networkService;

    public AuthRoomService(INetworkService networkService)
    {
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
    }

    public UserDto? CurrentUser { get; protected set; }

    public event EventHandler<UserDto?>? CurrentUserChanged;
    public event EventHandler<RoomDto>? RoomUpdated;
    public event EventHandler<string>? RoomDeleted;

    public Task<OperationResult<LoginResultDto>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_01): Gửi LoginRequest qua INetworkService và xác thực Metadata.");

    public Task<OperationResult> LogoutAsync(string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_02): Gửi LogoutRequest và hủy phiên hiện tại.");

    public Task<OperationResult> ChangePasswordAsync(
        string userId,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_03): Gửi yêu cầu đổi mật khẩu.");

    public Task<OperationResult<UserDto>> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_04): Truy xuất hồ sơ người dùng từ Server Metadata.");

    public Task<OperationResult<UserDto>> UpdateUserProfileAsync(
        string userId,
        string fullName,
        string email,
        string? avatarPath = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_05): Cập nhật thông tin hồ sơ cá nhân.");

    public Task<OperationResult<IReadOnlyList<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_06..13): Lấy danh sách tài khoản từ Server Metadata.");

    public Task<OperationResult<UserDto>> CreateUserAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_06): Admin thêm tài khoản mới.");

    public Task<OperationResult<UserDto>> UpdateUserAsync(UserDto user, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_07): Admin sửa thông tin tài khoản.");

    public Task<OperationResult> LockUserAsync(string userId, string reason, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_08): Admin khóa tài khoản và ngắt phiên online.");

    public Task<OperationResult> UnlockUserAsync(string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_09): Admin mở khóa tài khoản.");

    public Task<OperationResult<string>> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_10): Admin cấp lại mật khẩu mặc định.");

    public Task<OperationResult> AssignRoleAsync(string userId, UserRole role, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_01_11/12/13): Admin phân quyền Admin/GiangVien/SinhVien.");

    public Task<OperationResult<RoomDto>> CreateRoomAsync(
        RoomDto room,
        IReadOnlyList<string>? initialMemberIds = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_04_01): Tạo phòng chat mới trên Server Metadata.");

    public Task<OperationResult<RoomDto>> UpdateRoomAsync(RoomDto room, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_04_02): Cập nhật thông tin phòng chat.");

    public Task<OperationResult> DeleteRoomAsync(string roomId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_04_03): Xóa phòng chat và thông báo giải tán.");

    public Task<OperationResult<IReadOnlyList<UserDto>>> GetRoomMembersAsync(
        string roomId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_04_04): Lấy danh sách thành viên phòng.");

    public Task<OperationResult> AddRoomMembersAsync(
        string roomId,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_04_05): Thêm thành viên vào phòng chat.");

    public Task<OperationResult> RemoveRoomMemberAsync(
        string roomId,
        string userId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_04_06): Xóa thành viên khỏi phòng chat.");

    public Task<OperationResult<IReadOnlyList<ClassDto>>> GetClassesAsync(
        string? filterKeyword = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_06_01): Xem danh sách lớp học.");

    public Task<OperationResult<ClassDto>> CreateClassAsync(
        ClassDto classDto,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_06_02): Admin thêm lớp học mới.");

    public Task<OperationResult<ClassDto>> UpdateClassAsync(
        ClassDto classDto,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_06_03): Admin cập nhật lớp học.");

    public Task<OperationResult> DeleteClassAsync(string classId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_06_04): Admin xóa lớp học.");

    public Task<OperationResult<IReadOnlyList<UserDto>>> GetClassMembersAsync(
        string classId,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_06_05): Xem thành viên của lớp học.");

    public Task<OperationResult> AddClassMembersAsync(
        string classId,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_06_06): Thêm thành viên vào lớp học.");

    public Task<OperationResult> RemoveClassMembersAsync(
        string classId,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_06_06): Xóa thành viên khỏi lớp học.");

    public Task<OperationResult<RoomDto>> CreateOrLinkClassRoomAsync(
        string classId,
        string roomName,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("TODO (TV3 - MOD_06_07): Tạo/liên kết phòng chat theo lớp học.");

    protected virtual void OnCurrentUserChanged(UserDto? user)
        => CurrentUserChanged?.Invoke(this, user);

    protected virtual void OnRoomUpdated(RoomDto room)
        => RoomUpdated?.Invoke(this, room);

    protected virtual void OnRoomDeleted(string roomId)
        => RoomDeleted?.Invoke(this, roomId);
}

