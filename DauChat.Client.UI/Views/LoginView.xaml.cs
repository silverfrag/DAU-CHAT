using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DauChat.Client.UI.ViewModels;

namespace DauChat.Client.UI.Views;

public partial class LoginView : UserControl
{
    private bool _isUpdatingPassword;

    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => SyncPasswordFromViewModel();
        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is true) SyncPasswordFromViewModel();
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel oldVm)
        {
            oldVm.QuickFillPasswordRequested -= OnQuickFillPasswordRequested;
        }

        if (e.NewValue is LoginViewModel newVm)
        {
            newVm.QuickFillPasswordRequested += OnQuickFillPasswordRequested;
            SyncPasswordFromViewModel();
        }
    }

    private void OnQuickFillPasswordRequested(string _, string password)
    {
        _isUpdatingPassword = true;
        LoginPasswordBox.Password = password;
        _isUpdatingPassword = false;
    }

    private void SyncPasswordFromViewModel()
    {
        if (DataContext is LoginViewModel vm && LoginPasswordBox.Password != vm.Password)
        {
            _isUpdatingPassword = true;
            LoginPasswordBox.Password = vm.Password;
            _isUpdatingPassword = false;
        }
    }

    private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPassword) return;
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = LoginPasswordBox.Password;
        }
    }

    private void LoginInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel vm && vm.LoginCommand.CanExecute(null))
        {
            vm.LoginCommand.Execute(null);
        }
    }
}

