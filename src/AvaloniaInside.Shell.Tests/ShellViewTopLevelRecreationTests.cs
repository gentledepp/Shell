using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaInside.Shell.Presenters;

namespace AvaloniaInside.Shell.Tests;

/// <summary>
/// Android destroys and recreates the Activity (and its TopLevel) while the shell and its
/// navigation stack keep living; the shell must keep handling BackRequested on the new
/// TopLevel or a back press closes the app. The recreation is simulated by moving the
/// shell between two headless windows - exactly what AvaloniaActivity does to the content.
/// </summary>
public class ShellViewTopLevelRecreationTests
{
    [Fact]
    public Task BackRequested_OnInitialTopLevel_NavigatesBack() => RunOnUiThread(async () =>
    {
        var (navigator, shell) = CreateShell();
        var window = CreateWindow(shell);
        window.Show();
        PumpUntilLoaded(shell);

        await navigator.NavigateAsync("/home");
        await navigator.NavigateAsync("/details");

        var args = RaiseBackRequested(window);

        args.Handled.ShouldBeTrue("the shell should handle the back request while pages are on the stack");
        Pump();
        navigator.CurrentUri.AbsolutePath.ShouldBe("/home");

        window.Close();
    });

    [Fact]
    public Task BackRequested_AfterTopLevelRecreation_StillNavigatesBack() => RunOnUiThread(async () =>
    {
        var (navigator, shell) = CreateShell();

        var firstWindow = CreateWindow(shell);
        firstWindow.Show();
        PumpUntilLoaded(shell);

        await navigator.NavigateAsync("/home");
        await navigator.NavigateAsync("/details");

        // Activity destroyed: content detaches, TopLevel dies, shell + stack live on
        firstWindow.Content = null;
        Pump();
        firstWindow.Close();
        Pump();

        // new Activity: the same shell is attached to a fresh TopLevel
        var secondWindow = CreateWindow(shell);
        secondWindow.Show();
        PumpUntilLoaded(shell);

        var args = RaiseBackRequested(secondWindow);

        // unhandled would trigger the platform default: finishing the activity
        args.Handled.ShouldBeTrue(
            "the back request on the recreated TopLevel was not handled - on Android this closes the app");
        Pump();
        navigator.CurrentUri.AbsolutePath.ShouldBe("/home");

        secondWindow.Close();
    });

    [Fact]
    public Task BackRequested_AfterReattachToSameTopLevel_NavigatesBackExactlyOnce() => RunOnUiThread(async () =>
    {
        var (navigator, shell) = CreateShell();
        var window = CreateWindow(shell);
        window.Show();
        PumpUntilLoaded(shell);

        await navigator.NavigateAsync("/home");
        await navigator.NavigateAsync("/details");
        await navigator.NavigateAsync("/more");

        // handlers must not stack up on detach/re-attach - one back press, one navigation
        window.Content = null;
        Pump();
        window.Content = shell;
        PumpUntilLoaded(shell);

        var args = RaiseBackRequested(window);

        args.Handled.ShouldBeTrue();
        Pump();
        navigator.CurrentUri.AbsolutePath.ShouldBe("/details");

        window.Close();
    });

    #region Test setup helpers

    private static Task RunOnUiThread(Func<Task> body) =>
        HeadlessUnitTestSession
            .GetOrStartForAssembly(typeof(ShellViewTopLevelRecreationTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return 0;
            }, CancellationToken.None);

    private static (Navigator navigator, ShellView shell) CreateShell()
    {
        var registrar = new NavigationRegistrar();
        var navigator = new Navigator(
            registrar,
            new RelativeNavigateStrategy(registrar),
            new DefaultNavigationUpdateStrategy(new PresenterProvider()),
            new DefaultNavigationViewLocator());

        registrar.RegisterRoute("home", typeof(TestPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("details", typeof(TestPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("more", typeof(TestPage), NavigationNodeType.Page, NavigateType.Normal, null);

        var shell = new ShellView(navigator);
        return (navigator, shell);
    }

    private static Window CreateWindow(ShellView shell) => new()
    {
        // large enough that Back() skips the small-screen side-menu branch
        Width = 1280,
        Height = 800,
        Content = shell
    };

    private static RoutedEventArgs RaiseBackRequested(Window window)
    {
        var args = new RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(args);
        return args;
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void PumpUntilLoaded(Control control)
    {
        for (var i = 0; i < 20 && !control.IsLoaded; i++)
            Pump();

        control.IsLoaded.ShouldBeTrue("the control never got loaded");
    }

    private class TestPage : UserControl
    {
    }

    #endregion
}
