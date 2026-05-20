using Android.App;
using Avalonia;
using Avalonia.Android;
using ReactiveUI.Avalonia;
using AvaloniaInside.Shell;
using Android.Runtime;

namespace ShellBottomCustomNavigator.Android;

[Application]
public class Application : AvaloniaAndroidApplication<App>
{
    protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseShell()
            .UseReactiveUI(_ => { });
    }
}
