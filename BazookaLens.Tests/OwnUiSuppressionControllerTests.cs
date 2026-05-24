using BazookaLens.UI;
using Dalamud.Interface.Windowing;

namespace BazookaLens.Tests;

public sealed class OwnUiSuppressionControllerTests
{
    [Fact]
    public void SuppressClosesRegisteredWindowsAndRestoresTheirPriorStates()
    {
        var controller = new OwnUiSuppressionController();
        var openWindow = new TestWindow { IsOpen = true };
        var closedWindow = new TestWindow { IsOpen = false };
        controller.Register(openWindow);
        controller.Register(closedWindow);

        using (controller.Suppress())
        {
            Assert.True(controller.IsSuppressed);
            Assert.False(openWindow.IsOpen);
            Assert.False(closedWindow.IsOpen);
        }

        Assert.False(controller.IsSuppressed);
        Assert.True(openWindow.IsOpen);
        Assert.False(closedWindow.IsOpen);
    }

    [Fact]
    public void NestedSuppressRestoresOnlyAfterOuterScopeDisposes()
    {
        var controller = new OwnUiSuppressionController();
        var window = new TestWindow { IsOpen = true };
        controller.Register(window);

        using var outer = controller.Suppress();
        using (controller.Suppress())
        {
            Assert.False(window.IsOpen);
        }

        Assert.True(controller.IsSuppressed);
        Assert.False(window.IsOpen);

        outer.Dispose();

        Assert.False(controller.IsSuppressed);
        Assert.True(window.IsOpen);
    }

    private sealed class TestWindow : Window
    {
        public TestWindow()
            : base("Test###OwnUiSuppressionControllerTests")
        {
        }

        public override void Draw()
        {
        }
    }
}
