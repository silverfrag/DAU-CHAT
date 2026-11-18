using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DauChat.Client.UI.Infrastructure;
using DauChat.Client.UI.Models;
using DauChat.Client.UI.ViewModels;

namespace DauChat.Client.UI;

public partial class MainWindow : Window
{
    private static readonly KeySpline RailGlideSpline = CreateFrozenRailSpline();

    private static KeySpline CreateFrozenRailSpline()
    {
        var s = new KeySpline(0.16, 1.0, 0.30, 1.0);
        s.Freeze();
        return s;
    }

    private readonly MainWindowViewModel _vm;
    private NavSection? _previousSection;
    private bool _railIndicatorInitialized;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainWindowViewModel();
        DataContext = _vm;

        _vm.PropertyChanged += OnMainVmPropertyChanged;
        _vm.Shell.PropertyChanged += OnShellPropertyChanged;
        Loaded += (_, _) => ScheduleRailIndicatorSync(animate: false);
        SizeChanged += (_, _) => ScheduleRailIndicatorSync(animate: false);
    }

    private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsAuthenticated))
        {
            if (_vm.IsAuthenticated)
            {
                _railIndicatorInitialized = false;
                _previousSection = _vm.Shell.ActiveSection;
                ScheduleRailIndicatorSync(animate: false);
            }
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainShellViewModel.ActiveSection))
        {
            var next = _vm.Shell.ActiveSection;
            var direction = GetSectionDirection(_previousSection, next);
            _previousSection = next;

            ScheduleRailIndicatorSync(animate: true);
            PlaySpatialModuleTransition(next, direction);
        }
    }

    private static int GetSectionOrder(NavSection section) => section switch
    {
        NavSection.Admin => 0,
        NavSection.Messages => 1,
        NavSection.Rooms => 2,
        NavSection.Classes => 3,
        NavSection.Files => 4,
        NavSection.Storage => 5,
        _ => 1
    };

    private static int GetSectionDirection(NavSection? from, NavSection to)
    {
        if (!from.HasValue) return 0;
        return Math.Sign(GetSectionOrder(to) - GetSectionOrder(from.Value));
    }

    private RadioButton? GetNavButton(NavSection section) => section switch
    {
        NavSection.Admin => NavAdminBtn,
        NavSection.Messages => NavMessagesBtn,
        NavSection.Rooms => NavRoomsBtn,
        NavSection.Classes => NavClassesBtn,
        NavSection.Files => NavFilesBtn,
        NavSection.Storage => NavStorageBtn,
        _ => NavMessagesBtn
    };

    private FrameworkElement GetModuleView(NavSection section) => section switch
    {
        NavSection.Admin => AdminModuleView,
        NavSection.Classes => ClassesModuleView,
        NavSection.Files => FilesModuleView,
        NavSection.Storage => StorageModuleView,
        _ => ChatModuleView
    };

    private void PlaySpatialModuleTransition(NavSection section, int direction)
    {
        var view = GetModuleView(section);
        Dispatcher.BeginInvoke(() =>
        {
            if (view.Visibility == Visibility.Visible)
            {
                MotionBehavior.PlayDirectionalViewEntrance(view, direction);
            }
        }, DispatcherPriority.Render);
    }

    private void ScheduleRailIndicatorSync(bool animate)
    {
        Dispatcher.BeginInvoke(() => UpdateRailIndicator(animate), animate ? DispatcherPriority.Render : DispatcherPriority.Loaded);
    }

    private void UpdateRailIndicator(bool animate)
    {
        if (!_vm.IsAuthenticated) return;

        var targetBtn = GetNavButton(_vm.Shell.ActiveSection);
        if (targetBtn is null || targetBtn.Visibility != Visibility.Visible || targetBtn.ActualHeight <= 0)
        {
            return;
        }

        // Tính tọa độ Y chính xác của khuôn Icon 42x42 (offset +4px từ mép trên RadioButton)
        var relativeTop = targetBtn.TranslatePoint(new Point(0, 4), NavRailHost).Y;

        if (!animate || !_railIndicatorInitialized)
        {
            ActiveRailIndicatorTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            ActiveRailIndicatorTranslate.Y = relativeTop;
            ActiveRailIndicator.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(260)));
            _railIndicatorInitialized = true;
            return;
        }

        // Lướt nam châm mượt theo đường cong Expo-Out (0.16, 1, 0.3, 1) — phản hồi tức thì, không độ trễ
        var glideAnim = new DoubleAnimationUsingKeyFrames();
        glideAnim.KeyFrames.Add(new SplineDoubleKeyFrame(relativeTop, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)), RailGlideSpline));
        ActiveRailIndicatorTranslate.BeginAnimation(TranslateTransform.YProperty, glideAnim, HandoffBehavior.SnapshotAndReplace);

        var stretchY = new DoubleAnimationUsingKeyFrames();
        stretchY.KeyFrames.Add(new SplineDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90)), RailGlideSpline));
        stretchY.KeyFrames.Add(new SplineDoubleKeyFrame(1.00, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)), RailGlideSpline));
        ActiveRailIndicatorScale.BeginAnimation(ScaleTransform.ScaleYProperty, stretchY, HandoffBehavior.SnapshotAndReplace);

        var squeezeX = new DoubleAnimationUsingKeyFrames();
        squeezeX.KeyFrames.Add(new SplineDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90)), RailGlideSpline));
        squeezeX.KeyFrames.Add(new SplineDoubleKeyFrame(1.00, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)), RailGlideSpline));
        ActiveRailIndicatorScale.BeginAnimation(ScaleTransform.ScaleXProperty, squeezeX, HandoffBehavior.SnapshotAndReplace);
    }
}
