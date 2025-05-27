using NUnit.Framework;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using AvaloniaInside.Shell;
using Avalonia.Collections;
using AvaloniaInside.Shell.Data; // For SideMenuItem
using System.Linq;
using Avalonia.Markup.Xaml.Templates; // For DataTemplate
using Avalonia.Threading; // For Dispatcher
using System.Threading.Tasks; // For Task.Delay

// Since ShellView and its parts are in AvaloniaInside.Shell,
// and we've added InternalsVisibleTo("AvaloniaInside.Shell.Tests")
// we should be able to access internal members.

namespace AvaloniaInside.Shell.Tests
{
    [TestFixture]
    public class ShellViewSideMenuOverrideTests
    {
        private ShellView _shellView;
        private SideMenu _sideMenu;
        private SplitView _splitView;
        private NavigationBar _navigationBar;
        private StackContentView _contentView;
        private ContentControl _page1;
        private ContentControl _page2;

        [SetUp]
        public async Task SetUp()
        {
            // Avalonia services might require a dispatcher
            if (Dispatcher.UIThread == null)
            {
                // This is a simplified setup for a dispatcher in a test environment.
                // For more complex scenarios, a full Application instance might be needed via TestApplication.
                var mainLoop = new Avalonia.Threading.Internal.MainLoop(System.Threading.Thread.CurrentThread);
                Avalonia.Threading.Dispatcher.ResetInstance(mainLoop);
            }
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _shellView = new ShellView();

                // Manually create and assign template parts
                _splitView = new SplitView
                {
                    PanePlacement = PanePlacement.Left,
                    DisplayMode = SplitViewDisplayMode.Inline, // Default for large screen
                    IsPaneOpen = true,
                    OpenPaneLength = 250,
                    CompactPaneLength = 48
                };
                _sideMenu = new SideMenu();
                _navigationBar = new NavigationBar();
                _contentView = new StackContentView();

                // Assign parts to ShellView. This would normally be done by OnApplyTemplate.
                // We need to use reflection or make these fields internal/public for testing.
                // Assuming InternalsVisibleTo allows access to internal fields.
                var shellViewType = typeof(ShellView);
                shellViewType.GetField("_splitView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_shellView, _splitView);
                shellViewType.GetField("_sideMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_shellView, _sideMenu);
                shellViewType.GetField("_navigationBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_shellView, _navigationBar);
                shellViewType.GetField("_contentView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_shellView, _contentView);
                
                // Initialize _sideMenuItems for default tests
                var sideMenuItemsField = shellViewType.GetField("_sideMenuItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var sideMenuItems = sideMenuItemsField?.GetValue(_shellView) as AvaloniaList<SideMenuItem>;
                if (sideMenuItems != null)
                {
                    sideMenuItems.Add(new SideMenuItem { Title = "Default Item 1", Path = "/default1" });
                }
                 _sideMenu.Items = sideMenuItems; // Assign to the actual side menu part

                // Set default properties for ShellView that affect side menu behavior
                _shellView.ScreenSize = ShellView.ScreenSizeType.Large; // Default to large screen
                _shellView.LargeScreenSideMenuBehave = ShellView.SideMenuBehaveType.Keep; // Default behavior for large screen
                _shellView.SideMenuPresented = true; // Default state for Keep

                // Simulate template applied and initial setup
                // This is a simplification. A real OnApplyTemplate might do more.
                // We need to ensure _sideMenu.Items is initialized if it's not done above.
                 if (_sideMenu.Items == null) _sideMenu.Items = new AvaloniaList<object>();


                _page1 = new ContentControl { Name = "Page1" };
                _page2 = new ContentControl { Name = "Page2" };

                // Initial update to reflect default state
                SimulateNavigateToView(_page1); // Start with a view
            });
        }

        private void SimulateNavigateToView(Control view)
        {
            // This helper simulates navigating to a new view and triggers ShellView's update logic.
            // It's a simplified version of what the INavigator would do.
            
            // The _contentView needs to be part of the visual tree for some Avalonia features,
            // but for pure state testing, directly setting CurrentView and calling updates might suffice.
            if (_contentView != null)
            {
                _contentView.GetType().GetProperty("CurrentView", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(_contentView, view);
            }
            
            // Call methods that ShellView uses to update its state after navigation or property changes.
            // These might need to be made internal or public, or called via reflection if private.
            // Assuming UpdateSideMenu is public or internal.
            _shellView.UpdateSideMenu(); 
            // _shellView.UpdateBinding(); // If relevant for side menu state
        }

        [Test]
        public async Task Test_SideMenuOverride_DisplaysItemAndTemplate()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Arrange
                var overrideItem = new TextBlock { Text = "Override Content" };
                var overrideTemplate = new FuncDataTemplate<object>((value, namescope) => 
                    new TextBlock { Text = (value as TextBlock)?.Text + " Templated" }
                );

                ShellView.SetSideMenuOverrideItem(_page1, overrideItem);
                ShellView.SetSideMenuOverrideItemTemplate(_page1, overrideTemplate);

                // Act
                SimulateNavigateToView(_page1); // Re-simulate navigation to apply changes

                // Assert
                Assert.IsNotNull(_sideMenu.Contents, "SideMenu.Contents should not be null.");
                Assert.AreEqual(1, _sideMenu.Contents.Count, "SideMenu.Contents should have one item.");
                Assert.AreSame(overrideItem, _sideMenu.Contents.FirstOrDefault(), "SideMenu.Contents should contain the override item.");
                Assert.AreSame(overrideTemplate, _sideMenu.ContentsTemplate, "SideMenu.ContentsTemplate should be the specified template.");
                
                Assert.IsTrue(_splitView.IsPaneOpen, "SplitView pane should be open for override.");
                Assert.AreEqual(0, _splitView.CompactPaneLength, "SplitView CompactPaneLength should be 0 for override.");
                Assert.IsFalse(_navigationBar.HasSideMenuOption, "NavigationBar should not have side menu option for override.");
                Assert.IsTrue(_sideMenu.IsVisible, "SideMenu should be visible for override.");
            });
        }

        [Test]
        public async Task Test_SideMenuOverride_IgnoresOverrideSideMenuBehaveRemoved()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Arrange
                var overrideItem = new TextBlock { Text = "Override Content" };
                ShellView.SetSideMenuOverrideItem(_page1, overrideItem);
                ShellView.SetOverrideSideMenuBehave(_page1, ShellView.SideMenuBehaveType.Removed);

                // Act
                SimulateNavigateToView(_page1);

                // Assert
                // Override should take precedence over OverrideSideMenuBehave
                Assert.IsTrue(_sideMenu.IsVisible, "SideMenu should be visible when overridden, even if OverrideSideMenuBehave is Removed.");
                Assert.IsNotNull(_sideMenu.Contents, "SideMenu.Contents should not be null for override.");
                Assert.AreEqual(1, _sideMenu.Contents.Count, "SideMenu.Contents should have one item for override.");
                Assert.AreSame(overrideItem, _sideMenu.Contents.FirstOrDefault(), "SideMenu.Contents should contain the override item.");
                Assert.IsTrue(_splitView.IsPaneOpen, "SplitView pane should be open for override.");
                Assert.AreEqual(0, _splitView.CompactPaneLength, "SplitView CompactPaneLength should be 0 for override.");
                Assert.IsFalse(_navigationBar.HasSideMenuOption, "NavigationBar should not have side menu option for override.");
            });
        }

        [Test]
        public async Task Test_NoSideMenuOverride_RespectsOverrideSideMenuBehaveRemoved()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Arrange
                // Ensure no override item is set for _page1 for this test
                ShellView.SetSideMenuOverrideItem(_page1, null); 
                ShellView.SetOverrideSideMenuBehave(_page1, ShellView.SideMenuBehaveType.Removed);

                // Act
                SimulateNavigateToView(_page1);

                // Assert
                Assert.IsFalse(_sideMenu.IsVisible, "SideMenu should not be visible when OverrideSideMenuBehave is Removed and no override item is set.");
                Assert.IsFalse(_splitView.IsPaneOpen, "SplitView pane should be closed when side menu is Removed.");
                Assert.IsFalse(_navigationBar.HasSideMenuOption, "NavigationBar should not have side menu option when side menu is Removed.");
                
                // Check that the override item is not present
                if (_sideMenu.Contents != null && _sideMenu.Contents.Any())
                {
                    Assert.AreNotSame("Override Content", (_sideMenu.Contents.FirstOrDefault() as TextBlock)?.Text, "SideMenu should not contain override content.");
                }
            });
        }

        [Test]
        public async Task Test_NoSideMenuOverride_RespectsDefaultBehavior()
        {
             await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Arrange
                // Ensure no override item or specific behave type is set for _page1 for this test
                ShellView.SetSideMenuOverrideItem(_page1, null);
                ShellView.SetOverrideSideMenuBehave(_page1, null); // Use ShellView's default (Keep for LargeScreen)
                _shellView.LargeScreenSideMenuBehave = ShellView.SideMenuBehaveType.Keep; // Explicitly set for clarity
                _shellView.ScreenSize = ShellView.ScreenSizeType.Large;
                _shellView.SideMenuPresented = true; // Consistent with Keep

                // Act
                SimulateNavigateToView(_page1);

                // Assert
                // Behavior should be 'Keep' based on LargeScreenSideMenuBehave
                Assert.IsTrue(_sideMenu.IsVisible, "SideMenu should be visible for default Keep behavior.");
                Assert.IsTrue(_splitView.IsPaneOpen, "SplitView pane should be open for Keep behavior.");
                Assert.IsFalse(_navigationBar.HasSideMenuOption, "NavigationBar should not have side menu option for Keep behavior.");
                Assert.AreEqual(_shellView.DefaultSideMenuSize, _splitView.OpenPaneLength, "OpenPaneLength should be DefaultSideMenuSize for Keep.");
                
                Assert.IsNotNull(_sideMenu.Items, "SideMenu.Items should not be null for default behavior.");
                var defaultSideMenuItems = typeof(ShellView).GetField("_sideMenuItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_shellView) as AvaloniaList<SideMenuItem>;
                Assert.AreSame(defaultSideMenuItems, _sideMenu.Items, "SideMenu.Items should contain default menu items.");
                Assert.IsNull(_sideMenu.ContentsTemplate, "SideMenu.ContentsTemplate should be null for default item list behavior.");
            });
        }

        [Test]
        public async Task Test_SideMenuOverride_ThenNoOverride_SwitchesCorrectly()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Arrange
                var overrideItem = new TextBlock { Text = "Override For Page1" };
                ShellView.SetSideMenuOverrideItem(_page1, overrideItem);
                ShellView.SetSideMenuOverrideItemTemplate(_page1, null); // No specific template for this test

                // Ensure _page2 has no override
                ShellView.SetSideMenuOverrideItem(_page2, null);
                ShellView.SetOverrideSideMenuBehave(_page2, null); // Use ShellView's default behavior
                _shellView.LargeScreenSideMenuBehave = ShellView.SideMenuBehaveType.Keep; // Default for comparison

                // Act & Assert for Page1 (with override)
                SimulateNavigateToView(_page1);
                Assert.IsTrue(_sideMenu.IsVisible, "SideMenu should be visible for override on Page1.");
                Assert.AreEqual(1, _sideMenu.Contents?.Count, "SideMenu.Contents should have one item on Page1.");
                Assert.AreSame(overrideItem, _sideMenu.Contents?.FirstOrDefault(), "SideMenu.Contents should be override item on Page1.");
                Assert.AreEqual(0, _splitView.CompactPaneLength, "CompactPaneLength should be 0 on Page1.");

                // Act & Assert for Page2 (no override, default behavior)
                SimulateNavigateToView(_page2);
                Assert.IsTrue(_sideMenu.IsVisible, "SideMenu should be visible for default behavior on Page2.");
                Assert.AreNotEqual(0, _splitView.CompactPaneLength, "CompactPaneLength should NOT be 0 on Page2 (default behavior).");
                
                var defaultSideMenuItems = typeof(ShellView).GetField("_sideMenuItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_shellView) as AvaloniaList<SideMenuItem>;
                Assert.AreSame(defaultSideMenuItems, _sideMenu.Items, "SideMenu.Items should revert to default on Page2.");
                Assert.IsNull(_sideMenu.ContentsTemplate, "SideMenu.ContentsTemplate should be null for default item list behavior on Page2.");
                // Check if the specific override item from page1 is gone from _sideMenu.Contents
                Assert.IsTrue(_sideMenu.Contents == null || !_sideMenu.Contents.Contains(overrideItem), "SideMenu.Contents should not contain Page1's override item when on Page2.");
            });
        }
    }
}

// Notes on potential improvements/requirements for real test environment:
// 1. TestApplication: For full Avalonia initialization, TestApplication.Current might be needed.
//    This helps with styling, resources, and the dispatcher.
// 2. Reflection for Internals: Accessing internal fields like _splitView, _sideMenu is done via reflection.
//    Making them 'internal protected' or providing internal test helpers in ShellView might be cleaner.
// 3. Navigator Simulation: SimulateNavigateToView is basic. A more robust test might involve
//    a mock INavigator or a test version of the Navigator that allows inspecting its calls.
// 4. OnApplyTemplate: The manual assignment of parts in SetUp is a simplification.
//    If ShellView's OnApplyTemplate has complex logic, it might need to be callable in tests,
//    or its effects fully replicated.
// 5. UpdateBinding(): If this method affects side menu behavior, it should also be called in SimulateNavigateToView.
//    For now, assuming UpdateSideMenu() is the primary method for these tests.
// 6. AvaloniaList<object> for _sideMenu.Items: The current SideMenu uses AvaloniaList<object> for its Items
//    property when dealing with default menu items. The tests for default items check against _sideMenuItems.
// 7. DefaultSideMenuSize in Assert: `Assert.AreEqual(_shellView.DefaultSideMenuSize, _splitView.OpenPaneLength)`
//    This is correct for Keep behavior.
// 8. _sideMenu.Contents vs _sideMenu.Items: When override is active, content goes into _sideMenu.Contents.
//    When default, items are in _sideMenu.Items. Test_NoSideMenuOverride_RespectsDefaultBehavior
//    and Test_SideMenuOverride_ThenNoOverride_SwitchesCorrectly reflect this.
// 9. Dispatcher.UIThread.InvokeAsync: Wrapping test logic in this to ensure Avalonia UI objects
//    are created and manipulated on the correct thread.
