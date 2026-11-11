using System.Windows;
using DauChat.Core.AuthAndRooms;
using DauChat.Core.FileAndStorage;
using DauChat.Core.Messaging;
using DauChat.Core.Networking;
using DauChat.Shared.Interfaces;

namespace DauChat.Client.UI;

/// <summary>
/// Cửa sổ chính ứng dụng DAU Chat Client (TV1 - Leader phụ trách toàn bộ WPF UI).
/// </summary>
public partial class MainWindow : Window
{
    private readonly INetworkService _networkService;
    private readonly IAuthRoomService _authRoomService;
    private readonly IMessagingService _messagingService;
    private readonly IFileStorageService _fileStorageService;

    public MainWindow()
    {
        InitializeComponent();

        // Khởi tạo các service theo đúng hợp đồng DauChat.Shared (sau này TV1 có thể chuyển sang DI Container)
        _networkService = new NetworkService();
        _authRoomService = new AuthRoomService(_networkService);
        _messagingService = new MessagingService(_networkService, _authRoomService);
        _fileStorageService = new FileStorageService(_networkService);
    }
}
