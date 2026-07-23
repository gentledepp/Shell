using Avalonia;
using Avalonia.Headless;
using AvaloniaInside.Shell.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace AvaloniaInside.Shell.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<HeadlessTestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class HeadlessTestApp : Application
{
}
