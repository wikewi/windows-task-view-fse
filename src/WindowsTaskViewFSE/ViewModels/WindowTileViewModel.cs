using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WindowsTaskViewFSE.Helpers;
using WindowsTaskViewFSE.Models;
using WindowsTaskViewFSE.Services;

namespace WindowsTaskViewFSE.ViewModels;

public class WindowTileViewModel : ViewModelBase
{
    private readonly IThumbnailProvider? _thumbnailProvider;
    private WindowInfo _model;
    private bool _isFocused;
    private bool _isClosing;
    private int _index;
    private IntPtr _dwmThumbnailId = IntPtr.Zero;
    private IntPtr _dwmThumbnailDestinationHwnd = IntPtr.Zero;

    public WindowInfo Model
    {
        get => _model;
        set
        {
            if (SetProperty(ref _model, value))
            {
                OnPropertyChanged(nameof(Handle));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(ProcessName));
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(Thumbnail));
                OnPropertyChanged(nameof(IsMinimized));
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    public IntPtr Handle => _model.Handle;
    public string Title => _model.Title;
    public string DisplayTitle => _model.DisplayTitle;
    public string ProcessName => _model.ProcessName;
    public ImageSource? Icon => _model.Icon;
    public ImageSource? Thumbnail => _model.ThumbnailBitmap;
    public bool IsMinimized => _model.IsMinimized;
    public bool IsActive => _model.IsActive;

    public bool IsFocused
    {
        get => _isFocused;
        set => SetProperty(ref _isFocused, value);
    }

    public bool IsClosing
    {
        get => _isClosing;
        set => SetProperty(ref _isClosing, value);
    }

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public ICommand SelectCommand { get; }
    public ICommand CloseCommand { get; }

    /// <summary>
    /// True while a live DWM thumbnail is registered for this tile, so the view can hide the
    /// static fallback bitmap and let the compositor-rendered live preview show through instead.
    /// </summary>
    public bool HasLivePreview => _dwmThumbnailId != IntPtr.Zero;

    public WindowTileViewModel(
        WindowInfo model,
        Action<WindowTileViewModel>? onSelect = null,
        Action<WindowTileViewModel>? onClose = null,
        IThumbnailProvider? thumbnailProvider = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _thumbnailProvider = thumbnailProvider;

        SelectCommand = new RelayCommand(() => onSelect?.Invoke(this));
        CloseCommand = new RelayCommand(() => onClose?.Invoke(this));
    }

    /// <summary>
    /// Registers (if needed) and positions a live DWM window preview for this tile's window,
    /// rendered directly by the compositor into <paramref name="destRect"/> (in <paramref name="destinationHwnd"/>
    /// client coordinates). Cheap to call repeatedly - only issues a new registration once,
    /// unless <paramref name="destinationHwnd"/> changes (e.g. the tile is reparented to a
    /// different host window), in which case the thumbnail is re-registered against the new host.
    /// </summary>
    public void AttachLivePreview(IntPtr destinationHwnd, Rect destRect)
    {
        if (_thumbnailProvider == null || Handle == IntPtr.Zero || destinationHwnd == IntPtr.Zero) return;

        if (_dwmThumbnailId != IntPtr.Zero && _dwmThumbnailDestinationHwnd != destinationHwnd)
        {
            _thumbnailProvider.UnregisterDwmThumbnail(_dwmThumbnailId);
            _dwmThumbnailId = IntPtr.Zero;
        }

        if (_dwmThumbnailId == IntPtr.Zero)
        {
            _dwmThumbnailId = _thumbnailProvider.RegisterDwmThumbnail(destinationHwnd, Handle);
            if (_dwmThumbnailId != IntPtr.Zero)
            {
                _dwmThumbnailDestinationHwnd = destinationHwnd;
                OnPropertyChanged(nameof(HasLivePreview));
            }
        }

        if (_dwmThumbnailId != IntPtr.Zero)
        {
            _thumbnailProvider.UpdateDwmThumbnail(_dwmThumbnailId, destRect);
        }
    }

    /// <summary>
    /// Repositions an already-registered live preview (e.g. as the tile animates/resizes).
    /// No-op if a live preview hasn't been attached yet.
    /// </summary>
    public void UpdateLivePreviewRect(Rect destRect)
    {
        if (_dwmThumbnailId != IntPtr.Zero)
        {
            _thumbnailProvider?.UpdateDwmThumbnail(_dwmThumbnailId, destRect);
        }
    }

    /// <summary>
    /// Unregisters the live preview, e.g. when the tile is no longer one of the 3 visible
    /// carousel slots or the control is unloaded. Safe to call even if never attached.
    /// </summary>
    public void DetachLivePreview()
    {
        if (_dwmThumbnailId != IntPtr.Zero)
        {
            _thumbnailProvider?.UnregisterDwmThumbnail(_dwmThumbnailId);
            _dwmThumbnailId = IntPtr.Zero;
            _dwmThumbnailDestinationHwnd = IntPtr.Zero;
            OnPropertyChanged(nameof(HasLivePreview));
        }
    }
}
