using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WindowsTaskViewFSE.ViewModels;

namespace WindowsTaskViewFSE.Views;

public partial class WindowTile : UserControl
{
    private static readonly Duration AnimationDuration = new(TimeSpan.FromMilliseconds(180));
    private static readonly IEasingFunction AnimationEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };

    public WindowTile()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WindowTileViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is WindowTileViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateFocusState(newVm.IsFocused, animate: false);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is WindowTileViewModel vm)
        {
            if (e.PropertyName == nameof(WindowTileViewModel.IsFocused))
            {
                UpdateFocusState(vm.IsFocused, animate: true);
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
        double targetScale = isFocused ? 1.05 : 1.0;
        double targetShadowBlur = isFocused ? 24.0 : 0.0;
        double targetShadowOpacity = isFocused ? 0.85 : 0.0;
        double targetHintOpacity = isFocused ? 1.0 : 0.0;
        Color targetBorderColor = isFocused ? Color.FromRgb(16, 124, 16) : Color.FromRgb(56, 56, 56);
        double targetBorderThickness = isFocused ? 3.0 : 2.0;

        if (!animate)
        {
            TileScale.ScaleX = targetScale;
            TileScale.ScaleY = targetScale;
            TileShadow.BlurRadius = targetShadowBlur;
            TileShadow.Opacity = targetShadowOpacity;
            FocusHint.Opacity = targetHintOpacity;
            CardBorder.BorderBrush = new SolidColorBrush(targetBorderColor);
            CardBorder.BorderThickness = new Thickness(targetBorderThickness);
            return;
        }

        // Animate scale
        var scaleXAnim = new DoubleAnimation(targetScale, AnimationDuration) { EasingFunction = AnimationEase };
        var scaleYAnim = new DoubleAnimation(targetScale, AnimationDuration) { EasingFunction = AnimationEase };
        TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
        TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

        // Animate shadow
        var blurAnim = new DoubleAnimation(targetShadowBlur, AnimationDuration) { EasingFunction = AnimationEase };
        var opacityAnim = new DoubleAnimation(targetShadowOpacity, AnimationDuration) { EasingFunction = AnimationEase };
        TileShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, blurAnim);
        TileShadow.BeginAnimation(DropShadowEffect.OpacityProperty, opacityAnim);

        // Animate hint
        var hintAnim = new DoubleAnimation(targetHintOpacity, AnimationDuration) { EasingFunction = AnimationEase };
        FocusHint.BeginAnimation(OpacityProperty, hintAnim);

        // Animate border color
        var colorAnim = new ColorAnimation(targetBorderColor, AnimationDuration) { EasingFunction = AnimationEase };
        var brush = CardBorder.BorderBrush as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(56, 56, 56));
        CardBorder.BorderBrush = brush.IsFrozen ? brush.Clone() : brush;
        CardBorder.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);

        CardBorder.BorderThickness = new Thickness(targetBorderThickness);
    }

    private void AnimateHover(bool isHovered)
    {
        double targetScale = isHovered ? 1.02 : 1.0;
        var anim = new DoubleAnimation(targetScale, AnimationDuration) { EasingFunction = AnimationEase };
        TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

        Color targetBorderColor = isHovered ? Color.FromRgb(90, 90, 90) : Color.FromRgb(56, 56, 56);
        var colorAnim = new ColorAnimation(targetBorderColor, AnimationDuration) { EasingFunction = AnimationEase };
        var brush = CardBorder.BorderBrush as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(56, 56, 56));
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
