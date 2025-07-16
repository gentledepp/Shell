using Avalonia.Animation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaInside.Shell;

public partial class Navigator : INavigator
{
    private readonly INavigateStrategy _navigateStrategy;
    private readonly INavigationUpdateStrategy _updateStrategy;
    private readonly INavigationViewLocator _viewLocator;
    private readonly NavigationStack _stack;
    private readonly Dictionary<NavigationChain, TaskCompletionSource<NavigateResult>> _waitingList = new();
    private readonly ConcurrentDictionary<NavigationChain, CancellationTokenSource> _navigationCancellationTokens = new();

    private bool _navigating;
    private ShellView? _shellView;
    private CancellationTokenSource _currentNav;
    
    // AsyncLocal approach for managing nested NavigateAndWaitAsync calls
    private static readonly AsyncLocal<Stack<CancellationTokenSource>> _nestedNavigationStack = new();
    
    private static Stack<CancellationTokenSource> NestedNavigationStack => 
        _nestedNavigationStack.Value ??= new Stack<CancellationTokenSource>();

    public ShellView ShellView => _shellView ?? throw new ArgumentNullException(nameof(ShellView));

    public Uri CurrentUri => _stack.Current?.Uri ?? Registrar.RootUri;

    public NavigationChain? CurrentChain => _stack.Current;

    public INavigationRegistrar Registrar { get; }

    public Navigator(
        INavigationRegistrar navigationRegistrar,
        INavigateStrategy navigateStrategy,
        INavigationUpdateStrategy updateStrategy,
        INavigationViewLocator viewLocator)
    {
        Registrar = navigationRegistrar;
        _navigateStrategy = navigateStrategy;
        _updateStrategy = updateStrategy;
        _viewLocator = viewLocator;
        _stack = new(viewLocator);

        _updateStrategy.HostItemChanged += UpdateStrategyOnHostItemChanged;
    }

    public void RegisterShell(ShellView shellView)
    {
        if (_shellView != null) throw new ArgumentException("Register shell can call only once");
        _shellView = shellView;
    }

    public bool HasItemInStack()
    {
        var current = _stack.Current?.Back;
        while (current != null)
        {
            if (current is not HostNavigationChain)
                return true;

            current = current.Back;
        }

        return false;
    }

    private async Task NotifyAsync(
        Uri origin,
        Uri newUri,
        object? argument,
        bool hasArgument,
        object? sender,
        NavigateType? navigateType,
        bool withAnimation,
        IPageTransition? overrideTransition,
        CancellationToken cancellationToken = default)
    {
        if (!Registrar.TryGetNode(newUri.AbsolutePath, out var node))
        {
            Debug.WriteLine("Warning: Cannot find the path");
            return;
        }

        var finalNavigateType =
            !origin.AbsolutePath.Equals(newUri.AbsolutePath) && Registrar.TryGetNode(origin.AbsolutePath, out var originalNode)
                ? navigateType ?? originalNode.Navigate
                : navigateType ?? node.Navigate;
        
        var fromPage = _stack.Current?.Instance as INavigatorLifecycle;
        if (fromPage != null)
        {
            var args = new NaviagatingEventArgs
            {
                Sender = sender,
                From = CurrentUri,
                FromUri = origin,
                ToUri = newUri,
                Argument = argument,
                Navigate = finalNavigateType,
                WithAnimation = withAnimation,
                OverrideTransition = overrideTransition
            };

            await fromPage.OnNavigatingAsync(args, cancellationToken);
            if (args.Cancel) return;

            //Check for overrides

            if (argument != args.Argument)
            {
                argument = args.Argument;
                hasArgument = true;
            }

            finalNavigateType = args.Navigate;
            withAnimation = args.WithAnimation;
            overrideTransition = args.OverrideTransition;
        }
        try
        {
            _navigating = true;

            var stackChanges = _stack.Push(
                node,
                finalNavigateType,
                newUri);

            foreach (var newChain in stackChanges.NewNavigationChains)
            {
                SetupPage(newChain);
            }

            await _updateStrategy.UpdateChangesAsync(
                ShellView,
                stackChanges,
                finalNavigateType,
                argument,
                hasArgument,
                cancellationToken);

            CheckWaitingList(stackChanges, argument, hasArgument);

            if (fromPage != null)
            {
                var args = new NaviagateEventArgs
                {
                    Sender = sender,
                    From = fromPage,
                    To = _stack.Current?.Instance,
                    FromUri = origin,
                    ToUri = newUri,
                    Argument = argument,
                    Navigate = finalNavigateType,
                    WithAnimation = withAnimation,
                    OverrideTransition = overrideTransition
                };

                await fromPage.OnNavigateAsync(args, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {

        }
        finally
        {
            _navigating = false;
        }
    }

    private void SetupPage(NavigationChain chain)
    {
	    if (chain.Instance is not Page page) return;

	    page.Shell = ShellView;
	    page.Chain = chain;
    }

    private async Task SwitchHostedItem(
        NavigationChain old,
        NavigationChain chain,
        bool withAnimation,
        IPageTransition? overrideTransition,
        CancellationToken cancellationToken = default)
    {
        var newUri =
            await _navigateStrategy.NavigateAsync(_stack.Current, CurrentUri, chain.Uri.AbsolutePath,
                cancellationToken);
        if (CurrentUri.AbsolutePath != newUri.AbsolutePath)
        {
            await NotifyAsync(newUri, newUri, null, false, null, NavigateType.HostedItemChange, withAnimation, overrideTransition, cancellationToken);
        }
    }

    public Task NavigateAsync(string path, CancellationToken cancellationToken = default) =>
        NavigateAsync(path, null, null, false, null, true, null, cancellationToken);

    public Task NavigateAsync(string path, object? argument, CancellationToken cancellationToken = default) =>
        NavigateAsync(path, null, argument, true, null, true, null, cancellationToken);

    public Task NavigateAsync(
        string path,
        NavigateType? navigateType,
        CancellationToken cancellationToken = default) =>
        NavigateAsync(path, navigateType, null, false, null, true, null, cancellationToken);

    public Task NavigateAsync(
        string path,
        NavigateType? navigateType,
        object? argument,
        CancellationToken cancellationToken = default) =>
        NavigateAsync(path, navigateType, argument, true, null, true, null, cancellationToken);
    public Task NavigateAsync(
        string path,
        NavigateType? navigateType,
        object? sender,
        bool withAnimation = true,
        IPageTransition? overrideTransition = null,
        CancellationToken cancellationToken = default) =>
        NavigateAndWaitAsync(path, navigateType, null, false, sender, withAnimation, overrideTransition, cancellationToken);
    public Task NavigateAsync(
        string path,
        NavigateType? navigateType,
        object? argument,
        object? sender,
        bool withAnimation,
        IPageTransition? overrideTransition = null,
        CancellationToken cancellationToken = default) =>
        NavigateAndWaitAsync(path, navigateType, argument, true, sender, withAnimation, overrideTransition, cancellationToken);

    private async Task NavigateAsync(
        string path,
        NavigateType? navigateType,
        object? argument,
        bool hasArgument,
        object? sender,
        bool withAnimation,
        IPageTransition? overrideTransition,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _currentNav = cts;

            var originalUri = new Uri(CurrentUri, path);
            var newUri = await _navigateStrategy.NavigateAsync(_stack.Current, CurrentUri, path, cts.Token);
            if (CurrentUri.AbsolutePath != newUri.AbsolutePath)
                await NotifyAsync(originalUri, newUri, argument, hasArgument, sender, navigateType, withAnimation,
                    overrideTransition, cts.Token);
        }
        finally
        {
            _currentNav = null;
        }
    }

    public Task BackAsync(CancellationToken cancellationToken = default) =>
        BackAsync(null, false, null, true, null, cancellationToken);
    public Task BackAsync(object? argument, CancellationToken cancellationToken = default) =>
        BackAsync(argument, true, null, true, null, cancellationToken);
    public Task BackAsync(
        object? sender,
        bool withAnimation = true,
        IPageTransition? overrideTransition = null,
        CancellationToken cancellationToken = default) => BackAsync(null, false, sender, withAnimation, overrideTransition, cancellationToken);
    public Task BackAsync(
        object? argument,
        object? sender,
        bool withAnimation,
        IPageTransition? overrideTransition = null,
        CancellationToken cancellationToken = default) => BackAsync(argument, true, sender, withAnimation, overrideTransition, cancellationToken);

    private async Task BackAsync(
        object? argument,
        bool hasArgument,
        object? sender,
        bool withAnimation,
        IPageTransition? overrideTransition,
        CancellationToken cancellationToken = default)
    {
        // Cancel current NavigateAsync if in progress
        if (_currentNav is { } cts && !cts.IsCancellationRequested)
        {
            await cts.CancelAsync();
        }

        // Cancel nested NavigateAndWaitAsync calls
        while (NestedNavigationStack.Count > 0)
        {
            var nestedCts = NestedNavigationStack.Pop();
            if (!nestedCts.IsCancellationRequested)
            {
                await nestedCts.CancelAsync();
            }
        }

        // Cancel any pending NavigateAndWaitAsync for current chain
        if (_stack.Current != null && _navigationCancellationTokens.TryRemove(_stack.Current, out var chainCts))
        {
            if (!chainCts.IsCancellationRequested)
            {
                await chainCts.CancelAsync();
            }
        }

        var newUri = await _navigateStrategy.BackAsync(_stack.Current, CurrentUri, cancellationToken);
        if (newUri != null && CurrentUri.AbsolutePath != newUri.AbsolutePath)
            await NotifyAsync(newUri, newUri, argument, hasArgument, sender, NavigateType.Pop, withAnimation, overrideTransition, cancellationToken);
    }

    public Task<NavigateResult> NavigateAndWaitAsync(string path, CancellationToken cancellationToken = default) =>
        NavigateAndWaitAsync(path, null, null, false, null, true, null, cancellationToken);

    public Task<NavigateResult> NavigateAndWaitAsync(
        string path,
        object? argument,
        CancellationToken cancellationToken = default) =>
        NavigateAndWaitAsync(path, null, argument, true, null, true, null, cancellationToken);

    public Task<NavigateResult> NavigateAndWaitAsync(
        string path,
        NavigateType navigateType,
        CancellationToken cancellationToken = default) =>
        NavigateAndWaitAsync(path, navigateType, null, false, null, true, null, cancellationToken);

    public Task<NavigateResult> NavigateAndWaitAsync(
        string path,
        object? argument,
        NavigateType navigateType,
        CancellationToken cancellationToken = default) =>
        NavigateAndWaitAsync(path, navigateType, argument, true, null, true, null, cancellationToken);
    public Task<NavigateResult> NavigateAndWaitAsync(
        string path,
        object? sender,
        NavigateType navigateType,
        bool withAnimation = true,
        IPageTransition? overrideTransition = null,
        CancellationToken cancellationToken = default) =>
        NavigateAndWaitAsync(path, navigateType, null, false, sender, withAnimation, overrideTransition, cancellationToken);
    public Task<NavigateResult> NavigateAndWaitAsync(
        string path,
        object? argument,
        object? sender,
        NavigateType navigateType,
        bool withAnimation,
        IPageTransition? overrideTransition = null,
        CancellationToken cancellationToken = default) =>
        NavigateAndWaitAsync(path, navigateType, argument, true, sender, withAnimation, overrideTransition, cancellationToken);

    private async Task<NavigateResult> NavigateAndWaitAsync(
        string path,
        NavigateType? navigateType,
        object? argument,
        bool hasArgument,
        object? sender,
        bool withAnimation,
        IPageTransition? overrideTransition,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Push to nested navigation stack for proper cancellation handling
        NestedNavigationStack.Push(cts);
        
        try
        {
            var originalUri = new Uri(CurrentUri, path);
            var newUri = await _navigateStrategy.NavigateAsync(_stack.Current, CurrentUri, path, cts.Token);
            if (CurrentUri.AbsolutePath == newUri.AbsolutePath)
                return new NavigateResult(false, null); // Or maybe we should throw exception.

            await NotifyAsync(originalUri, newUri, argument, hasArgument, sender, navigateType, withAnimation, overrideTransition, cts.Token);
            var chain = _stack.Current;

            if (!_waitingList.TryGetValue(chain, out var tcs))
                _waitingList[chain] = tcs = new TaskCompletionSource<NavigateResult>();

            // Store cancellation token for this chain
            _navigationCancellationTokens[chain] = cts;

            try
            {
                // Wait for navigation to complete or be cancelled
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                var cancellationTask = Task.Delay(Timeout.Infinite, combinedCts.Token);
                var navigationTask = tcs.Task;

                var completedTask = await Task.WhenAny(navigationTask, cancellationTask);
                
                if (completedTask == cancellationTask)
                {
                    // Navigation was cancelled
                    throw new OperationCanceledException(cts.Token);
                }

                return await navigationTask;
            }
            finally
            {
                _waitingList.Remove(chain);
                _navigationCancellationTokens.TryRemove(chain, out _);
            }
        }
        finally
        {
            // Remove from nested navigation stack
            if (NestedNavigationStack.Count > 0 && NestedNavigationStack.Peek() == cts)
            {
                NestedNavigationStack.Pop();
            }
        }
    }

    private void CheckWaitingList(
        NavigationStackChanges navigationStackChanges,
        object? argument,
        bool hasArgument)
    {
        if (navigationStackChanges.Removed == null) return;
        foreach (var chain in navigationStackChanges.Removed)
        {
            if (_waitingList.TryGetValue(chain, out var tcs))
                tcs.TrySetResult(new NavigateResult(hasArgument, argument));
            
            // Clean up cancellation tokens for removed chains
            _navigationCancellationTokens.TryRemove(chain, out _);
        }
    }

    private void UpdateStrategyOnHostItemChanged(object? sender, HostItemChangeEventArgs e)
    {
        if (e.OldChain != null && e.NewChain != e.OldChain && !_navigating)
        {
            _ = SwitchHostedItem(e.OldChain, e.NewChain, true, null);
        }
    }
}
