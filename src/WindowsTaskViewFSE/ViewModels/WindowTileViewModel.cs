using System.Windows.Input;
using System.Windows.Media;
using WindowsTaskViewFSE.Helpers;
using WindowsTaskViewFSE.Models;

namespace WindowsTaskViewFSE.ViewModels;

public class WindowTileViewModel : ViewModelBase
{
    private WindowInfo _model;
    private bool _isFocused;
    private bool _isClosing;
    private int _index;

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

    public WindowTileViewModel(
        WindowInfo model,
        Action<WindowTileViewModel>? onSelect = null,
        Action<WindowTileViewModel>? onClose = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));

        SelectCommand = new RelayCommand(() => onSelect?.Invoke(this));
        CloseCommand = new RelayCommand(() => onClose?.Invoke(this));
    }
}
