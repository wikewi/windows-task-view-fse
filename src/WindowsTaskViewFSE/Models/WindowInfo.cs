using System.Windows;
using System.Windows.Media;

namespace WindowsTaskViewFSE.Models;

public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public ImageSource? Icon { get; set; }
    public ImageSource? ThumbnailBitmap { get; set; }
    public Rect Bounds { get; set; }
    public bool IsMinimized { get; set; }
    public bool IsMaximized { get; set; }
    public bool IsActive { get; set; }
    public int ZOrder { get; set; }
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title)
        ? (string.IsNullOrWhiteSpace(ProcessName) ? "Application" : ProcessName)
        : Title;

    public override string ToString() => $"{DisplayTitle} (PID: {ProcessId}, HWND: {Handle})";
}
