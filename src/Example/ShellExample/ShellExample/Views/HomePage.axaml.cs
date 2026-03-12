using System.Linq;
using Avalonia.Markup.Xaml;
using AvaloniaInside.Shell;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using AvaloniaInside.Shell.Data;

namespace ShellExample.Views;

public partial class HomePage : Page
{
	public HomePage()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

    public override Task InitialiseAsync(CancellationToken cancellationToken)
	{
		DataContext = new ViewModels.HomePageViewModel(Navigator);
		return Task.CompletedTask;
	}

    private void ShowHidePetsClicked(object? sender, RoutedEventArgs e)
    {
        var shell = this.FindLogicalAncestorOfType<ShellView>();

        var item = shell.Items.OfType<SideMenuItem>().Single(itm => itm.Path == "/main/pets/dog");
        item.IsVisible = !item.IsVisible;


        var item2 = shell.Items.OfType<SideMenuItem>().Single(itm => itm.Path == "/main/pets/cat");
        item2.IsVisible = !item2.IsVisible;
    }
}
