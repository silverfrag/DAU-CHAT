using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DauChat.Client.UI.Models;
using DauChat.Client.UI.ViewModels;

namespace DauChat.Client.UI.Views;

public partial class ChatWorkspaceView : UserControl
{
    private ChatWorkspaceViewModel? _boundVm;
    private bool _isNearBottom = true;

    public ChatWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_boundVm is not null)
        {
            _boundVm.ScrollToEndRequested -= OnScrollToEndRequested;
            _boundVm.ThreadMessageAppended -= OnThreadMessageAppended;
            _boundVm.FocusComposerRequested -= OnFocusComposerRequested;
        }

        _boundVm = DataContext as ChatWorkspaceViewModel;
        if (_boundVm is not null)
        {
            _boundVm.ScrollToEndRequested += OnScrollToEndRequested;
            _boundVm.ThreadMessageAppended += OnThreadMessageAppended;
            _boundVm.FocusComposerRequested += OnFocusComposerRequested;
        }
    }

    private void OnScrollToEndRequested()
    {
        Dispatcher.BeginInvoke(() =>
        {
            MessagesScrollViewer.ScrollToEnd();
            _isNearBottom = true;
            _boundVm?.ClearUnseenScrollIndicator();
        }, DispatcherPriority.Background);
    }

    private void OnThreadMessageAppended(bool isOwnMessage)
    {
        if (isOwnMessage || _isNearBottom)
        {
            OnScrollToEndRequested();
        }
        else
        {
            _boundVm?.NotifyUnseenScrollMessageArrived();
        }
    }

    private void OnFocusComposerRequested()
    {
        Dispatcher.BeginInvoke(() => ComposerTextBox.Focus(), DispatcherPriority.Background);
    }

    private void MessagesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        var distanceToBottom = sv.ScrollableHeight - sv.VerticalOffset;
        _isNearBottom = distanceToBottom <= 64;
        if (_isNearBottom)
        {
            _boundVm?.ClearUnseenScrollIndicator();
        }
    }

    private void ChatArea_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not ChatWorkspaceViewModel vm || !vm.CanSendMessages || vm.SelectedConversation is null)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropFilesOverlay.Visibility = Visibility.Visible;
            e.Handled = true;
        }
    }

    private void ChatArea_PreviewDragLeave(object sender, DragEventArgs e)
    {
        var pos = e.GetPosition(ChatColumnGrid);
        if (pos.X <= 2 || pos.Y <= 2 || pos.X >= ChatColumnGrid.ActualWidth - 2 || pos.Y >= ChatColumnGrid.ActualHeight - 2)
        {
            DropFilesOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void MessageRow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MessageItemModel msg }
            && !msg.CanReplyThisMessage
            && !msg.CanCopyThisMessage
            && !msg.CanPinThisMessage
            && !msg.CanRecallThisMessage
            && !msg.CanDeleteForMe)
        {
            e.Handled = true;
        }
    }

    private async void ChatArea_PreviewDrop(object sender, DragEventArgs e)
    {
        DropFilesOverlay.Visibility = Visibility.Collapsed;
        if (DataContext is not ChatWorkspaceViewModel vm || !vm.CanSendMessages || vm.SelectedConversation is null)
        {
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] files
            && files.Length > 0)
        {
            e.Handled = true;
            await vm.SendFilesFromPathsAsync(files);
        }
    }

    private async void ComposerTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is ChatWorkspaceViewModel escVm && escVm.HasReplyingTo)
        {
            e.Handled = true;
            escVm.CancelReplyCommand.Execute(null);
            return;
        }

        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (DataContext is ChatWorkspaceViewModel pasteVm && pasteVm.CanSendMessages && pasteVm.SelectedConversation is not null)
            {
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    if (img is not null)
                    {
                        e.Handled = true;
                        await pasteVm.SendClipboardImageAsync(img);
                        return;
                    }
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    var fileList = Clipboard.GetFileDropList();
                    if (fileList.Count > 0)
                    {
                        var paths = new string[fileList.Count];
                        fileList.CopyTo(paths, 0);
                        e.Handled = true;
                        await pasteVm.SendFilesFromPathsAsync(paths);
                        return;
                    }
                }
            }
        }

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
