using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AvaloniaInside.Shell.Tests;

/// <summary>
/// <see cref="ShellView.CurrentPage"/> is the page the shell shows: the page itself, or for a tab host the page of its
/// selected tab.
/// </summary>
public class ShellViewCurrentPageTests
{
    [Fact]
    public Task BeforeAnyNavigation_IsNull() => RunOnUiThread(() =>
    {
        var (_, shell, window) = CreateShell();

        shell.CurrentPage.ShouldBeNull();
        window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public Task OnAPage_IsThatPage() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();

        await navigator.NavigateAsync("/details");
        Pump();

        shell.CurrentPage.ShouldBeOfType<DetailsPage>();
        window.Close();
    });

    [Fact]
    public Task FollowsNavigation_ForwardAndBack() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();

        await navigator.NavigateAsync("/details");
        Pump();
        var details = shell.CurrentPage;

        await navigator.NavigateAsync("/details/more");
        Pump();
        shell.CurrentPage.ShouldBeOfType<MorePage>();

        await navigator.BackAsync();
        Pump();
        shell.CurrentPage.ShouldBeSameAs(details);
        window.Close();
    });

    [Fact]
    public Task OnATab_IsThePageOfTheTab() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();

        await navigator.NavigateAsync("/main/inspections");
        Pump();

        shell.CurrentPage.ShouldBeOfType<InspectionsPage>();
        window.Close();
    });

    [Fact]
    public Task OnTheTabHost_IsThePageOfItsSelectedTab() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();

        await navigator.NavigateAsync("/main");
        Pump();

        shell.CurrentPage.ShouldBeOfType<InspectionsPage>();
        window.Close();
    });

    [Fact]
    public Task AfterTheSideMenuSwitchesTheTab_IsThePageOfThatTab() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();

        await navigator.NavigateAsync("/main/inspections");
        Pump();
        // as the side menu does
        await navigator.NavigateAsync("/main/tasks", NavigateType.HostedItemChange);
        Pump();

        shell.CurrentPage.ShouldBeOfType<TasksPage>();
        window.Close();
    });

    [Fact]
    public Task AfterSelectingAnotherTab_IsThePageOfThatTab() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();

        await navigator.NavigateAsync("/main/inspections");
        Pump();
        var tabs = shell.ContentView!.CurrentView.ShouldBeOfType<TabPage>();

        // as a click on the tab strip does
        tabs.SelectedItem = tabs.ItemsSource!.OfType<NavigationChain>().Single(c => c.Instance is TasksPage);
        Pump();

        shell.CurrentPage.ShouldBeOfType<TasksPage>();
        window.Close();
    });

    [Fact]
    public Task OnAPageOverTheTabs_IsThatPage() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();

        await navigator.NavigateAsync("/main/inspections");
        Pump();
        await navigator.NavigateAsync("/details");
        Pump();

        shell.CurrentPage.ShouldBeOfType<DetailsPage>();
        window.Close();
    });

    [Fact]
    public Task OnATabOfANestedTabHost_IsThePageOfThatTab_BelowItsHost() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();

        await navigator.NavigateAsync("/main/tasks/taskdetail");
        Pump();

        var page = shell.CurrentPage.ShouldBeOfType<HeaderPage>();
        // what the page's actions bubble up through: its tab host, not the outer tabs
        var ancestors = page.GetVisualAncestors().ToList();
        ancestors.OfType<TaskDetailTabs>().ShouldHaveSingleItem();
        ancestors.ShouldNotContain(a => a.GetType() == typeof(TabPage));
        window.Close();
    });

    [Fact]
    public Task AfterSelectingAnotherTabOfANestedTabHost_IsThePageOfThatTab() => RunOnUiThread(async () =>
    {
        var (navigator, shell, window) = CreateShell();

        await navigator.NavigateAsync("/main/tasks/taskdetail");
        Pump();
        var tabs = shell.ContentView!.CurrentView.ShouldBeOfType<TaskDetailTabs>();

        // as a click on the tab strip does
        tabs.SelectedItem = tabs.ItemsSource!.OfType<NavigationChain>().Single(c => c.Instance is ContentPage);
        Pump();

        shell.CurrentPage.ShouldBeOfType<ContentPage>();
        window.Close();
    });

    #region Headless harness

    private static Task RunOnUiThread(Func<Task> body) =>
        HeadlessUnitTestSession
            .GetOrStartForAssembly(typeof(ShellViewCurrentPageTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return 0;
            }, CancellationToken.None);

    private static (Navigator navigator, ShellView shell, Window window) CreateShell()
    {
        var registrar = new NavigationRegistrar();
        var navigator = new Navigator(
            registrar,
            new RelativeNavigateStrategy(registrar),
            new DefaultNavigationUpdateStrategy(new AvaloniaInside.Shell.Presenters.PresenterProvider()),
            new DefaultNavigationViewLocator());

        registrar.RegisterRoute("main", typeof(TabPage), NavigationNodeType.Host, NavigateType.ReplaceRoot, null);
        registrar.RegisterRoute("main/inspections", typeof(InspectionsPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("main/tasks", typeof(TasksPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("main/tasks/taskdetail", typeof(TaskDetailTabs), NavigationNodeType.Host, NavigateType.Normal, null);
        registrar.RegisterRoute("main/tasks/taskdetail/header", typeof(HeaderPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("main/tasks/taskdetail/content", typeof(ContentPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("details", typeof(DetailsPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("details/more", typeof(MorePage), NavigationNodeType.Page, NavigateType.Normal, null);

        var shell = new ShellView(navigator);

        // the real shell theme, so the shell templates its content view and the tab page its tabs;
        // styles must be in place before the shell attaches, or its implicit ControlTheme is cached as "none"
        var window = new Window { Width = 1280, Height = 800 };
        window.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        window.Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://AvaloniaInside.Shell.Tests"))
        {
            Source = new Uri("avares://AvaloniaInside.Shell/Default.axaml")
        });
        window.Content = shell;
        window.Show();
        PumpUntilLoaded(shell);
        shell.ContentView.ShouldNotBeNull().PageTransition = null; // keep the headless run synchronous

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
    private sealed class TasksPage : Page;
    private sealed class DetailsPage : Page;
    private sealed class MorePage : Page;
    private sealed class TaskDetailTabs : TabPage;
    private sealed class HeaderPage : Page;
    private sealed class ContentPage : Page;
}
