using DauChat.Shared.Enums;
using DauChat.Shared.Models;

namespace DauChat.Shared.Interfaces;

/// <summary>
/// Hợp đồng tầng Tài khoản, Phân quyền RBAC, Phòng chat và Lớp học do TV3 (Nguyễn Thành Long) phụ trách.
/// Bao phủ Phân hệ 1 (MOD_01_01 -> MOD_01_13), Nhánh 4.1–4.2 (MOD_04_01 -> MOD_04_06) và Phân hệ 6 (MOD_06_01 -> MOD_06_07).
/// Lưu ý: Mọi giao tiếp mạng của TV3 thực hiện qua INetworkService (TV2); TV4 sử dụng Interface này để lấy thành viên phòng.
/// </summary>
public interface IAuthRoomService
{
    UserDto? CurrentUser { get; }

    #region Phân hệ 1.1 & 1.2: Xác thực & Hồ sơ cá nhân [ALL] (MOD_01_01 -> MOD_01_05)
    /// <summary>MOD_01_01: Đăng nhập, xác thực với Server Metadata và gắn kết nối Socket với UserId.</summary>
    Task<OperationResult<LoginResultDto>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_01_02: Đăng xuất, hủy phiên hiện tại và cập nhật trạng thái Offline.</summary>
    Task<OperationResult> LogoutAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>MOD_01_03: Đổi mật khẩu (kiểm tra mật khẩu cũ và cập nhật mật khẩu băm mới).</summary>
    Task<OperationResult> ChangePasswordAsync(
        string userId,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_01_04: Xem thông tin hồ sơ cá nhân từ Server Metadata.</summary>
    Task<OperationResult<UserDto>> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>MOD_01_05: Cập nhật thông tin hồ sơ cá nhân (Họ tên, Email, Ảnh đại diện).</summary>
    Task<OperationResult<UserDto>> UpdateUserProfileAsync(
        string userId,
        string fullName,
        string email,
        string? avatarPath = null,
        CancellationToken cancellationToken = default);
    #endregion

    #region Phân hệ 1.3 & 1.4: Quản trị tài khoản & Phân quyền RBAC [A] (MOD_01_06 -> MOD_01_13)
    /// <summary>Lấy danh sách tài khoản phục vụ Admin và danh bạ người dùng.</summary>
    Task<OperationResult<IReadOnlyList<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>MOD_01_06: Admin thêm tài khoản mới (Admin / Giảng viên / Sinh viên).</summary>
    Task<OperationResult<UserDto>> CreateUserAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_01_07: Admin chỉnh sửa thông tin tài khoản bất kỳ.</summary>
    Task<OperationResult<UserDto>> UpdateUserAsync(UserDto user, CancellationToken cancellationToken = default);

    /// <summary>MOD_01_08: Admin khóa tài khoản và ngắt phiên nếu đang Online.</summary>
    Task<OperationResult> LockUserAsync(
        string userId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_01_09: Admin mở khóa tài khoản.</summary>
    Task<OperationResult> UnlockUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>MOD_01_10: Admin cấp lại mật khẩu mặc định cho người dùng.</summary>
    Task<OperationResult<string>> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>MOD_01_11 -> MOD_01_13: Admin phân quyền Admin / Giảng viên / Sinh viên cho tài khoản.</summary>
    Task<OperationResult> AssignRoleAsync(
        string userId,
        UserRole role,
        CancellationToken cancellationToken = default);
    #endregion

    #region Phân hệ 4.1 & 4.2: Quản lý Phòng chat & Thành viên phòng [A][GV] (MOD_04_01 -> MOD_04_06)
    /// <summary>MOD_04_01: Tạo phòng chat mới trên Server Metadata.</summary>
    Task<OperationResult<RoomDto>> CreateRoomAsync(
        RoomDto room,
        IReadOnlyList<string>? initialMemberIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_02: Sửa tên hoặc mô tả phòng chat.</summary>
    Task<OperationResult<RoomDto>> UpdateRoomAsync(RoomDto room, CancellationToken cancellationToken = default);

    /// <summary>MOD_04_03: Xóa phòng chat và thông báo giải tán phòng.</summary>
    Task<OperationResult> DeleteRoomAsync(string roomId, CancellationToken cancellationToken = default);

    /// <summary>MOD_04_04: Lấy danh sách thành viên trong phòng (TV4 sử dụng để broadcast tin nhắn phòng).</summary>
    Task<OperationResult<IReadOnlyList<UserDto>>> GetRoomMembersAsync(
        string roomId,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_05: Thêm sinh viên/giảng viên vào phòng chat.</summary>
    Task<OperationResult> AddRoomMembersAsync(
        string roomId,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_04_06: Xóa thành viên khỏi phòng chat.</summary>
    Task<OperationResult> RemoveRoomMemberAsync(
        string roomId,
        string userId,
        CancellationToken cancellationToken = default);
    #endregion

    #region Phân hệ 6: Quản lý Lớp học [A][GV] (MOD_06_01 -> MOD_06_07)
    /// <summary>MOD_06_01: Xem danh sách lớp học / nhóm môn học.</summary>
    Task<OperationResult<IReadOnlyList<ClassDto>>> GetClassesAsync(
        string? filterKeyword = null,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_06_02: Admin thêm lớp học mới.</summary>
    Task<OperationResult<ClassDto>> CreateClassAsync(
        ClassDto classDto,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_06_03: Admin cập nhật thông tin lớp học / giảng viên phụ trách.</summary>
    Task<OperationResult<ClassDto>> UpdateClassAsync(
        ClassDto classDto,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_06_04: Admin xóa lớp học khỏi Metadata.</summary>
    Task<OperationResult> DeleteClassAsync(string classId, CancellationToken cancellationToken = default);

    /// <summary>MOD_06_05: Xem danh sách giảng viên và sinh viên thuộc lớp học.</summary>
    Task<OperationResult<IReadOnlyList<UserDto>>> GetClassMembersAsync(
        string classId,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_06_06: Thêm thành viên vào lớp học.</summary>
    Task<OperationResult> AddClassMembersAsync(
        string classId,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_06_06: Xóa thành viên khỏi lớp học.</summary>
    Task<OperationResult> RemoveClassMembersAsync(
        string classId,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_06_07: Tạo phòng chat gắn với lớp học và đồng bộ toàn bộ thành viên lớp vào phòng.</summary>
    Task<OperationResult<RoomDto>> CreateOrLinkClassRoomAsync(
        string classId,
        string roomName,
        CancellationToken cancellationToken = default);
    #endregion

    #region Sự kiện thay đổi Metadata cho UI
    event EventHandler<UserDto?>? CurrentUserChanged;
    event EventHandler<RoomDto>? RoomUpdated;
    event EventHandler<string>? RoomDeleted;
    #endregion
}

