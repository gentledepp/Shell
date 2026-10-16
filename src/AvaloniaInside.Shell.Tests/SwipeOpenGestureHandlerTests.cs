using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace AvaloniaInside.Shell.Tests;

/// <summary>
/// A horizontal drag opens the pane - unless it starts on a control that uses the drag itself,
/// e.g. moving the caret in a TextBox. Dragged with a real mouse or finger in a headless window.
/// </summary>
public class SwipeOpenGestureHandlerTests
{
    [Theory]
    [InlineData(PointerType.Mouse)]
    [InlineData(PointerType.Touch)]
    public Task Drag_OnEmptyArea_OpensThePane(PointerType pointerType) => RunOnUiThread(() =>
    {
        var (window, host) = CreateWindow();
        var paneOpen = AttachHandler(host);

        Drag(window, new Point(300, 50), pointerType);

        paneOpen().ShouldBeTrue();
        window.Close();
    });

    [Theory]
    [InlineData(PointerType.Mouse)]
    [InlineData(PointerType.Touch)]
    public Task Drag_InATextBox_DoesNotOpenThePane(PointerType pointerType) => RunOnUiThread(() =>
    {
        var (window, host) = CreateWindow();
        var textBox = new TextBox { Text = "some text to select by dragging over it" };
        host.Children.Add(Place(textBox));
        var paneOpen = AttachHandler(host);

        Drag(window, new Point(150, 120), pointerType);

        paneOpen().ShouldBeFalse();
        window.Close();
    });

    [Theory]
    [InlineData(PointerType.Mouse)]
    [InlineData(PointerType.Touch)]
    public Task Drag_OnASlider_MovesTheSliderInsteadOfOpeningThePane(PointerType pointerType) => RunOnUiThread(() =>
    {
        var (window, host) = CreateWindow();
        host.Children.Add(Place(new Slider { Minimum = 0, Maximum = 100 }));
        var paneOpen = AttachHandler(host);

        Drag(window, new Point(150, 120), pointerType);

        paneOpen().ShouldBeFalse();
        window.Close();
    });

    [Theory]
    [InlineData(PointerType.Mouse)]
    [InlineData(PointerType.Touch)]
    public Task Drag_OnAControlMarkedAsSwipeExcluded_DoesNotOpenThePane(PointerType pointerType) => RunOnUiThread(() =>
    {
        var (window, host) = CreateWindow();
        var canvas = new Border { Background = Brushes.Gray, Child = new Border { Background = Brushes.Black, Margin = new Thickness(10) } };
        SwipeOpenGestureHandler.SetIsSwipeExcluded(canvas, true);
        host.Children.Add(Place(canvas));
        var paneOpen = AttachHandler(host);

        Drag(window, new Point(150, 120), pointerType);

        paneOpen().ShouldBeFalse();
        window.Close();
    });

    #region Test setup helpers

    private static Task RunOnUiThread(Action body) =>
        HeadlessUnitTestSession
            .GetOrStartForAssembly(typeof(SwipeOpenGestureHandlerTests).Assembly)
            .Dispatch(() =>
            {
                body();
                return 0;
            }, CancellationToken.None);

    private static (Window window, Canvas host) CreateWindow()
    {
        var host = new Canvas { Background = Brushes.White };
        var window = new Window { Width = 800, Height = 600, Content = host };
        window.Styles.Add(new FluentTheme());
        window.Show();
        Pump();
        return (window, host);
    }

    private static Control Place(Control control)
    {
        control.Width = 400;
        control.Height = 40;
        Canvas.SetLeft(control, 100);
        Canvas.SetTop(control, 100);
        return control;
    }

    private static Func<bool> AttachHandler(Control host)
    {
        var open = false;
        var handler = new SwipeOpenGestureHandler(
            hitTestArea: host,
            canSwipeOpen: () => !open,
            canSwipeClose: () => open,
            isPaneOpen: () => open,
            targetWidth: () => 250,
            setOpenPaneLength: _ => { },
            setPaneOpen: _ => { },
            commitState: o => open = o);
        handler.Attach();
        return () => open;
    }

    /// <summary>Drags the mouse or a finger 300px to the right, far enough to fully open the pane</summary>
    private static void Drag(Window window, Point start, PointerType pointerType)
    {
        if (pointerType == PointerType.Touch)
        {
            var finger = new Finger(window);
            finger.Down(start);
            for (var x = 20; x <= 300; x += 20)
                finger.Move(start + new Point(x, 0));
            finger.Up(start + new Point(300, 0));
        }
        else
        {
            window.MouseDown(start, MouseButton.Left);
            for (var x = 20; x <= 300; x += 20)
                window.MouseMove(start + new Point(x, 0));
            window.MouseUp(start + new Point(300, 0), MouseButton.Left);
        }

        // let the snap animation finish
        for (var i = 0; i < 30; i++)
        {
            Thread.Sleep(10);
            Pump();
        }
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Feeds raw touch input to a headless window. Avalonia.Headless only simulates the mouse and the raw touch
    /// types are not in Avalonia's reference assemblies, so this goes through the window's Input callback by reflection.
    /// </summary>
    private sealed class Finger
    {
        private static readonly Assembly AvaloniaBase = typeof(Visual).Assembly;
        private static readonly Type RawTouchEventArgsType = AvaloniaBase.GetType("Avalonia.Input.Raw.RawTouchEventArgs", true)!;
        private static readonly Type RawPointerEventTypeType = AvaloniaBase.GetType("Avalonia.Input.Raw.RawPointerEventType", true)!;
        private static readonly object TouchDevice = Activator.CreateInstance(AvaloniaBase.GetType("Avalonia.Input.TouchDevice", true)!)!;

        private static long _nextId = 1000;
        private static ulong _timestamp = 1;

        private readonly Delegate _input;
        private readonly object _inputRoot;
        private readonly long _id = Interlocked.Increment(ref _nextId);

        public Finger(Window window)
        {
            var impl = typeof(TopLevel)
                .GetProperty("PlatformImpl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(window)!;
            _input = (Delegate)impl.GetType().GetProperty("Input")!.GetValue(impl)!;
            _inputRoot = impl.GetType().GetProperty("InputRoot")!.GetValue(impl)!;
        }

        public void Down(Point point) => Send("TouchBegin", point);
        public void Move(Point point) => Send("TouchUpdate", point);
        public void Up(Point point) => Send("TouchEnd", point);

        private void Send(string type, Point point)
        {
            // lay out first, as the headless mouse helpers do, so the touch hits controls added since the last pump
            Pump();
            var args = Activator.CreateInstance(RawTouchEventArgsType,
                TouchDevice, _timestamp++, _inputRoot, Enum.Parse(RawPointerEventTypeType, type), point, RawInputModifiers.None, _id)!;
            _input.DynamicInvoke(args);
            Pump();
        }
    }

    #endregion
}
