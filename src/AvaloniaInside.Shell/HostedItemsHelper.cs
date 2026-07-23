using System;
using System.Collections;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AvaloniaInside.Shell;

public static class HostedItemsHelper
{
	private class ItemsControlProxy(ItemsControl itemsControl) : IHostItems
	{
		public IEnumerable? ItemsSource
		{
			get => itemsControl.ItemsSource;
			set => itemsControl.ItemsSource = value;
		}

		public ItemCollection Items => itemsControl.Items;
	}

	private class SelectingItemsControlProxy(SelectingItemsControl itemsControl)
		: ItemsControlProxy(itemsControl), ISelectableHostItems
	{
		public event EventHandler<SelectionChangedEventArgs>? SelectionChanged
		{
			add => itemsControl.SelectionChanged += value;
			remove => itemsControl.SelectionChanged -= value;
		}

		public object? SelectedItem
		{
			get => itemsControl.SelectedItem;
			set => itemsControl.SelectedItem = value;
		}
	}

	public static bool CanBeHosted(Type viewType) =>
		viewType.IsSubclassOf(typeof(ItemsControl)) || typeof(IHostItems).IsAssignableFrom(viewType);

	public static bool CanBeHosted(object view) =>
		view is ItemsControl or SelectingItemsControl or IHostItems or ISelectableHostItems;

	public static IHostItems? GetHostedItems(object? control)
	{
		if (GetSelectableHostedItems(control) is { } casted) return casted;

		if (control is IHostItems hostedItems)
			return hostedItems;
		if (control is ItemsControl itemsControl)
			return new ItemsControlProxy(itemsControl);

		return null;
	}

	public static ISelectableHostItems? GetSelectableHostedItems(object? control)
	{
		if (control is ISelectableHostItems selectableHostedItem)
			return selectableHostedItem;
		if (control is SelectingItemsControl selectingItemsControl)
			return new SelectingItemsControlProxy(selectingItemsControl);

		return null;
	}

	/// <summary>
	/// Resolves the control that actually goes into the content stack for <paramref name="chain"/>:
	/// the chain's own view for plain pages, or the outermost host control (e.g. the tab page) for
	/// hosted chains - populating the host's items and selecting the chain on the way up.
	/// </summary>
	public static object GetHostControl(NavigationChain chain)
	{
		if (!chain.Hosted)
			return chain.Instance;

		var current = chain;
		while (current != null)
		{
			if (current.Back is HostNavigationChain parent &&
			    GetHostedItems(current.Back?.Instance) is { } hostedItems)
			{
				if ((hostedItems.Items ?? hostedItems.ItemsSource) is not IList collection)
				{
					hostedItems.ItemsSource = collection = new AvaloniaList<object>();
				}

				foreach (var hostedChildChain in parent.Nodes.Where(hostedChildChain =>
					         !collection.Contains(hostedChildChain)))
				{
					collection.Add(hostedChildChain);
				}

				if (hostedItems is ISelectableHostItems selectingItemsControl)
					selectingItemsControl.SelectedItem = current;
			}
			else
			{
				break;
			}

			current = current.Back;
		}

		return current?.Instance ?? chain.Instance;
	}
}
