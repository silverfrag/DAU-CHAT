using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using DauChat.Client.UI.ViewModels;

namespace DauChat.Client.UI.Views;

public partial class ChatWorkspaceView : UserControl
{
    private ChatWorkspaceViewModel? _boundVm;

    public ChatWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_boundVm is not null)
        {
            _boundVm.VisibleMessages.CollectionChanged -= VisibleMessages_CollectionChanged;
        }

        _boundVm = DataContext as ChatWorkspaceViewModel;
        if (_boundVm is not null)
        {
            _boundVm.VisibleMessages.CollectionChanged += VisibleMessages_CollectionChanged;
        }
    }

    private void VisibleMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        MessagesScrollViewer.ScrollToEnd();
    }

    private void ComposerTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            if (DataContext is ChatWorkspaceViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
            }
        }
    }
}

