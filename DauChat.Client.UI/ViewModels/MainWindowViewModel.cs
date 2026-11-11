using DauChat.Client.UI.Infrastructure;
using DauChat.Shared.Models;

namespace DauChat.Client.UI.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private bool _isAuthenticated;

    public MainWindowViewModel()
    {
        Login = new LoginViewModel();
        Shell = new MainShellViewModel(OnLoggedOut);

        Login.LoginSucceeded += OnLoginSucceeded;
    }

    public LoginViewModel Login { get; }
    public MainShellViewModel Shell { get; }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    private async void OnLoginSucceeded(UserDto user)
    {
        await Shell.InitializeForUserAsync(user);
        IsAuthenticated = true;
    }

    private void OnLoggedOut(string? reason)
    {
        IsAuthenticated = false;
        if (!Login.RememberUsername)
        {
            Login.Username = string.Empty;
        }
        Login.Password = string.Empty;
        Login.ErrorMessage = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason;
    }
}
