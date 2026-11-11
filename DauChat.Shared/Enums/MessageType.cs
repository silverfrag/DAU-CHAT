namespace DauChat.Shared.Enums;

/// <summary>
/// Loại thông điệp trong hội thoại 1-1 và phòng chat (MOD_03_03, MOD_04_10, MOD_04_14, MOD_05_01).
/// </summary>
public enum MessageType
{
    Text = 1,
    FileAttachment = 2,
    PinnedAnnouncement = 3,
    SystemNotice = 4
}

