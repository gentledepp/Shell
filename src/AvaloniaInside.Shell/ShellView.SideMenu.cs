using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AvaloniaInside.Shell.Data;

namespace AvaloniaInside.Shell;

public partial class ShellView
{
	private readonly AvaloniaList<SideMenuItem> _sideMenuItems = new();

	private bool _skipChanges = false;

	#region Properties

	public double SideMenuSize => ScreenSize == ScreenSizeType.Small ? Math.Min(Bounds.Width - 35, DefaultSideMenuSize) : DefaultSideMenuSize;

	public static readonly StyledProperty<double> DefaultSideMenuSizeProperty = AvaloniaProperty.Register<ShellView,double>(nameof(DefaultSideMenuSize), 250);

	#region SwipeToOpenSideMenuEnabled

	public static readonly StyledProperty<bool> SwipeToOpenSideMenuEnabledProperty =
		AvaloniaProperty.Register<ShellView, bool>(nameof(SwipeToOpenSideMenuEnabled), defaultValue: true);

	public bool SwipeToOpenSideMenuEnabled
	{
		get => GetValue(SwipeToOpenSideMenuEnabledProperty);
		set => SetValue(SwipeToOpenSideMenuEnabledProperty, value);
	}

	#endregion

	public double DefaultSideMenuSize
    {
		get { return GetValue(DefaultSideMenuSizeProperty);}
		set { SetValue(DefaultSideMenuSizeProperty, value); }
	}

	#region SideMenuPresented

	private bool _sideMenuPresented;
	public static readonly DirectProperty<ShellView, bool> SideMenuPresentedProperty =
		AvaloniaProperty.RegisterDirect<ShellView, bool>(
			nameof(SideMenuPresented),
			o => o.SideMenuPresented,
			(o, v) => o.SideMenuPresented = v);
	public bool SideMenuPresented
	{
		get => _sideMenuPresented;
		set
		{
			if (SetAndRaise(SideMenuPresentedProperty, ref _sideMenuPresented, value))
				UpdateSideMenu();
		}
	}

	#endregion

	#region LargeScreenSideMenuBehave

	private SideMenuBehaveType _largeScreenSideMenuBehave = SideMenuBehaveType.Keep;
	public static readonly DirectProperty<ShellView, SideMenuBehaveType> LargeScreenSideMenuBehaveProperty =
		AvaloniaProperty.RegisterDirect<ShellView, SideMenuBehaveType>(
			nameof(SideMenuBehaveType),
			o => o.LargeScreenSideMenuBehave,
			(o, v) => o.LargeScreenSideMenuBehave = v);
	public SideMenuBehaveType LargeScreenSideMenuBehave
	{
		get => _largeScreenSideMenuBehave;
		set
		{
			if (SetAndRaise(LargeScreenSideMenuBehaveProperty, ref _largeScreenSideMenuBehave, value))
				UpdateSideMenu();
		}
	}

	#endregion

	#region MediumScreenSideMenuBehave

	private SideMenuBehaveType _mediumScreenSideMenuBehave = SideMenuBehaveType.Default;
	public static readonly DirectProperty<ShellView, SideMenuBehaveType> MediumScreenSideMenuBehaveProperty =
		AvaloniaProperty.RegisterDirect<ShellView, SideMenuBehaveType>(
			nameof(SideMenuBehaveType),
			o => o.MediumScreenSideMenuBehave,
			(o, v) => o.MediumScreenSideMenuBehave = v);
	public SideMenuBehaveType MediumScreenSideMenuBehave
	{
		get => _mediumScreenSideMenuBehave;
		set
		{
			if (SetAndRaise(MediumScreenSideMenuBehaveProperty, ref _mediumScreenSideMenuBehave, value))
				UpdateSideMenu();
		}
	}

	#endregion

	#region SmallScreenSideMenuBehave

	private SideMenuBehaveType _smallScreenSideMenuBehave = SideMenuBehaveType.Default;
	public static readonly DirectProperty<ShellView, SideMenuBehaveType> SmallScreenSideMenuBehaveProperty =
		AvaloniaProperty.RegisterDirect<ShellView, SideMenuBehaveType>(
			nameof(SideMenuBehaveType),
			o => o.SmallScreenSideMenuBehave,
			(o, v) => o.SmallScreenSideMenuBehave = v);
	public SideMenuBehaveType SmallScreenSideMenuBehave
	{
		get => _smallScreenSideMenuBehave;
		set
		{
			if (SetAndRaise(SmallScreenSideMenuBehaveProperty, ref _smallScreenSideMenuBehave, value))
				UpdateSideMenu();
		}
	}

	#endregion

	#region LargeScreenSideMenuMode

	public static readonly StyledProperty<SplitViewDisplayMode> LargeScreenSideMenuModeProperty =
		AvaloniaProperty.Register<ShellView, SplitViewDisplayMode>(
			nameof(LargeScreenSideMenuMode),
			defaultValue: SplitViewDisplayMode.Inline);

	public SplitViewDisplayMode LargeScreenSideMenuMode
	{
		get => GetValue(LargeScreenSideMenuModeProperty);
		set => SetValue(LargeScreenSideMenuModeProperty, value);
	}

	#endregion

	#region MediumScreenSideMenuMode

	public static readonly StyledProperty<SplitViewDisplayMode> MediumScreenSideMenuModeProperty =
		AvaloniaProperty.Register<ShellView, SplitViewDisplayMode>(
			nameof(MediumScreenSideMenuMode),
			defaultValue: SplitViewDisplayMode.CompactInline);

	public SplitViewDisplayMode MediumScreenSideMenuMode
	{
		get => GetValue(MediumScreenSideMenuModeProperty);
		set => SetValue(MediumScreenSideMenuModeProperty, value);
	}

	#endregion

	#region SmallScreenSideMenuMode

	public static readonly StyledProperty<SplitViewDisplayMode> SmallScreenSideMenuModeProperty =
		AvaloniaProperty.Register<ShellView, SplitViewDisplayMode>(
			nameof(SmallScreenSideMenuMode),
			defaultValue: SplitViewDisplayMode.Overlay);

	public SplitViewDisplayMode SmallScreenSideMenuMode
	{
		get => GetValue(SmallScreenSideMenuModeProperty);
		set => SetValue(SmallScreenSideMenuModeProperty, value);
	}

	#endregion

	#region SideMenuHeader

	public static readonly StyledProperty<object?> SideMenuHeaderProperty =
		AvaloniaProperty.Register<SideMenu, object?>(
			nameof(SideMenuHeader));

	public object? SideMenuHeader
	{
		get => GetValue(SideMenuHeaderProperty);
		set => SetValue(SideMenuHeaderProperty, value);
	}

	#endregion

	#region SideMenuFooter

	public static readonly StyledProperty<object?> SideMenuFooterProperty =
		AvaloniaProperty.Register<SideMenu, object?>(
			nameof(SideMenuFooter));

	public object? SideMenuFooter
	{
		get => GetValue(SideMenuFooterProperty);
		set => SetValue(SideMenuFooterProperty, value);
	}

	#endregion

	#region SideMenuContentsTemplate

	public static readonly StyledProperty<IDataTemplate> SideMenuContentsTemplateProperty =
		AvaloniaProperty.Register<SideMenu, IDataTemplate>(
			nameof(SideMenuContentsTemplate));

	public IDataTemplate SideMenuContentsTemplate
	{
		get => GetValue(SideMenuContentsTemplateProperty);
		set => SetValue(SideMenuContentsTemplateProperty, value);
	}

	#endregion

	#region SideMenuContents

	public static readonly StyledProperty<AvaloniaList<object>> SideMenuContentsProperty =
		AvaloniaProperty.Register<SideMenu, AvaloniaList<object>>(
			nameof(SideMenuContents),
			defaultValue: new AvaloniaList<object>());

	public AvaloniaList<object> SideMenuContents
	{
		get => GetValue(SideMenuContentsProperty);
		set => SetValue(SideMenuContentsProperty, value);
	}

	#endregion

	#region SideMenuSelectedItem

	private SideMenuItem? _sideMenuSelectedItem;
	public static readonly DirectProperty<ShellView, SideMenuItem?> SideMenuSelectedItemProperty =
		AvaloniaProperty.RegisterDirect<ShellView, SideMenuItem?>(
			nameof(SideMenuSelectedItem),
			o => o.SideMenuSelectedItem,
			(o, v) => o.SideMenuSelectedItem = v);

	public SideMenuItem? SideMenuSelectedItem
	{
		get => _sideMenuSelectedItem;
		set
		{
			if (SetAndRaise(SideMenuSelectedItemProperty, ref _sideMenuSelectedItem, value))
				SideMenuItemChanged(value);
		}
	}

	#endregion

	#endregion

	#region Attached properties

	#region OverrideSideMenuBehave

	public static readonly AttachedProperty<SideMenuBehaveType?> OverrideSideMenuBehaveProperty =
		AvaloniaProperty.RegisterAttached<ShellView, AvaloniaObject, SideMenuBehaveType?>("OverrideSideMenuBehave",
			defaultValue: null);

	public static SideMenuBehaveType? GetOverrideSideMenuBehave(AvaloniaObject element) =>
		element.GetValue(OverrideSideMenuBehaveProperty);

	public static void SetOverrideSideMenuBehave(AvaloniaObject element, SideMenuBehaveType? parameter) =>
		element.SetValue(OverrideSideMenuBehaveProperty, parameter);

	#endregion

	#endregion

	#region SideMenu Item Management

	public void AddSideMenuItem(SideMenuItem item)
	{
		item.PropertyChanged += OnSideMenuItemPropertyChanged;
		_sideMenuItems.Add(item);
	}

	public void InsertSideMenuItem(int index, SideMenuItem item)
	{
		item.PropertyChanged += OnSideMenuItemPropertyChanged;
		_sideMenuItems.Insert(index, item);
	}

	public bool RemoveSideMenuItem(SideMenuItem item)
	{
		item.PropertyChanged -= OnSideMenuItemPropertyChanged;
		return _sideMenuItems.Remove(item);
	}

	private void OnSideMenuItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(SideMenuItem.IsVisible))
			SyncTabVisibility();
	}

	#endregion

	#region Behavior

	protected virtual Task MenuActionAsync(CancellationToken cancellationToken)
	{
		SideMenuPresented = !SideMenuPresented;
		return Task.CompletedTask;
	}

	internal virtual void UpdateSideMenu()
	{
		if (_splitView == null || NavigationBar == null) return;

		// Check if current page has local pane content
		if (_contentView?.CurrentView is Page page && page.Pane != null)
		{
			// Hide global side menu completely when page has its own pane
			_splitView.OpenPaneLength = 0;
			_splitView.SetCurrentValue(SplitView.IsPaneOpenProperty, false);
			NavigationBar.HasSideMenuOption = false;
			return;
		}

		switch (GetCurrentBehave())
		{
			case SideMenuBehaveType.Default:
				_splitView.OpenPaneLength = SideMenuPresented ? SideMenuSize : 0;
				_splitView.SetCurrentValue(SplitView.IsPaneOpenProperty, SideMenuPresented);
				NavigationBar.HasSideMenuOption = true;
				break;
			case SideMenuBehaveType.Keep:
				_splitView.OpenPaneLength = SideMenuSize;
				_splitView.SetCurrentValue(SplitView.IsPaneOpenProperty, true);
				NavigationBar.HasSideMenuOption = false;
				break;
			case SideMenuBehaveType.Closed:
				_splitView.OpenPaneLength = 0;
				_splitView.SetCurrentValue(SplitView.IsPaneOpenProperty, true);
				NavigationBar.HasSideMenuOption = true;
				break;
            case SideMenuBehaveType.Removed:
				_splitView.OpenPaneLength = 0;
				_splitView.CompactPaneLength = 0;
                _splitView.SetCurrentValue(SplitView.IsPaneOpenProperty, false);
                NavigationBar.HasSideMenuOption = false;
                break;
        }
	}

	private SideMenuBehaveType GetCurrentBehave()
	{
		var view = this._contentView?.CurrentView;

		if (view is StyledElement element && GetOverrideSideMenuBehave(element) is { } overrideBehave)
			return overrideBehave;

		return ScreenSize switch
		{
			ScreenSizeType.Small => SmallScreenSideMenuBehave,
			ScreenSizeType.Medium => MediumScreenSideMenuBehave,
			ScreenSizeType.Large => LargeScreenSideMenuBehave,
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	protected virtual void SelectSideMenuItem()
	{
		if (_sideMenu == null) return;
		_skipChanges = true;
		SideMenuSelectedItem = _sideMenuItems
			.FirstOrDefault(f => f.Path == Navigator.CurrentUri.AbsolutePath);
		_skipChanges = false;
	}

	private void SideMenuItemChanged(SideMenuItem item)
	{
		if (item == null || _skipChanges) return;
		_ = Navigator.NavigateAsync(item.Path, NavigateType.HostedItemChange);

		if (GetCurrentBehave() != SideMenuBehaveType.Keep)
			SideMenuPresented = false;
	}

	protected virtual void SyncTabVisibility()
	{
		var currentChain = Navigator.CurrentChain;
		if (currentChain == null) return;

		foreach (var chain in currentChain.GetAscendingNodes())
		{
			if (chain is HostNavigationChain hostChain)
				SyncHostTabVisibility(hostChain, currentChain);
		}
	}

	private void SyncHostTabVisibility(HostNavigationChain hostChain, NavigationChain currentChain)
	{
		NavigationChain? firstVisible = null;
		var selectedIsHidden = false;

		foreach (var node in hostChain.Nodes)
		{
			var route = node.Node.Route;

			// Match SideMenuItems by exact path or descendant path
			var matchingItems = _sideMenuItems
				.Where(s => s.Path == route || s.Path.StartsWith(route + "/"))
				.ToList();

			if (matchingItems.Count > 0)
				node.IsVisible = matchingItems.Any(m => m.IsVisible);

			if (node.IsVisible && firstVisible == null)
				firstVisible = node;

			if (node == currentChain && !node.IsVisible)
				selectedIsHidden = true;

			// Recursively process nested HostNavigationChains
			if (node is HostNavigationChain childHost)
				SyncHostTabVisibility(childHost, currentChain);
		}

		if (selectedIsHidden && firstVisible != null)
			_ = Navigator.NavigateAsync(firstVisible.Node.Route, NavigateType.HostedItemChange);
	}

	#endregion
}
