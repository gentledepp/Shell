using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaInside.Shell;

namespace ShellExample.Views;

public partial class CancellationHelperPage : Page
{
    public CancellationHelperPage()
    {
        InitializeComponent();
    }

    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        Debug.WriteLine("CancellationHelperPage.InitialiseAsync: Starting initialization");
        
        try
        {
            // This helper page cancels the NavigateAndWaitAsync chain by calling BackAsync
            if (Navigator != null)
            {
                Debug.WriteLine("CancellationHelperPage.InitialiseAsync: Calling Navigator.BackAsync to cancel NavigateAndWaitAsync chain");
                await Navigator.BackAsync();
                Debug.WriteLine("CancellationHelperPage.InitialiseAsync: NavigateAndWaitAsync chain cancelled successfully");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CancellationHelperPage.InitialiseAsync: Exception occurred: {ex.Message}");
            throw;
        }
        
        await base.InitialiseAsync(cancellationToken);
        Debug.WriteLine("CancellationHelperPage.InitialiseAsync: Completed");
    }

    public override async Task AppearAsync(CancellationToken cancellationToken)
    {
        Debug.WriteLine("CancellationHelperPage.AppearAsync: This should not be called if navigation is cancelled");
        await base.AppearAsync(cancellationToken);
    }
}