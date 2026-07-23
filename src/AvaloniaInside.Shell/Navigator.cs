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

    private bool _navigating;
    private ShellView? _shellView;
    private CancellationTokenSource? _currentNavigationCancellationToken;

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

    public Task RestoreStackAsync(
        IReadOnlyList<RestoreStackEntry> entries,
        CancellationToken cancellationToken = default) =>
        RestoreStackCoreAsync(null, entries, cancellationToken);

    public Task RestoreStackAsync(
        string basePath,
        IReadOnlyList<RestoreStackEntry> entries,
        CancellationToken cancellationToken = default) =>
        RestoreStackCoreAsync(basePath, entries, cancellationToken);

    private async Task RestoreStackCoreAsync(
        string? basePath,
        IReadOnlyList<RestoreStackEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0) return;

        // Resolve each path to the registrar's node + uri so the seeded chain nodes are the same
        // instances that Back/Pop later look up by uri (Pop matches chains by node reference).
        var seeds = new List<RestoreSeedEntry>(entries.Count);
        foreach (var entry in entries)
        {
            var uri = new Uri(Registrar.RootUri, entry.Path);
            if (!Registrar.TryGetNode(uri.AbsolutePath, out var node))
            {
                Debug.WriteLine($"Warning: RestoreStackAsync cannot find path '{entry.Path}'");
                return;
            }

            seeds.Add(new RestoreSeedEntry(node, uri, entry.Deferred, entry.ArgumentFactory));
        }

        try
        {
            _navigating = true;

            // Replace the root with the base (e.g. the tab shell) in the stack only - the base
            // control is mounted silently beneath whatever is visible (e.g. the splash), so the
            // user keeps looking at the current page until the front is ready to present.
            NavigationStackChanges? baseChanges = null;
            if (basePath != null)
            {
                // resolve like NavigateAsync would, incl. a host's default-node redirect
                var baseUri = await _navigateStrategy.NavigateAsync(
                    _stack.Current, CurrentUri, basePath, cancellationToken);
                if (!Registrar.TryGetNode(baseUri.AbsolutePath, out var baseNode))
                {
                    Debug.WriteLine($"Warning: RestoreStackAsync cannot find base path '{basePath}'");
                    return;
                }

                baseChanges = _stack.Push(baseNode, NavigateType.ReplaceRoot, baseUri);

                if (_stack.Current is { } baseChain
                    && HostedItemsHelper.GetHostControl(baseChain) is Avalonia.Controls.Control baseControl
                    && ShellView.ContentView is { } cv
                    && !cv.Children.Contains(baseControl))
                {
                    cv.AddSilentBase(baseControl);
                }
            }

            var changes = _stack.SeedRestore(seeds);

            // the base chains initialise together with the eager seeded ones; the base never
            // appeared, so it must not get a Disappear when the front presents
            if (baseChanges != null)
            {
                for (var i = 0; i < baseChanges.NewNavigationChains.Count; i++)
                    changes.NewNavigationChains.Insert(i, baseChanges.NewNavigationChains[i]);
                changes.Previous = null;
            }

            foreach (var newChain in changes.NewNavigationChains)
                SetupPage(newChain);

            // The front is the last entry; resolve its argument eagerly for delivery.
            object? frontArgument = null;
            var hasFrontArgument = false;
            if (entries[^1].ArgumentFactory is { } frontFactory)
            {
                frontArgument = await frontFactory(cancellationToken);
                hasFrontArgument = true;
            }

            // Present the front on top of the base (kept in the tree) and deliver its argument.
            // Deferred parents stay out of the visual tree until first revealed on back.
            await _updateStrategy.UpdateChangesAsync(
                ShellView, changes, NavigateType.Normal, frontArgument, hasFrontArgument, cancellationToken);

            // Only now that the front covers the screen, drop what the base replacement removed
            // (e.g. the splash) - it sat beneath the front, so the removal is invisible.
            if (baseChanges?.Removed is { Count: > 0 })
            {
                await _updateStrategy.UpdateChangesAsync(
                    ShellView,
                    new NavigationStackChanges { Removed = baseChanges.Removed },
                    NavigateType.ReplaceRoot, null, false, cancellationToken);
            }
        }
        finally
        {
            _navigating = false;
        }
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

            // A deferred back-stack entry (seeded by RestoreStackAsync) is materialized the first
            // time it becomes the front - i.e. when the user navigates back to it. Create its view
            // and resolve its argument now (load-before-reveal) so it appears already populated.
            if (stackChanges.Front is { IsDeferred: true } deferredFront)
            {
                deferredFront.Instance = _viewLocator.GetView(deferredFront.Node);
                deferredFront.IsDeferred = false;
                stackChanges.NewNavigationChains.Add(deferredFront);

                if (deferredFront.DeferredArgumentFactory is { } factory)
                {
                    argument = await factory(cancellationToken);
                    hasArgument = true;
                    deferredFront.DeferredArgumentFactory = null;
                }
            }

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
            _currentNavigationCancellationToken = cts;

            var originalUri = new Uri(CurrentUri, path);
            var newUri = await _navigateStrategy.NavigateAsync(_stack.Current, CurrentUri, path, cts.Token);
            if (CurrentUri.AbsolutePath != newUri.AbsolutePath)
                await NotifyAsync(originalUri, newUri, argument, hasArgument, sender, navigateType, withAnimation,
                    overrideTransition, cts.Token);
        }
        finally
        {
            _currentNavigationCancellationToken = null;
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
        if (_currentNavigationCancellationToken is { } cts && !_currentNavigationCancellationToken.IsCancellationRequested)
            await cts.CancelAsync();

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
        
        _currentNavigationCancellationToken = cts;
        NavigationChain? chain = null;
        TaskCompletionSource<NavigateResult>? tcs = null;

        try
        {

            var originalUri = new Uri(CurrentUri, path);

            var newUri = await _navigateStrategy.NavigateAsync(_stack.Current, CurrentUri, path, cts.Token);
            if (CurrentUri.AbsolutePath == newUri.AbsolutePath)
                return new NavigateResult(false, null); // Or maybe we should throw exception.

            await NotifyAsync(originalUri, newUri, argument, hasArgument, sender, navigateType, withAnimation,
                overrideTransition, cts.Token);
            
            chain = _stack.Current;

            if (!_waitingList.TryGetValue(chain, out tcs))
                _waitingList[chain] = tcs = new TaskCompletionSource<NavigateResult>();
        }
        finally
        {
            if (cts.IsCancellationRequested)
                tcs?.TrySetCanceled();

            _currentNavigationCancellationToken = null;
        }

        
        try
        {
            return await tcs.Task;
        }
        finally
        {
            _waitingList.Remove(chain);
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
