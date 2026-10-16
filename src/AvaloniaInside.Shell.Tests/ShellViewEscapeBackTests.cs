using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaInside.Shell.Presenters;

namespace AvaloniaInside.Shell.Tests;

/// <summary>
/// Esc navigates back on its key up - unless its key down was used by something else, e.g. a page's
/// key binding that closes a search, or an open dropdown. Pressed as real keys in a headless window.
/// </summary>
public class ShellViewEscapeBackTests
{
    [Fact]
    public Task Escape_NavigatesBack() => RunOnUiThread(async () =>
    {
        var (navigator, window) = await CreateShellOnDetailsPage();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();

        navigator.CurrentUri.AbsolutePath.ShouldBe("/home");
        window.Close();
    });

    [Fact]
    public Task Escape_WhoseKeyDownWasHandled_StaysOnThePage() => RunOnUiThread(async () =>
    {
        var (navigator, window) = await CreateShellOnDetailsPage();
        var closedSearches = 0;
        window.KeyBindings.Add(EscapeBinding(() => closedSearches++));

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();

        closedSearches.ShouldBe(1);
        navigator.CurrentUri.AbsolutePath.ShouldBe("/details");
        window.Close();
    });

    [Fact]
    public Task Escape_HeldDown_WhoseFirstKeyDownWasHandled_StaysOnThePage() => RunOnUiThread(async () =>
    {
        var (navigator, window) = await CreateShellOnDetailsPage();
        var searchOpen = true;
        window.KeyBindings.Add(EscapeBinding(() => searchOpen = false, () => searchOpen));

        // the key repeats while held: the search is closed by the first key down, the others are not used
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();

        searchOpen.ShouldBeFalse();
        navigator.CurrentUri.AbsolutePath.ShouldBe("/details");
        window.Close();
    });

    [Fact]
    public Task Escape_AfterAHandledEscapeWhoseKeyUpNeverCame_NavigatesBack() => RunOnUiThread(async () =>
    {
        var (navigator, window) = await CreateShellOnDetailsPage();
        var searchOpen = true;
        window.KeyBindings.Add(EscapeBinding(() => searchOpen = false, () => searchOpen));

        // the key up of the first Esc got lost (e.g. the window lost focus), then the user types on
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.None);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();

        navigator.CurrentUri.AbsolutePath.ShouldBe("/home");
        window.Close();
    });

    #region Test setup helpers

    private static Task RunOnUiThread(Func<Task> body) =>
        HeadlessUnitTestSession
            .GetOrStartForAssembly(typeof(ShellViewEscapeBackTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return 0;
            }, CancellationToken.None);

    private static async Task<(Navigator navigator, Window window)> CreateShellOnDetailsPage()
    {
        var registrar = new NavigationRegistrar();
        var navigator = new Navigator(
            registrar,
            new RelativeNavigateStrategy(registrar),
            new DefaultNavigationUpdateStrategy(new PresenterProvider()),
            new DefaultNavigationViewLocator());

        registrar.RegisterRoute("home", typeof(TestPage), NavigationNodeType.Page, NavigateType.Normal, null);
        registrar.RegisterRoute("details", typeof(TestPage), NavigationNodeType.Page, NavigateType.Normal, null);

        var shell = new ShellView(navigator);
        var window = new Window
        {
            // large enough that Back() skips the small-screen side-menu branch
            Width = 1280,
            Height = 800,
            Content = shell
        };
        window.Show();
        PumpUntilLoaded(shell);

        await navigator.NavigateAsync("/home");
        await navigator.NavigateAsync("/details");
        Pump();

        return (navigator, window);
    }

    private static KeyBinding EscapeBinding(Action execute, Func<bool>? canExecute = null) => new()
    {
        Gesture = new KeyGesture(Key.Escape),
        Command = new DelegateCommand(execute, canExecute ?? (() => true))
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

    private class TestPage : UserControl
    {
    }

    private sealed class DelegateCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public bool CanExecute(object? parameter) => canExecute();
        public void Execute(object? parameter) => execute();

#pragma warning disable 67 // Event not used
        public event EventHandler? CanExecuteChanged;
#pragma warning restore 67
    }

    #endregion
}
