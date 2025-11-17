using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AvaloniaInside.Shell;

namespace ShellExample.ViewModels
{
    public class SecondViewModel : ViewModelBase
    {

        private ShellView.SideMenuBehaveType? _selectedBehavior = null;
        public ShellView.SideMenuBehaveType?[] BehaviorOptions { get; } = new ShellView.SideMenuBehaveType?[]
        {
            null,
            ShellView.SideMenuBehaveType.Default,
            ShellView.SideMenuBehaveType.Keep,
            ShellView.SideMenuBehaveType.Closed,
            ShellView.SideMenuBehaveType.Removed
        };

        public ShellView.SideMenuBehaveType? SelectedBehavior
        {
            get => _selectedBehavior;
            set
            {
                if (_selectedBehavior != value)
                {
                    _selectedBehavior = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Title { get; set; } = "2nd view";
    }
}
