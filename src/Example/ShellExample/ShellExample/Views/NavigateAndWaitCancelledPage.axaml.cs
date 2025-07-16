using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaInside.Shell;

namespace ShellExample.Views;

public partial class NavigateAndWaitCancelledPage : Page
{
    public NavigateAndWaitCancelledPage()
    {
        InitializeComponent();
    }

    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        Debug.WriteLine("NavigateAndWaitCancelledPage.InitialiseAsync: Starting initialization");
        
        try
        {
            // This demonstrates nested NavigateAndWaitAsync cancellation
            // We'll navigate to a helper page that will cancel the navigation
            if (Navigator != null)
            {
                Debug.WriteLine("NavigateAndWaitCancelledPage.InitialiseAsync: Calling NavigateAndWaitAsync to helper page");
                
                // This NavigateAndWaitAsync call should be cancelled by the helper page
                var result = await Navigator.NavigateAndWaitAsync("/cancellation-helper");
                
                // If we reach here, the cancellation didn't work as expected
                Debug.WriteLine($"NavigateAndWaitCancelledPage.InitialiseAsync: NavigateAndWaitAsync returned: {result}");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("NavigateAndWaitCancelledPage.InitialiseAsync: Navigation was cancelled as expected");
            throw; // Re-throw to ensure proper cancellation handling
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NavigateAndWaitCancelledPage.InitialiseAsync: Unexpected exception: {ex.Message}");
            throw;
        }
        
        await base.InitialiseAsync(cancellationToken);
        Debug.WriteLine("NavigateAndWaitCancelledPage.InitialiseAsync: Completed");
    }

    public override async Task AppearAsync(CancellationToken cancellationToken)
    {
        Debug.WriteLine("NavigateAndWaitCancelledPage.AppearAsync: This should not be called if navigation is cancelled");
        await base.AppearAsync(cancellationToken);
    }
}