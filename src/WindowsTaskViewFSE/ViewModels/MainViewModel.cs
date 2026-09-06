using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using WindowsTaskViewFSE.Helpers;
using WindowsTaskViewFSE.Models;
using WindowsTaskViewFSE.Services;

namespace WindowsTaskViewFSE.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IWindowManager _windowManager;
    private readonly IInputManager _inputManager;
    private readonly ISoundService _soundService;
    private readonly IControllerInputService _controllerService;
    private readonly DispatcherTimer _clockTimer;

    private WindowTileViewModel? _selectedWindow;
    private int _selectedIndex = -1;
    private int _columnsCount = 3;
    private string _searchQuery = string.Empty;
    private string _headerTime = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isControllerConnected;
    private bool _isSoundMuted;

    public event EventHandler? RequestClose;

    public ObservableCollection<WindowTileViewModel> Windows { get; } = new();
    public ObservableCollection<WindowTileViewModel> FilteredWindows { get; } = new();

    public WindowTileViewModel? SelectedWindow
    {
        get => _selectedWindow;
        set
        {
            if (_selectedWindow != value)
            {
                if (_selectedWindow != null)
                {
                    _selectedWindow.IsFocused = false;
                }

                _selectedWindow = value;

                if (_selectedWindow != null)
                {
                    _selectedWindow.IsFocused = true;
                    _selectedIndex = FilteredWindows.IndexOf(_selectedWindow);
                }
                else
                {
                    _selectedIndex = -1;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedIndex));
                OnPropertyChanged(nameof(HasSelectedWindow));
            }
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value >= 0 && value < FilteredWindows.Count)
            {
                SelectedWindow = FilteredWindows[value];
            }
            else if (FilteredWindows.Count == 0)
            {
                SelectedWindow = null;
            }
        }
    }

    public bool HasSelectedWindow => SelectedWindow != null;

    public int ColumnsCount
    {
        get => _columnsCount;
        set => SetProperty(ref _columnsCount, Math.Max(1, value));
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

    public string HeaderTime
    {
        get => _headerTime;
        set => SetProperty(ref _headerTime, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsControllerConnected
    {
        get => _isControllerConnected;
        set => SetProperty(ref _isControllerConnected, value);
    }

    public bool IsSoundMuted
    {
        get => _isSoundMuted;
        set
        {
            if (SetProperty(ref _isSoundMuted, value))
            {
                _soundService.IsMuted = value;
            }
        }
    }

    public int TotalWindowsCount => FilteredWindows.Count;

    // Commands
    public ICommand NavigateCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CloseCurrentCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand DismissCommand { get; }
    public ICommand ToggleMuteCommand { get; }

    public MainViewModel(
        IWindowManager windowManager,
        IInputManager inputManager,
        ISoundService soundService,
        IControllerInputService controllerService)
    {
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _controllerService = controllerService ?? throw new ArgumentNullException(nameof(controllerService));

        NavigateCommand = new RelayCommand<object>(param =>
        {
            if (param is NavigationDirection dir)
            {
                Navigate(dir);
            }
            else if (param is string str && Enum.TryParse<NavigationDirection>(str, true, out var parsed))
            {
                Navigate(parsed);
            }
        });

        SelectCommand = new RelayCommand(SelectCurrent);
        CloseCurrentCommand = new RelayCommand(CloseSelected);
        RefreshCommand = new RelayCommand(RefreshWindows);
        DismissCommand = new RelayCommand(Dismiss);
        ToggleMuteCommand = new RelayCommand(() => IsSoundMuted = !IsSoundMuted);

        // Subscriptions
        _inputManager.NavigationRequested += OnNavigationRequested;
        _windowManager.WindowsChanged += OnWindowsChanged;
        _controllerService.ConnectionChanged += OnControllerConnectionChanged;
        IsControllerConnected = _controllerService.IsConnected;

        // Clock timer
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
    }

    public void Initialize()
    {
        _windowManager.StartMonitoring();
        _inputManager.Start();
        RefreshWindows();
    }

    public void RefreshWindows()
    {
        IntPtr previousSelectedHwnd = SelectedWindow?.Handle ?? IntPtr.Zero;

        var rawWindows = _windowManager.GetOpenWindows();
        Windows.Clear();

        int index = 0;
        foreach (var win in rawWindows)
        {
            var tile = new WindowTileViewModel(
                win,
                onSelect: tileVm => SelectTile(tileVm),
                onClose: tileVm => CloseTile(tileVm))
            {
                Index = index++
            };
            Windows.Add(tile);
        }

        ApplyFilter(previousSelectedHwnd);
        StatusMessage = $"{FilteredWindows.Count} app{(FilteredWindows.Count == 1 ? "" : "s")} running";
    }

    public void Navigate(NavigationDirection direction)
    {
        if (FilteredWindows.Count == 0) return;

        int current = SelectedIndex;
        if (current < 0) current = 0;

        int count = FilteredWindows.Count;
        int cols = ColumnsCount;
        int next = current;

        switch (direction)
        {
            case NavigationDirection.Left:
                next = (current - 1 + count) % count;
                break;

            case NavigationDirection.Right:
                next = (current + 1) % count;
                break;

            case NavigationDirection.Up:
                if (current - cols >= 0)
                {
                    next = current - cols;
                }
                else
                {
                    // Wrap to bottom in same column or last item
                    int target = current + (count / cols) * cols;
                    if (target >= count) target -= cols;
                    next = (target >= 0 && target < count) ? target : (count - 1);
                }
                break;

            case NavigationDirection.Down:
                if (current + cols < count)
                {
                    next = current + cols;
                }
                else
                {
                    // Wrap to top in same column
                    next = current % cols;
                    if (next >= count) next = 0;
                }
                break;

            case NavigationDirection.PageLeft:
                next = Math.Max(0, current - cols * 2);
                break;

            case NavigationDirection.PageRight:
                next = Math.Min(count - 1, current + cols * 2);
                break;

            case NavigationDirection.Select:
                SelectCurrent();
                return;

            case NavigationDirection.Back:
                Dismiss();
                return;

            case NavigationDirection.CloseApp:
                CloseSelected();
                return;

            case NavigationDirection.Refresh:
                RefreshWindows();
                _soundService.PlayNotification();
                return;
        }

        if (next != current || SelectedWindow == null)
        {
            SelectedIndex = next;
            _soundService.PlayNavigate();
            _controllerService.TriggerHapticFeedback(0, 15000, 15000, 40);
        }
    }

    public void SelectCurrent()
    {
        if (SelectedWindow == null) return;
        SelectTile(SelectedWindow);
    }

    public void SelectTile(WindowTileViewModel tile)
    {
        if (tile == null) return;
        _soundService.PlaySelect();
        _controllerService.TriggerHapticFeedback(0, 35000, 35000, 100);

        _windowManager.SwitchToWindow(tile.Handle);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public void CloseSelected()
    {
        if (SelectedWindow == null) return;
        CloseTile(SelectedWindow);
    }

    public void CloseTile(WindowTileViewModel tile)
    {
        if (tile == null) return;
        _soundService.PlayClose();
        _controllerService.TriggerHapticFeedback(0, 45000, 20000, 120);

        tile.IsClosing = true;
        _windowManager.CloseWindow(tile.Handle);

        int oldIndex = FilteredWindows.IndexOf(tile);
        Windows.Remove(tile);
        FilteredWindows.Remove(tile);
        OnPropertyChanged(nameof(TotalWindowsCount));

        if (FilteredWindows.Count > 0)
        {
            int nextIndex = Math.Min(oldIndex, FilteredWindows.Count - 1);
            SelectedIndex = Math.Max(0, nextIndex);
        }
        else
        {
            SelectedWindow = null;
        }

        StatusMessage = $"{FilteredWindows.Count} app{(FilteredWindows.Count == 1 ? "" : "s")} running";
    }

    public void Dismiss()
    {
        _soundService.PlayBack();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyFilter(IntPtr? preferSelectedHwnd = null)
    {
        FilteredWindows.Clear();

        var query = SearchQuery?.Trim() ?? string.Empty;
        var items = string.IsNullOrWhiteSpace(query)
            ? Windows
            : new ObservableCollection<WindowTileViewModel>(
                Windows.Where(w =>
                    w.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    w.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)));

        int idx = 0;
        foreach (var item in items)
        {
            item.Index = idx++;
            FilteredWindows.Add(item);
        }

        OnPropertyChanged(nameof(TotalWindowsCount));

        if (FilteredWindows.Count > 0)
        {
            WindowTileViewModel? target = null;
            if (preferSelectedHwnd.HasValue && preferSelectedHwnd.Value != IntPtr.Zero)
            {
                target = FilteredWindows.FirstOrDefault(w => w.Handle == preferSelectedHwnd.Value);
            }

            SelectedWindow = target ?? FilteredWindows[0];
        }
        else
        {
            SelectedWindow = null;
        }
    }

    private void OnNavigationRequested(object? sender, NavigationDirection direction)
    {
        Navigate(direction);
    }

    private void OnWindowsChanged(object? sender, EventArgs e)
    {
        RefreshWindows();
    }

    private void OnControllerConnectionChanged(object? sender, bool isConnected)
    {
        IsControllerConnected = isConnected;
        if (isConnected)
        {
            _soundService.PlayNotification();
        }
    }

    private void UpdateClock()
    {
        HeaderTime = DateTime.Now.ToString("h:mm tt");
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _inputManager.NavigationRequested -= OnNavigationRequested;
        _windowManager.WindowsChanged -= OnWindowsChanged;
        _controllerService.ConnectionChanged -= OnControllerConnectionChanged;
        _windowManager.StopMonitoring();
        _inputManager.Stop();
    }
}
