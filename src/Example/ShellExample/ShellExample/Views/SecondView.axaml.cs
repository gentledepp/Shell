using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Markup.Xaml;
using AvaloniaInside.Shell;
using ShellExample.ViewModels;

namespace ShellExample.Views;

public partial class SecondView : Page, INotifyPropertyChanged
{

	public SecondView()
	{
		InitializeComponent();
        DataContext = new SecondViewModel();
    }

    private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}

