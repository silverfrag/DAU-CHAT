using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            _boundVm.PropertyChanged -= OnBoundVmPropertyChanged;
        }

        _boundVm = DataContext as ChatWorkspaceViewModel;
        if (_boundVm is not null)
        {
            _boundVm.ScrollToEndRequested += OnScrollToEndRequested;
            _boundVm.ThreadMessageAppended += OnThreadMessageAppended;
            _boundVm.FocusComposerRequested += OnFocusComposerRequested;
            _boundVm.PropertyChanged += OnBoundVmPropertyChanged;
        }
    }

    private void OnBoundVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatWorkspaceViewModel.SelectedConversation))
        {
            Dispatcher.BeginInvoke(PlayConversationSwitchMotion, DispatcherPriority.Render);
        }
    }

    private static readonly KeySpline ThreadSweepSpline = CreateFrozenSweepSpline();
    private int _threadAnimToken;

    private static KeySpline CreateFrozenSweepSpline()
    {
        var s = new KeySpline(0.16, 1.0, 0.30, 1.0);
        s.Freeze();
        return s;
    }

    private void PlayConversationSwitchMotion()
    {
        var token = ++_threadAnimToken;
        MessagesScrollViewer.CacheMode = new BitmapCache { EnableClearType = true };

        var opacityAnim = new DoubleAnimationUsingKeyFrames();
        opacityAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.18, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacityAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)), ThreadSweepSpline));
        MessagesScrollViewer.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);

        if (MessagesScrollViewer.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform(0, 0);
            MessagesScrollViewer.RenderTransform = translate;
        }

        var xAnim = new DoubleAnimationUsingKeyFrames();
        xAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(16, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        xAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), ThreadSweepSpline));
        xAnim.Completed += (_, _) =>
        {
            if (_threadAnimToken == token)
            {
                MessagesScrollViewer.CacheMode = null;
            }
        };
        translate.BeginAnimation(TranslateTransform.XProperty, xAnim, HandoffBehavior.SnapshotAndReplace);

        var yAnim = new DoubleAnimationUsingKeyFrames();
        yAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(5, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), ThreadSweepSpline));
        translate.BeginAnimation(TranslateTransform.YProperty, yAnim, HandoffBehavior.SnapshotAndReplace);
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
        if (sender is FrameworkElement fe)
        {
            if (fe.ContextMenu is { } menu)
            {
                menu.PlacementTarget = fe;
            }

            if (fe.DataContext is MessageItemModel msg
                && !msg.CanReplyThisMessage
                && !msg.CanCopyThisMessage
                && !msg.CanPinThisMessage
                && !msg.CanRecallThisMessage
                && !msg.CanDeleteForMe)
            {
                e.Handled = true;
            }
        }
    }

    private void ChatArea_PreviewDrop(object sender, DragEventArgs e)
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
            vm.StageFiles(files);
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
                        pasteVm.StageFiles(paths);
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
