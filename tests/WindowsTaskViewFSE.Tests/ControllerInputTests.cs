using WindowsTaskViewFSE.Helpers;
using WindowsTaskViewFSE.Services;
using Xunit;

namespace WindowsTaskViewFSE.Tests;

public class ControllerInputTests
{
    [Fact]
    public void ControllerButtonEventArgs_HoldsPropertiesCorrectly()
    {
        var args = new ControllerButtonEventArgs(ControllerButton.A, 1);
        Assert.Equal(ControllerButton.A, args.Button);
        Assert.Equal(1, args.UserIndex);
    }

    [Fact]
    public void ControllerNavigationEventArgs_HoldsPropertiesCorrectly()
    {
        var args = new ControllerNavigationEventArgs(NavigationDirection.Down);
        Assert.Equal(NavigationDirection.Down, args.Direction);
    }

    [Fact]
    public void XInputConstants_MatchStandardXboxValues()
    {
        Assert.Equal(0x1000, XInputInterop.XINPUT_GAMEPAD_A);
        Assert.Equal(0x2000, XInputInterop.XINPUT_GAMEPAD_B);
        Assert.Equal(0x4000, XInputInterop.XINPUT_GAMEPAD_X);
        Assert.Equal(0x8000, XInputInterop.XINPUT_GAMEPAD_Y);
        Assert.Equal(0x0001, XInputInterop.XINPUT_GAMEPAD_DPAD_UP);
        Assert.Equal(0x0002, XInputInterop.XINPUT_GAMEPAD_DPAD_DOWN);
        Assert.Equal(0x0004, XInputInterop.XINPUT_GAMEPAD_DPAD_LEFT);
        Assert.Equal(0x0008, XInputInterop.XINPUT_GAMEPAD_DPAD_RIGHT);
        Assert.Equal(7849, XInputInterop.XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE);
    }
}
