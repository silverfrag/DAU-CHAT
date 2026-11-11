using DauChat.Shared.Enums;

namespace DauChat.Shared.Models;

/// <summary>
/// Tóm tắt một cuộc trò chuyện cá nhân 1-1 từ Local (MOD_03_01, MOD_03_02).
/// </summary>
public sealed class ConversationSummaryDto
{
    public string PartnerId { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string? PartnerAvatarPath { get; set; }
    public PresenceStatus PartnerPresence { get; set; } = PresenceStatus.Offline;
    public string LastMessagePreview { get; set; } = string.Empty;
    public DateTime? LastMessageAtUtc { get; set; }
    public int UnreadCount { get; set; }
}

