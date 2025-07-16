using System.Threading;
using System.Threading.Tasks;
using AvaloniaInside.Shell;

namespace ShellExample.Views;

public partial class NavigationCancellationDemoPage : Page
{
    public NavigationCancellationDemoPage()
    {
        InitializeComponent();
    }

    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        // This page doesn't cancel its own navigation - it's just a demo page
        await base.InitialiseAsync(cancellationToken);
    }
}