using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;
using AvaloniaInside.Shell.Data;

namespace AvaloniaInside.Shell;

public partial class ShellView
{
	private readonly AvaloniaList<SideMenuItem> _sideMenuItems = new();

	private bool _skipChanges = false;

	#region Properties

	public double SideMenuSize => ScreenSize == ScreenSizeType.Small ? DesiredSize.Width - 35 : DefaultSideMenuSize;

	public static readonly StyledProperty<double> DefaultSideMenuSizeProperty = AvaloniaProperty.Register<ShellView,double>(nameof(DefaultSideMenuSize), 250);

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
		AvaloniaProperty.RegisterAttached<ShellView, AvaloniaObject, SideMenuBehaveType?>(
			"OverrideSideMenuBehave",
			defaultValue: null);

	static ShellView()
	{
		OverrideSideMenuBehaveProperty.Changed.AddClassHandler<Control>(OnOverrideSideMenuBehaveChanged);
		OverrideSideMenuProperty.Changed.AddClassHandler<Control>(OnOverrideSideMenuChanged);
	}

	public static SideMenuBehaveType? GetOverrideSideMenuBehave(AvaloniaObject element) =>
		element.GetValue(OverrideSideMenuBehaveProperty);

	public static void SetOverrideSideMenuBehave(AvaloniaObject element, SideMenuBehaveType? parameter) =>
		element.SetValue(OverrideSideMenuBehaveProperty, parameter);

	private static void OnOverrideSideMenuBehaveChanged(Control control, AvaloniaPropertyChangedEventArgs e)
	{
		// Find the ShellView ancestor and update its side menu
		var shellView = control.FindAncestorOfType<ShellView>();
		shellView?.UpdateSideMenu();
	}

	#endregion


    #region OverrideSideMenu

    public static readonly AttachedProperty<object?> OverrideSideMenuProperty =
        AvaloniaProperty.RegisterAttached<ShellView, AvaloniaObject, object?>(
            "OverrideSideMenu",
            defaultValue: null);

    public static object? GetOverrideSideMenu(AvaloniaObject element) =>
        element.GetValue(OverrideSideMenuProperty);

    public static void SetOverrideSideMenu(AvaloniaObject element, object? parameter) =>
        element.SetValue(OverrideSideMenuProperty, parameter);

    private static void OnOverrideSideMenuChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        // Find the ShellView ancestor and update its side menu
        var shellView = control.FindAncestorOfType<ShellView>();
        shellView?.UpdateSideMenu();
    }

    #endregion

	#endregion

	#region Behavior

	protected virtual Task MenuActionAsync(CancellationToken cancellationToken)
	{
		SideMenuPresented = !SideMenuPresented;
		return Task.CompletedTask;
	}

	protected virtual async Task UpdateSideMenuAsync(
		NavigateType navigateType,
		CancellationToken cancellationToken = default)
	{
		if (_splitView == null || NavigationBar == null) return;

		var currentOverride = GetCurrentOverride();
		var currentBehave = GetCurrentBehave();

		// Force side menu button over back button when there's custom override content
		// BUT not when behavior is Removed (side menu is hidden, so back button should show)
		NavigationBar.ForceSideMenuButton = currentOverride != null && currentBehave != SideMenuBehaveType.Removed;

		switch (currentBehave)
		{
			case SideMenuBehaveType.Default:
				_splitView.OpenPaneLength = SideMenuPresented ? SideMenuSize : 0;
				_splitView.IsPaneOpen = SideMenuPresented;
				NavigationBar.HasSideMenuOption = true;
				break;
			case SideMenuBehaveType.Keep:
				_splitView.OpenPaneLength = SideMenuSize;
				_splitView.IsPaneOpen = true;
				NavigationBar.HasSideMenuOption = false;
				break;
			case SideMenuBehaveType.Closed:
				_splitView.OpenPaneLength = 0;
				_splitView.IsPaneOpen = true;
				NavigationBar.HasSideMenuOption = true;
				break;
            case SideMenuBehaveType.Removed:
				_splitView.OpenPaneLength = 0;
				_splitView.CompactPaneLength = 0;
                _splitView.IsPaneOpen = false;
                NavigationBar.HasSideMenuOption = false;
                break;
        }

		// Animate the override content in/out with the same transition as the view
		await _sideMenu.SetOverrideAsync(
			currentOverride,
			GetCurrentViewDataContext(),
			navigateType,
			cancellationToken);
    }

	// Synchronous wrapper for backward compatibility (uses default navigate type)
	protected virtual void UpdateSideMenu()
	{
		_ = UpdateSideMenuAsync(NavigateType.Normal);
	}

    private object? GetCurrentOverride()
    {
        // Prioritize modal view when it has content
        var view = _modalView?.HasContent == true
            ? _modalView.CurrentView
            : _contentView?.CurrentView;

        if (view is StyledElement element && GetOverrideSideMenu(element) is { } overrideBehave)
            return overrideBehave;

        return null;
    }

	private object? GetCurrentViewDataContext()
	{
		// Prioritize modal view when it has content
		var view = _modalView?.HasContent == true
			? _modalView.CurrentView
			: _contentView?.CurrentView;

		return view is StyledElement element ? element.DataContext : null;
	}

	private SideMenuBehaveType GetCurrentBehave()
	{
		// Prioritize modal view when it has content
		var view = _modalView?.HasContent == true
			? _modalView.CurrentView
			: _contentView?.CurrentView;

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
		if (_sideMenu == null || Navigator == null) return;
		_skipChanges = true;
		SideMenuSelectedItem = _sideMenuItems
			.FirstOrDefault(f => f.Path == Navigator.CurrentUri.AbsolutePath);
		_skipChanges = false;
	}

	private void SideMenuItemChanged(SideMenuItem item)
	{
		if (item == null || _skipChanges || Navigator == null) return;
		_ = Navigator.NavigateAsync(item.Path, NavigateType.HostedItemChange);

		if (GetCurrentBehave() != SideMenuBehaveType.Keep)
			SideMenuPresented = false;
	}

	#endregion
}
