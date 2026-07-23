using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaloniaInside.Shell;

namespace AvaloniaInside.Shell.Tests;

public class DeferredBackStackTests
{
    // ---------------------------------------------------------------------
    // Task 1.1 - pure NavigationStack.SeedRestore structure
    // ---------------------------------------------------------------------

    private static NavigationNode Node(string path) =>
        new(path, typeof(Page), NavigationNodeType.Page, NavigateType.Normal, "");

    private static (NavigationStack stack, Mock<INavigationViewLocator> locator) NewStack()
    {
        var locator = new Mock<INavigationViewLocator>();
        locator.Setup(l => l.GetView(It.IsAny<NavigationNode>())).Returns(() => new object());
        return (new NavigationStack(locator.Object), locator);
    }

    [Fact]
    public void SeedRestore_DoesNotInstantiateDeferredEntries()
    {
        var (stack, locator) = NewStack();
        var main = Node("/main");
        var inspection = Node("/inspection");
        var form = Node("/inspection/form");

        var changes = stack.SeedRestore(new List<RestoreSeedEntry>
        {
            new(main, new Uri("app://root/main"), Deferred: false, null),
            new(inspection, new Uri("app://root/inspection"), Deferred: true, _ => Task.FromResult<object?>("arg")),
            new(form, new Uri("app://root/inspection/form"), Deferred: false, null),
        });

        // front is the form
        stack.Current!.Node.ShouldBe(form);
        stack.Current.Back!.Node.ShouldBe(inspection);
        stack.Current.Back.IsDeferred.ShouldBeTrue();
        stack.Current.Back.Instance.ShouldBeNull();
        stack.Current.Back.Back!.Node.ShouldBe(main);
        stack.Current.Back.Back.IsDeferred.ShouldBeFalse();

        // only the two eager entries were instantiated
        locator.Verify(l => l.GetView(main), Times.Once);
        locator.Verify(l => l.GetView(form), Times.Once);
        locator.Verify(l => l.GetView(inspection), Times.Never);
        changes.NewNavigationChains.Count.ShouldBe(2);
    }

    // ---------------------------------------------------------------------
    // Task 1.2 - RestoreStackAsync presents the front, seeds parents
    // ---------------------------------------------------------------------

    [Fact]
    public Task RestoreStackAsync_PresentsFront_SeedsDeferredMiddle_AndEagerBase() => RunOnUiThread(async () =>
    {
        ProbePage.Reset();
        var (navigator, shell) = CreateRestoreShell();
        var window = CreateWindow(shell);
        window.Show();
        PumpUntilLoaded(shell);

        // establish the base (host-correct in the real app; a plain page here)
        await navigator.NavigateAsync("/main");
        Pump();
        ProbePage.Created.OfType<MainProbe>().ShouldHaveSingleItem();

        // seed the deferred parent + present the target on top
        await navigator.RestoreStackAsync(new List<RestoreStackEntry>
        {
            new("/inspection", Deferred: true, _ => Task.FromResult<object?>("inspection-arg")),
            new("/inspection/form", ArgumentFactory: _ => Task.FromResult<object?>("form-arg")),
        });
        Pump();

        // landed directly on the form, initialised, appeared, argument delivered
        navigator.CurrentUri.AbsolutePath.ShouldBe("/inspection/form");
        var form = ProbePage.Created.OfType<FormProbe>().ShouldHaveSingleItem();
        form.Argument.ShouldBe("form-arg");
        form.InitialiseCount.ShouldBe(1);
        form.AppearCount.ShouldBe(1);

        // deferred middle not instantiated
        ProbePage.Created.OfType<InspectionProbe>().ShouldBeEmpty();

        window.Close();
    });

    // ---------------------------------------------------------------------
    // Task 1.3 - back materializes the deferred entry exactly once
    // ---------------------------------------------------------------------

    [Fact]
    public Task Back_ToDeferredEntry_MaterializesAndDeliversArgumentOnce() => RunOnUiThread(async () =>
    {
        ProbePage.Reset();
        var (navigator, shell) = CreateRestoreShell();
        var window = CreateWindow(shell);
        window.Show();
        PumpUntilLoaded(shell);

        await navigator.NavigateAsync("/main");
        Pump();

        var inspectionArgCalls = 0;
        await navigator.RestoreStackAsync(new List<RestoreStackEntry>
        {
            new("/inspection", Deferred: true, _ =>
            {
                inspectionArgCalls++;
                return Task.FromResult<object?>("inspection-arg");
            }),
            new("/inspection/form", ArgumentFactory: _ => Task.FromResult<object?>("form-arg")),
        });
        Pump();

        // deferred: nothing created / no factory run yet
        ProbePage.Created.OfType<InspectionProbe>().ShouldBeEmpty();
        inspectionArgCalls.ShouldBe(0);

        // back -> materialize the inspection with its argument (load-before-reveal)
        await navigator.BackAsync();
        Pump();

        navigator.CurrentUri.AbsolutePath.ShouldBe("/inspection");
        var inspection = ProbePage.Created.OfType<InspectionProbe>().ShouldHaveSingleItem();
        inspection.Argument.ShouldBe("inspection-arg");
        inspection.InitialiseCount.ShouldBe(1);
        inspection.AppearCount.ShouldBe(1);
        inspectionArgCalls.ShouldBe(1);

        // back again -> the eager base; inspection is not re-materialized
        await navigator.BackAsync();
        Pump();

        navigator.CurrentUri.AbsolutePath.ShouldBe("/main");
        ProbePage.Created.OfType<InspectionProbe>().Count().ShouldBe(1);
        inspectionArgCalls.ShouldBe(1);

        window.Close();
    });

    #region Headless harness

    private static Task RunOnUiThread(Func<Task> body) =>
        HeadlessUnitTestSession
            .GetOrStartForAssembly(typeof(DeferredBackStackTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return 0;
            }, CancellationToken.None);

    private static (Navigator navigator, ShellView shell) CreateRestoreShell()
    {
        var registrar = new NavigationRegistrar();
        var navigator = new Navigator(
            registrar,
            new RelativeNavigateStrategy(registrar),
            new DefaultNavigationUpdateStrategy(new AvaloniaInside.Shell.Presenters.PresenterProvider()),
            new DefaultNavigationViewLocator());

        registrar.RegisterRoute("main", typeof(MainProbe), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("inspection", typeof(InspectionProbe), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("inspection/form", typeof(FormProbe), NavigationNodeType.Page, NavigateType.Normal, null);

        var shell = new ShellView(navigator);
        return (navigator, shell);
    }

    private static Window CreateWindow(ShellView shell) => new()
    {
        Width = 1280,
        Height = 800,
        Content = shell
    };

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

    #region Probe pages

    private class ProbePage : UserControl, INavigationLifecycle
    {
        public static readonly List<ProbePage> Created = new();

        public static void Reset() => Created.Clear();

        public object? Argument { get; private set; }
        public int InitialiseCount { get; private set; }
        public int AppearCount { get; private set; }

        protected ProbePage() => Created.Add(this);

        public Task InitialiseAsync(CancellationToken cancellationToken)
        {
            InitialiseCount++;
            return Task.CompletedTask;
        }

        public Task AppearAsync(CancellationToken cancellationToken)
        {
            AppearCount++;
            return Task.CompletedTask;
        }

        public Task ArgumentAsync(object args, CancellationToken cancellationToken)
        {
            Argument = args;
            return Task.CompletedTask;
        }

        public Task DisappearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task TerminateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MainProbe : ProbePage;
    private sealed class InspectionProbe : ProbePage;
    private sealed class FormProbe : ProbePage;

    #endregion
}
