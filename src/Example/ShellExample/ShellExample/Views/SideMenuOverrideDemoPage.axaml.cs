using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives; // Required for TemplateAppliedEventArgs
using Avalonia.Controls.Templates;  // For IDataTemplate, FuncDataTemplate
using Avalonia.Layout;              // For HorizontalAlignment, VerticalAlignment etc.
using Avalonia.Media;               // For Brushes, Thickness
using AvaloniaInside.Shell;         // For ShellView attached properties

namespace ShellExample.Views
{
    public partial class SideMenuOverrideDemoPage : UserControl
    {
        private Button? _toggleOverrideButton;
        private bool _isOverrideActive = false;
        private Control? _overrideContent;
        private IDataTemplate? _overrideTemplate;

        public SideMenuOverrideDemoPage()
        {
            InitializeComponent();
            InitializeOverrideContentAndTemplate();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeOverrideContentAndTemplate()
        {
            _overrideContent = new Border
            {
                Background = Brushes.LightSkyBlue,
                Padding = new Avalonia.Thickness(20),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = "Custom Side Menu!", FontSize = 18, FontWeight = FontWeight.Bold },
                        new TextBlock { Text = "This is entirely new content." },
                        new Button { Content = "A Button Inside" }
                    }
                }
            };

            _overrideTemplate = new FuncDataTemplate<Control>((controlFromItem, ns) =>
                new StackPanel
                {
                    Spacing = 5,
                    Children =
                    {
                        new TextBlock { Text = "Template Applied:", Foreground = Brushes.DarkMagenta, FontWeight = FontWeight.SemiBold },
                        new ContentPresenter { Content = controlFromItem } // controlFromItem will be _overrideContent
                    }
                }, true); // true for "supports recycling"
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _toggleOverrideButton = e.NameScope.Find<Button>("ToggleOverrideButton");

            if (_toggleOverrideButton != null)
            {
                _toggleOverrideButton.Click += ToggleOverrideButton_Click;
                _toggleOverrideButton.Content = "Show Custom Side Menu"; // Initial state
            }
        }

        private void ToggleOverrideButton_Click(object? sender, RoutedEventArgs e)
        {
            _isOverrideActive = !_isOverrideActive;

            if (_isOverrideActive)
            {
                ShellView.SetSideMenuOverrideItem(this, _overrideContent);
                ShellView.SetSideMenuOverrideItemTemplate(this, _overrideTemplate);
                if (_toggleOverrideButton != null) _toggleOverrideButton.Content = "Hide Custom Side Menu";
            }
            else
            {
                ShellView.SetSideMenuOverrideItem(this, null);
                ShellView.SetSideMenuOverrideItemTemplate(this, null);
                if (_toggleOverrideButton != null) _toggleOverrideButton.Content = "Show Custom Side Menu";
            }
        }
    }
}
