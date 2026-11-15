using DauChat.Core.Messaging;
using DauChat.Shared.Interfaces;

namespace DauChat.SandboxTest;

internal sealed class SandboxTestContext(IMessagingService messagingService)
{
    public IMessagingService MessagingService { get; } = messagingService;

    public string TestUserId { get; } = $"sv_test_{Guid.NewGuid():N}";

    public string TestPartnerId { get; } = $"gv_test_{Guid.NewGuid():N}";

    public string TestDataRoot => Path.Combine("LocalData", TestUserId);

    public string ConversationId
    {
        get
        {
            var participantIds = new[] { TestUserId, TestPartnerId }
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            return $"dm_{participantIds[0]}_{participantIds[1]}";
        }
    }

    public void Cleanup()
    {
        if (Directory.Exists(TestDataRoot))
            Directory.Delete(TestDataRoot, recursive: true);
    }
}
