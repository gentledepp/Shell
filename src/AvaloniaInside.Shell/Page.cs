using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using ReactiveUI;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using static AvaloniaInside.Shell.ShellView;

namespace AvaloniaInside.Shell;

[PseudoClasses(":modal")]
[TemplatePart("PART_TabStripPlaceHolder", typeof(ContentPresenter))]
public class Page : UserControl, INavigationLifecycle, INavigatorLifecycle, INavigationBarProvider
{
	private ContentPresenter? _navigationBarPlaceHolder;
	private NavigationBar? _navigationBar;
	private SplitView? _localSplitView;
	private SplitViewDisplayMode _previousLocalDisplayMode;
	private SwipeOpenGestureHandler? _paneSwipeHandler;

	public Page()
	{
		ToggleSideMenuCommand = ReactiveCommand.Create(() =>
		{
			IsPaneOpen = !IsPaneOpen;
		});
	}

	#region Properties

	public NavigationBar? NavigationBar => Shell?.NavigationBar;

	public NavigationBar? AttachedNavigationBar => _navigationBar;

	public INavigator? Navigator => Shell?.Navigator;

	protected override Type StyleKeyOverride => typeof(Page);

	#region Shell

	public static readonly StyledProperty<ShellView?> ShellProperty =
		AvaloniaProperty.Register<Page, ShellView?>(nameof(Shell));

	public ShellView? Shell
	{
		get => GetValue(ShellProperty);
		internal set => SetValue(ShellProperty, value);
	}

	#endregion

	#region Chain

	public static readonly DirectProperty<Page, NavigationChain?> BackCommandProperty =
		AvaloniaProperty.RegisterDirect<Page, NavigationChain?>(
			nameof(Chain),
			o => o.Chain,
			(o, v) => o.Chain = v);

	private NavigationChain? _chain;

	public NavigationChain? Chain
	{
		get => _chain;
		set
		{
			if (SetAndRaise(BackCommandProperty, ref _chain, value))
				IsModal = value?.Type == NavigateType.Modal;
		}
	}

	#endregion

	#region SafePadding

	public static readonly StyledProperty<Thickness> SafePaddingProperty =
		AvaloniaProperty.Register<Page, Thickness>(nameof(SafePadding));

	public Thickness SafePadding
	{
		get => GetValue(SafePaddingProperty);
		set => SetValue(SafePaddingProperty, value);
	}

	#endregion

	#region ApplyTopSafePadding

	public static readonly StyledProperty<bool> ApplyTopSafePaddingProperty =
		AvaloniaProperty.Register<Page, bool>(nameof(ApplyTopSafePadding), defaultValue: true);

	public bool ApplyTopSafePadding
	{
		get => GetValue(ApplyTopSafePaddingProperty);
		set => SetValue(ApplyTopSafePaddingProperty, value);
	}

	#endregion

	#region ApplyBottomSafePadding

	public static readonly StyledProperty<bool> ApplyBottomSafePaddingProperty =
		AvaloniaProperty.Register<Page, bool>(nameof(ApplyBottomSafePadding), defaultValue: true);

	public bool ApplyBottomSafePadding
	{
		get => GetValue(ApplyBottomSafePaddingProperty);
		set => SetValue(ApplyBottomSafePaddingProperty, value);
	}

	#endregion

	#region ApplyLeftSafePadding

	public static readonly StyledProperty<bool> ApplyLeftSafePaddingProperty =
		AvaloniaProperty.Register<Page, bool>(nameof(ApplyLeftSafePadding), defaultValue: true);

	public bool ApplyLeftSafePadding
	{
		get => GetValue(ApplyLeftSafePaddingProperty);
		set => SetValue(ApplyLeftSafePaddingProperty, value);
	}

	#endregion

	#region ApplyRightSafePadding

	public static readonly StyledProperty<bool> ApplyRightSafePaddingProperty =
		AvaloniaProperty.Register<Page, bool>(nameof(ApplyRightSafePadding), defaultValue: true);

	public bool ApplyRightSafePadding
	{
		get => GetValue(ApplyRightSafePaddingProperty);
		set => SetValue(ApplyRightSafePaddingProperty, value);
	}

	#endregion

	#region TopSafeSpace

	public static readonly StyledProperty<double> TopSafeSpaceProperty =
		AvaloniaProperty.Register<Page, double>(nameof(TopSafeSpace));

	public double TopSafeSpace
	{
		get => GetValue(TopSafeSpaceProperty);
		set => SetValue(TopSafeSpaceProperty, value);
	}

	#endregion

	#region TopSafePadding

	public static readonly StyledProperty<Thickness> TopSafePaddingProperty =
		AvaloniaProperty.Register<Page, Thickness>(nameof(TopSafePadding));

	public Thickness TopSafePadding
	{
		get => GetValue(TopSafePaddingProperty);
		set => SetValue(TopSafePaddingProperty, value);
	}

	#endregion

	#region BottomSafeSpace

	public static readonly StyledProperty<double> BottomSafeSpaceProperty =
		AvaloniaProperty.Register<Page, double>(nameof(BottomSafeSpace));

	public double BottomSafeSpace
	{
		get => GetValue(BottomSafeSpaceProperty);
		set => SetValue(BottomSafeSpaceProperty, value);
	}

	#endregion

	#region BottomSafePadding

	public static readonly StyledProperty<Thickness> BottomSafePaddingProperty =
		AvaloniaProperty.Register<Page, Thickness>(nameof(BottomSafePadding));

	public Thickness BottomSafePadding
	{
		get => GetValue(BottomSafePaddingProperty);
		set => SetValue(BottomSafePaddingProperty, value);
	}

	#endregion

	#region LeftSafeSpace

	public static readonly StyledProperty<double> LeftSafeSpaceProperty =
		AvaloniaProperty.Register<Page, double>(nameof(LeftSafeSpace));

	public double LeftSafeSpace
	{
		get => GetValue(LeftSafeSpaceProperty);
		set => SetValue(LeftSafeSpaceProperty, value);
	}

	#endregion

	#region LeftSafePadding

	public static readonly StyledProperty<Thickness> LeftSafePaddingProperty =
		AvaloniaProperty.Register<Page, Thickness>(nameof(LeftSafePadding));

	public Thickness LeftSafePadding
	{
		get => GetValue(LeftSafePaddingProperty);
		set => SetValue(LeftSafePaddingProperty, value);
	}

	#endregion

	#region RightSafeSpace

	public static readonly StyledProperty<double> RightSafeSpaceProperty =
		AvaloniaProperty.Register<Page, double>(nameof(RightSafeSpace));

	public double RightSafeSpace
	{
		get => GetValue(RightSafeSpaceProperty);
		set => SetValue(RightSafeSpaceProperty, value);
	}

	#endregion

	#region RightSafePadding

	public static readonly StyledProperty<Thickness> RightSafePaddingProperty =
		AvaloniaProperty.Register<Page, Thickness>(nameof(RightSafePadding));

	public Thickness RightSafePadding
	{
		get => GetValue(RightSafePaddingProperty);
		set => SetValue(RightSafePaddingProperty, value);
	}

	#endregion

	#region PageSafePadding

	public static readonly StyledProperty<Thickness> PageSafePaddingProperty =
		AvaloniaProperty.Register<Page, Thickness>(nameof(PageSafePadding));

	public Thickness PageSafePadding
	{
		get => GetValue(PageSafePaddingProperty);
		set => SetValue(PageSafePaddingProperty, value);
	}

	#endregion

	#region TabSafePadding

	public static readonly StyledProperty<Thickness> TabSafePaddingProperty =
		AvaloniaProperty.Register<Page, Thickness>(nameof(TabSafePadding));

	public Thickness TabSafePadding
	{
		get => GetValue(TabSafePaddingProperty);
		set => SetValue(TabSafePaddingProperty, value);
	}

	#endregion

	#region IsModal

	/// <summary>
	/// Defines the <see cref="IsModal"/> property.
	/// </summary>
	public static readonly StyledProperty<bool> IsModalProperty =
		AvaloniaProperty.Register<ToggleButton, bool>(nameof(IsModal), false);

	/// <summary>
	/// Gets or sets whether the <see cref="Page"/> is modal.
	/// </summary>
	public bool IsModal
	{
		get => GetValue(IsModalProperty);
		internal set => SetValue(IsModalProperty, value);
	}

	#endregion

	#region Pane

	public static readonly StyledProperty<object?> PaneProperty =
		AvaloniaProperty.Register<Page, object?>(nameof(Pane));

	/// <summary>
	/// Gets or sets the content for the page-local side pane.
	/// When set, the default Page template will display a responsive SplitView with this content.
	/// </summary>
	public object? Pane
	{
		get => GetValue(PaneProperty);
		set => SetValue(PaneProperty, value);
	}

	#endregion

	#region IsPaneOpen

	public static readonly DirectProperty<Page, bool> IsPaneOpenProperty =
		AvaloniaProperty.RegisterDirect<Page, bool>(
			nameof(IsPaneOpen),
			o => o.IsPaneOpen,
			(o, v) => o.IsPaneOpen = v);

	private bool _isPaneOpen;

	/// <summary>
	/// Gets or sets whether the page-local pane is open.
	/// </summary>
	public bool IsPaneOpen
	{
		get => _isPaneOpen;
        set
        {
            if(SetAndRaise(IsPaneOpenProperty, ref _isPaneOpen, value))
				UpdateLocalSideMenu();
        }
    }

	#endregion

	#region LocalSideMenuSize

	public static readonly StyledProperty<double> LocalSideMenuSizeProperty =
		AvaloniaProperty.Register<Page, double>(nameof(LocalSideMenuSize), defaultValue: 250);

	public double LocalSideMenuSize
	{
		get => GetValue(LocalSideMenuSizeProperty);
		private set => SetValue(LocalSideMenuSizeProperty, value);
	}

	#endregion

	#region LocalSideMenuDisplayMode

	public static readonly StyledProperty<SplitViewDisplayMode> LocalSideMenuDisplayModeProperty =
		AvaloniaProperty.Register<Page, SplitViewDisplayMode>(
			nameof(LocalSideMenuDisplayMode),
			defaultValue: SplitViewDisplayMode.Overlay);

	public SplitViewDisplayMode LocalSideMenuDisplayMode
	{
		get => GetValue(LocalSideMenuDisplayModeProperty);
		private set => SetValue(LocalSideMenuDisplayModeProperty, value);
	}

	#endregion

	#region LargeScreenLocalSideMenuMode

	public static readonly StyledProperty<SplitViewDisplayMode> LargeScreenLocalSideMenuModeProperty =
		AvaloniaProperty.Register<Page, SplitViewDisplayMode>(
			nameof(LargeScreenLocalSideMenuMode),
			defaultValue: SplitViewDisplayMode.Inline);

	public SplitViewDisplayMode LargeScreenLocalSideMenuMode
	{
		get => GetValue(LargeScreenLocalSideMenuModeProperty);
		set => SetValue(LargeScreenLocalSideMenuModeProperty, value);
	}

	#endregion

	#region MediumScreenLocalSideMenuMode

	public static readonly StyledProperty<SplitViewDisplayMode> MediumScreenLocalSideMenuModeProperty =
		AvaloniaProperty.Register<Page, SplitViewDisplayMode>(
			nameof(MediumScreenLocalSideMenuMode),
			defaultValue: SplitViewDisplayMode.Inline);

	public SplitViewDisplayMode MediumScreenLocalSideMenuMode
	{
		get => GetValue(MediumScreenLocalSideMenuModeProperty);
		set => SetValue(MediumScreenLocalSideMenuModeProperty, value);
	}

	#endregion

	#region SmallScreenLocalSideMenuMode

	public static readonly StyledProperty<SplitViewDisplayMode> SmallScreenLocalSideMenuModeProperty =
		AvaloniaProperty.Register<Page, SplitViewDisplayMode>(
			nameof(SmallScreenLocalSideMenuMode),
			defaultValue: SplitViewDisplayMode.Overlay);

	public SplitViewDisplayMode SmallScreenLocalSideMenuMode
	{
		get => GetValue(SmallScreenLocalSideMenuModeProperty);
		set => SetValue(SmallScreenLocalSideMenuModeProperty, value);
	}

	#endregion

	#region LocalSideMenuOpenPaneLength

	public static readonly StyledProperty<double> LocalSideMenuOpenPaneLengthProperty =
		AvaloniaProperty.Register<Page, double>(nameof(LocalSideMenuOpenPaneLength), defaultValue: 0);

	public double LocalSideMenuOpenPaneLength
	{
		get => GetValue(LocalSideMenuOpenPaneLengthProperty);
		internal set => SetValue(LocalSideMenuOpenPaneLengthProperty, value);
	}

	#endregion

	#region LargeScreenLocalSideMenuBehave

	public static readonly StyledProperty<ShellView.SideMenuBehaveType?> LargeScreenLocalSideMenuBehaveProperty =
		AvaloniaProperty.Register<Page, ShellView.SideMenuBehaveType?>(nameof(LargeScreenLocalSideMenuBehave));

	public ShellView.SideMenuBehaveType? LargeScreenLocalSideMenuBehave
	{
		get => GetValue(LargeScreenLocalSideMenuBehaveProperty);
		set => SetValue(LargeScreenLocalSideMenuBehaveProperty, value);
	}

	#endregion

	#region MediumScreenLocalSideMenuBehave

	public static readonly StyledProperty<ShellView.SideMenuBehaveType?> MediumScreenLocalSideMenuBehaveProperty =
		AvaloniaProperty.Register<Page, ShellView.SideMenuBehaveType?>(nameof(MediumScreenLocalSideMenuBehave));

	public ShellView.SideMenuBehaveType? MediumScreenLocalSideMenuBehave
	{
		get => GetValue(MediumScreenLocalSideMenuBehaveProperty);
		set => SetValue(MediumScreenLocalSideMenuBehaveProperty, value);
	}

	#endregion

	#region SmallScreenLocalSideMenuBehave

	public static readonly StyledProperty<ShellView.SideMenuBehaveType?> SmallScreenLocalSideMenuBehaveProperty =
		AvaloniaProperty.Register<Page, ShellView.SideMenuBehaveType?>(nameof(SmallScreenLocalSideMenuBehave));

	public ShellView.SideMenuBehaveType? SmallScreenLocalSideMenuBehave
	{
		get => GetValue(SmallScreenLocalSideMenuBehaveProperty);
		set => SetValue(SmallScreenLocalSideMenuBehaveProperty, value);
	}

	#endregion

	#region ToggleSideMenuCommand

	/// <summary>
	/// Command that toggles the page-local pane.
	/// This is used by the NavigationBar when the Page has local Pane content.
	/// </summary>
	public ICommand ToggleSideMenuCommand { get; }

	#endregion

	#endregion

	#region Lifecycle

	public virtual Task AppearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	public virtual Task ArgumentAsync(object args, CancellationToken cancellationToken) => Task.CompletedTask;
	public virtual Task DisappearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	public virtual Task InitialiseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	public virtual Task TerminateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public virtual Task OnNavigateAsync(NaviagateEventArgs args, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	public virtual Task OnNavigatingAsync(NaviagatingEventArgs args, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	#endregion

	#region Setup and template

	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);
		ApplyNavigationBar();
		AttachedNavigationBar?.UpdateView(this);

		this[!ApplyTopSafePaddingProperty] = this[!ShellView.EnableSafeAreaForTopProperty];
		this[!ApplyBottomSafePaddingProperty] = this[!ShellView.EnableSafeAreaForBottomProperty];
		this[!ApplyLeftSafePaddingProperty] = this[!ShellView.EnableSafeAreaForLeftProperty];
		this[!ApplyRightSafePaddingProperty] = this[!ShellView.ApplyRightSafePaddingProperty];

		// allow previewer to display page-based views
		if (Design.IsDesignMode && Chain is null)
			return;

		IsModal = Chain.Type == NavigateType.Modal;

		// Initialize local side menu
		UpdateLocalSideMenu();

		// Subscribe to Shell's ScreenSize changes and SizeChanged event
		if (Shell != null)
		{
			Shell.PropertyChanged += OnShellPropertyChanged;
			Shell.SizeChanged += OnShellSizeChanged;
		}
	}

	protected override void OnUnloaded(RoutedEventArgs e)
	{
		base.OnUnloaded(e);

		// Cleanup event subscriptions
		if (Shell != null)
		{
			Shell.PropertyChanged -= OnShellPropertyChanged;
			Shell.SizeChanged -= OnShellSizeChanged;
		}

		if (_localSplitView != null)
		{
			_localSplitView.PaneClosing -= LocalSplitViewOnPaneClosing;
		}

		_paneSwipeHandler?.Dispose();
		_paneSwipeHandler = null;
	}

	private void OnShellPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == ShellView.ScreenSizeProperty)
		{
			UpdateLocalSideMenu();
		}
	}

	private void OnShellSizeChanged(object? sender, SizeChangedEventArgs e)
	{
		// Update pane width when window size changes (needed for overlay mode)
		if (Pane != null)
		{
			UpdateLocalSideMenu();
		}
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == ShellProperty)
		{
			if (Shell != null)
			{
				this[!SafePaddingProperty] = Shell[!ShellView.SafePaddingProperty];
			}
		}
		else if (change.Property == SafePaddingProperty)
		{
			UpdateSafePaddingSizes();
		}
		else if (change.Property == IsModalProperty)
		{
			PseudoClasses.Set(":modal", IsModal);
			UpdateSafePaddingSizes();
		}
		else if (change.Property == PaneProperty ||
		         change.Property == IsPaneOpenProperty ||
		         change.Property == LargeScreenLocalSideMenuBehaveProperty ||
		         change.Property == MediumScreenLocalSideMenuBehaveProperty ||
		         change.Property == SmallScreenLocalSideMenuBehaveProperty ||
		         change.Property == LargeScreenLocalSideMenuModeProperty ||
		         change.Property == MediumScreenLocalSideMenuModeProperty ||
		         change.Property == SmallScreenLocalSideMenuModeProperty)
		{
			UpdateLocalSideMenu();
			// Notify Shell to update its side menu when Pane changes
			if (change.Property == PaneProperty)
			{
				Shell?.UpdateSideMenu();
			}
		}
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);
		_navigationBarPlaceHolder = e.NameScope.Find<ContentPresenter>("PART_NavigationBarPlaceHolder");
		_localSplitView = e.NameScope.Find<SplitView>("PART_LocalSplitView");

		if (_localSplitView != null)
		{
			_localSplitView.PaneClosing += LocalSplitViewOnPaneClosing;

			_paneSwipeHandler?.Dispose();
			_paneSwipeHandler = new SwipeOpenGestureHandler(
				hitTestArea: this,
				canSwipeOpen: () => LocalSideMenuBehaviorAllowsToggle() && !IsPaneOpen,
				canSwipeClose: () => LocalSideMenuBehaviorAllowsToggle() && IsPaneOpen,
				isPaneOpen: () => IsPaneOpen,
				targetWidth: () => LocalSideMenuSize,
				setOpenPaneLength: w => LocalSideMenuOpenPaneLength = w,
				setPaneOpen: o =>
				{
					if (_localSplitView != null)
						_localSplitView.SetCurrentValue(SplitView.IsPaneOpenProperty, o);
				},
				commitState: open =>
				{
					IsPaneOpen = open;
					UpdateLocalSideMenu();
				});
			_paneSwipeHandler.Attach();
		}
	}

	private void ApplyNavigationBar()
	{
		if (_navigationBarPlaceHolder == null || _navigationBar != null)
			return;

		if (Shell?.NavigationBarAttachType is not ({ } type and not NavigationBarAttachType.ToShell))
			return;

		if (IsModal && !Shell.NavigationBarForModal)
			return;

		if ((type == NavigationBarAttachType.ToLastPage && Chain is HostNavigationChain) ||
		    (type == NavigationBarAttachType.ToFirstHostThenPage && Chain.Back is HostNavigationChain))
			return;

		_navigationBarPlaceHolder.Content = _navigationBar = new NavigationBar(this);
	}

    #endregion

    #region Local Side Menu
    public double SideMenuSize => Shell is null ? 250 : (Shell.ScreenSize == ScreenSizeType.Small ? Shell.Bounds.Width - 35 : Shell!.DefaultSideMenuSize);
    protected virtual void UpdateLocalSideMenu()
	{
		// Only update if Pane content is set
		if (Pane == null)
		{
			LocalSideMenuOpenPaneLength = 0;
			return;
		}

		// Use the ShellView's SideMenuSize property to ensure consistent width
        LocalSideMenuSize = Math.Min(SideMenuSize, Shell?.DefaultSideMenuSize??250);

		// Get current screen size for DisplayMode selection
		var screenSize = Shell?.ScreenSize ?? ShellView.ScreenSizeType.Medium;

		// Set DisplayMode based on screen size (independent of behavior type)
		var newDisplayMode = screenSize switch
		{
			ShellView.ScreenSizeType.Small => SmallScreenLocalSideMenuMode,
			ShellView.ScreenSizeType.Medium => MediumScreenLocalSideMenuMode,
			ShellView.ScreenSizeType.Large => LargeScreenLocalSideMenuMode,
			_ => SplitViewDisplayMode.Overlay
		};

		// Apply behavior logic
		var currentBehave = GetCurrentLocalSideMenuBehave();

		// Handle transition from Inline/CompactInline to Overlay mode
		// Similar to ShellView.UpdateScreenSize logic (lines 584-587)
		bool isTransitioningToOverlay =
			(newDisplayMode == SplitViewDisplayMode.Overlay) &&
			(_previousLocalDisplayMode == SplitViewDisplayMode.Inline ||
			 _previousLocalDisplayMode == SplitViewDisplayMode.CompactInline ||
			 _previousLocalDisplayMode == SplitViewDisplayMode.CompactOverlay);

		if (isTransitioningToOverlay && IsPaneOpen && currentBehave == ShellView.SideMenuBehaveType.Default)
		{
			// Force close the pane when transitioning to Overlay mode
			// This ensures the SplitView's overlay dismiss handlers are properly initialized
			IsPaneOpen = false;
		}

		LocalSideMenuDisplayMode = newDisplayMode;
		_previousLocalDisplayMode = newDisplayMode;

		switch (currentBehave)
		{
			case ShellView.SideMenuBehaveType.Default:
				LocalSideMenuOpenPaneLength = IsPaneOpen ? LocalSideMenuSize : 0;
				if (_localSplitView != null)
					_localSplitView.SetCurrentValue(SplitView.IsPaneOpenProperty, IsPaneOpen);
				break;
			case ShellView.SideMenuBehaveType.Keep:
				LocalSideMenuOpenPaneLength = LocalSideMenuSize;
				IsPaneOpen = true;
				break;
			case ShellView.SideMenuBehaveType.Closed:
				LocalSideMenuOpenPaneLength = 0;
				IsPaneOpen = false;
				break;
			case ShellView.SideMenuBehaveType.Removed:
				LocalSideMenuOpenPaneLength = 0;
				IsPaneOpen = false;
				break;
		}

		// Update navigation bar button visibility based on new behavior
		AttachedNavigationBar?.UpdateButtons();
	}

	private ShellView.SideMenuBehaveType GetCurrentLocalSideMenuBehave()
	{
		var screenSize = Shell?.ScreenSize ?? ShellView.ScreenSizeType.Medium;

		return screenSize switch
		{
			ShellView.ScreenSizeType.Small => SmallScreenLocalSideMenuBehave ?? ShellView.SideMenuBehaveType.Default,
			ShellView.ScreenSizeType.Medium => MediumScreenLocalSideMenuBehave ?? ShellView.SideMenuBehaveType.Default,
			ShellView.ScreenSizeType.Large => LargeScreenLocalSideMenuBehave ?? ShellView.SideMenuBehaveType.Keep,
			_ => ShellView.SideMenuBehaveType.Default
		};
	}

	/// <summary>
	/// Returns true if the current behavior allows the pane to be toggled
	/// </summary>
	public bool LocalSideMenuBehaviorAllowsToggle()
	{
		if (Pane == null) return false;

		var currentBehave = GetCurrentLocalSideMenuBehave();
		return currentBehave == ShellView.SideMenuBehaveType.Default;
	}

	private void LocalSplitViewOnPaneClosing(object? sender, CancelRoutedEventArgs e)
	{
		if (_paneSwipeHandler is { IsGestureActive: true })
		{
			e.Cancel = true;
			return;
		}
		IsPaneOpen = false;
	}

	#endregion

	#region Sizing

	protected virtual void UpdateSafePaddingSizes()
	{
		var safePadding = !IsModal ? SafePadding : new Thickness(0, 0, 0, 0);

		TopSafeSpace = safePadding.Top;
		TopSafePadding = new Thickness(0, safePadding.Top, 0, 0);
		BottomSafeSpace = safePadding.Bottom;
		BottomSafePadding = new Thickness(0, 0, 0, safePadding.Bottom);
		LeftSafeSpace = safePadding.Left;
		LeftSafePadding = new Thickness(safePadding.Left, 0, 0, 0);
		RightSafeSpace = safePadding.Right;
		RightSafePadding = new Thickness(0, 0, safePadding.Right, 0);

		PageSafePadding  = new Thickness(safePadding.Left, 0, safePadding.Right, safePadding.Bottom);
	}

	#endregion
}
