namespace AvaloniaInside.Shell;

/// <summary>
/// Implemented by a navigation target whose transient UI state is restored after a cold-start
/// restore. The navigator calls <see cref="Restore"/> with the matching
/// <see cref="RestoreStackEntry.RestoreState"/>, after the navigation argument and before init.
/// </summary>
public interface IRestorable
{
	void Restore(object? state);
}
