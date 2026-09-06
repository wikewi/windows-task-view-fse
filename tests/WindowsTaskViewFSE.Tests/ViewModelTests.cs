using System.Collections.ObjectModel;
using System.Windows.Input;
using WindowsTaskViewFSE.Models;
using WindowsTaskViewFSE.Services;
using WindowsTaskViewFSE.ViewModels;
using Xunit;

namespace WindowsTaskViewFSE.Tests;

public class MockWindowManager : IWindowManager
{
    public List<WindowInfo> Windows { get; set; } = new();
    public IntPtr LastSwitchedHandle { get; private set; }
    public IntPtr LastClosedHandle { get; private set; }
    public bool IsMonitoringStarted { get; private set; }
    public bool IsMonitoringStopped { get; private set; }

    public event EventHandler? WindowsChanged;

    public IReadOnlyList<WindowInfo> GetOpenWindows() => Windows.ToList();

    public bool SwitchToWindow(IntPtr handle)
    {
        LastSwitchedHandle = handle;
        return true;
    }

    public bool CloseWindow(IntPtr handle)
    {
        LastClosedHandle = handle;
        var found = Windows.FirstOrDefault(w => w.Handle == handle);
        if (found != null)
        {
            Windows.Remove(found);
            WindowsChanged?.Invoke(this, EventArgs.Empty);
        }
        return true;
    }

    public bool MinimizeWindow(IntPtr handle) => true;
    public bool MaximizeWindow(IntPtr handle) => true;

    public void StartMonitoring() => IsMonitoringStarted = true;
    public void StopMonitoring() => IsMonitoringStopped = true;

    public void TriggerWindowsChanged() => WindowsChanged?.Invoke(this, EventArgs.Empty);
}

public class MockInputManager : IInputManager
{
    public event EventHandler<NavigationDirection>? NavigationRequested;
    public bool IsStarted { get; private set; }
    public bool IsStopped { get; private set; }

    public void HandleKeyDown(KeyEventArgs e) { }
    public void Start() => IsStarted = true;
    public void Stop() => IsStopped = true;

    public void TriggerNavigation(NavigationDirection direction)
    {
        NavigationRequested?.Invoke(this, direction);
    }
}

public class MockSoundService : ISoundService
{
    public bool IsMuted { get; set; }
    public int NavCount { get; private set; }
    public int SelectCount { get; private set; }
    public int BackCount { get; private set; }
    public int CloseCount { get; private set; }
    public int NotifCount { get; private set; }

    public void PlayNavigate() => NavCount++;
    public void PlaySelect() => SelectCount++;
    public void PlayBack() => BackCount++;
    public void PlayClose() => CloseCount++;
    public void PlayNotification() => NotifCount++;
}

public class MockControllerService : IControllerInputService
{
    public bool IsConnected { get; set; } = true;
    public event EventHandler<ControllerButtonEventArgs>? ButtonDown;
    public event EventHandler<ControllerButtonEventArgs>? ButtonUp;
    public event EventHandler<ControllerNavigationEventArgs>? NavigationRequested;
    public event EventHandler<bool>? ConnectionChanged;

    public bool IsStarted { get; private set; }
    public bool IsStopped { get; private set; }
    public int HapticFeedbackCount { get; private set; }

    public void Start() => IsStarted = true;
    public void Stop() => IsStopped = true;

    public void TriggerHapticFeedback(int userIndex = 0, ushort leftMotor = 32000, ushort rightMotor = 32000, int durationMs = 120)
    {
        HapticFeedbackCount++;
    }

    public void SetConnected(bool connected)
    {
        IsConnected = connected;
        ConnectionChanged?.Invoke(this, connected);
    }

    public void TriggerNav(NavigationDirection dir)
    {
        NavigationRequested?.Invoke(this, new ControllerNavigationEventArgs(dir));
    }

    public void TriggerButtonDown(ControllerButton button, int userIndex = 0)
    {
        ButtonDown?.Invoke(this, new ControllerButtonEventArgs(button, userIndex));
    }

    public void TriggerButtonUp(ControllerButton button, int userIndex = 0)
    {
        ButtonUp?.Invoke(this, new ControllerButtonEventArgs(button, userIndex));
    }
}

public class ViewModelTests
{
    private static List<WindowInfo> CreateSampleWindows(int count = 6)
    {
        var list = new List<WindowInfo>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new WindowInfo
            {
                Handle = new IntPtr(0x1000 + i),
                Title = $"Application {i + 1}",
                ProcessName = $"app{i + 1}",
                ProcessId = 1000 + i,
                ZOrder = i
            });
        }
        return list;
    }

    [Fact]
    public void WindowTileViewModel_MapsProperties_Correctly()
    {
        var model = new WindowInfo
        {
            Handle = new IntPtr(123),
            Title = "Game Title",
            ProcessName = "GameProcess",
            IsMinimized = true
        };

        bool selectCalled = false;
        bool closeCalled = false;

        var vm = new WindowTileViewModel(
            model,
            onSelect: t => selectCalled = true,
            onClose: t => closeCalled = true);

        Assert.Equal(new IntPtr(123), vm.Handle);
        Assert.Equal("Game Title", vm.Title);
        Assert.Equal("Game Title", vm.DisplayTitle);
        Assert.Equal("GameProcess", vm.ProcessName);
        Assert.True(vm.IsMinimized);
        Assert.False(vm.IsFocused);

        vm.SelectCommand.Execute(null);
        Assert.True(selectCalled);

        vm.CloseCommand.Execute(null);
        Assert.True(closeCalled);
    }

    [Fact]
    public void MainViewModel_Initialize_PopulatesWindowsAndSelectsFirst()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(4) };
        var mockIm = new MockInputManager();
        var mockSound = new MockSoundService();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockSound, mockCtrl);
        mainVm.Initialize();

        Assert.True(mockWm.IsMonitoringStarted);
        Assert.True(mockIm.IsStarted);
        Assert.Equal(4, mainVm.TotalWindowsCount);
        Assert.NotNull(mainVm.SelectedWindow);
        Assert.Equal(0, mainVm.SelectedIndex);
        Assert.True(mainVm.SelectedWindow!.IsFocused);
    }

    [Fact]
    public void MainViewModel_NavigateRightAndLeft_UpdatesSelection()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(4) };
        var mockIm = new MockInputManager();
        var mockSound = new MockSoundService();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockSound, mockCtrl) { ColumnsCount = 2 };
        mainVm.Initialize();

        Assert.Equal(0, mainVm.SelectedIndex);

        // Move Right
        mainVm.Navigate(NavigationDirection.Right);
        Assert.Equal(1, mainVm.SelectedIndex);
        Assert.Equal(1, mockSound.NavCount);

        // Move Right again
        mainVm.Navigate(NavigationDirection.Right);
        Assert.Equal(2, mainVm.SelectedIndex);

        // Move Left
        mainVm.Navigate(NavigationDirection.Left);
        Assert.Equal(1, mainVm.SelectedIndex);

        // Move Left to 0
        mainVm.Navigate(NavigationDirection.Left);
        Assert.Equal(0, mainVm.SelectedIndex);

        // Wrap around Left
        mainVm.Navigate(NavigationDirection.Left);
        Assert.Equal(3, mainVm.SelectedIndex);
    }

    [Fact]
    public void MainViewModel_NavigateUpDown_In2DGrid_CalculatesCorrectly()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(6) }; // 2 rows x 3 cols
        var mockIm = new MockInputManager();
        var mockSound = new MockSoundService();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockSound, mockCtrl) { ColumnsCount = 3 };
        mainVm.Initialize();

        Assert.Equal(0, mainVm.SelectedIndex);

        // Down from index 0 -> index 3 (0 + 3)
        mainVm.Navigate(NavigationDirection.Down);
        Assert.Equal(3, mainVm.SelectedIndex);

        // Down from index 3 -> wraps back to 0
        mainVm.Navigate(NavigationDirection.Down);
        Assert.Equal(0, mainVm.SelectedIndex);

        // Up from index 0 -> wraps to bottom index 3
        mainVm.Navigate(NavigationDirection.Up);
        Assert.Equal(3, mainVm.SelectedIndex);

        // Up from index 3 -> index 0
        mainVm.Navigate(NavigationDirection.Up);
        Assert.Equal(0, mainVm.SelectedIndex);
    }

    [Fact]
    public void MainViewModel_SearchFilter_FiltersCorrectly()
    {
        var mockWm = new MockWindowManager
        {
            Windows = new List<WindowInfo>
            {
                new() { Handle = new IntPtr(1), Title = "Firefox Browser", ProcessName = "firefox" },
                new() { Handle = new IntPtr(2), Title = "Discord", ProcessName = "Discord" },
                new() { Handle = new IntPtr(3), Title = "Spotify Music", ProcessName = "Spotify" }
            }
        };
        var mockIm = new MockInputManager();
        var mockSound = new MockSoundService();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockSound, mockCtrl);
        mainVm.Initialize();

        Assert.Equal(3, mainVm.TotalWindowsCount);

        mainVm.SearchQuery = "spot";
        Assert.Equal(1, mainVm.TotalWindowsCount);
        Assert.Equal("Spotify Music", mainVm.SelectedWindow?.Title);

        mainVm.SearchQuery = "";
        Assert.Equal(3, mainVm.TotalWindowsCount);
    }

    [Fact]
    public void MainViewModel_SelectCurrent_TriggersSwitchAndCloseRequest()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(3) };
        var mockIm = new MockInputManager();
        var mockSound = new MockSoundService();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockSound, mockCtrl);
        mainVm.Initialize();

        bool closeRequested = false;
        mainVm.RequestClose += (s, e) => closeRequested = true;

        mainVm.SelectedIndex = 1;
        var selectedHwnd = mainVm.SelectedWindow!.Handle;

        mainVm.SelectCurrent();

        Assert.Equal(selectedHwnd, mockWm.LastSwitchedHandle);
        Assert.True(closeRequested);
        Assert.Equal(1, mockSound.SelectCount);
    }

    [Fact]
    public void MainViewModel_CloseSelected_ClosesWindowAndAdjustsIndex()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(3) };
        var mockIm = new MockInputManager();
        var mockSound = new MockSoundService();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockSound, mockCtrl);
        mainVm.Initialize();

        mainVm.SelectedIndex = 1;
        var handleToClose = mainVm.SelectedWindow!.Handle;

        mainVm.CloseSelected();

        Assert.Equal(handleToClose, mockWm.LastClosedHandle);
        Assert.Equal(2, mainVm.TotalWindowsCount);
        Assert.Equal(1, mockSound.CloseCount);
        Assert.NotNull(mainVm.SelectedWindow);
    }

    [Fact]
    public void MainViewModel_Dismiss_PlaysBackSoundAndCloses()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(2) };
        var mockIm = new MockInputManager();
        var mockSound = new MockSoundService();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockSound, mockCtrl);
        mainVm.Initialize();

        bool closeRequested = false;
        mainVm.RequestClose += (s, e) => closeRequested = true;

        mainVm.Dismiss();

        Assert.True(closeRequested);
        Assert.Equal(1, mockSound.BackCount);
    }

    [Fact]
    public void MainViewModel_ToggleMute_TogglesMuteState()
    {
        var mockWm = new MockWindowManager();
        var mockIm = new MockInputManager();
        var mockSound = new MockSoundService();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockSound, mockCtrl);
        Assert.False(mainVm.IsSoundMuted);

        mainVm.ToggleMuteCommand.Execute(null);
        Assert.True(mainVm.IsSoundMuted);
        Assert.True(mockSound.IsMuted);

        mainVm.ToggleMuteCommand.Execute(null);
        Assert.False(mainVm.IsSoundMuted);
        Assert.False(mockSound.IsMuted);
    }
}
