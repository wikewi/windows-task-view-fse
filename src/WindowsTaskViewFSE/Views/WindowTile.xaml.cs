using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WindowsTaskViewFSE.ViewModels;

namespace WindowsTaskViewFSE.Views;

public partial class WindowTile : UserControl
{
    private static readonly Duration AnimationDuration = new(TimeSpan.FromMilliseconds(180));
    private static readonly IEasingFunction AnimationEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };

    /// <summary>
    /// Minimum change (in device-independent pixels) before the live preview's destination
    /// rectangle is re-sent to DWM, so continuous LayoutUpdated churn doesn't spam the compositor.
    /// </summary>
    private const double PreviewRectEpsilon = 0.5;

    private bool _isLoaded;
    private Rect _lastPreviewRect = Rect.Empty;

    public WindowTile()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        LayoutUpdated += OnLayoutUpdated;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        RefreshLivePreview();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _lastPreviewRect = Rect.Empty;
        (DataContext as WindowTileViewModel)?.DetachLivePreview();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        RefreshLivePreview();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WindowTileViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            oldVm.DetachLivePreview();
        }

        _lastPreviewRect = Rect.Empty;

        if (e.NewValue is WindowTileViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateFocusState(newVm.IsFocused, animate: false);
            BringIntoViewIfFocused(newVm.IsFocused);
            RefreshLivePreview();
        }
    }

    /// <summary>
    /// (Re)registers the live DWM preview for the current window and positions it over
    /// <see cref="PreviewSurface"/>. Called from Loaded/Unloaded, layout changes, and whenever
    /// the bound window changes, so the 3 visible carousel tiles always show live content.
    /// </summary>
    private void RefreshLivePreview()
    {
        if (!_isLoaded) return;
        if (DataContext is not WindowTileViewModel vm || vm.Handle == IntPtr.Zero) return;

        var hostWindow = Window.GetWindow(this);
        if (hostWindow == null) return;

        if (PresentationSource.FromVisual(hostWindow) is not HwndSource hwndSource) return;

        Point topLeft;
        try
        {
            topLeft = PreviewSurface.TranslatePoint(new Point(0, 0), hostWindow);
        }
        catch (InvalidOperationException)
        {
            // Not connected to a PresentationSource yet.
            return;
        }

        double width = PreviewSurface.ActualWidth;
        double height = PreviewSurface.ActualHeight;
        if (width <= 0 || height <= 0) return;

        // DwmUpdateThumbnailProperties expects the destination rectangle in physical pixels,
        // but WPF layout/TranslatePoint operate in device-independent units (DIPs). Convert
        // using the host window's composition transform so the preview is correctly sized
        // and positioned at any DPI scale factor.
        var toDevice = hwndSource.CompositionTarget.TransformToDevice;
        var topLeftDevice = toDevice.Transform(topLeft);
        var bottomRightDevice = toDevice.Transform(new Point(topLeft.X + width, topLeft.Y + height));

        var rect = new Rect(topLeftDevice, new Size(bottomRightDevice.X - topLeftDevice.X, bottomRightDevice.Y - topLeftDevice.Y));
        if (_lastPreviewRect.Equals(rect) ||
            (Math.Abs(_lastPreviewRect.X - rect.X) < PreviewRectEpsilon &&
             Math.Abs(_lastPreviewRect.Y - rect.Y) < PreviewRectEpsilon &&
             Math.Abs(_lastPreviewRect.Width - rect.Width) < PreviewRectEpsilon &&
             Math.Abs(_lastPreviewRect.Height - rect.Height) < PreviewRectEpsilon))
        {
            return;
        }

        _lastPreviewRect = rect;
        vm.AttachLivePreview(hwndSource.Handle, rect);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is WindowTileViewModel vm)
        {
            if (e.PropertyName == nameof(WindowTileViewModel.IsFocused))
            {
                UpdateFocusState(vm.IsFocused, animate: true);
                BringIntoViewIfFocused(vm.IsFocused);
            }
            else if (e.PropertyName == nameof(WindowTileViewModel.IsClosing))
            {
                if (vm.IsClosing)
                {
                    AnimateClose();
                }
            }
        }
    }

    private void BringIntoViewIfFocused(bool isFocused)
    {
        if (isFocused)
        {
            BringIntoView();
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (DataContext is WindowTileViewModel vm && !vm.IsFocused)
        {
            AnimateHover(true);
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is WindowTileViewModel vm && !vm.IsFocused)
        {
            AnimateHover(false);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowTileViewModel vm)
        {
            vm.SelectCommand.Execute(null);
        }
    }

    private void UpdateFocusState(bool isFocused, bool animate)
    {
        double targetScale = isFocused ? 1.0 : 0.8;
        double targetOpacity = isFocused ? 1.0 : 0.55;
        double targetHintOpacity = isFocused ? 1.0 : 0.0;
        Color targetBorderColor = isFocused ? Color.FromRgb(0x00, 0xD4, 0xFF) : Color.FromRgb(58, 58, 58);
        double targetBorderThickness = isFocused ? 4.0 : 2.0;

        if (!animate)
        {
            TileScale.ScaleX = targetScale;
            TileScale.ScaleY = targetScale;
            RootGrid.Opacity = targetOpacity;
            CloseButton.Opacity = targetHintOpacity;
            CardBorder.BorderBrush = new SolidColorBrush(targetBorderColor);
            CardBorder.BorderThickness = new Thickness(targetBorderThickness);
            return;
        }

        // Animate scale
        var scaleXAnim = new DoubleAnimation(targetScale, AnimationDuration) { EasingFunction = AnimationEase };
        var scaleYAnim = new DoubleAnimation(targetScale, AnimationDuration) { EasingFunction = AnimationEase };
        TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
        TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

        // Animate overall dimming for side (non-focused) tiles
        var opacityAnim = new DoubleAnimation(targetOpacity, AnimationDuration) { EasingFunction = AnimationEase };
        RootGrid.BeginAnimation(OpacityProperty, opacityAnim);

        // Animate close button visibility
        var hintAnim = new DoubleAnimation(targetHintOpacity, AnimationDuration) { EasingFunction = AnimationEase };
        CloseButton.BeginAnimation(OpacityProperty, hintAnim);

        // Animate border color
        var colorAnim = new ColorAnimation(targetBorderColor, AnimationDuration) { EasingFunction = AnimationEase };
        var brush = CardBorder.BorderBrush as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(58, 58, 58));
        CardBorder.BorderBrush = brush.IsFrozen ? brush.Clone() : brush;
        CardBorder.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);

        CardBorder.BorderThickness = new Thickness(targetBorderThickness);
    }

    private void AnimateHover(bool isHovered)
    {
        double targetScale = isHovered ? 0.86 : 0.8;
        var anim = new DoubleAnimation(targetScale, AnimationDuration) { EasingFunction = AnimationEase };
        TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

        Color targetBorderColor = isHovered ? Color.FromRgb(90, 90, 90) : Color.FromRgb(58, 58, 58);
        var colorAnim = new ColorAnimation(targetBorderColor, AnimationDuration) { EasingFunction = AnimationEase };
        var brush = CardBorder.BorderBrush as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(58, 58, 58));
        CardBorder.BorderBrush = brush.IsFrozen ? brush.Clone() : brush;
        CardBorder.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
    }

    private void AnimateClose()
    {
        var scaleAnim = new DoubleAnimation(0.7, TimeSpan.FromMilliseconds(150)) { EasingFunction = AnimationEase };
        var fadeAnim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(150)) { EasingFunction = AnimationEase };

        TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        RootGrid.BeginAnimation(OpacityProperty, fadeAnim);
    }
}
