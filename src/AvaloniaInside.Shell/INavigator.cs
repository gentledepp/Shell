using Avalonia.Animation;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaInside.Shell;

/// <summary>
/// One entry for <see cref="INavigator.RestoreStackAsync"/>. <paramref name="Path"/> is a route
/// path (resolved against the registrar). When <paramref name="Deferred"/> is false the view is
/// created immediately; the front entry's <paramref name="ArgumentFactory"/> is awaited to supply
/// its argument. When <paramref name="Deferred"/> is true the entry is seeded without a view and
/// <paramref name="ArgumentFactory"/> is awaited only when the entry is first revealed on back.
/// </summary>
public sealed record RestoreStackEntry(
	string Path,
	bool Deferred = false,
	Func<CancellationToken, Task<object?>>? ArgumentFactory = null);

public interface INavigator
{
	Uri CurrentUri { get; }

	INavigationRegistrar Registrar { get; }

	NavigationChain? CurrentChain { get; }

	void RegisterShell(ShellView shellView);

	bool HasItemInStack();

	/// <summary>
	/// Seeds a back stack <b>on top of</b> the current stack and lands directly on the last (front)
	/// entry. Establish the desired base first (e.g. the tab shell via a <see cref="NavigateType.ReplaceRoot"/>
	/// navigation) - it is kept beneath the seeded entries and reachable on back. Entries are
	/// bottom-first; the front is presented with its argument (from its
	/// <see cref="RestoreStackEntry.ArgumentFactory"/>). Parent entries should be
	/// <see cref="RestoreStackEntry.Deferred"/>: they are seeded structurally and materialize - view
	/// created and <see cref="RestoreStackEntry.ArgumentFactory"/> awaited - only when first revealed
	/// on back. Used by cold-start navigation restore so the user lands on the view they were in
	/// without every parent being hydrated up front.
	/// </summary>
	Task RestoreStackAsync(
		System.Collections.Generic.IReadOnlyList<RestoreStackEntry> entries,
		CancellationToken cancellationToken = default);

	Task NavigateAsync(string path, CancellationToken cancellationToken = default);
	Task NavigateAsync(string path, object? argument, CancellationToken cancellationToken = default);
	Task NavigateAsync(
		string path,
		NavigateType? navigateType,
		CancellationToken cancellationToken = default);
	Task NavigateAsync(
		string path,
		NavigateType? navigateType,
		object? argument,
		CancellationToken cancellationToken = default);
    Task NavigateAsync(
		string path,
		NavigateType? navigateType,
		object? sender,
		bool withAnimation,
		IPageTransition? overrideTransition = null,
		CancellationToken cancellationToken = default);
    Task NavigateAsync(
		string path,
		NavigateType? navigateType,
		object? argument,
		object? sender,
		bool withAnimation,
		IPageTransition? overrideTransition = null,
		CancellationToken cancellationToken = default);


    Task BackAsync(CancellationToken cancellationToken = default);
	Task BackAsync(object? argument, CancellationToken cancellationToken = default);
    Task BackAsync(
		object? sender,
		bool withAnimation,
		IPageTransition? overrideTransition = null,
		CancellationToken cancellationToken = default);
    Task BackAsync(
		object? argument,
        object? sender,
        bool withAnimation,
        IPageTransition? overrideTransition = null,
        CancellationToken cancellationToken = default);

    Task<NavigateResult> NavigateAndWaitAsync(string path, CancellationToken cancellationToken = default);
	Task<NavigateResult> NavigateAndWaitAsync(
		string path,
		object? argument,
		CancellationToken cancellationToken = default);
	Task<NavigateResult> NavigateAndWaitAsync(
		string path,
		NavigateType navigateType,
		CancellationToken cancellationToken = default);
	Task<NavigateResult> NavigateAndWaitAsync(
		string path,
		object? argument,
		NavigateType navigateType,
		CancellationToken cancellationToken = default);
    Task<NavigateResult> NavigateAndWaitAsync(
		string path,
		object? sender,
		NavigateType navigateType,
		bool withAnimation,
		IPageTransition? overrideTransition = null,
		CancellationToken cancellationToken = default);
    Task<NavigateResult> NavigateAndWaitAsync(
		string path,
		object? argument,
        object? sender,
        NavigateType navigateType,
        bool withAnimation,
        IPageTransition? overrideTransition = null,
        CancellationToken cancellationToken = default);
}
