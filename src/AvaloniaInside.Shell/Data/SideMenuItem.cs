using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace AvaloniaInside.Shell.Data;

public class SideMenuItem : IItem, INotifyPropertyChanged
{
	private bool _isVisible = true;

	public string Title { get; set; }
	public string Path { get; set; }
	public IImage? Icon { get; set; }

	public bool IsVisible
	{
		get => _isVisible;
		set
		{
			if (_isVisible == value) return;
			_isVisible = value;
			OnPropertyChanged();
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
