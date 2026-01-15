using Android.App;
using Android.Runtime;
using Avalonia.Android;
using ShellExample;
using Avalonia;
using ReactiveUI.Avalonia;
using AvaloniaInside.Shell;

[Application]
public class Application : AvaloniaAndroidApplication<App>
{
    protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            //.WithInterFont()
            .UseReactiveUI()
            .UseShell();
    }
}
