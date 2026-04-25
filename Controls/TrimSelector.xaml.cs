using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace TORQUE.Controls;

public partial class TrimSelector : System.Windows.Controls.UserControl
{
    private const double MinimumSelectionRatio = 0.02d;
    private const double HandleGrabRadius = 18d;
    private const double PlayheadOverlapHideDistance = 8d;
    private InteractionZone _activeInteractionZone = InteractionZone.None;
    private MouseButton? _activeMouseButton;

    public static readonly DependencyProperty StartRatioProperty =
        DependencyProperty.Register(
            nameof(StartRatio),
            typeof(double),
            typeof(TrimSelector),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRatioPropertyChanged));

    public static readonly DependencyProperty EndRatioProperty =
        DependencyProperty.Register(
            nameof(EndRatio),
            typeof(double),
            typeof(TrimSelector),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRatioPropertyChanged));

    public static readonly DependencyProperty PlayheadRatioProperty =
        DependencyProperty.Register(
            nameof(PlayheadRatio),
            typeof(double),
            typeof(TrimSelector),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRatioPropertyChanged));

    public TrimSelector()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisuals();
    }

    public double StartRatio
    {
        get => (double)GetValue(StartRatioProperty);
        set => SetValue(StartRatioProperty, value);
    }

    public double EndRatio
    {
        get => (double)GetValue(EndRatioProperty);
        set => SetValue(EndRatioProperty, value);
    }

    public double PlayheadRatio
    {
        get => (double)GetValue(PlayheadRatioProperty);
        set => SetValue(PlayheadRatioProperty, value);
    }

    private static void OnRatioPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var selector = (TrimSelector)dependencyObject;
        selector.CoerceRatios();
        selector.UpdateVisuals();
    }

    private void LeftThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        StartRatio += e.HorizontalChange / ActualWidth;
        CoerceRatios();
        UpdateVisuals();
    }

    private void RightThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        EndRatio += e.HorizontalChange / ActualWidth;
        CoerceRatios();
        UpdateVisuals();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisuals();
    }

    private void RootGrid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        var mouseX = e.GetPosition(this).X;
        var controlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (controlPressed)
        {
            BeginInteraction(InteractionZone.Playhead, mouseX, MouseButton.Left);
            e.Handled = true;
            return;
        }

        if (IsThumbHit(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var zone = GetHandleInteractionZone(mouseX);
        if (zone == InteractionZone.None)
        {
            return;
        }

        BeginInteraction(zone, mouseX, MouseButton.Left);
        e.Handled = true;
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_activeInteractionZone == InteractionZone.None)
        {
            return;
        }

        if (!IsActiveButtonPressed(e))
        {
            ReleaseInteractionCapture();
            return;
        }

        UpdateInteraction(_activeInteractionZone, e.GetPosition(this).X);
        e.Handled = true;
    }

    private void RootGrid_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_activeInteractionZone == InteractionZone.None || _activeMouseButton != MouseButton.Left)
        {
            return;
        }

        UpdateInteraction(_activeInteractionZone, e.GetPosition(this).X);
        ReleaseInteractionCapture();
        e.Handled = true;
    }

    private void RootGrid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        BeginInteraction(InteractionZone.Playhead, e.GetPosition(this).X, MouseButton.Right);
        e.Handled = true;
    }

    private void RootGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_activeInteractionZone == InteractionZone.None || _activeMouseButton != MouseButton.Right)
        {
            return;
        }

        UpdateInteraction(_activeInteractionZone, e.GetPosition(this).X);
        ReleaseInteractionCapture();
        e.Handled = true;
    }

    private void RootGrid_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _activeInteractionZone = InteractionZone.None;
        _activeMouseButton = null;
    }

    private void CoerceRatios()
    {
        var safeStart = Math.Clamp(StartRatio, 0d, 1d);
        var safeEnd = Math.Clamp(EndRatio, 0d, 1d);

        if (safeEnd < safeStart + MinimumSelectionRatio)
        {
            if (safeEnd >= 1d)
            {
                safeStart = Math.Max(0d, 1d - MinimumSelectionRatio);
                safeEnd = 1d;
            }
            else
            {
                safeEnd = Math.Min(1d, safeStart + MinimumSelectionRatio);
            }
        }

        if (!safeStart.Equals(StartRatio))
        {
            SetCurrentValue(StartRatioProperty, safeStart);
        }

        if (!safeEnd.Equals(EndRatio))
        {
            SetCurrentValue(EndRatioProperty, safeEnd);
        }

        var safePlayhead = Math.Clamp(PlayheadRatio, safeStart, safeEnd);
        if (!safePlayhead.Equals(PlayheadRatio))
        {
            SetCurrentValue(PlayheadRatioProperty, safePlayhead);
        }
    }

    private void UpdateVisuals()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var width = ActualWidth;
        var left = width * StartRatio;
        var right = width * EndRatio;
        var selectionWidth = Math.Max(0d, right - left);

        LeftMask.Width = left;
        RightMask.Width = Math.Max(0d, width - right);
        Canvas.SetLeft(RightMask, right);

        SelectionSurface.Width = selectionWidth;
        Canvas.SetLeft(SelectionSurface, left);

        SelectionTopLine.Width = selectionWidth;
        Canvas.SetLeft(SelectionTopLine, left);
        Canvas.SetTop(SelectionTopLine, 0d);

        SelectionBottomLine.Width = selectionWidth;
        Canvas.SetLeft(SelectionBottomLine, left);
        Canvas.SetTop(SelectionBottomLine, Math.Max(0d, ActualHeight - SelectionBottomLine.Height));

        Canvas.SetLeft(LeftThumb, left - (LeftThumb.Width / 2d));
        Canvas.SetLeft(RightThumb, right - (RightThumb.Width / 2d));

        var playhead = width * PlayheadRatio;
        Canvas.SetLeft(PlayheadIndicator, Math.Max(0d, playhead - (PlayheadIndicator.Width / 2d)));
        PlayheadIndicator.Visibility =
            Math.Abs(playhead - left) <= PlayheadOverlapHideDistance ||
            Math.Abs(playhead - right) <= PlayheadOverlapHideDistance
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    private void UpdateInteraction(InteractionZone zone, double mouseX)
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        var ratio = Math.Clamp(mouseX / ActualWidth, 0d, 1d);

        switch (zone)
        {
            case InteractionZone.Left:
                StartRatio = ratio;
                break;
            case InteractionZone.Right:
                EndRatio = ratio;
                break;
            case InteractionZone.Playhead:
                PlayheadRatio = ratio;
                break;
            default:
                return;
        }

        CoerceRatios();
        UpdateVisuals();
    }

    private InteractionZone GetHandleInteractionZone(double mouseX)
    {
        if (ActualWidth <= 0)
        {
            return InteractionZone.None;
        }

        var left = ActualWidth * StartRatio;
        var right = ActualWidth * EndRatio;

        if (Math.Abs(mouseX - left) <= HandleGrabRadius)
        {
            return InteractionZone.Left;
        }

        if (Math.Abs(mouseX - right) <= HandleGrabRadius)
        {
            return InteractionZone.Right;
        }

        return InteractionZone.None;
    }

    private void BeginInteraction(InteractionZone zone, double mouseX, MouseButton mouseButton)
    {
        _activeInteractionZone = zone;
        _activeMouseButton = mouseButton;
        Mouse.Capture(this, CaptureMode.SubTree);
        UpdateInteraction(zone, mouseX);
    }

    private bool IsActiveButtonPressed(MouseEventArgs e)
    {
        return _activeMouseButton switch
        {
            MouseButton.Left => e.LeftButton == MouseButtonState.Pressed,
            MouseButton.Right => e.RightButton == MouseButtonState.Pressed,
            _ => false,
        };
    }

    private static bool IsThumbHit(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Thumb)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void ReleaseInteractionCapture()
    {
        if (Mouse.Captured == this)
        {
            Mouse.Capture(null);
        }

        _activeInteractionZone = InteractionZone.None;
        _activeMouseButton = null;
    }

    private enum InteractionZone
    {
        None,
        Left,
        Right,
        Playhead,
    }
}

