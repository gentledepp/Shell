using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Animation;
using AvaloniaInside.Shell.Data;

namespace AvaloniaInside.Shell;

public class SideMenu : TemplatedControl
{
	private ListBox _listBox;
	private StackContentView? _overrideView;
	private object? _currentOverrideContent;
	private readonly SemaphoreSlim _overrideLock = new(1, 1);

	#region HeaderTemplate

	public static readonly StyledProperty<IDataTemplate> HeaderTemplateProperty =
		AvaloniaProperty.Register<SideMenu, IDataTemplate>(
			nameof(HeaderTemplate));

	public IDataTemplate HeaderTemplate
	{
		get => GetValue(HeaderTemplateProperty);
		set => SetValue(HeaderTemplateProperty, value);
	}

	#endregion

	#region Header

	public static readonly StyledProperty<object?> HeaderProperty =
		AvaloniaProperty.Register<SideMenu, object?>(
			nameof(Header));

	public object? Header
	{
		get => GetValue(HeaderProperty);
		set => SetValue(HeaderProperty, value);
	}

	#endregion

	#region FooterTemplate

	public static readonly StyledProperty<IDataTemplate> FooterTemplateProperty =
		AvaloniaProperty.Register<SideMenu, IDataTemplate>(
			nameof(FooterTemplate));

	public IDataTemplate FooterTemplate
	{
		get => GetValue(FooterTemplateProperty);
		set => SetValue(FooterTemplateProperty, value);
	}

	#endregion

	#region Footer

	public static readonly StyledProperty<object?> FooterProperty =
		AvaloniaProperty.Register<SideMenu, object?>(
			nameof(Footer));

	public object? Footer
	{
		get => GetValue(FooterProperty);
		set => SetValue(FooterProperty, value);
	}

	#endregion

	#region Items

	private IList<SideMenuItem> _items;
	public static readonly DirectProperty<SideMenu, IList<SideMenuItem>> ItemsProperty =
		AvaloniaProperty.RegisterDirect<SideMenu, IList<SideMenuItem>>(
			nameof(Items),
			o => o.Items,
			(o, v) => o.Items = v);
	public IList<SideMenuItem> Items
	{
		get => _items;
		set => SetAndRaise(ItemsProperty, ref _items, value);
	}

	#endregion

	#region SelectedItem

	private SideMenuItem? _selectedItem;
    private ContentControl? _currentOverrideView;

    public static readonly DirectProperty<SideMenu, SideMenuItem?> SelectedItemProperty =
		AvaloniaProperty.RegisterDirect<SideMenu, SideMenuItem?>(
			nameof(SelectedItem),
			o => o.SelectedItem,
			(o, v) => o.SelectedItem = v);
	public SideMenuItem? SelectedItem
	{
		get => _selectedItem;
		set => SetAndRaise(SelectedItemProperty, ref _selectedItem, value);
	}

	#endregion

	#region ContentsTemplate

	public static readonly StyledProperty<IDataTemplate> ContentsTemplateProperty =
		AvaloniaProperty.Register<SideMenu, IDataTemplate>(
			nameof(ContentsTemplate));

	public IDataTemplate ContentsTemplate
	{
		get => GetValue(ContentsTemplateProperty);
		set => SetValue(ContentsTemplateProperty, value);
	}

	#endregion

	
    #region SideMenuOverride

    public static readonly StyledProperty<object?> SideMenuOverrideProperty =
        AvaloniaProperty.Register<SideMenu, object?>(
            nameof(SideMenuOverride));

    public object? SideMenuOverride
    {
        get => GetValue(SideMenuOverrideProperty);
        set =>SetValue(SideMenuOverrideProperty, value);
    }

    #endregion

    #region SideMenuOverrideDataContext

    public static readonly StyledProperty<object?> SideMenuOverrideDataContextProperty =
        AvaloniaProperty.Register<SideMenu, object?>(
            nameof(SideMenuOverrideDataContext));

    public object? SideMenuOverrideDataContext
    {
        get => GetValue(SideMenuOverrideDataContextProperty);
        set => SetValue(SideMenuOverrideDataContextProperty, value);
    }

    #endregion

    #region OverridePageTransition

    public static readonly StyledProperty<IPageTransition?> OverridePageTransitionProperty =
        AvaloniaProperty.Register<SideMenu, IPageTransition?>(
            nameof(OverridePageTransition));

    public IPageTransition? OverridePageTransition
    {
        get => GetValue(OverridePageTransitionProperty);
        set => SetValue(OverridePageTransitionProperty, value);
    }

    #endregion


	#region Contents

	public static readonly StyledProperty<IList> ContentsProperty =
		AvaloniaProperty.Register<SideMenu, IList>(
			nameof(Contents));

	public IList Contents
	{
		get => GetValue(ContentsProperty);
		set => SetValue(ContentsProperty, value);
	}

	#endregion

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);
		_listBox = e.NameScope.Find<ListBox>("PART_Items")
		           ?? throw new KeyNotFoundException("PART_Items not found in SideMenu template");
		_overrideView = e.NameScope.Find<StackContentView>("PART_OverrideView");

		SetupUi();
	}

	private void SetupUi()
	{
		_listBox!.ItemsSource ??= new AvaloniaList<object>();
		_listBox!.SelectionChanged += OnSelectionChanged;
	}

	private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{

	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		Debug.WriteLine(change.Property.Name);
	}

	public async Task SetOverrideAsync(
		object? overrideContent,
		object? dataContext,
		NavigateType navigateType,
		CancellationToken cancellationToken = default)
	{
		if (_overrideView == null) return;

		// Use semaphore to prevent concurrent calls from creating duplicate visual parents
		await _overrideLock.WaitAsync(cancellationToken);
		try
		{
			// Skip if the content hasn't changed to avoid duplicate visual parent errors
			if (ReferenceEquals(_currentOverrideContent, overrideContent))
			{
				// Just update DataContext if needed
				if (_overrideView.CurrentView is ContentControl existingControl)
				{
					existingControl.DataContext = dataContext;
				}
				return;
			}

			// Clear existing content if any
			if (_overrideView.HasContent && _currentOverrideView != null)
            {
                await _overrideView.RemoveViewAsync(_currentOverrideView, navigateType, cancellationToken);
				_currentOverrideContent = null;
                _currentOverrideView = null;
            }

			if (overrideContent != null)
			{
				// Create a ContentControl with the override content
				var control = new ContentControl
				{
					Content = overrideContent,
					DataContext = dataContext
				};

				// Push to stack with animation
				await _overrideView.PushViewAsync(control, navigateType, cancellationToken);
				_currentOverrideContent = overrideContent;
                _currentOverrideView = control;
            }
		}
		finally
		{
			_overrideLock.Release();
		}
	}
}
