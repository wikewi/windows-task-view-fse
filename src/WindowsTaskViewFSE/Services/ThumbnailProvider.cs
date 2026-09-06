using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsTaskViewFSE.Helpers;

namespace WindowsTaskViewFSE.Services;

public class ThumbnailProvider : IThumbnailProvider
{
    public ImageSource? CaptureWindowThumbnail(IntPtr handle, int width = 480, int height = 270)
    {
        if (handle == IntPtr.Zero) return null;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var bitmapSource = CaptureViaPrintWindow(handle, width, height);
                if (bitmapSource != null)
                {
                    return bitmapSource;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThumbnailProvider] Capture error for {handle}: {ex.Message}");
        }

        return CreateFallbackThumbnail();
    }

    private BitmapSource? CaptureViaPrintWindow(IntPtr hWnd, int targetWidth, int targetHeight)
    {
        if (!NativeInterop.GetWindowRect(hWnd, out var rect))
            return null;

        int srcWidth = rect.Width;
        int srcHeight = rect.Height;

        if (srcWidth <= 0 || srcHeight <= 0)
            return null;

        IntPtr hdcSrc = NativeInterop.GetDC(hWnd);
        if (hdcSrc == IntPtr.Zero) return null;

        IntPtr hdcDest = NativeInterop.CreateCompatibleDC(hdcSrc);
        IntPtr hBitmap = NativeInterop.CreateCompatibleBitmap(hdcSrc, srcWidth, srcHeight);
        IntPtr hOld = NativeInterop.SelectObject(hdcDest, hBitmap);

        BitmapSource? result = null;

        try
        {
            // Try PW_RENDERFULLCONTENT (Windows 8.1+)
            bool printed = NativeInterop.PrintWindow(hWnd, hdcDest, NativeInterop.PW_RENDERFULLCONTENT);
            if (!printed)
            {
                // Fallback to standard PrintWindow
                printed = NativeInterop.PrintWindow(hWnd, hdcDest, 0);
            }

            if (!printed)
            {
                // Fallback to BitBlt
                NativeInterop.BitBlt(hdcDest, 0, 0, srcWidth, srcHeight, hdcSrc, 0, 0, NativeInterop.SRCCOPY);
            }

            result = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(targetWidth, targetHeight));

            if (result != null && result.CanFreeze)
            {
                result.Freeze();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThumbnailProvider] Error in CaptureViaPrintWindow: {ex.Message}");
        }
        finally
        {
            NativeInterop.SelectObject(hdcDest, hOld);
            NativeInterop.DeleteObject(hBitmap);
            NativeInterop.DeleteDC(hdcDest);
            NativeInterop.ReleaseDC(hWnd, hdcSrc);
        }

        return result;
    }

    public ImageSource? ExtractWindowIcon(IntPtr handle, string? executablePath = null)
    {
        if (handle == IntPtr.Zero) return null;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 1. Try WM_GETICON
                IntPtr hIcon = NativeInterop.SendMessage(handle, NativeInterop.WM_GETICON, NativeInterop.ICON_BIG, IntPtr.Zero);
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = NativeInterop.SendMessage(handle, NativeInterop.WM_GETICON, NativeInterop.ICON_SMALL2, IntPtr.Zero);
                }
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = NativeInterop.SendMessage(handle, NativeInterop.WM_GETICON, NativeInterop.ICON_SMALL, IntPtr.Zero);
                }

                // 2. Try GetClassLongPtr
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = NativeInterop.GetClassLongPtr(handle, NativeInterop.GCLP_HICON);
                }
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = NativeInterop.GetClassLongPtr(handle, NativeInterop.GCLP_HICONSM);
                }

                if (hIcon != IntPtr.Zero)
                {
                    var iconSource = Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    if (iconSource.CanFreeze) iconSource.Freeze();
                    return iconSource;
                }

                // 3. Try executable path icon extraction
                if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                {
                    IntPtr[] largeIcons = new IntPtr[1];
                    IntPtr[] smallIcons = new IntPtr[1];
                    uint count = NativeInterop.ExtractIconEx(executablePath, 0, largeIcons, smallIcons, 1);
                    if (count > 0 && largeIcons[0] != IntPtr.Zero)
                    {
                        var iconSource = Imaging.CreateBitmapSourceFromHIcon(
                            largeIcons[0],
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        if (iconSource.CanFreeze) iconSource.Freeze();
                        NativeInterop.DestroyIcon(largeIcons[0]);
                        if (smallIcons[0] != IntPtr.Zero) NativeInterop.DestroyIcon(smallIcons[0]);
                        return iconSource;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThumbnailProvider] ExtractWindowIcon error for {handle}: {ex.Message}");
        }

        return CreateFallbackAppIcon();
    }

    public IntPtr RegisterDwmThumbnail(IntPtr destinationHwnd, IntPtr sourceHwnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return IntPtr.Zero;
        try
        {
            int hr = NativeInterop.DwmRegisterThumbnail(destinationHwnd, sourceHwnd, out IntPtr thumbnailId);
            return hr == 0 ? thumbnailId : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public bool UpdateDwmThumbnail(IntPtr thumbnailHandle, Rect destRect, byte opacity = 255, bool visible = true)
    {
        if (thumbnailHandle == IntPtr.Zero || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        try
        {
            var props = new NativeInterop.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = NativeInterop.DWM_TNP_RECTDESTINATION | NativeInterop.DWM_TNP_OPACITY | NativeInterop.DWM_TNP_VISIBLE,
                rcDestination = new NativeInterop.RECT
                {
                    Left = (int)destRect.Left,
                    Top = (int)destRect.Top,
                    Right = (int)destRect.Right,
                    Bottom = (int)destRect.Bottom
                },
                opacity = opacity,
                fVisible = visible,
                fSourceClientAreaOnly = false
            };

            return NativeInterop.DwmUpdateThumbnailProperties(thumbnailHandle, ref props) == 0;
        }
        catch
        {
            return false;
        }
    }

    public void UnregisterDwmThumbnail(IntPtr thumbnailHandle)
    {
        if (thumbnailHandle == IntPtr.Zero || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        try
        {
            NativeInterop.DwmUnregisterThumbnail(thumbnailHandle);
        }
        catch
        {
            // Ignore on cleanup
        }
    }

    private static ImageSource CreateFallbackThumbnail()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var backgroundBrush = new LinearGradientBrush(
                Color.FromRgb(30, 30, 30),
                Color.FromRgb(15, 15, 15),
                new Point(0, 0),
                new Point(1, 1));
            dc.DrawRectangle(backgroundBrush, null, new Rect(0, 0, 480, 270));

            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1.5);
            dc.DrawRoundedRectangle(null, borderPen, new Rect(10, 10, 460, 250), 6, 6);

            // Draw a subtle window wireframe
            var titleBarBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            dc.DrawRoundedRectangle(titleBarBrush, null, new Rect(10, 10, 460, 25), 6, 6);
        }

        var rtb = new RenderTargetBitmap(480, 270, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        if (rtb.CanFreeze) rtb.Freeze();
        return rtb;
    }

    private static ImageSource CreateFallbackAppIcon()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var iconBrush = new SolidColorBrush(Color.FromRgb(16, 124, 16)); // Xbox Green
            dc.DrawRoundedRectangle(iconBrush, null, new Rect(0, 0, 32, 32), 6, 6);

            var pen = new Pen(Brushes.White, 2);
            dc.DrawRoundedRectangle(null, pen, new Rect(6, 6, 20, 20), 3, 3);
            dc.DrawLine(pen, new Point(6, 12), new Point(26, 12));
        }

        var rtb = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        if (rtb.CanFreeze) rtb.Freeze();
        return rtb;
    }
}
