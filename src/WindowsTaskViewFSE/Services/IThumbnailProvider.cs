using System.Windows;
using System.Windows.Media;

namespace WindowsTaskViewFSE.Services;

public interface IThumbnailProvider
{
    ImageSource? CaptureWindowThumbnail(IntPtr handle, int width = 480, int height = 270);
    ImageSource? ExtractWindowIcon(IntPtr handle, string? executablePath = null);
    IntPtr RegisterDwmThumbnail(IntPtr destinationHwnd, IntPtr sourceHwnd);
    bool UpdateDwmThumbnail(IntPtr thumbnailHandle, Rect destRect, byte opacity = 255, bool visible = true);
    void UnregisterDwmThumbnail(IntPtr thumbnailHandle);
}
