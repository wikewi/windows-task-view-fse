using WindowsTaskViewFSE.Models;

namespace WindowsTaskViewFSE.Services;

public interface IWindowManager
{
    IReadOnlyList<WindowInfo> GetOpenWindows();
    bool SwitchToWindow(IntPtr handle);
    bool CloseWindow(IntPtr handle);
    bool MinimizeWindow(IntPtr handle);
    bool MaximizeWindow(IntPtr handle);
    void StartMonitoring();
    void StopMonitoring();
    event EventHandler? WindowsChanged;
}
