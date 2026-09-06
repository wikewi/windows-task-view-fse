using System.Windows.Input;

namespace WindowsTaskViewFSE.Services;

public class InputManager : IInputManager, IDisposable
{
    private readonly IControllerInputService _controllerService;

    public event EventHandler<NavigationDirection>? NavigationRequested;

    public InputManager(IControllerInputService controllerService)
    {
        _controllerService = controllerService ?? throw new ArgumentNullException(nameof(controllerService));
        _controllerService.NavigationRequested += OnControllerNavigation;
    }

    public void Start()
    {
        _controllerService.Start();
    }

    public void Stop()
    {
        _controllerService.Stop();
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (e.Handled) return;

        NavigationDirection? direction = e.Key switch
        {
            Key.Up => NavigationDirection.Up,
            Key.Down => NavigationDirection.Down,
            Key.Left => NavigationDirection.Left,
            Key.Right => NavigationDirection.Right,
            Key.Enter or Key.Space => NavigationDirection.Select,
            Key.Escape => NavigationDirection.Back,
            Key.Delete or Key.X => NavigationDirection.CloseApp,
            Key.F5 => NavigationDirection.Refresh,
            Key.PageUp or Key.Prior => NavigationDirection.PageLeft,
            Key.PageDown or Key.Next => NavigationDirection.PageRight,
            _ => null
        };

        if (direction.HasValue)
        {
            e.Handled = true;
            NavigationRequested?.Invoke(this, direction.Value);
        }
    }

    private void OnControllerNavigation(object? sender, ControllerNavigationEventArgs e)
    {
        NavigationRequested?.Invoke(this, e.Direction);
    }

    public void Dispose()
    {
        _controllerService.NavigationRequested -= OnControllerNavigation;
    }
}
