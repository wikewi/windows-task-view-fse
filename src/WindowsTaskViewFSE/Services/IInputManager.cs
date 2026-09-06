using System.Windows.Input;

namespace WindowsTaskViewFSE.Services;

public interface IInputManager
{
    event EventHandler<NavigationDirection>? NavigationRequested;
    void HandleKeyDown(KeyEventArgs e);
    void Start();
    void Stop();
}
