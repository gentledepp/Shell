using System;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaInside.Shell;

public interface INavigationService
{
	bool HasItemInStack();
	void RegisterPage(string route, Type page, NavigateType navigate);
	void RegisterHost(string route, Type page, string defaultPath, NavigateType navigate);

	Task NavigateAsync(string path, CancellationToken cancellationToken = default);
	Task NavigateAsync(string path, object? argument, CancellationToken cancellationToken = default);
	Task NavigateAsync(string path, NavigateType? navigateType, object? argument,
		CancellationToken cancellationToken = default);
	Task BackAsync(CancellationToken cancellationToken = default);
	Task BackAsync(object? argument, CancellationToken cancellationToken = default);
}
