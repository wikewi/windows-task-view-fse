using System.Windows;
using WindowsTaskViewFSE.Models;
using Xunit;

namespace WindowsTaskViewFSE.Tests;

public class WindowInfoTests
{
    [Fact]
    public void DisplayTitle_WhenTitleIsPresent_ReturnsTitle()
    {
        var info = new WindowInfo
        {
            Title = "Visual Studio Code",
            ProcessName = "Code"
        };

        Assert.Equal("Visual Studio Code", info.DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_WhenTitleIsEmpty_ReturnsProcessName()
    {
        var info = new WindowInfo
        {
            Title = "",
            ProcessName = "chrome"
        };

        Assert.Equal("chrome", info.DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_WhenBothEmpty_ReturnsApplicationFallback()
    {
        var info = new WindowInfo
        {
            Title = "   ",
            ProcessName = ""
        };

        Assert.Equal("Application", info.DisplayTitle);
    }

    [Fact]
    public void WindowInfo_ToString_ContainsTitleAndProcessId()
    {
        var info = new WindowInfo
        {
            Title = "Xbox App",
            ProcessName = "XboxApp",
            ProcessId = 1234,
            Handle = new IntPtr(0x5678)
        };

        string str = info.ToString();
        Assert.Contains("Xbox App", str);
        Assert.Contains("1234", str);
    }

    [Fact]
    public void WindowInfo_InitialValues_AreSensible()
    {
        var info = new WindowInfo();
        Assert.Equal(IntPtr.Zero, info.Handle);
        Assert.False(info.IsMinimized);
        Assert.False(info.IsMaximized);
        Assert.False(info.IsActive);
        Assert.True(info.LastAccessed <= DateTime.UtcNow);
    }
}
