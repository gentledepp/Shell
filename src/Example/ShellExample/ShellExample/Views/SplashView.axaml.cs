using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaInside.Shell;
using Projektanker.Icons.Avalonia;
using System.Threading;
using System.Threading.Tasks;

namespace ShellExample.Views
{
    public partial class SplashView : Page
    {
        public SplashView()
        {
            InitializeComponent();
        }

        public override async Task InitialiseAsync(CancellationToken cancellationToken)
        {
            await Navigator.NavigateAsync("/welcome", cancellationToken);
            await base.InitialiseAsync(cancellationToken);
        }

    }
}
