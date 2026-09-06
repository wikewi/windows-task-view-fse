using System.Windows.Threading;
using WindowsTaskViewFSE.Helpers;

namespace WindowsTaskViewFSE.Services;

public class ControllerInputService : IControllerInputService, IDisposable
{
    private readonly DispatcherTimer _pollTimer;
    private ushort _previousButtons = 0;
    private bool _isConnected = false;
    private readonly int _userIndex = 0;

    // Auto-repeat state for DPad and Stick
    private NavigationDirection? _heldDirection = null;
    private DateTime _heldStartTime = DateTime.MinValue;
    private DateTime _lastRepeatTime = DateTime.MinValue;
    private static readonly TimeSpan InitialRepeatDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(130);

    private const short StickThreshold = 15000;

    public bool IsConnected => _isConnected;

    public event EventHandler<ControllerButtonEventArgs>? ButtonDown;
    public event EventHandler<ControllerButtonEventArgs>? ButtonUp;
    public event EventHandler<ControllerNavigationEventArgs>? NavigationRequested;
    public event EventHandler<bool>? ConnectionChanged;

    public ControllerInputService(int userIndex = 0)
    {
        _userIndex = userIndex;
        _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS polling
        };
        _pollTimer.Tick += OnPollTick;
    }

    public void Start()
    {
        if (!_pollTimer.IsEnabled)
        {
            _pollTimer.Start();
        }
    }

    public void Stop()
    {
        if (_pollTimer.IsEnabled)
        {
            _pollTimer.Stop();
        }
    }

    public void TriggerHapticFeedback(int userIndex = 0, ushort leftMotor = 32000, ushort rightMotor = 32000, int durationMs = 120)
    {
        Task.Run(async () =>
        {
            try
            {
                var vibration = new XInputInterop.XINPUT_VIBRATION
                {
                    wLeftMotorSpeed = leftMotor,
                    wRightMotorSpeed = rightMotor
                };
                XInputInterop.SetState(userIndex, ref vibration);

                await Task.Delay(durationMs);

                var stopVibration = new XInputInterop.XINPUT_VIBRATION
                {
                    wLeftMotorSpeed = 0,
                    wRightMotorSpeed = 0
                };
                XInputInterop.SetState(userIndex, ref stopVibration);
            }
            catch
            {
                // Ignored
            }
        });
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        int result = XInputInterop.GetState(_userIndex, out var state);
        bool currentlyConnected = (result == XInputInterop.ERROR_SUCCESS);

        if (currentlyConnected != _isConnected)
        {
            _isConnected = currentlyConnected;
            ConnectionChanged?.Invoke(this, _isConnected);
        }

        if (!_isConnected)
        {
            _previousButtons = 0;
            _heldDirection = null;
            return;
        }

        ushort currentButtons = state.Gamepad.wButtons;
        short thumbLX = state.Gamepad.sThumbLX;
        short thumbLY = state.Gamepad.sThumbLY;

        ProcessButtons(currentButtons);
        ProcessNavigation(currentButtons, thumbLX, thumbLY);

        _previousButtons = currentButtons;
    }

    private void ProcessButtons(ushort current)
    {
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_A, ControllerButton.A);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_B, ControllerButton.B);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_X, ControllerButton.X);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_Y, ControllerButton.Y);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_LEFT_SHOULDER, ControllerButton.LeftShoulder);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_RIGHT_SHOULDER, ControllerButton.RightShoulder);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_START, ControllerButton.Start);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_BACK, ControllerButton.Back);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_DPAD_UP, ControllerButton.DPadUp);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_DPAD_DOWN, ControllerButton.DPadDown);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_DPAD_LEFT, ControllerButton.DPadLeft);
        CheckButton(current, XInputInterop.XINPUT_GAMEPAD_DPAD_RIGHT, ControllerButton.DPadRight);
    }

    private void CheckButton(ushort current, ushort flag, ControllerButton button)
    {
        bool wasPressed = (_previousButtons & flag) != 0;
        bool isPressed = (current & flag) != 0;

        if (!wasPressed && isPressed)
        {
            ButtonDown?.Invoke(this, new ControllerButtonEventArgs(button, _userIndex));
            HandleButtonNavigation(button);
        }
        else if (wasPressed && !isPressed)
        {
            ButtonUp?.Invoke(this, new ControllerButtonEventArgs(button, _userIndex));
        }
    }

    private void HandleButtonNavigation(ControllerButton button)
    {
        switch (button)
        {
            case ControllerButton.A:
                NavigationRequested?.Invoke(this, new ControllerNavigationEventArgs(NavigationDirection.Select));
                break;
            case ControllerButton.B:
                NavigationRequested?.Invoke(this, new ControllerNavigationEventArgs(NavigationDirection.Back));
                break;
            case ControllerButton.X:
                NavigationRequested?.Invoke(this, new ControllerNavigationEventArgs(NavigationDirection.CloseApp));
                break;
            case ControllerButton.Y:
                NavigationRequested?.Invoke(this, new ControllerNavigationEventArgs(NavigationDirection.Refresh));
                break;
            case ControllerButton.LeftShoulder:
                NavigationRequested?.Invoke(this, new ControllerNavigationEventArgs(NavigationDirection.PageLeft));
                break;
            case ControllerButton.RightShoulder:
                NavigationRequested?.Invoke(this, new ControllerNavigationEventArgs(NavigationDirection.PageRight));
                break;
        }
    }

    private void ProcessNavigation(ushort buttons, short thumbLX, short thumbLY)
    {
        NavigationDirection? activeDir = null;

        if ((buttons & XInputInterop.XINPUT_GAMEPAD_DPAD_UP) != 0 || thumbLY > StickThreshold)
        {
            activeDir = NavigationDirection.Up;
        }
        else if ((buttons & XInputInterop.XINPUT_GAMEPAD_DPAD_DOWN) != 0 || thumbLY < -StickThreshold)
        {
            activeDir = NavigationDirection.Down;
        }
        else if ((buttons & XInputInterop.XINPUT_GAMEPAD_DPAD_LEFT) != 0 || thumbLX < -StickThreshold)
        {
            activeDir = NavigationDirection.Left;
        }
        else if ((buttons & XInputInterop.XINPUT_GAMEPAD_DPAD_RIGHT) != 0 || thumbLX > StickThreshold)
        {
            activeDir = NavigationDirection.Right;
        }

        DateTime now = DateTime.UtcNow;

        if (activeDir.HasValue)
        {
            if (_heldDirection != activeDir)
            {
                _heldDirection = activeDir;
                _heldStartTime = now;
                _lastRepeatTime = now;
                NavigationRequested?.Invoke(this, new ControllerNavigationEventArgs(activeDir.Value));
            }
            else
            {
                if (now - _heldStartTime >= InitialRepeatDelay && now - _lastRepeatTime >= RepeatInterval)
                {
                    _lastRepeatTime = now;
                    NavigationRequested?.Invoke(this, new ControllerNavigationEventArgs(activeDir.Value));
                }
            }
        }
        else
        {
            _heldDirection = null;
        }
    }

    public void Dispose()
    {
        Stop();
        _pollTimer.Tick -= OnPollTick;
    }
}
