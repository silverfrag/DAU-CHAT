using System.Windows;
using DauChat.Client.UI.ViewModels;
using DauChat.Shared.Enums;

namespace DauChat.Client.UI;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Đã xảy ra lỗi giao diện: {args.Exception.Message}",
                "DAU Chat",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var mainWindow = new MainWindow();

        var posArg = Array.FindIndex(e.Args, a => a.Equals("--pos", StringComparison.OrdinalIgnoreCase));
        if (posArg >= 0 && posArg + 1 < e.Args.Length)
        {
            var parts = e.Args[posArg + 1].Split(',');
            if (parts.Length == 2
                && double.TryParse(parts[0], out var left)
                && double.TryParse(parts[1], out var top))
            {
                mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                mainWindow.Left = left;
                mainWindow.Top = top;
                mainWindow.Width = 1220;
                mainWindow.Height = 740;
            }
        }

        mainWindow.Show();

        var demoArg = Array.FindIndex(e.Args, a => a.Equals("--demo-user", StringComparison.OrdinalIgnoreCase));
        if (demoArg >= 0 && demoArg + 1 < e.Args.Length && mainWindow.DataContext is MainWindowViewModel vm)
        {
            var username = e.Args[demoArg + 1].Trim();
            vm.Login.Username = username;
            vm.Login.Password = "123456";
            vm.Login.LoginCommand.Execute(null);

            for (var i = 0; i < 40 && !vm.IsAuthenticated; i++)
            {
                await Task.Delay(50);
            }

            if (vm.IsAuthenticated && Services.ClientServiceHost.Current.AuthRoom.CurrentUser is { } user)
            {
                var roleLabel = user.Role switch
                {
                    UserRole.Admin => "Quản trị viên",
                    UserRole.GiangVien => "Giảng viên",
                    _ => "Sinh viên"
                };
                mainWindow.Title = $"DAU Chat • {roleLabel} ({user.Username})";
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.ClientServiceHost.Current.DemoAuthRoom.CleanupSessionOnExit();
        base.OnExit(e);
    }
}
