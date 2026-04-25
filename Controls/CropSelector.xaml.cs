using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TORQUE.Controls;

public partial class CropSelector : System.Windows.Controls.UserControl
{
    private const double MinimumWidthRatio = 0.05d;
    private const double MinimumHeightRatio = 0.05d;
    private CropInteractionZone _activeInteractionZone = CropInteractionZone.None;

    public static readonly DependencyProperty LeftRatioProperty =
        DependencyProperty.Register(
            nameof(LeftRatio),
            typeof(double),
            typeof(CropSelector),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRatioPropertyChanged));

    public static readonly DependencyProperty TopRatioProperty =
        DependencyProperty.Register(
            nameof(TopRatio),
            typeof(double),
            typeof(CropSelector),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRatioPropertyChanged));

    public static readonly DependencyProperty RightRatioProperty =
        DependencyProperty.Register(
            nameof(RightRatio),
            typeof(double),
            typeof(CropSelector),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRatioPropertyChanged));

    public static readonly DependencyProperty BottomRatioProperty =
        DependencyProperty.Register(
            nameof(BottomRatio),
            typeof(double),
            typeof(CropSelector),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRatioPropertyChanged));

    public CropSelector()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisuals();
    }

    public double LeftRatio
    {
        get => (double)GetValue(LeftRatioProperty);
        set => SetValue(LeftRatioProperty, value);
    }

    public double TopRatio
    {
        get => (double)GetValue(TopRatioProperty);
        set => SetValue(TopRatioProperty, value);
    }

    public double RightRatio
    {
        get => (double)GetValue(RightRatioProperty);
        set => SetValue(RightRatioProperty, value);
    }

    public double BottomRatio
    {
        get => (double)GetValue(BottomRatioProperty);
        set => SetValue(BottomRatioProperty, value);
    }

    private static void OnRatioPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var selector = (CropSelector)dependencyObject;
        selector.CoerceRatios();
        selector.UpdateVisuals();
    }

    private void SelectionThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var widthRatio = RightRatio - LeftRatio;
        var heightRatio = BottomRatio - TopRatio;
        var horizontalDelta = e.HorizontalChange / ActualWidth;
        var verticalDelta = e.VerticalChange / ActualHeight;

        var nextLeft = Math.Clamp(LeftRatio + horizontalDelta, 0d, 1d - widthRatio);
        var nextTop = Math.Clamp(TopRatio + verticalDelta, 0d, 1d - heightRatio);

        SetCurrentValue(LeftRatioProperty, nextLeft);
        SetCurrentValue(RightRatioProperty, nextLeft + widthRatio);
        SetCurrentValue(TopRatioProperty, nextTop);
        SetCurrentValue(BottomRatioProperty, nextTop + heightRatio);
        UpdateVisuals();
    }

    private void LeftThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        LeftRatio += e.HorizontalChange / ActualWidth;
        CoerceRatios();
        UpdateVisuals();
    }

    private void RightThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        RightRatio += e.HorizontalChange / ActualWidth;
        CoerceRatios();
        UpdateVisuals();
    }

    private void TopThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualHeight <= 0)
        {
            return;
        }

        TopRatio += e.VerticalChange / ActualHeight;
        CoerceRatios();
        UpdateVisuals();
    }

    private void BottomThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualHeight <= 0)
        {
            return;
        }

        BottomRatio += e.VerticalChange / ActualHeight;
        CoerceRatios();
        UpdateVisuals();
    }

    private void TopLeftThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        LeftRatio += e.HorizontalChange / ActualWidth;
        TopRatio += e.VerticalChange / ActualHeight;
        CoerceRatios();
        UpdateVisuals();
    }

    private void TopRightThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        RightRatio += e.HorizontalChange / ActualWidth;
        TopRatio += e.VerticalChange / ActualHeight;
        CoerceRatios();
        UpdateVisuals();
    }

    private void BottomLeftThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        LeftRatio += e.HorizontalChange / ActualWidth;
        BottomRatio += e.VerticalChange / ActualHeight;
        CoerceRatios();
        UpdateVisuals();
    }

    private void BottomRightThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        RightRatio += e.HorizontalChange / ActualWidth;
        BottomRatio += e.VerticalChange / ActualHeight;
        CoerceRatios();
        UpdateVisuals();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisuals();
    }

    private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || IsThumbHit(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var zone = GetInteractionZone(e.GetPosition(this));
        if (zone == CropInteractionZone.None)
        {
            return;
        }

        _activeInteractionZone = zone;
        Mouse.Capture(this, CaptureMode.SubTree);
        UpdateInteraction(zone, e.GetPosition(this));
        e.Handled = true;
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_activeInteractionZone == CropInteractionZone.None)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            ReleaseInteractionCapture();
            return;
        }

        UpdateInteraction(_activeInteractionZone, e.GetPosition(this));
        e.Handled = true;
    }

    private void RootGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_activeInteractionZone == CropInteractionZone.None)
        {
            return;
        }

        UpdateInteraction(_activeInteractionZone, e.GetPosition(this));
        ReleaseInteractionCapture();
        e.Handled = true;
    }

    private void RootGrid_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _activeInteractionZone = CropInteractionZone.None;
    }

    private void CoerceRatios()
    {
        var safeLeft = Math.Clamp(LeftRatio, 0d, 1d);
        var safeTop = Math.Clamp(TopRatio, 0d, 1d);
        var safeRight = Math.Clamp(RightRatio, 0d, 1d);
        var safeBottom = Math.Clamp(BottomRatio, 0d, 1d);

        if (safeRight < safeLeft + MinimumWidthRatio)
        {
            if (safeRight >= 1d)
            {
                safeLeft = Math.Max(0d, 1d - MinimumWidthRatio);
                safeRight = 1d;
            }
            else
            {
                safeRight = Math.Min(1d, safeLeft + MinimumWidthRatio);
            }
        }

        if (safeBottom < safeTop + MinimumHeightRatio)
        {
            if (safeBottom >= 1d)
            {
                safeTop = Math.Max(0d, 1d - MinimumHeightRatio);
                safeBottom = 1d;
            }
            else
            {
                safeBottom = Math.Min(1d, safeTop + MinimumHeightRatio);
            }
        }

        if (!safeLeft.Equals(LeftRatio))
        {
            SetCurrentValue(LeftRatioProperty, safeLeft);
        }

        if (!safeTop.Equals(TopRatio))
        {
            SetCurrentValue(TopRatioProperty, safeTop);
        }

        if (!safeRight.Equals(RightRatio))
        {
            SetCurrentValue(RightRatioProperty, safeRight);
        }

        if (!safeBottom.Equals(BottomRatio))
        {
            SetCurrentValue(BottomRatioProperty, safeBottom);
        }
    }

    private void UpdateVisuals()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        var left = width * LeftRatio;
        var top = height * TopRatio;
        var right = width * RightRatio;
        var bottom = height * BottomRatio;
        var selectionWidth = Math.Max(0d, right - left);
        var selectionHeight = Math.Max(0d, bottom - top);

        TopMask.Width = width;
        TopMask.Height = top;
        Canvas.SetLeft(TopMask, 0d);
        Canvas.SetTop(TopMask, 0d);

        LeftMask.Width = left;
        LeftMask.Height = selectionHeight;
        Canvas.SetLeft(LeftMask, 0d);
        Canvas.SetTop(LeftMask, top);

        RightMask.Width = Math.Max(0d, width - right);
        RightMask.Height = selectionHeight;
        Canvas.SetLeft(RightMask, right);
        Canvas.SetTop(RightMask, top);

        BottomMask.Width = width;
        BottomMask.Height = Math.Max(0d, height - bottom);
        Canvas.SetLeft(BottomMask, 0d);
        Canvas.SetTop(BottomMask, bottom);

        SelectionThumb.Width = selectionWidth;
        SelectionThumb.Height = selectionHeight;
        Canvas.SetLeft(SelectionThumb, left);
        Canvas.SetTop(SelectionThumb, top);

        SelectionBorder.Width = selectionWidth;
        SelectionBorder.Height = selectionHeight;
        Canvas.SetLeft(SelectionBorder, left);
        Canvas.SetTop(SelectionBorder, top);

        LeftThumb.Height = Math.Max(24d, selectionHeight);
        Canvas.SetLeft(LeftThumb, left - (LeftThumb.Width / 2d));
        Canvas.SetTop(LeftThumb, top);

        RightThumb.Height = Math.Max(24d, selectionHeight);
        Canvas.SetLeft(RightThumb, right - (RightThumb.Width / 2d));
        Canvas.SetTop(RightThumb, top);

        TopThumb.Width = Math.Max(24d, selectionWidth);
        Canvas.SetLeft(TopThumb, left);
        Canvas.SetTop(TopThumb, top - (TopThumb.Height / 2d));

        BottomThumb.Width = Math.Max(24d, selectionWidth);
        Canvas.SetLeft(BottomThumb, left);
        Canvas.SetTop(BottomThumb, bottom - (BottomThumb.Height / 2d));

        Canvas.SetLeft(TopLeftThumb, left - (TopLeftThumb.Width / 2d));
        Canvas.SetTop(TopLeftThumb, top - (TopLeftThumb.Height / 2d));

        Canvas.SetLeft(TopRightThumb, right - (TopRightThumb.Width / 2d));
        Canvas.SetTop(TopRightThumb, top - (TopRightThumb.Height / 2d));

        Canvas.SetLeft(BottomLeftThumb, left - (BottomLeftThumb.Width / 2d));
        Canvas.SetTop(BottomLeftThumb, bottom - (BottomLeftThumb.Height / 2d));

        Canvas.SetLeft(BottomRightThumb, right - (BottomRightThumb.Width / 2d));
        Canvas.SetTop(BottomRightThumb, bottom - (BottomRightThumb.Height / 2d));
    }

    private void UpdateInteraction(CropInteractionZone zone, Point position)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var horizontalRatio = Math.Clamp(position.X / ActualWidth, 0d, 1d);
        var verticalRatio = Math.Clamp(position.Y / ActualHeight, 0d, 1d);

        switch (zone)
        {
            case CropInteractionZone.Left:
                LeftRatio = horizontalRatio;
                break;
            case CropInteractionZone.Right:
                RightRatio = horizontalRatio;
                break;
            case CropInteractionZone.Top:
                TopRatio = verticalRatio;
                break;
            case CropInteractionZone.Bottom:
                BottomRatio = verticalRatio;
                break;
            case CropInteractionZone.TopLeft:
                LeftRatio = horizontalRatio;
                TopRatio = verticalRatio;
                break;
            case CropInteractionZone.TopRight:
                RightRatio = horizontalRatio;
                TopRatio = verticalRatio;
                break;
            case CropInteractionZone.BottomLeft:
                LeftRatio = horizontalRatio;
                BottomRatio = verticalRatio;
                break;
            case CropInteractionZone.BottomRight:
                RightRatio = horizontalRatio;
                BottomRatio = verticalRatio;
                break;
            default:
                return;
        }

        CoerceRatios();
        UpdateVisuals();
    }

    private CropInteractionZone GetInteractionZone(Point position)
    {
        var left = ActualWidth * LeftRatio;
        var top = ActualHeight * TopRatio;
        var right = ActualWidth * RightRatio;
        var bottom = ActualHeight * BottomRatio;

        var isLeft = position.X < left;
        var isRight = position.X > right;
        var isTop = position.Y < top;
        var isBottom = position.Y > bottom;

        if (isLeft && isTop)
        {
            return CropInteractionZone.TopLeft;
        }

        if (isRight && isTop)
        {
            return CropInteractionZone.TopRight;
        }

        if (isLeft && isBottom)
        {
            return CropInteractionZone.BottomLeft;
        }

        if (isRight && isBottom)
        {
            return CropInteractionZone.BottomRight;
        }

        if (isLeft)
        {
            return CropInteractionZone.Left;
        }

        if (isRight)
        {
            return CropInteractionZone.Right;
        }

        if (isTop)
        {
            return CropInteractionZone.Top;
        }

        if (isBottom)
        {
            return CropInteractionZone.Bottom;
        }

        return CropInteractionZone.None;
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

        _activeInteractionZone = CropInteractionZone.None;
    }

    private enum CropInteractionZone
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }
}

