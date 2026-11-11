using DauChat.Shared.Enums;

namespace DauChat.Shared.Models;

/// <summary>
/// Payload yêu cầu đăng nhập (MOD_01_01).
/// </summary>
public sealed class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả đăng nhập kèm hồ sơ và vai trò người dùng (MOD_01_01).
/// </summary>
public sealed class LoginResultDto
{
    public UserDto User { get; set; } = new();
    public string SessionToken { get; set; } = string.Empty;
    public DateTime LoggedInAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Payload đổi mật khẩu (MOD_01_03).
/// </summary>
public sealed class ChangePasswordRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Payload Admin tạo tài khoản mới (MOD_01_06).
/// </summary>
public sealed class CreateUserRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string InitialPassword { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.SinhVien;
    public string? ClassId { get; set; }
}

