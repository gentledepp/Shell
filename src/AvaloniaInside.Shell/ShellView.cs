using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaInside.Shell.Platform;
using ReactiveUI;
using Splat;

namespace AvaloniaInside.Shell;

[TemplatePart("PART_NavigationBarPlaceHolder", typeof(ContentPresenter))]
[TemplatePart("PART_ContentView", typeof(StackContentView))]
[TemplatePart("PART_SplitView", typeof(SplitView))]
[TemplatePart("PART_SideMenu", typeof(SideMenu))]
[TemplatePart("PART_Modal", typeof(StackContentView))]
public partial class ShellView : TemplatedControl, INavigationBarProvider
{
    #region Enums

    public enum ScreenSizeType
    {
        Small,
        Medium,
        Large
    }

    public enum SideMenuBehaveType
    {
        Default,
        Keep,
        Closed,
        Removed
    }

    #endregion

    #region Variables

    private SplitView? _splitView;
    private StackContentView? _contentView;
    private NavigationBar? _navigationBar;
    private StackContentView? _modalView;
    private SideMenu? _sideMenu;
    private ContentPresenter? _navigationBarPlaceHolder;

    private bool _loadedFlag;
    private TopLevel? _attachedTopLevel;
    private SwipeOpenGestureHandler? _sideMenuSwipeHandler;

    #endregion

    #region Properties

    public NavigationBar? NavigationBar => FindNavigationBar();

    public NavigationBar? AttachedNavigationBar => _navigationBar;

    public StackContentView? ContentView => _contentView;

    public ICommand BackCommand { get; set; }

    public ICommand SideMenuCommand { get; set; }

    protected override Type StyleKeyOverride => typeof(ShellView);

    #region ScreenSize

    private ScreenSizeType _screenSize = ScreenSizeType.Large;

    public static readonly DirectProperty<ShellView, ScreenSizeType> ScreenSizeProperty =
        AvaloniaProperty.RegisterDirect<ShellView, ScreenSizeType>(
            nameof(ScreenSize),
            o => o.ScreenSize,
            (o, v) => o.ScreenSize = v);

    public ScreenSizeType ScreenSize
    {
        get => _screenSize;
        set
        {
            var oldValue = _screenSize;
            if (SetAndRaise(ScreenSizeProperty, ref _screenSize, value))
                UpdateScreenSize(oldValue, value);
        }
    }

    #endregion

    #region DefaultRoute

    public static readonly DirectProperty<ShellView, string?> DefaultRouteProperty = AvaloniaProperty
        .RegisterDirect<ShellView, string?>(
            nameof(DefaultRoute),
            o => o.DefaultRoute,
            (o, v) => o.DefaultRoute = v);

    public string? DefaultRoute { get; set; }

    #endregion

    #region DefaultPageTransition

    /// <summary>
    /// Defines the <see cref="DefaultPageTransitionProperty"/> property.
    /// </summary>
    public static readonly StyledProperty<IPageTransition?> DefaultPageTransitionProperty =
        AvaloniaProperty.Register<ShellView, IPageTransition?>(
            nameof(DefaultPageTransition),
            defaultValue: PlatformSetup.TransitionForPage);

    /// <summary>
    /// Gets or sets the animation played when content appears and disappears.
    /// </summary>
    public IPageTransition? DefaultPageTransition
    {
        get => GetValue(DefaultPageTransitionProperty);
        set => SetValue(DefaultPageTransitionProperty, value);
    }

    #endregion

    #region ModalPageTransition

    /// <summary>
    /// Defines the <see cref="DefaultPageTransitionProperty"/> property.
    /// </summary>
    public static readonly StyledProperty<IPageTransition?> ModalPageTransitionProperty =
	    AvaloniaProperty.Register<ShellView, IPageTransition?>(
		    nameof(ModalPageTransition),
		    defaultValue: PlatformSetup.TransitionForModal);

    /// <summary>
    /// Gets or sets the animation played when content appears and disappears.
    /// </summary>
    public IPageTransition? ModalPageTransition
    {
	    get => GetValue(ModalPageTransitionProperty);
	    set => SetValue(ModalPageTransitionProperty, value);
    }

    #endregion

    #region NavigationBarAttachType

    /// <summary>
    /// Defines the <see cref="NavigationBarAttachTypeProperty"/> property.
    /// </summary>
    public static readonly StyledProperty<NavigationBarAttachType> NavigationBarAttachTypeProperty =
	    AvaloniaProperty.Register<ShellView, NavigationBarAttachType>(
		    nameof(NavigationBarAttachType),
		    defaultValue: PlatformSetup.NavigationBarAttachType);

    /// <summary>
    /// Gets or sets the type of attach navigation bar.
    /// </summary>
    public NavigationBarAttachType NavigationBarAttachType
    {
	    get => GetValue(NavigationBarAttachTypeProperty);
	    set => SetValue(NavigationBarAttachTypeProperty, value);
    }

    #endregion

    #region NavigationBarForModal

    /// <summary>
    /// Defines the <see cref="NavigationBarAttachTypeProperty"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> NavigationBarForModalProperty =
	    AvaloniaProperty.Register<ShellView, bool>(
		    nameof(NavigationBarForModal),
		    defaultValue: false);

    /// <summary>
    /// Gets or sets the type of attach navigation bar.
    /// </summary>
    public bool NavigationBarForModal
    {
	    get => GetValue(NavigationBarForModalProperty);
	    set => SetValue(NavigationBarForModalProperty, value);
    }

    #endregion

    #endregion

    #region Safe area properties

    #region SafePadding

    public static readonly StyledProperty<Thickness> SafePaddingProperty =
	    AvaloniaProperty.Register<ShellView, Thickness>(nameof(SafePadding));

    public Thickness SafePadding
    {
	    get => GetValue(SafePaddingProperty);
	    set => SetValue(SafePaddingProperty, value);
    }

    #endregion

    #region EnableSafeArea

    public static readonly StyledProperty<bool> EnableSafeAreaProperty =
	    AvaloniaProperty.Register<ShellView, bool>(nameof(EnableSafeArea), defaultValue: true);

    public bool EnableSafeArea
    {
	    get => GetValue(EnableSafeAreaProperty);
	    set => SetValue(EnableSafeAreaProperty, value);
    }

    #endregion

    #region ApplyTopSafePadding

    public static readonly StyledProperty<bool> ApplyTopSafePaddingProperty =
	    AvaloniaProperty.Register<NavigationBar, bool>(nameof(ApplyTopSafePadding), defaultValue: true);

    public bool ApplyTopSafePadding
    {
	    get => GetValue(ApplyTopSafePaddingProperty);
	    private set => SetValue(ApplyTopSafePaddingProperty, value);
    }

    #endregion

    #region ApplyBottomSafePadding

    public static readonly StyledProperty<bool> ApplyBottomSafePaddingProperty =
	    AvaloniaProperty.Register<NavigationBar, bool>(nameof(ApplyBottomSafePadding), defaultValue: true);

    public bool ApplyBottomSafePadding
    {
	    get => GetValue(ApplyBottomSafePaddingProperty);
	    private set => SetValue(ApplyBottomSafePaddingProperty, value);
    }

    #endregion

    #region ApplyLeftSafePadding

    public static readonly StyledProperty<bool> ApplyLeftSafePaddingProperty =
	    AvaloniaProperty.Register<NavigationBar, bool>(nameof(ApplyLeftSafePadding), defaultValue: true);

    public bool ApplyLeftSafePadding
    {
	    get => GetValue(ApplyLeftSafePaddingProperty);
	    private set => SetValue(ApplyLeftSafePaddingProperty, value);
    }

    #endregion

    #region ApplyRightSafePadding

    public static readonly StyledProperty<bool> ApplyRightSafePaddingProperty =
	    AvaloniaProperty.Register<NavigationBar, bool>(nameof(ApplyRightSafePadding), defaultValue: true);

    public bool ApplyRightSafePadding
    {
	    get => GetValue(ApplyRightSafePaddingProperty);
	    private set => SetValue(ApplyRightSafePaddingProperty, value);
    }

    #endregion

    #endregion

    #region Attached properties

    #region EnableSafeAreaForTop

    public static readonly AttachedProperty<bool> EnableSafeAreaForTopProperty =
        AvaloniaProperty.RegisterAttached<NavigationBar, AvaloniaObject, bool>("EnableSafeAreaForTop",
            defaultValue: true);

    public static bool GetEnableSafeAreaForTop(AvaloniaObject element) =>
        element.GetValue(EnableSafeAreaForTopProperty);

    public static void SetEnableSafeAreaForTop(AvaloniaObject element, bool parameter) =>
        element.SetValue(EnableSafeAreaForTopProperty, parameter);

    #endregion

    #region EnableSafeAreaForBottom

    public static readonly AttachedProperty<bool> EnableSafeAreaForBottomProperty =
        AvaloniaProperty.RegisterAttached<NavigationBar, AvaloniaObject, bool>("EnableSafeAreaForBottom",
            defaultValue: true);

    public static bool GetEnableSafeAreaForBottom(AvaloniaObject element) =>
        element.GetValue(EnableSafeAreaForBottomProperty);

    public static void SetEnableSafeAreaForBottom(AvaloniaObject element, bool parameter) =>
        element.SetValue(EnableSafeAreaForBottomProperty, parameter);

    #endregion

    #region EnableSafeAreaForLeft

    public static readonly AttachedProperty<bool> EnableSafeAreaForLeftProperty =
	    AvaloniaProperty.RegisterAttached<NavigationBar, AvaloniaObject, bool>("EnableSafeAreaForLeft",
		    defaultValue: true);

    public static bool GetEnableSafeAreaForLeft(AvaloniaObject element) =>
	    element.GetValue(EnableSafeAreaForLeftProperty);

    public static void SetEnableSafeAreaForLeft(AvaloniaObject element, bool parameter) =>
	    element.SetValue(EnableSafeAreaForLeftProperty, parameter);

    #endregion

    #region EnableSafeAreaForRight

    public static readonly AttachedProperty<bool> EnableSafeAreaForRightProperty =
	    AvaloniaProperty.RegisterAttached<NavigationBar, AvaloniaObject, bool>("EnableSafeAreaForRight",
		    defaultValue: true);

    public static bool GetEnableSafeAreaForRight(AvaloniaObject element) =>
	    element.GetValue(EnableSafeAreaForRightProperty);

    public static void SetEnableSafeAreaForRight(AvaloniaObject element, bool parameter) =>
	    element.SetValue(EnableSafeAreaForRightProperty, parameter);

    #endregion

    #endregion

    #region Ctor and loading

    public ShellView()
    : this(Locator.Current.GetService<INavigator>())
    {   
    }

    public ShellView(INavigator? navigator)
    {
        Navigator = navigator ?? throw new ArgumentException("Cannot find INavigationService");
        Navigator.RegisterShell(this);

        BackCommand = ReactiveCommand.CreateFromTask(BackActionAsync);
        SideMenuCommand = ReactiveCommand.CreateFromTask(MenuActionAsync);

        var isMobile = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
        if (!isMobile)
        {
            Classes.Add("Mobile");
            _sideMenuPresented = true;
        }

        SizeChanged += OnSizeChanged;
        Items.CollectionChanged += ItemsOnCollectionChanged;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // On Android the Activity (and its TopLevel) is destroyed and recreated while the shell
        // keeps living, so TopLevel-bound events must be re-wired on every TopLevel change -
        // otherwise a back press reaches no handler and closes the app.
        AttachTopLevelEvents(TopLevel.GetTopLevel(this));

        if (DefaultRoute != null  && !_loadedFlag)
        {
            _ = Navigator.NavigateAsync(DefaultRoute, CancellationToken.None);
            _loadedFlag = true;
        }

        // recreate the handler disposed in OnUnloaded - re-attachment does not re-run OnApplyTemplate
        if (_sideMenuSwipeHandler is null)
            SetupSideMenuSwipeHandler();

        OnSafeEdgeSetup();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        DetachTopLevelEvents();
        _sideMenuSwipeHandler?.Dispose();
        _sideMenuSwipeHandler = null;
    }

    private void AttachTopLevelEvents(TopLevel? topLevel)
    {
        if (ReferenceEquals(_attachedTopLevel, topLevel))
            return;

        DetachTopLevelEvents();

        if (topLevel is null)
            return;

        topLevel.BackRequested += TopLevelOnBackRequested;
        topLevel.KeyUp += TopLevelOnKeyUp;

        if (topLevel.InsetsManager is { } insetsManager)
            insetsManager.SafeAreaChanged += InsetsManagerOnSafeAreaChanged;

        _attachedTopLevel = topLevel;
    }

    private void DetachTopLevelEvents()
    {
        if (_attachedTopLevel is null)
            return;

        _attachedTopLevel.BackRequested -= TopLevelOnBackRequested;
        _attachedTopLevel.KeyUp -= TopLevelOnKeyUp;

        if (_attachedTopLevel.InsetsManager is { } insetsManager)
            insetsManager.SafeAreaChanged -= InsetsManagerOnSafeAreaChanged;

        _attachedTopLevel = null;
    }

    private void InsetsManagerOnSafeAreaChanged(object? sender, Avalonia.Controls.Platform.SafeAreaChangedArgs e)
        => OnSafeEdgeSetup();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _splitView = e.NameScope.Find<SplitView>("PART_SplitView");
        _navigationBar = e.NameScope.Find<NavigationBar>("PART_NavigationBar");
        _contentView = e.NameScope.Find<StackContentView>("PART_ContentView");
        _modalView = e.NameScope.Find<StackContentView>("PART_Modal");
        _sideMenu = e.NameScope.Find<SideMenu>("PART_SideMenu");
        _navigationBarPlaceHolder = e.NameScope.Find<ContentPresenter>("PART_NavigationBarPlaceHolder");

        SetupUi();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        switch (change.Property.Name)
        {
            case nameof(LargeScreenSideMenuMode):
            case nameof(MediumScreenSideMenuMode):
            case nameof(SmallScreenSideMenuMode):
                UpdateSideMenu();
                break;
        }
    }

    private void SetupUi()
    {
        OnSafeEdgeSetup();
        UpdateSideMenu();
        SetupNavigationBar();

        if (_splitView != null)
        {
	        _splitView.PaneClosing += SplitViewOnPaneClosing;
        }

        if (_sideMenu != null)
        {
	        _sideMenu.Items = _sideMenuItems;
        }

        SetupSideMenuSwipeHandler();
    }

    private void SetupSideMenuSwipeHandler()
    {
        _sideMenuSwipeHandler?.Dispose();

        if (_splitView == null) return;

        _sideMenuSwipeHandler = new SwipeOpenGestureHandler(
           hitTestArea: this,
           canSwipeOpen: () =>
              SwipeToOpenSideMenuEnabled &&
              GetCurrentBehave() == SideMenuBehaveType.Default &&
              !SideMenuPresented &&
              !(_contentView?.CurrentView is Page { Pane: not null }),
           canSwipeClose: () =>
              SwipeToOpenSideMenuEnabled &&
              GetCurrentBehave() == SideMenuBehaveType.Default &&
              SideMenuPresented &&
              !(_contentView?.CurrentView is Page { Pane: not null }),
           isPaneOpen: () => SideMenuPresented,
           targetWidth: () => SideMenuSize,
           setOpenPaneLength: w => _splitView.OpenPaneLength = w,
           setPaneOpen: o => _splitView.SetCurrentValue(SplitView.IsPaneOpenProperty, o),
           commitState: open =>
           {
              _sideMenuPresented = open;
              RaisePropertyChanged(SideMenuPresentedProperty, !open, open);
              UpdateSideMenu();
           });
        _sideMenuSwipeHandler.Attach();
    }

    protected virtual void OnSafeEdgeSetup()
    {
	    if (!EnableSafeArea)
	    {
		    return;
	    }

	    TopLevel.SetAutoSafeAreaPadding(this, false);

        if (TopLevel.GetTopLevel(this) is { InsetsManager: { } insetsManager })
            SafePadding = insetsManager.SafeAreaPadding;
    }

    private void SetupNavigationBar()
    {
	    if (_navigationBarPlaceHolder != null &&
	        NavigationBarAttachType == NavigationBarAttachType.ToShell &&
	        _navigationBar == null)
	    {
		    _navigationBarPlaceHolder.Content = _navigationBar = new NavigationBar(this);
	    }
    }

    #endregion

    #region Services and navigation

    public INavigator Navigator { get; }

    private NavigationBar? FindNavigationBar()
    {
	    if (NavigationBarAttachType == NavigationBarAttachType.ToShell) return _navigationBar;
	    return Navigator.CurrentChain?.GetAscendingNodes()
		    .Select(s => s.Instance)
		    .OfType<Page>()
		    .FirstOrDefault(f => f.AttachedNavigationBar != null)?.AttachedNavigationBar;
    }

    #endregion

    #region View Stack Manager

    public async Task PushViewAsync(
	    object view,
        NavigateType navigateType,
        CancellationToken cancellationToken = default)
    {
        await (_contentView?.PushViewAsync(view, navigateType, cancellationToken) ?? Task.CompletedTask);
        AttachedNavigationBar?.UpdateView(Navigator.CurrentChain?.Instance);
        SelectSideMenuItem();
        SyncTabVisibility();
        UpdateBinding();
        UpdateSideMenu();
    }

    public async Task RemoveViewAsync(object view,
        NavigateType navigateType,
        CancellationToken cancellationToken = default)
    {
        await (_contentView?.RemoveViewAsync(view, navigateType, cancellationToken) ?? Task.CompletedTask);
        await (_modalView?.RemoveViewAsync(view, navigateType, cancellationToken) ?? Task.CompletedTask);

        SelectSideMenuItem();
        SyncTabVisibility();
        UpdateBinding();
        UpdateSideMenu();
    }

    public async Task ClearStackAsync(CancellationToken cancellationToken)
    {
        await (_contentView?.ClearStackAsync(cancellationToken) ?? Task.CompletedTask);
        await (_modalView?.ClearStackAsync(cancellationToken) ?? Task.CompletedTask);

        SelectSideMenuItem();
        SyncTabVisibility();
        UpdateBinding();
        UpdateSideMenu();
    }

    public Task ModalAsync(object instance, NavigateType navigateType, CancellationToken cancellationToken) =>
        _modalView?.PushViewAsync(instance, navigateType, cancellationToken) ?? Task.CompletedTask;

    /// <summary>
    /// Decides synchronously whether a back request will be handled by the shell and,
    /// if so, kicks off the corresponding action.
    /// The decision MUST be made synchronously: on Android the platform reads
    /// <see cref="RoutedEventArgs.Handled"/> immediately after raising BackRequested and,
    /// if it is still false, performs its default back action (finishing the activity).
    /// Awaiting the navigation before returning would leave Handled==false at that point,
    /// causing the app to close (or double-navigate) instead of navigating back.
    /// </summary>
    private bool Back()
    {
        if (ScreenSize == ScreenSizeType.Small && SideMenuPresented)
        {
            SideMenuPresented = false;
            return true;
        }

        // Close an open page-local pane (e.g. a checklist's chapters menu) before navigating.
        // The page-local SplitView also listens to TopLevel.BackRequested, but this handler runs
        // first and would otherwise navigate (setting Handled=true) before the SplitView could
        // close its own pane. We only dismiss when the pane is actually shown as an overlay and
        // the current behavior allows toggling (i.e. it is not permanently pinned open).
        if ((_modalView?.CurrentView ?? _contentView?.CurrentView) is Page { IsPaneOpen: true } page
            && page.LocalSideMenuBehaviorAllowsToggle()
            && page.LocalSideMenuDisplayMode is SplitViewDisplayMode.Overlay or SplitViewDisplayMode.CompactOverlay)
        {
            page.IsPaneOpen = false;
            return true;
        }

        var result = Navigator.HasItemInStack();
        if (result)
            // Fire-and-forget: the navigation (incl. animation) runs asynchronously,
            // but we have already decided synchronously that the shell handles this back request.
            _ = Navigator.BackAsync();

        return result;
    }

    #endregion

    #region Ui Events

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ScreenSize = e.NewSize.Width switch
        {
            <= 768 => ScreenSizeType.Small,
            <= 1024 => ScreenSizeType.Medium,
            _ => ScreenSizeType.Large,
        };
    }

    private void TopLevelOnBackRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = Back();
    }

    private void TopLevelOnKeyUp(object? sender, KeyEventArgs e)
    {
        if (OperatingSystem.IsAndroid())
            return;

        if (e.Key == Key.Escape)
        {
            Back();
        }
    }

    private void SplitViewOnPaneClosing(object? sender, CancelRoutedEventArgs e)
    {
        if (_sideMenuSwipeHandler is { IsGestureActive: true })
        {
            e.Cancel = true;
            return;
        }
        SideMenuPresented = false;
    }

    protected virtual Task BackActionAsync(CancellationToken cancellationToken)
    {
        return Navigator.BackAsync(cancellationToken);
    }

    #endregion

    #region Screen Actions

    private void UpdateScreenSize(ScreenSizeType old, ScreenSizeType newScreen)
    {
        Classes.Add(newScreen.ToString());
        Classes.Remove(old.ToString());

        if (_splitView == null) return;

        _splitView.DisplayMode = newScreen switch
        {
            ScreenSizeType.Small => SmallScreenSideMenuMode,
            ScreenSizeType.Medium => MediumScreenSideMenuMode,
            ScreenSizeType.Large => LargeScreenSideMenuMode,
            _ => throw new ArgumentOutOfRangeException(nameof(newScreen), newScreen, null)
        };

        if (newScreen == ScreenSizeType.Small && SideMenuPresented)
            SideMenuPresented = false;
        else
            UpdateSideMenu();
    }

    protected virtual void UpdateBinding()
    {
	    var view = _contentView?.CurrentView;
	    if (view is StyledElement element)
	    {
		    this[!ApplyTopSafePaddingProperty] = element[!EnableSafeAreaForTopProperty];
		    this[!ApplyBottomSafePaddingProperty] = element[!EnableSafeAreaForBottomProperty];
		    this[!ApplyLeftSafePaddingProperty] = element[!EnableSafeAreaForLeftProperty];
		    this[!ApplyRightSafePaddingProperty] = element[!ApplyRightSafePaddingProperty];
	    }
	    else
	    {
		    ApplyTopSafePadding = true;
		    ApplyBottomSafePadding = true;
		    ApplyLeftSafePadding = true;
		    ApplyRightSafePadding = true;
	    }
	}

    #endregion
}
