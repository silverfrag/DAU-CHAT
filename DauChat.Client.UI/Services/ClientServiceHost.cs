using DauChat.Shared.Interfaces;

namespace DauChat.Client.UI.Services;

/// <summary>
/// Điểm lắp ghép 4 Interface chuẩn của DauChat.Shared cho tầng UI (TV1).
/// Mặc định dùng bộ DemoClientServices nội bộ (tài khoản &amp; dữ liệu mẫu đồ án) để chạy UI độc lập.
/// Khi tích hợp nhánh TV2–TV5, chỉ cần đổi sang dùng DauChat.Core.Networking / AuthAndRooms / Messaging / FileAndStorage.
/// </summary>
internal sealed class ClientServiceHost
{
    public static ClientServiceHost Current { get; } = new();

    private ClientServiceHost()
    {
        DemoStore = new DemoDataStore();
        DemoNetwork = new DemoNetworkService(DemoStore);
        DemoAuthRoom = new DemoAuthRoomService(DemoStore, DemoNetwork);
        DemoMessaging = new DemoMessagingService(DemoStore, () => DemoAuthRoom.CurrentUser);
        DemoFileStorage = new DemoFileStorageService(DemoStore);

        Network = DemoNetwork;
        AuthRoom = DemoAuthRoom;
        Messaging = DemoMessaging;
        FileStorage = DemoFileStorage;
    }

    public DemoDataStore DemoStore { get; }
    public DemoNetworkService DemoNetwork { get; }
    public DemoAuthRoomService DemoAuthRoom { get; }
    public DemoMessagingService DemoMessaging { get; }
    public DemoFileStorageService DemoFileStorage { get; }

    public INetworkService Network { get; }
    public IAuthRoomService AuthRoom { get; }
    public IMessagingService Messaging { get; }
    public IFileStorageService FileStorage { get; }

    public bool IsDemoMode => true;
}
