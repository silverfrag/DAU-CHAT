using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DauChat.Client.UI.Infrastructure;

public enum MotionPreset
{
    None,
    FadeOnly,
    SlideUpFade,
    SlideDownFade,
    SlideFromRightFade,
    DrawerRight,
    ScaleFade,
    ModalCard,
    ModalBackdropFade,
    AuthCardEntrance,
    WorkspaceReveal,
    DrawLineX,
    LogoBloom,
    StaggerItem
}

/// <summary>
/// Hệ thống chuyển động cao cấp tối ưu GPU 60 FPS (Zero-Drop Compositing):
/// - Tự động bật GPU BitmapCache trong đúng thời gian animation diễn ra (di chuyển 1 texture quad thay vì re-layout hàng trăm control),
///   sau khi dừng sẽ tự giải phóng CacheMode để chữ ClearType sắc nét tuyệt đối.
/// - Tái sử dụng TransformGroup, đường cong Apple/Linear Expo-Out KeySpline(0.16, 1, 0.3, 1) và Enter/Exit mượt 2 chiều.
/// </summary>
public static class MotionBehavior
{
    private static readonly KeySpline ExpoOutSpline = CreateFrozenSpline(0.16, 1.0, 0.30, 1.0);
    private static readonly KeySpline SoftExitSpline = CreateFrozenSpline(0.40, 0.0, 0.85, 1.0);
    private static readonly BackEase SpringBackEase = CreateFrozenBackEase(0.22);

    private static KeySpline CreateFrozenSpline(double x1, double y1, double x2, double y2)
    {
        var spline = new KeySpline(x1, y1, x2, y2);
        spline.Freeze();
        return spline;
    }

    private static BackEase CreateFrozenBackEase(double amplitude)
    {
        var ease = new BackEase { Amplitude = amplitude, EasingMode = EasingMode.EaseOut };
        ease.Freeze();
        return ease;
    }

    public static readonly DependencyProperty PresetProperty =
        DependencyProperty.RegisterAttached(
            "Preset",
            typeof(MotionPreset),
            typeof(MotionBehavior),
            new PropertyMetadata(MotionPreset.None, OnPresetChanged));

    public static readonly DependencyProperty DurationMsProperty =
        DependencyProperty.RegisterAttached(
            "DurationMs",
            typeof(int),
            typeof(MotionBehavior),
            new PropertyMetadata(0));

    public static readonly DependencyProperty DelayMsProperty =
        DependencyProperty.RegisterAttached(
            "DelayMs",
            typeof(int),
            typeof(MotionBehavior),
            new PropertyMetadata(0));

    public static readonly DependencyProperty IsShownProperty =
        DependencyProperty.RegisterAttached(
            "IsShown",
            typeof(bool?),
            typeof(MotionBehavior),
            new PropertyMetadata(null, OnIsShownChanged));

    private static readonly DependencyProperty AnimationTokenProperty =
        DependencyProperty.RegisterAttached(
            "AnimationToken",
            typeof(int),
            typeof(MotionBehavior),
            new PropertyMetadata(0));

    private static readonly DependencyProperty OwnedBitmapCacheProperty =
        DependencyProperty.RegisterAttached(
            "OwnedBitmapCache",
            typeof(bool),
            typeof(MotionBehavior),
            new PropertyMetadata(false));

    public static MotionPreset GetPreset(DependencyObject obj) => (MotionPreset)obj.GetValue(PresetProperty);
    public static void SetPreset(DependencyObject obj, MotionPreset value) => obj.SetValue(PresetProperty, value);

    public static int GetDurationMs(DependencyObject obj) => (int)obj.GetValue(DurationMsProperty);
    public static void SetDurationMs(DependencyObject obj, int value) => obj.SetValue(DurationMsProperty, value);

    public static int GetDelayMs(DependencyObject obj) => (int)obj.GetValue(DelayMsProperty);
    public static void SetDelayMs(DependencyObject obj, int value) => obj.SetValue(DelayMsProperty, value);

    public static bool? GetIsShown(DependencyObject obj) => (bool?)obj.GetValue(IsShownProperty);
    public static void SetIsShown(DependencyObject obj, bool? value) => obj.SetValue(IsShownProperty, value);

    private static int NextToken(DependencyObject obj)
    {
        var next = (int)obj.GetValue(AnimationTokenProperty) + 1;
        obj.SetValue(AnimationTokenProperty, next);
        return next;
    }

    private static int CurrentToken(DependencyObject obj) => (int)obj.GetValue(AnimationTokenProperty);

    private static void EnableTemporaryGpuCache(FrameworkElement fe)
    {
        if (fe.CacheMode is null)
        {
            fe.CacheMode = new BitmapCache { EnableClearType = true };
            fe.SetValue(OwnedBitmapCacheProperty, true);
        }
    }

    private static void ReleaseTemporaryGpuCache(FrameworkElement fe)
    {
        if ((bool)fe.GetValue(OwnedBitmapCacheProperty))
        {
            fe.CacheMode = null;
            fe.SetValue(OwnedBitmapCacheProperty, false);
        }
    }

    private static (ScaleTransform Scale, TranslateTransform Translate) EnsureTransforms(FrameworkElement fe, Point origin)
    {
        fe.RenderTransformOrigin = origin;
        if (fe.RenderTransform is TransformGroup existing
            && existing.Children.Count == 2
            && existing.Children[0] is ScaleTransform s
            && existing.Children[1] is TranslateTransform t)
        {
            return (s, t);
        }

        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform(0, 0);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(translate);
        fe.RenderTransform = group;
        return (scale, translate);
    }

    private static void OnPresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;

        fe.Loaded -= OnElementLoaded;
        fe.IsVisibleChanged -= OnElementIsVisibleChanged;

        if ((MotionPreset)e.NewValue != MotionPreset.None)
        {
            fe.Loaded += OnElementLoaded;
            fe.IsVisibleChanged += OnElementIsVisibleChanged;
        }
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;

        var isShown = GetIsShown(fe);
        if (isShown.HasValue)
        {
            if (isShown.Value)
            {
                fe.Visibility = Visibility.Visible;
                PlayEntrance(fe);
            }
            else
            {
                fe.Visibility = Visibility.Collapsed;
                fe.Opacity = 0;
            }
            return;
        }

        if (fe.IsVisible)
        {
            PlayEntrance(fe);
        }
    }

    private static void OnElementIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (GetIsShown(fe).HasValue) return;

        if (e.NewValue is true)
        {
            PlayEntrance(fe);
        }
    }

    private static void OnIsShownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;
        if (e.NewValue is not bool show) return;

        if (!fe.IsLoaded)
        {
            fe.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            fe.Opacity = show ? 1 : 0;
            return;
        }

        if (show)
        {
            fe.Visibility = Visibility.Visible;
            fe.IsHitTestVisible = true;
            PlayEntrance(fe);
        }
        else
        {
            if (fe.Visibility != Visibility.Visible) return;
            fe.IsHitTestVisible = false;
            PlayExit(fe);
        }
    }

    public static void PlayDirectionalViewEntrance(FrameworkElement fe, int directionSign)
    {
        var fromY = directionSign == 0 ? 18d : (directionSign > 0 ? 22d : -22d);
        PlayCustomEntrance(fe, durationMs: 310, delayMs: 0, fromX: 0, fromY: fromY, fromScaleX: 0.988, fromScaleY: 0.988, origin: new Point(0.5, 0.5), useSpring: false, useGpuCache: true);
    }

    public static void PlayEntrance(FrameworkElement fe, MotionPreset? overridePreset = null)
    {
        var preset = overridePreset ?? GetPreset(fe);
        if (preset == MotionPreset.None) return;

        var customMs = GetDurationMs(fe);
        var delayMs = GetDelayMs(fe);

        switch (preset)
        {
            case MotionPreset.FadeOnly:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 220, delayMs, 0, 0, 1, 1, new Point(0.5, 0.5), false, false);
                break;

            case MotionPreset.ModalBackdropFade:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 200, delayMs, 0, 0, 1, 1, new Point(0.5, 0.5), false, false);
                break;

            case MotionPreset.SlideUpFade:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 300, delayMs, 0, 18, 0.99, 0.99, new Point(0.5, 0.5), false, true);
                break;

            case MotionPreset.SlideDownFade:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 250, delayMs, 0, -12, 0.992, 0.992, new Point(0.5, 0.0), false, true);
                break;

            case MotionPreset.SlideFromRightFade:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 300, delayMs, 34, 0, 0.985, 0.985, new Point(1.0, 0.5), false, true);
                break;

            case MotionPreset.DrawerRight:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 320, delayMs, 44, 0, 0.982, 0.992, new Point(1.0, 0.5), false, true);
                break;

            case MotionPreset.ScaleFade:
            case MotionPreset.ModalCard:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 300, delayMs, 0, 16, 0.945, 0.945, new Point(0.5, 0.5), useSpring: true, useGpuCache: true);
                break;

            case MotionPreset.AuthCardEntrance:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 420, delayMs, 0, 20, 0.965, 0.965, new Point(0.5, 0.5), false, true);
                break;

            case MotionPreset.WorkspaceReveal:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 360, delayMs, 0, 14, 0.98, 0.98, new Point(0.5, 0.5), false, true);
                break;

            case MotionPreset.DrawLineX:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 480, delayMs, 0, 0, 0.01, 1.0, new Point(0.0, 0.5), false, false);
                break;

            case MotionPreset.LogoBloom:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 460, delayMs, 0, 0, 0.86, 0.86, new Point(0.5, 0.5), useSpring: true, useGpuCache: false);
                break;

            case MotionPreset.StaggerItem:
                PlayCustomEntrance(fe, customMs > 0 ? customMs : 360, delayMs, -12, 10, 0.985, 0.985, new Point(0.0, 0.5), false, false);
                break;
        }
    }

    private static void PlayCustomEntrance(
        FrameworkElement fe,
        int durationMs,
        int delayMs,
        double fromX,
        double fromY,
        double fromScaleX,
        double fromScaleY,
        Point origin,
        bool useSpring,
        bool useGpuCache)
    {
        var token = NextToken(fe);
        var beginTime = TimeSpan.FromMilliseconds(delayMs);
        var duration = TimeSpan.FromMilliseconds(durationMs);

        if (useGpuCache)
        {
            EnableTemporaryGpuCache(fe);
        }

        if (delayMs > 0)
        {
            fe.BeginAnimation(UIElement.OpacityProperty, null);
            fe.Opacity = 0;
        }

        var opacityAnim = CreateSplineAnimation(0.0, 1.0, Math.Min(durationMs, 260), delayMs, ExpoOutSpline);
        var needsTransform = Math.Abs(fromX) > 0.01
            || Math.Abs(fromY) > 0.01
            || Math.Abs(fromScaleX - 1.0) > 0.001
            || Math.Abs(fromScaleY - 1.0) > 0.001;

        if (!needsTransform)
        {
            if (useGpuCache)
            {
                opacityAnim.Completed += (_, _) =>
                {
                    if (CurrentToken(fe) == token) ReleaseTemporaryGpuCache(fe);
                };
            }
            fe.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);
            return;
        }

        fe.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);

        var (scale, translate) = EnsureTransforms(fe, origin);
        Timeline? completionAnchor = null;

        if (Math.Abs(fromX) > 0.01)
        {
            var animX = CreateSplineAnimation(fromX, 0.0, durationMs, delayMs, ExpoOutSpline);
            completionAnchor = animX;
            translate.BeginAnimation(TranslateTransform.XProperty, animX, HandoffBehavior.SnapshotAndReplace);
        }
        else
        {
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = 0;
        }

        if (Math.Abs(fromY) > 0.01)
        {
            var animY = CreateSplineAnimation(fromY, 0.0, durationMs, delayMs, ExpoOutSpline);
            completionAnchor ??= animY;
            translate.BeginAnimation(TranslateTransform.YProperty, animY, HandoffBehavior.SnapshotAndReplace);
        }
        else
        {
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.Y = 0;
        }

        if (Math.Abs(fromScaleX - 1.0) > 0.001)
        {
            Timeline animSx = useSpring
                ? new DoubleAnimation
                {
                    From = fromScaleX,
                    To = 1.0,
                    BeginTime = beginTime,
                    Duration = duration,
                    EasingFunction = SpringBackEase
                }
                : CreateSplineAnimation(fromScaleX, 1.0, durationMs, delayMs, ExpoOutSpline);
            completionAnchor ??= animSx;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, (AnimationTimeline)animSx, HandoffBehavior.SnapshotAndReplace);
        }
        else
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.ScaleX = 1;
        }

        if (Math.Abs(fromScaleY - 1.0) > 0.001)
        {
            Timeline animSy = useSpring
                ? new DoubleAnimation
                {
                    From = fromScaleY,
                    To = 1.0,
                    BeginTime = beginTime,
                    Duration = duration,
                    EasingFunction = SpringBackEase
                }
                : CreateSplineAnimation(fromScaleY, 1.0, durationMs, delayMs, ExpoOutSpline);
            completionAnchor ??= animSy;
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, (AnimationTimeline)animSy, HandoffBehavior.SnapshotAndReplace);
        }
        else
        {
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleY = 1;
        }

        if (useGpuCache && completionAnchor is not null)
        {
            completionAnchor.Completed += (_, _) =>
            {
                if (CurrentToken(fe) == token) ReleaseTemporaryGpuCache(fe);
            };
        }
    }

    private static void PlayExit(FrameworkElement fe)
    {
        var token = NextToken(fe);
        var preset = GetPreset(fe);

        var (exitMs, toX, toY, toScale) = preset switch
        {
            MotionPreset.DrawerRight => (210, 40d, 0d, 0.985d),
            MotionPreset.SlideFromRightFade => (190, 30d, 0d, 0.985d),
            MotionPreset.SlideDownFade => (170, 0d, -10d, 0.992d),
            MotionPreset.SlideUpFade => (170, 0d, 10d, 0.992d),
            MotionPreset.ModalCard or MotionPreset.ScaleFade => (190, 0d, 12d, 0.955d),
            MotionPreset.ModalBackdropFade => (190, 0d, 0d, 1.0d),
            MotionPreset.AuthCardEntrance => (230, 0d, -12d, 0.97d),
            MotionPreset.WorkspaceReveal => (210, 0d, 10d, 0.982d),
            _ => (170, 0d, 0d, 1.0d)
        };

        var needsTransform = Math.Abs(toX) > 0.01 || Math.Abs(toY) > 0.01 || Math.Abs(toScale - 1.0) > 0.001;
        if (needsTransform)
        {
            EnableTemporaryGpuCache(fe);
        }

        var opacityAnim = CreateSplineAnimation(fe.Opacity, 0.0, exitMs, 0, SoftExitSpline);
        opacityAnim.Completed += (_, _) =>
        {
            if (CurrentToken(fe) == token && GetIsShown(fe) == false)
            {
                fe.Visibility = Visibility.Collapsed;
                ReleaseTemporaryGpuCache(fe);
            }
        };
        fe.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);

        if (!needsTransform) return;

        var origin = preset is MotionPreset.DrawerRight or MotionPreset.SlideFromRightFade
            ? new Point(1.0, 0.5)
            : new Point(0.5, 0.5);
        var (scale, translate) = EnsureTransforms(fe, origin);

        if (Math.Abs(toX) > 0.01)
        {
            translate.BeginAnimation(TranslateTransform.XProperty,
                CreateSplineAnimation(0, toX, exitMs, 0, SoftExitSpline), HandoffBehavior.SnapshotAndReplace);
        }
        if (Math.Abs(toY) > 0.01)
        {
            translate.BeginAnimation(TranslateTransform.YProperty,
                CreateSplineAnimation(0, toY, exitMs, 0, SoftExitSpline), HandoffBehavior.SnapshotAndReplace);
        }
        if (Math.Abs(toScale - 1.0) > 0.001)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                CreateSplineAnimation(1.0, toScale, exitMs, 0, SoftExitSpline), HandoffBehavior.SnapshotAndReplace);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                CreateSplineAnimation(1.0, toScale, exitMs, 0, SoftExitSpline), HandoffBehavior.SnapshotAndReplace);
        }
    }

    private static DoubleAnimationUsingKeyFrames CreateSplineAnimation(
        double from,
        double to,
        int durationMs,
        int delayMs,
        KeySpline spline)
    {
        var anim = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs)
        };
        anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)), spline));
        return anim;
    }
}
