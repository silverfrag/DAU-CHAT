using System.Windows;
using DauChat.Client.UI.ViewModels;

namespace DauChat.Client.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
