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

    public Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync() => Task.FromResult(GetOpenWindows());

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
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockCtrl);
        mainVm.Initialize();

        Assert.True(mockWm.IsMonitoringStarted);
        Assert.True(mockIm.IsStarted);
        Assert.Equal(4, mainVm.TotalWindowsCount);
        Assert.NotNull(mainVm.SelectedWindow);
        Assert.Equal(0, mainVm.SelectedIndex);
        Assert.True(mainVm.SelectedWindow!.IsFocused);
    }

    [Fact]
    public void MainViewModel_NavigateUpAndDown_UpdatesSelection()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(4) };
        var mockIm = new MockInputManager();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockCtrl) { RowsCount = 2 };
        mainVm.Initialize();

        Assert.Equal(0, mainVm.SelectedIndex);

        // Move Down (adjacent item within the same column)
        mainVm.Navigate(NavigationDirection.Down);
        Assert.Equal(1, mainVm.SelectedIndex);
        Assert.Equal(1, mockCtrl.HapticFeedbackCount);

        // Move Down again
        mainVm.Navigate(NavigationDirection.Down);
        Assert.Equal(2, mainVm.SelectedIndex);

        // Move Up
        mainVm.Navigate(NavigationDirection.Up);
        Assert.Equal(1, mainVm.SelectedIndex);

        // Move Up to 0
        mainVm.Navigate(NavigationDirection.Up);
        Assert.Equal(0, mainVm.SelectedIndex);

        // Wrap around Up
        mainVm.Navigate(NavigationDirection.Up);
        Assert.Equal(3, mainVm.SelectedIndex);
    }

    [Fact]
    public void MainViewModel_NavigateLeftRight_InColumnMajorGrid_CalculatesCorrectly()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(6) }; // 3 columns x 2 rows
        var mockIm = new MockInputManager();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockCtrl) { RowsCount = 3 };
        mainVm.Initialize();

        Assert.Equal(0, mainVm.SelectedIndex);

        // Right from index 0 -> index 3 (0 + 3 rows)
        mainVm.Navigate(NavigationDirection.Right);
        Assert.Equal(3, mainVm.SelectedIndex);

        // Right from index 3 -> wraps back to 0
        mainVm.Navigate(NavigationDirection.Right);
        Assert.Equal(0, mainVm.SelectedIndex);

        // Left from index 0 -> wraps to rightmost column index 3
        mainVm.Navigate(NavigationDirection.Left);
        Assert.Equal(3, mainVm.SelectedIndex);

        // Left from index 3 -> index 0
        mainVm.Navigate(NavigationDirection.Left);
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
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockCtrl);
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
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockCtrl);
        mainVm.Initialize();

        bool closeRequested = false;
        mainVm.RequestClose += (s, e) => closeRequested = true;

        mainVm.SelectedIndex = 1;
        var selectedHwnd = mainVm.SelectedWindow!.Handle;

        mainVm.SelectCurrent();

        Assert.Equal(selectedHwnd, mockWm.LastSwitchedHandle);
        Assert.True(closeRequested);
    }

    [Fact]
    public void MainViewModel_CloseSelected_ClosesWindowAndAdjustsIndex()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(3) };
        var mockIm = new MockInputManager();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockCtrl);
        mainVm.Initialize();

        mainVm.SelectedIndex = 1;
        var handleToClose = mainVm.SelectedWindow!.Handle;

        mainVm.CloseSelected();

        Assert.Equal(handleToClose, mockWm.LastClosedHandle);
        Assert.Equal(2, mainVm.TotalWindowsCount);
        Assert.NotNull(mainVm.SelectedWindow);
    }

    [Fact]
    public void MainViewModel_Dismiss_RequestsClose()
    {
        var mockWm = new MockWindowManager { Windows = CreateSampleWindows(2) };
        var mockIm = new MockInputManager();
        var mockCtrl = new MockControllerService();

        var mainVm = new MainViewModel(mockWm, mockIm, mockCtrl);
        mainVm.Initialize();

        bool closeRequested = false;
        mainVm.RequestClose += (s, e) => closeRequested = true;

        mainVm.Dismiss();

        Assert.True(closeRequested);
    }
}
