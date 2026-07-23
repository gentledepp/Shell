using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace AvaloniaInside.Shell;

public class DefaultNavigationUpdateStrategy : INavigationUpdateStrategy
{
	private readonly IPresenterProvider _presenterProvider;

	public DefaultNavigationUpdateStrategy(IPresenterProvider presenterProvider)
	{
		_presenterProvider = presenterProvider;
	}

	public event EventHandler<HostItemChangeEventArgs>? HostItemChanged;

	public async Task UpdateChangesAsync(
		ShellView shellView,
		NavigationStackChanges changes,
		NavigateType navigateType,
		object? argument,
		bool hasArgument,
		CancellationToken cancellationToken)
	{
		var isSame = changes.Previous == changes.Front;

		foreach (var instance in changes.NewNavigationChains.Select(s => s.Instance))
		{
			if (instance is INavigationLifecycle navigationLifecycle)
				await navigationLifecycle.InitialiseAsync(cancellationToken);

			SubscribeForUpdateIfNeeded(instance);
		}

		if (changes.Previous?.Instance is INavigationLifecycle oldInstanceLifecycle && !isSame)
			await oldInstanceLifecycle.DisappearAsync(cancellationToken);

		// A deferred back-stack entry just materialized on back was never added to the visual tree.
		// Insert it directly beneath the outgoing page so the pop reveal-transition brings it forward.
		// Only freshly materialized chains qualify (on a Pop the front is in NewNavigationChains
		// exactly when it was just materialized): any other front missing from the content stack is
		// hosted - its view lives inside its host control (e.g. a tab page, which is the actual
		// reveal target already in the stack) and re-parenting it would throw.
		if (navigateType == NavigateType.Pop
			&& changes.Front is { } front
			&& changes.NewNavigationChains.Contains(front)
			&& front.Instance is Control materializedFront
			&& shellView.ContentView is { } contentView
			&& !contentView.Children.Contains(materializedFront))
		{
			contentView.InsertBeneathCurrent(materializedFront);
		}

		if (changes.Removed != null)
			await InvokeRemoveAsync(shellView, changes.Removed, changes.Previous, navigateType, cancellationToken);

		if (changes.Front?.Instance is INavigationLifecycle newInstanceLifecycle)
		{
			if (!isSame)
				await newInstanceLifecycle.AppearAsync(cancellationToken);

			if (hasArgument)
				await newInstanceLifecycle.ArgumentAsync(argument, cancellationToken);
		}

		if (!isSame && changes.Front != null && navigateType != NavigateType.Pop)
			await _presenterProvider.For(navigateType).PresentAsync(shellView, changes.Front, navigateType, cancellationToken);
	}

	private async Task InvokeRemoveAsync(ShellView shellView,
        IList<NavigationChain> removed,
        NavigationChain? previous,
        NavigateType navigateType,
        CancellationToken cancellationToken)
	{
		var presenter = _presenterProvider.Remove();
		foreach (var chain in removed)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (previous == chain)
				await _presenterProvider.Remove().PresentAsync(shellView, previous, navigateType, cancellationToken);
			else
				await presenter.PresentAsync(shellView, chain, navigateType, cancellationToken);

			if (chain.Instance is INavigationLifecycle lifecycle)
				await lifecycle.TerminateAsync(cancellationToken);

			UnSubscribeForUpdateIfNeeded(chain.Instance);
		}
	}

	private void SubscribeForUpdateIfNeeded(object? instance)
	{
		if (HostedItemsHelper.GetSelectableHostedItems(instance) is {} hosted)
			hosted.SelectionChanged += SelectingItemsControlOnSelectionChanged;

	}

	private void UnSubscribeForUpdateIfNeeded(object instance)
	{
		if (HostedItemsHelper.GetSelectableHostedItems(instance) is {} hosted)
			hosted.SelectionChanged -= SelectingItemsControlOnSelectionChanged;
	}

	private void SelectingItemsControlOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems?.Count > 0 && e.AddedItems[0] is NavigationChain chain)
		{
			HostItemChanged?.Invoke(this, new HostItemChangeEventArgs(
				e.RemovedItems?.Count > 0 ? e.RemovedItems[0] as NavigationChain : null,
				chain));
		}
	}
}
