using DauChat.Shared.Enums;

namespace DauChat.Shared.Models;

/// <summary>
/// DTO thông tin người dùng (Users & Roles trên Server Metadata - MOD_01_01 -> MOD_01_13).
/// </summary>
public sealed class UserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public UserRole Role { get; set; } = UserRole.SinhVien;
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public PresenceStatus Presence { get; set; } = PresenceStatus.Offline;
    public string? ClassId { get; set; }
    public string? ClassName { get; set; }
    public string? LockReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
}

