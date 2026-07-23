using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaInside.Shell.Presenters;

public abstract class PresenterBase : IPresenter
{
	protected object GetHostControl(NavigationChain chain) =>
		HostedItemsHelper.GetHostControl(chain);

	public abstract Task PresentAsync(ShellView shellView, NavigationChain chain, NavigateType navigateType,
        CancellationToken cancellationToken);
}
