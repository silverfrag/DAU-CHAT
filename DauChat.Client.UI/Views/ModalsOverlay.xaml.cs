using System.Windows;
using System.Windows.Controls;
using DauChat.Client.UI.ViewModels;

namespace DauChat.Client.UI.Views;

public partial class ModalsOverlay : UserControl
{
    public ModalsOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainShellViewModel oldVm)
        {
            oldVm.PasswordFieldsResetRequested -= ClearPasswordInputs;
        }

        if (e.NewValue is MainShellViewModel newVm)
        {
            newVm.PasswordFieldsResetRequested += ClearPasswordInputs;
        }
    }

    private void ClearPasswordInputs()
    {
        OldPwdBox.Password = string.Empty;
        NewPwdBox.Password = string.Empty;
        ConfirmPwdBox.Password = string.Empty;
    }

    private void OldPwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm) vm.OldPassword = OldPwdBox.Password;
    }

    private void NewPwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm) vm.NewPassword = NewPwdBox.Password;
    }

    private void ConfirmPwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel vm) vm.ConfirmPassword = ConfirmPwdBox.Password;
    }
}
