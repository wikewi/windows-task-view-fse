namespace WindowsTaskViewFSE.Services;

public enum ControllerButton
{
    None = 0,
    A,
    B,
    X,
    Y,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    LeftShoulder,
    RightShoulder,
    Start,
    Back,
    LeftThumb,
    RightThumb
}

public enum NavigationDirection
{
    Up,
    Down,
    Left,
    Right,
    Select,
    Back,
    CloseApp,
    Refresh,
    PageLeft,
    PageRight
}

public class ControllerButtonEventArgs : EventArgs
{
    public ControllerButton Button { get; }
    public int UserIndex { get; }

    public ControllerButtonEventArgs(ControllerButton button, int userIndex = 0)
    {
        Button = button;
        UserIndex = userIndex;
    }
}

public class ControllerNavigationEventArgs : EventArgs
{
    public NavigationDirection Direction { get; }

    public ControllerNavigationEventArgs(NavigationDirection direction)
    {
        Direction = direction;
    }
}

public interface IControllerInputService
{
    bool IsConnected { get; }
    event EventHandler<ControllerButtonEventArgs>? ButtonDown;
    event EventHandler<ControllerButtonEventArgs>? ButtonUp;
    event EventHandler<ControllerNavigationEventArgs>? NavigationRequested;
    event EventHandler<bool>? ConnectionChanged;

    void Start();
    void Stop();
    void TriggerHapticFeedback(int userIndex = 0, ushort leftMotor = 32000, ushort rightMotor = 32000, int durationMs = 120);
}
