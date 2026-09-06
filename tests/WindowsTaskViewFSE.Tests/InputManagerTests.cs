using WindowsTaskViewFSE.Services;
using Xunit;

namespace WindowsTaskViewFSE.Tests;

public class InputManagerTests
{
    [Fact]
    public void InputManager_PropagatesControllerNavigationEvents()
    {
        var mockCtrl = new MockControllerService();
        var inputManager = new InputManager(mockCtrl);

        NavigationDirection? receivedDir = null;
        inputManager.NavigationRequested += (s, dir) => receivedDir = dir;

        inputManager.Start();
        Assert.True(mockCtrl.IsStarted);

        mockCtrl.TriggerNav(NavigationDirection.Right);
        Assert.Equal(NavigationDirection.Right, receivedDir);

        mockCtrl.TriggerNav(NavigationDirection.Select);
        Assert.Equal(NavigationDirection.Select, receivedDir);

        inputManager.Stop();
        Assert.True(mockCtrl.IsStopped);
    }
}
