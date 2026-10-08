using AvaloniaInside.Shell;

namespace AvaloniaInside.Shell.Tests;

/// <summary>
/// Side-menu navigation and tab switches use <see cref="NavigateType.HostedItemChange"/>: the page that is left must be
/// reported as <see cref="NavigationStackChanges.Previous"/>, so it gets DisappearAsync like with every other navigation type.
/// </summary>
public class HostedItemChangeTests
{
    private static NavigationNode Node(string path) =>
        new(path, typeof(Page), NavigationNodeType.Page, NavigateType.Normal, "");

    private static NavigationStack NewStack()
    {
        var locator = new Mock<INavigationViewLocator>();
        locator.Setup(l => l.GetView(It.IsAny<NavigationNode>())).Returns(() => new object());
        return new NavigationStack(locator.Object);
    }

    [Fact]
    public void HostedItemChange_ReportsThePageThatIsLeft_AsPrevious()
    {
        var stack = NewStack();
        stack.Push(Node("/settings"), NavigateType.Normal, new Uri("app://root/settings"));
        var history = stack.Push(Node("/settings/history"), NavigateType.Normal, new Uri("app://root/settings/history")).Front;

        var changes = stack.Push(Node("/inspections"), NavigateType.HostedItemChange, new Uri("app://root/inspections"));

        changes.Previous.ShouldBeSameAs(history);
        changes.Front.ShouldNotBeSameAs(history);
    }

    [Fact]
    public void HostedItemChange_AsFirstNavigation_HasNoPrevious()
    {
        var stack = NewStack();

        var changes = stack.Push(Node("/inspections"), NavigateType.HostedItemChange, new Uri("app://root/inspections"));

        changes.Previous.ShouldBeNull();
        changes.Front.ShouldNotBeNull();
    }
}
