using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsTaskViewFSE.Helpers;
using WindowsTaskViewFSE.Models;

namespace WindowsTaskViewFSE.Services;

public class WindowManager : IWindowManager, IDisposable
{
    private readonly IThumbnailProvider _thumbnailProvider;
    private readonly DispatcherTimer _pollTimer;
    private readonly uint _currentProcessId;
    private IntPtr _winEventHook = IntPtr.Zero;
    private NativeInterop.WinEventDelegate? _winEventDelegate;
    private bool _isMonitoring;
    private readonly HashSet<IntPtr> _knownHandles = new();
    private readonly Dictionary<IntPtr, ImageSource?> _thumbnailCache = new();
    private readonly Dictionary<IntPtr, ImageSource?> _iconCache = new();
    private readonly HashSet<IntPtr> _dirtyThumbnailHandles = new();

    public event EventHandler? WindowsChanged;

    public WindowManager(IThumbnailProvider thumbnailProvider)
    {
        _thumbnailProvider = thumbnailProvider ?? throw new ArgumentNullException(nameof(thumbnailProvider));
        _currentProcessId = (uint)Process.GetCurrentProcess().Id;

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _pollTimer.Tick += OnPollTick;
    }

    public IReadOnlyList<WindowInfo> GetOpenWindows()
    {
        var windows = new List<WindowInfo>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return windows;
        }

        IntPtr foregroundHwnd = NativeInterop.GetForegroundWindow();
        int zOrder = 0;

        NativeInterop.EnumWindows((hWnd, lParam) =>
        {
            if (IsValidAppWindow(hWnd))
            {
                var info = CreateWindowInfo(hWnd, foregroundHwnd, zOrder++);
                if (info != null)
                {
                    windows.Add(info);
                }
            }
            return true;
        }, IntPtr.Zero);

        PruneStaleCacheEntries(windows.Select(w => w.Handle));

        return windows;
    }

    /// <summary>
    /// Enumerates open windows off the UI thread so the caller (e.g. the main view model)
    /// stays responsive while thumbnails are captured and process metadata is resolved.
    /// </summary>
    public Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync()
    {
        return Task.Run(GetOpenWindows);
    }

    public bool SwitchToWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NativeInterop.ForceForegroundWindow(handle);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowManager] SwitchToWindow error: {ex.Message}");
        }

        return false;
    }

    public bool CloseWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr result = NativeInterop.SendMessage(handle, NativeInterop.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                WindowsChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowManager] CloseWindow error: {ex.Message}");
        }

        return false;
    }

    public bool MinimizeWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return NativeInterop.ShowWindow(handle, NativeInterop.SW_MINIMIZE);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowManager] MinimizeWindow error: {ex.Message}");
        }

        return false;
    }

    public bool MaximizeWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return NativeInterop.ShowWindow(handle, NativeInterop.SW_SHOWMAXIMIZED);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowManager] MaximizeWindow error: {ex.Message}");
        }

        return false;
    }

    public void StartMonitoring()
    {
        if (_isMonitoring) return;
        _isMonitoring = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                _winEventDelegate = WinEventCallback;
                _winEventHook = NativeInterop.SetWinEventHook(
                    NativeInterop.EVENT_SYSTEM_FOREGROUND,
                    NativeInterop.EVENT_OBJECT_NAMECHANGE,
                    IntPtr.Zero,
                    _winEventDelegate,
                    0,
                    0,
                    NativeInterop.WINEVENT_OUTOFCONTEXT | NativeInterop.WINEVENT_SKIPOWNPROCESS);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowManager] SetWinEventHook error: {ex.Message}");
            }
        }

        _pollTimer.Start();
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring) return;
        _isMonitoring = false;

        _pollTimer.Stop();

        if (_winEventHook != IntPtr.Zero)
        {
            try
            {
                NativeInterop.UnhookWinEvent(_winEventHook);
            }
            catch { }
            _winEventHook = IntPtr.Zero;
        }
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != 0 || hwnd == IntPtr.Zero) return;

        lock (_dirtyThumbnailHandles)
        {
            _dirtyThumbnailHandles.Add(hwnd);
        }

        Application.Current?.Dispatcher?.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            WindowsChanged?.Invoke(this, EventArgs.Empty);
        }));
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        var currentWindows = GetOpenWindows();
        var currentHandles = new HashSet<IntPtr>(currentWindows.Select(w => w.Handle));

        if (!currentHandles.SetEquals(_knownHandles))
        {
            _knownHandles.Clear();
            foreach (var h in currentHandles) _knownHandles.Add(h);
            WindowsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool IsValidAppWindow(IntPtr hWnd)
    {
        if (!NativeInterop.IsWindowVisible(hWnd))
            return false;

        NativeInterop.GetWindowThreadProcessId(hWnd, out uint processId);
        if (processId == _currentProcessId)
            return false;

        int textLength = NativeInterop.GetWindowTextLength(hWnd);
        if (textLength == 0)
            return false;

        var sbClass = new StringBuilder(256);
        NativeInterop.GetClassName(hWnd, sbClass, sbClass.Capacity);
        string className = sbClass.ToString();

        if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
            or "Windows.UI.Core.CoreWindow" or "ApplicationFrameWindow")
        {
            if (className is "ApplicationFrameWindow")
            {
                // Windows Store apps wrapper: verify if cloaked or real title
                if (IsCloaked(hWnd)) return false;
            }
            else
            {
                return false;
            }
        }

        if (IsCloaked(hWnd))
            return false;

        long style = (long)NativeInterop.GetWindowLongPtr(hWnd, NativeInterop.GWL_STYLE);
        long exStyle = (long)NativeInterop.GetWindowLongPtr(hWnd, NativeInterop.GWL_EXSTYLE);

        if ((style & NativeInterop.WS_CHILD) != 0)
            return false;

        if ((exStyle & NativeInterop.WS_EX_TOOLWINDOW) != 0 && (exStyle & NativeInterop.WS_EX_APPWINDOW) == 0)
            return false;

        if (!NativeInterop.GetWindowRect(hWnd, out var rect))
            return false;

        if (rect.Width <= 0 || rect.Height <= 0)
            return false;

        return true;
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        int hr = NativeInterop.DwmGetWindowAttribute(hWnd, NativeInterop.DWMWA_CLOAKED, out int cloaked, sizeof(int));
        return hr == 0 && cloaked != 0;
    }

    private WindowInfo? CreateWindowInfo(IntPtr hWnd, IntPtr foregroundHwnd, int zOrder)
    {
        try
        {
            var sbTitle = new StringBuilder(512);
            NativeInterop.GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
            string title = sbTitle.ToString();

            NativeInterop.GetWindowThreadProcessId(hWnd, out uint processId);
            string processName = string.Empty;
            string executablePath = string.Empty;

            try
            {
                using var proc = Process.GetProcessById((int)processId);
                processName = proc.ProcessName;
                try
                {
                    executablePath = proc.MainModule?.FileName ?? string.Empty;
                }
                catch { }
            }
            catch { }

            NativeInterop.GetWindowRect(hWnd, out var rect);
            bool isMinimized = NativeInterop.IsIconic(hWnd);
            bool isMaximized = NativeInterop.IsZoomed(hWnd);
            bool isActive = hWnd == foregroundHwnd;

            var icon = GetOrCreateIcon(hWnd, executablePath);
            var thumbnail = GetOrCreateThumbnail(hWnd, isActive);

            return new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ProcessName = processName,
                ExecutablePath = executablePath,
                ProcessId = (int)processId,
                Icon = icon,
                ThumbnailBitmap = thumbnail,
                Bounds = new Rect(rect.Left, rect.Top, rect.Width, rect.Height),
                IsMinimized = isMinimized,
                IsMaximized = isMaximized,
                IsActive = isActive,
                ZOrder = zOrder,
                LastAccessed = isActive ? DateTime.UtcNow : DateTime.UtcNow.AddMinutes(-zOrder)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowManager] Error creating WindowInfo for {hWnd}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Returns the cached window icon if available, otherwise extracts and caches it.
    /// Icons rarely change for the lifetime of a window, so they are cached indefinitely per handle.
    /// </summary>
    private ImageSource? GetOrCreateIcon(IntPtr hWnd, string executablePath)
    {
        if (_iconCache.TryGetValue(hWnd, out var cachedIcon) && cachedIcon != null)
        {
            return cachedIcon;
        }

        var icon = _thumbnailProvider.ExtractWindowIcon(hWnd, executablePath);
        _iconCache[hWnd] = icon;
        return icon;
    }

    /// <summary>
    /// Returns the cached thumbnail for a window unless it is missing, dirty (window content changed),
    /// or currently the active/foreground window (which is refreshed on every poll to stay accurate).
    /// This avoids the costly PrintWindow/BitBlt capture for every window on every poll tick.
    /// </summary>
    private ImageSource? GetOrCreateThumbnail(IntPtr hWnd, bool isActive)
    {
        bool isDirty;
        lock (_dirtyThumbnailHandles)
        {
            isDirty = _dirtyThumbnailHandles.Remove(hWnd);
        }

        bool hasCached = _thumbnailCache.TryGetValue(hWnd, out var cachedThumbnail) && cachedThumbnail != null;

        if (hasCached && !isDirty && !isActive)
        {
            return cachedThumbnail;
        }

        var thumbnail = _thumbnailProvider.CaptureWindowThumbnail(hWnd, 480, 270);
        _thumbnailCache[hWnd] = thumbnail;
        return thumbnail;
    }

    private void PruneStaleCacheEntries(IEnumerable<IntPtr> currentHandles)
    {
        var current = new HashSet<IntPtr>(currentHandles);

        foreach (var stale in _thumbnailCache.Keys.Where(h => !current.Contains(h)).ToList())
        {
            _thumbnailCache.Remove(stale);
        }

        foreach (var stale in _iconCache.Keys.Where(h => !current.Contains(h)).ToList())
        {
            _iconCache.Remove(stale);
        }

        lock (_dirtyThumbnailHandles)
        {
            _dirtyThumbnailHandles.RemoveWhere(h => !current.Contains(h));
        }
    }

    public void Dispose()
    {
        StopMonitoring();
        _pollTimer.Tick -= OnPollTick;
    }
}
