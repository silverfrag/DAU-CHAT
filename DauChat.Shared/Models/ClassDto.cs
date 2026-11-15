namespace DauChat.Shared.Models;

/// <summary>
/// DTO thông tin lớp học / nhóm môn học (MOD_06_01 -> MOD_06_07).
/// </summary>
public sealed class ClassDto
{
    public string ClassId { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string? LecturerId { get; set; }
    public string? LecturerName { get; set; }
    public string? LinkedRoomId { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public bool AllowStudentJoinByCode { get; set; } = true;
    public int MemberCount { get; set; }
    public List<string> MemberIds { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Kết quả khi Sinh viên tham gia lớp học / phòng môn học bằng mã lớp (kiểu Google Classroom).
/// </summary>
public sealed class ClassJoinResultDto
{
    public ClassDto? JoinedClass { get; set; }
    public RoomDto? JoinedRoom { get; set; }
    public string Summary { get; set; } = string.Empty;
}

