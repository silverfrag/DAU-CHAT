using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using DauChat.Client.UI.Infrastructure;
using DauChat.Client.UI.Models;
using DauChat.Client.UI.Services;
using DauChat.Shared.Constants;
using DauChat.Shared.Models;

namespace DauChat.Client.UI.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private string _username = "sv230001";
    private string _password = "123456";
    private string _serverHost = ProtocolConstants.DefaultServerHost;
    private string _serverPortText = ProtocolConstants.DefaultServerPort.ToString();
    private bool _showServerConfig;
    private bool _rememberUsername = true;
    private bool _isConnecting;
    private string _errorMessage = string.Empty;
    private string _selectedDemoUsername = "sv230001";

    public event Action<UserDto>? LoginSucceeded;
    public event Action<string, string>? QuickFillPasswordRequested;

    public LoginViewModel()
    {
        DemoAccounts = new ObservableCollection<DemoQuickAccountModel>
        {
            new()
            {
                Label = "Sinh viên (Chuẩn)",
                RoleTag = "Sinh viên",
                Username = "sv230001",
                Password = "123456",
                Note = "Bùi Trần Nhật Hưng • Ẩn tab Quản trị",
                BadgeBgHex = "#EFF6FF",
                BadgeFgHex = "#1D4ED8"
            },
            new()
            {
                Label = "Giảng viên",
                RoleTag = "Giảng viên",
                Username = "gv_demo01",
                Password = "123456",
                Note = "ThS. Nguyễn Văn Minh • Tạo phòng & Ghim",
                BadgeBgHex = "#F1F5F9",
                BadgeFgHex = "#1E293B"
            },
            new()
            {
                Label = "Quản trị viên",
                RoleTag = "Admin",
                Username = "admin",
                Password = "123456",
                Note = "Trần Quang Dũng • Toàn quyền hệ thống",
                BadgeBgHex = "#FEF2F2",
                BadgeFgHex = "#CB0914"
            },
            new()
            {
                Label = "SV Bị khóa (Test)",
                RoleTag = "Đã khóa",
                Username = "sv230003",
                Password = "123456",
                Note = "Kiểm thử từ chối đăng nhập",
                BadgeBgHex = "#FFFBEB",
                BadgeFgHex = "#B45309"
            }
        };

        ToggleServerConfigCommand = new RelayCommand(() => ShowServerConfig = !ShowServerConfig);
        SelectDemoAccountCommand = new RelayCommand(param =>
        {
            if (param is DemoQuickAccountModel demo)
            {
                Username = demo.Username;
                Password = demo.Password;
                SelectedDemoUsername = demo.Username;
                ErrorMessage = string.Empty;
                QuickFillPasswordRequested?.Invoke(demo.Username, demo.Password);
            }
        });
        LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, () => !IsConnecting);
        LaunchThreeRolesCommand = new AsyncRelayCommand(LaunchThreeRoleWindowsAsync, () => !IsConnecting);
    }

    public ObservableCollection<DemoQuickAccountModel> DemoAccounts { get; }

    public ICommand ToggleServerConfigCommand { get; }
    public ICommand SelectDemoAccountCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand LaunchThreeRolesCommand { get; }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    public string ServerHost
    {
        get => _serverHost;
        set => SetProperty(ref _serverHost, value);
    }

    public string ServerPortText
    {
        get => _serverPortText;
        set => SetProperty(ref _serverPortText, value);
    }

    public bool ShowServerConfig
    {
        get => _showServerConfig;
        set => SetProperty(ref _showServerConfig, value);
    }

    public bool RememberUsername
    {
        get => _rememberUsername;
        set => SetProperty(ref _rememberUsername, value);
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        set
        {
            if (SetProperty(ref _isConnecting, value))
            {
                OnPropertyChanged(nameof(LoginButtonText));
            }
        }
    }

    public string LoginButtonText => IsConnecting ? "Đang xác thực TCP..." : "Đăng nhập vào DAU Chat";

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string SelectedDemoUsername
    {
        get => _selectedDemoUsername;
        set => SetProperty(ref _selectedDemoUsername, value);
    }

    private async Task ExecuteLoginAsync()
    {
        ErrorMessage = string.Empty;

        if (!int.TryParse(ServerPortText.Trim(), out var port) || port is < 1 or > 65535)
        {
            ErrorMessage = "Cổng TCP Server phải nằm trong khoảng 1 – 65535 (mặc định 9050).";
            return;
        }

        IsConnecting = true;
        try
        {
            var host = ClientServiceHost.Current;
            var connRes = await host.Network.ConnectAsync(ServerHost.Trim(), port);
            if (!connRes.Success)
            {
                ErrorMessage = connRes.Message;
                return;
            }

            var authRes = await host.AuthRoom.LoginAsync(Username.Trim(), Password);
            if (!authRes.Success || authRes.Data is null)
            {
                await host.Network.DisconnectAsync();
                ErrorMessage = authRes.Message;
                return;
            }

            LoginSucceeded?.Invoke(authRes.Data.User);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private async Task LaunchThreeRoleWindowsAsync()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            Process.Start(new ProcessStartInfo(exePath, "--demo-user gv_demo01 --pos 150,54") { UseShellExecute = true });
            Process.Start(new ProcessStartInfo(exePath, "--demo-user admin --pos 276,90") { UseShellExecute = true });
        }

        if (Application.Current?.MainWindow is { } win)
        {
            win.WindowStartupLocation = WindowStartupLocation.Manual;
            win.Left = 24;
            win.Top = 18;
            win.Width = 1220;
            win.Height = 740;
            win.Title = "DAU Chat • Sinh viên (sv230001)";
        }

        Username = "sv230001";
        Password = "123456";
        QuickFillPasswordRequested?.Invoke("sv230001", "123456");
        await ExecuteLoginAsync();
    }
}

