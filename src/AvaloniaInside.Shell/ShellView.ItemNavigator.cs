using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Metadata;
using AvaloniaInside.Shell.Data;

namespace AvaloniaInside.Shell;

public partial class ShellView
{
	[Content] public AvaloniaList<IItem> Items { get; } = new();

	private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				if (e.NewItems == null) break;
				if (e.NewStartingIndex >= 0 && e.NewStartingIndex < Items.Count - e.NewItems.Count)
				{
					// Insert at specific index
					var sideMenuIndex = Items.Take(e.NewStartingIndex).OfType<SideMenuItem>().Count();
					foreach (var item in e.NewItems.Cast<IItem>())
					{
						if (item is SideMenuItem sideMenuItem)
							_sideMenuItems.Insert(sideMenuIndex++, sideMenuItem);
						else
							OnAddItem(item);
					}
				}
				else
				{
					foreach (var item in e.NewItems.Cast<IItem>())
						OnAddItem(item);
				}
				break;

			case NotifyCollectionChangedAction.Remove:
				if (e.OldItems == null) break;
				foreach (var item in e.OldItems.Cast<IItem>())
				{
					if (item is SideMenuItem sideMenuItem)
						_sideMenuItems.Remove(sideMenuItem);
				}
				break;

			default:
				throw new NotSupportedException($"Collection action '{e.Action}' is not supported");
		}
	}

	protected virtual void OnAddItem(IItem item)
	{
		switch (item)
		{
			case Host host:
				AddRoute(host, string.Empty);
				break;
			case Route route:
				AddRoute(route, string.Empty);
				break;
			case SideMenuItem sideMenuItem:
				_sideMenuItems.Add(sideMenuItem);
				break;
		}
	}

	private void AddRoute(Route route, string basePath)
	{
		var path = $"{basePath}/{route.Path}";
		var host = route as Host;

		if (host != null && !HostedItemsHelper.CanBeHosted(host.Page))
			throw new AggregateException("Host must inherits from ItemsControl");

		Navigator.Registrar.RegisterRoute(
			path,
			route.Page,
			host == null ? NavigationNodeType.Page : NavigationNodeType.Host,
			route.Type,
			host?.Default);

		foreach (var subRoute in route.Routes)
			AddRoute(subRoute, path);
	}
}
