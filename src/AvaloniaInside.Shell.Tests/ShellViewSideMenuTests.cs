using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AvaloniaInside.Shell.Tests;

/// <summary>
/// Whether the side menu is presented (<see cref="ShellView.SideMenuPresented"/>) is the user's choice: a page that hides
/// the side menu while it is shown must not change it.
/// </summary>
public class ShellViewSideMenuTests
{
    [Fact]
    public Task APageThatRemovesTheSideMenu_LeavesItPresented_ForThePagesAfterIt() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();
        await navigator.NavigateAsync("/main");
        Pump();
        shell.SideMenuPresented.ShouldBeTrue("presented on the desktop from the start");

        // a page without the side menu (e.g. a checklist), then home again
        await navigator.NavigateAsync("/nomenu");
        Pump();
        PaneOf(shell).IsPaneOpen.ShouldBeFalse("hidden while that page is shown");
        await navigator.NavigateAsync("/main", NavigateType.ReplaceRoot);
        Pump();

        shell.SideMenuPresented.ShouldBeTrue();
        PaneOf(shell).IsPaneOpen.ShouldBeTrue();
        window.Close();
    });

    [Fact]
    public Task APageWithItsOwnPane_LeavesTheSideMenuPresented_ForThePagesAfterIt() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();
        await navigator.NavigateAsync("/main");
        Pump();

        await navigator.NavigateAsync("/ownpane");
        Pump();
        await navigator.BackAsync();
        Pump();

        shell.SideMenuPresented.ShouldBeTrue();
        PaneOf(shell).IsPaneOpen.ShouldBeTrue();
        window.Close();
    });

    // a page's navigation bar is created after the page is shown: it shows the side menu button as the shell says
    [Fact]
    public Task KeptOpen_ANewNavigationBar_HasNoSideMenuButton() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell(ShellView.ScreenSizeType.Large);
        await navigator.NavigateAsync("/main");
        Pump();

        await navigator.NavigateAsync("/nomenu");
        Pump();
        await navigator.NavigateAsync("/main", NavigateType.ReplaceRoot);
        Pump();

        PaneOf(shell).IsPaneOpen.ShouldBeTrue();
        shell.NavigationBar.ShouldNotBeNull().HasSideMenuOption.ShouldBeFalse();
        window.Close();
    });

    // a phone turned to landscape: a page's own pane is no overlay any more, and the app opens it (as its PageBase does)
    [Fact]
    public Task TurnedToLandscape_APageOpensItsOwnPane() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell(ShellView.ScreenSizeType.Small);
        await navigator.NavigateAsync("/main");
        Pump();
        await navigator.NavigateAsync("/ownpane");
        Pump();
        var page = shell.ContentView!.CurrentView.ShouldBeOfType<OwnPanePage>();
        page.IsPaneOpen.ShouldBeFalse("closed on the phone held upright");

        window.Width = 900;
        Pump();
        Pump();

        page.IsPaneOpen.ShouldBeTrue();
        window.Close();
    });

    // the side menu closed by the user (its pane closes) stays closed
    [Fact]
    public Task ClosedByTheUser_IsNoLongerPresented() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();
        await navigator.NavigateAsync("/main");
        Pump();

        PaneOf(shell).IsPaneOpen = false;
        Pump();

        shell.SideMenuPresented.ShouldBeFalse();
        window.Close();
    });

    #region Headless harness

    private static SplitView PaneOf(ShellView shell) =>
        shell.GetVisualDescendants().OfType<SplitView>().First(s => s.Name == "PART_SplitView");

    private static Task RunOnUiThread(Func<Task> body) =>
        HeadlessUnitTestSession
            .GetOrStartForAssembly(typeof(ShellViewSideMenuTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return 0;
            }, CancellationToken.None);

    private static (Navigator navigator, ShellView shell, Window window) CreateShell(
        ShellView.ScreenSizeType screen = ShellView.ScreenSizeType.Medium)
    {
        var registrar = new NavigationRegistrar();
        var navigator = new Navigator(
            registrar,
            new RelativeNavigateStrategy(registrar),
            new DefaultNavigationUpdateStrategy(new AvaloniaInside.Shell.Presenters.PresenterProvider()),
            new DefaultNavigationViewLocator());

        registrar.RegisterRoute("main", typeof(TabPage), NavigationNodeType.Host, NavigateType.ReplaceRoot, null);
        registrar.RegisterRoute("main/inspections", typeof(InspectionsPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("nomenu", typeof(NoSideMenuPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("ownpane", typeof(OwnPanePage), NavigationNodeType.Page, NavigateType.Normal, null);

        // on a medium screen the side menu shows only while it is presented; on a large one it is kept open, without
        // a button to close it
        var shell = new ShellView(navigator)
        {
            MediumScreenSideMenuMode = SplitViewDisplayMode.Inline,
            MediumScreenSideMenuBehave = ShellView.SideMenuBehaveType.Default,
            LargeScreenSideMenuMode = SplitViewDisplayMode.Inline,
            LargeScreenSideMenuBehave = ShellView.SideMenuBehaveType.Keep,
        };

        var window = new Window
        {
            Width = screen switch { ShellView.ScreenSizeType.Large => 1280, ShellView.ScreenSizeType.Small => 400, _ => 900 },
            Height = 800
        };
        window.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        window.Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://AvaloniaInside.Shell.Tests"))
        {
            Source = new Uri("avares://AvaloniaInside.Shell/Default.axaml")
        });
        window.Content = shell;
        window.Show();
        PumpUntilLoaded(shell);
        shell.ContentView.ShouldNotBeNull().PageTransition = null; // keep the headless run synchronous
        shell.ScreenSize.ShouldBe(screen);

        return (navigator, shell, window);
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

    #endregion

    private sealed class InspectionsPage : Page;

    private sealed class NoSideMenuPage : Page
    {
        public NoSideMenuPage() => ShellView.SetOverrideSideMenuBehave(this, ShellView.SideMenuBehaveType.Removed);
    }

    private sealed class OwnPanePage : Page
    {
        public OwnPanePage()
        {
            Pane = new TextBlock { Text = "chapters" };
            // as the app's PageBase: open the pane while it is no overlay, on every change of the window size
            AttachedToVisualTree += (_, _) => TopLevel.GetTopLevel(this)!.GetObservable(BoundsProperty)
                .Subscribe(new AnonymousObserver<Avalonia.Rect>(_ =>
                    IsPaneOpen = LocalSideMenuDisplayMode is not (SplitViewDisplayMode.Overlay or SplitViewDisplayMode.CompactOverlay)));
        }
    }
}
