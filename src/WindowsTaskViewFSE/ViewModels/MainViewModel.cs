using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using WindowsTaskViewFSE.Helpers;
using WindowsTaskViewFSE.Models;
using WindowsTaskViewFSE.Services;

namespace WindowsTaskViewFSE.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// Number of windows skipped by a single LB/RB (PageLeft/PageRight) press, so paging feels
    /// like a fast scrub through the carousel rather than moving one window at a time.
    /// </summary>
    private const int PageJumpSize = 3;

    private readonly IWindowManager _windowManager;
    private readonly IInputManager _inputManager;
    private readonly IControllerInputService _controllerService;
    private readonly IThumbnailProvider? _thumbnailProvider;
    private readonly DispatcherTimer _clockTimer;

    private WindowTileViewModel? _selectedWindow;
    private int _selectedIndex = -1;
    private string _searchQuery = string.Empty;
    private string _headerTime = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isControllerConnected;

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
                OnPropertyChanged(nameof(CenterTile));
                OnPropertyChanged(nameof(LeftTile));
                OnPropertyChanged(nameof(RightTile));
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

    /// <summary>
    /// The currently focused/active window, shown large in the center of the 3-window carousel.
    /// </summary>
    public WindowTileViewModel? CenterTile => SelectedWindow;

    /// <summary>
    /// The window shown to the left of center (previous window in the circular carousel).
    /// Null when there are fewer than 3 windows open (with exactly 2 windows, the other
    /// window is shown only on the right to avoid the same tile appearing on both sides).
    /// </summary>
    public WindowTileViewModel? LeftTile => FilteredWindows.Count >= 3 ? GetTileAtOffset(-1) : null;

    /// <summary>
    /// The window shown to the right of center (next window in the circular carousel).
    /// Null when there are fewer than 2 windows open. With exactly 2 windows open,
    /// GetTileAtOffset(-1) and GetTileAtOffset(1) both resolve to the same neighboring
    /// window, so it is only ever shown here (on the right) - <see cref="LeftTile"/> is
    /// suppressed in that case rather than binding the same instance to both slots.
    /// </summary>
    public WindowTileViewModel? RightTile => GetTileAtOffset(1);

    private WindowTileViewModel? GetTileAtOffset(int offset)
    {
        int count = FilteredWindows.Count;
        if (count <= 1 || SelectedIndex < 0) return null;

        int idx = ((SelectedIndex + offset) % count + count) % count;
        return FilteredWindows[idx];
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

    public int TotalWindowsCount => FilteredWindows.Count;

    // Commands
    public ICommand NavigateCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CloseCurrentCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand DismissCommand { get; }

    public MainViewModel(
        IWindowManager windowManager,
        IInputManager inputManager,
        IControllerInputService controllerService,
        IThumbnailProvider? thumbnailProvider = null)
    {
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
        _controllerService = controllerService ?? throw new ArgumentNullException(nameof(controllerService));
        _thumbnailProvider = thumbnailProvider;

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
        // Fire-and-forget: enumeration happens off the UI thread (see WindowManager.GetOpenWindowsAsync)
        // so the carousel stays responsive to input while windows/thumbnails are gathered.
        _ = RefreshWindowsAsync();
    }

    private async Task RefreshWindowsAsync()
    {
        IntPtr previousSelectedHwnd = SelectedWindow?.Handle ?? IntPtr.Zero;

        IReadOnlyList<WindowInfo> rawWindows;
        try
        {
            rawWindows = await _windowManager.GetOpenWindowsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Failed to enumerate windows: {ex.Message}");
            StatusMessage = "Unable to refresh open apps.";
            return;
        }

        Windows.Clear();

        int index = 0;
        foreach (var win in rawWindows)
        {
            var tile = new WindowTileViewModel(
                win,
                onSelect: tileVm => SelectTile(tileVm),
                onClose: tileVm => CloseTile(tileVm),
                thumbnailProvider: _thumbnailProvider)
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
        int next = current;

        switch (direction)
        {
            // The carousel is a single circular row of windows: Up/Left move to the previous
            // window, Down/Right move to the next one (matching D-Pad Left/Right and Up/Down).
            case NavigationDirection.Up:
            case NavigationDirection.Left:
                next = (current - 1 + count) % count;
                break;

            case NavigationDirection.Down:
            case NavigationDirection.Right:
                next = (current + 1) % count;
                break;

            case NavigationDirection.PageLeft:
                next = ((current - PageJumpSize) % count + count) % count;
                break;

            case NavigationDirection.PageRight:
                next = (current + PageJumpSize) % count;
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
                return;
        }

        if (next != current || SelectedWindow == null)
        {
            SelectedIndex = next;
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

        // The removal can shift which windows sit to the left/right of the current selection
        // even when SelectedWindow itself doesn't change, so always refresh the side tiles.
        OnPropertyChanged(nameof(LeftTile));
        OnPropertyChanged(nameof(RightTile));

        StatusMessage = $"{FilteredWindows.Count} app{(FilteredWindows.Count == 1 ? "" : "s")} running";
    }

    public void Dismiss()
    {
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
