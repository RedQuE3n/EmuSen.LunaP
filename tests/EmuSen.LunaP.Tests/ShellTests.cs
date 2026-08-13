using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Settings;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // The shell: menu bar, toolbar, card, splitter, side panel, AppWindow - see docs/LunaP.md §26.
    //
    // Every control is asserted through a REAL TEMPLATE PART, not through the property that fed
    // it. §5.5 records why, and this pass added five more subclasses of stock Avalonia controls,
    // each of which renders as absolutely nothing if its style key override is dropped - a failure
    // that throws no exception and fails no property assertion.
    public class ShellTests : IDisposable
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ShellTests).GetTypeInfo().Assembly);

        private readonly string _configDir;

        // Keeps remembered pane layout off whoever is running the suite, the same way
        // WindowingTests keeps remembered geometry off them.
        public ShellTests()
        {
            _configDir = Path.Combine(Path.GetTempPath(), "lunap-shell-" + Guid.NewGuid().ToString("N"));
            LunaSettings.Store = new JsonSettingsStore(_configDir);
        }

        public void Dispose()
        {
            LunaSettings.Store = new JsonSettingsStore(Path.Combine(Path.GetTempPath(), "lunap-unset"));
            if (Directory.Exists(_configDir)) Directory.Delete(_configDir, recursive: true);
        }

        private static Task Realised<T>(Func<T> make, Action<T> assert) where T : Control => Session.Dispatch(() =>
        {
            T control = make();
            var window = new ToolWindow { Width = 600, Height = 400, Content = control };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            assert(control);
            window.Close();
        }, default);

        private static Task Shown(Func<AppWindow> make, Action<AppWindow> assert) => Session.Dispatch(() =>
        {
            AppWindow window = make();
            window.Width = 800;
            window.Height = 600;
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            assert(window);
            window.Close();
        }, default);

        // ------------------------------------------------------------------ menu bar

        [Fact]
        public Task A_menu_bar_builds_one_top_level_item_per_menu() => Realised(() =>
        {
            var bar = new MenuBar();
            bar.SetMenus(
                new LunaMenu("File", new LunaAction("Open")),
                new LunaMenu("Help", new LunaAction("About")));
            return bar;
        }, bar =>
        {
            // Through the presenter, not through the ItemsSource: a Menu whose style key stopped
            // matching keeps its items and draws none of them.
            Assert.NotNull(bar.FindPart<ItemsPresenter>());

            Assert.True(bar.GetVisualChildren().Any(),
                "The menu bar has no visual children, so it borrowed no theme and drew nothing.");

            MenuItem[] items = bar.GetVisualDescendants().OfType<MenuItem>().ToArray();
            Assert.Equal(2, items.Length);
            Assert.Equal("File", items[0].Header);
            Assert.Equal("Help", items[1].Header);
        });

        // Theme/Controls/MenuBar.axaml is the only style file in the kit whose control borrows its
        // template from FluentTheme, so §28.1's sweep sees a visual tree whether that file reaches
        // the bar or not. Dropping its StyleInclude turned NOTHING red, and chasing that down found
        // the style had never applied at all: `luna|MenuBar` is a type selector, Avalonia matches
        // those against the STYLE KEY, and MenuBar pins its key to Menu so it can have a template
        // (§30). The selector is `Menu.luna-menu-bar` now.
        //
        // Padding is the half worth asserting. Before the fix its diagnostic priority was Unset -
        // nothing anywhere set it - so this value can only come from that file. Background is
        // asserted alongside it but proves less on its own: Fluent independently paints a Menu
        // transparent, which is exactly why the defect was invisible for so long.
        [Fact]
        public Task A_menu_bar_is_styled_by_the_kit_and_not_only_by_fluent() => Realised(() =>
        {
            var bar = new MenuBar();
            bar.SetMenus(new LunaMenu("File", new LunaAction("Open")));
            return bar;
        }, bar =>
        {
            Assert.Equal(new Thickness(2, 0), bar.Padding);
            Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(bar.Background).Color);
        });

        // A top-level entry is a plain MenuItem, deliberately: File is not a command, and giving
        // it an action would put three uninvokable actions in front of the shortcut binder.
        [Fact]
        public Task A_top_level_menu_is_not_an_action() => Realised(() =>
        {
            var bar = new MenuBar();
            bar.SetMenus(new LunaMenu("File", new LunaAction("Open")));
            return bar;
        }, bar =>
        {
            MenuItem top = bar.GetVisualDescendants().OfType<MenuItem>().First();
            Assert.IsNotType<ActionMenuItem>(top);
        });

        [Fact]
        public Task A_menu_item_follows_its_actions_label_and_enabled_state() => Realised(() =>
        {
            var action = new LunaAction("Pause") { Shortcut = KeyGesture.Parse("Ctrl+P") };
            return new ActionMenuItem(action);
        }, item =>
        {
            // TEMPLATED AT ALL, FIRST. Every other assertion in this test passes for a control
            // that renders as nothing: Header, IsEnabled and InputGesture are properties, and a
            // subclass whose style key stopped matching keeps all three and draws none of them.
            // This assertion was added because dropping the style key override turned NOTHING
            // red - the guard did not cover the trap it was written for. §26.11.
            item.ApplyTemplate();
            Assert.True(item.GetVisualChildren().Any(),
                "The menu item has no visual children, so it was never templated.");

            Assert.Equal("Pause", item.Header);
            Assert.True(item.IsEnabled);
            Assert.Equal(KeyGesture.Parse("Ctrl+P"), item.InputGesture);

            item.Action.Text = "Resume";
            item.Action.IsEnabled = false;

            Assert.Equal("Resume", item.Header);
            Assert.False(item.IsEnabled);
        });

        [Fact]
        public Task A_checkable_menu_item_shows_a_tick_that_follows_the_action() => Realised(() =>
        {
            var action = new LunaAction("Grid") { IsCheckable = true };
            return new ActionMenuItem(action);
        }, item =>
        {
            Assert.Equal(MenuItemToggleType.CheckBox, item.ToggleType);
            Assert.False(item.IsChecked);

            item.Action.IsChecked = true;
            Assert.True(item.IsChecked);
        });

        // THE GUARD FOR THE ORDER-OF-EVENTS PROBLEM in ActionMenuItem. Anything that moves
        // IsChecked without going through the action is put back, so a stock MenuItem toggling
        // itself on click cannot leave the tick disagreeing with the command's state.
        [Fact]
        public Task A_menu_items_tick_cannot_be_moved_behind_the_actions_back() => Realised(() =>
        {
            var action = new LunaAction("Grid") { IsCheckable = true };
            return new ActionMenuItem(action);
        }, item =>
        {
            item.IsChecked = true;

            Assert.False(item.Action.IsChecked);
            Assert.False(item.IsChecked);
        });

        // ------------------------------------------------------------------ toolbar

        [Fact]
        public Task A_toolbar_builds_buttons_toggles_and_separators() => Realised(() =>
        {
            var bar = new ToolBar();
            bar.SetActions(
                new LunaAction("Open"),
                LunaAction.Separator(),
                new LunaAction("Grid") { IsCheckable = true });
            return bar;
        }, bar =>
        {
            Assert.NotNull(bar.FindPart<ItemsPresenter>());
            Assert.Equal(1, bar.CountParts<ActionButton>());
            Assert.Equal(1, bar.CountParts<ActionToggle>());
            Assert.Equal(1, bar.CountParts<Separator>());
        });

        [Fact]
        public Task A_toolbar_button_follows_its_action() => Realised(() =>
        {
            var bar = new ToolBar();
            bar.SetActions(new LunaAction("Open ROM...") { HelpText = "Chooses a ROM to load." });
            return bar;
        }, bar =>
        {
            ActionButton button = bar.FindPart<ActionButton>()!;

            // Templated, before anything about what it says: the style-key trap again (§5.5).
            Assert.True(button.GetVisualChildren().Any(),
                "The toolbar button has no visual children, so it was never templated.");

            Assert.Equal("Open ROM...", button.Content);
            Assert.Equal("Open ROM...", ControlAutomationPeer.CreatePeerForElement(button).GetName());
            Assert.Equal("Chooses a ROM to load.", ControlAutomationPeer.CreatePeerForElement(button).GetHelpText());

            button.Action.IsEnabled = false;

            // Both, and the second one is the interesting assertion. Command alone disables
            // through IsEffectivelyEnabled and leaves IsEnabled true, which would have this
            // control and ActionToggle answering differently for the same disabled action.
            Assert.False(button.IsEffectivelyEnabled);
            Assert.False(button.IsEnabled);
        });

        // Pressing a toolbar toggle runs the command and leaves the button showing what the
        // command now says - the two cannot end up disagreeing.
        [Fact]
        public Task Pressing_a_toolbar_toggle_invokes_the_action_and_matches_it() => Realised(() =>
        {
            var bar = new ToolBar();
            bar.SetActions(new LunaAction("Grid") { IsCheckable = true });
            return bar;
        }, bar =>
        {
            ActionToggle toggle = bar.FindPart<ActionToggle>()!;
            Assert.True(toggle.GetVisualChildren().Any(),
                "The toolbar toggle has no visual children, so it was never templated.");

            var provider = ControlAutomationPeer.CreatePeerForElement(toggle).GetProvider<IToggleProvider>();

            Assert.NotNull(provider);
            provider!.Toggle();

            Assert.True(toggle.Action.IsChecked);
            Assert.True(toggle.IsChecked);

            provider.Toggle();

            Assert.False(toggle.Action.IsChecked);
            Assert.False(toggle.IsChecked);
        });

        // ------------------------------------------------------------------ card

        [Fact]
        public Task A_card_renders_its_header_through_a_real_part() => Realised(
            () => new Card { Header = "Emulation", Content = new TextBlock { Text = "inside" } },
            card =>
            {
                ContentPresenter header = card.FindNamed<ContentPresenter>("PART_Header");

                Assert.True(header.IsVisible);
                Assert.Equal("Emulation", header.Content);
            });

        [Fact]
        public Task A_card_with_no_header_collapses_the_header_row() => Realised(
            () => new Card { Content = new TextBlock { Text = "inside" } },
            card => Assert.False(card.FindNamed<ContentPresenter>("PART_Header").IsVisible));

        // ------------------------------------------------------------------ split pane

        [Fact]
        public Task A_split_pane_lays_out_a_fixed_pane_an_elastic_one_and_a_divider() => Realised(
            () => new SplitPane
            {
                FixedSize = 150,
                First = new TextBlock { Text = "left" },
                Second = new TextBlock { Text = "right" },
            },
            split =>
            {
                Grid grid = split.FindNamed<Grid>("PART_Grid");

                Assert.Equal(3, grid.ColumnDefinitions.Count);
                Assert.True(grid.ColumnDefinitions[0].Width.IsAbsolute);
                Assert.Equal(150, grid.ColumnDefinitions[0].Width.Value);
                Assert.True(grid.ColumnDefinitions[2].Width.IsStar);
                Assert.True(split.FindNamed<GridSplitter>("PART_Splitter").IsVisible);
            });

        [Fact]
        public Task The_second_pane_can_be_the_fixed_one() => Realised(
            () => new SplitPane
            {
                Fixed = SplitSide.Second,
                FixedSize = 220,
                First = new TextBlock { Text = "content" },
                Second = new TextBlock { Text = "panel" },
            },
            split =>
            {
                Grid grid = split.FindNamed<Grid>("PART_Grid");

                Assert.True(grid.ColumnDefinitions[0].Width.IsStar);
                Assert.Equal(220, grid.ColumnDefinitions[2].Width.Value);
            });

        [Fact]
        public Task A_vertical_split_uses_rows() => Realised(
            () => new SplitPane
            {
                Orientation = Orientation.Vertical,
                First = new TextBlock { Text = "top" },
                Second = new TextBlock { Text = "bottom" },
            },
            split =>
            {
                Grid grid = split.FindNamed<Grid>("PART_Grid");

                Assert.Equal(3, grid.RowDefinitions.Count);
                Assert.Empty(grid.ColumnDefinitions);
                Assert.Equal(GridResizeDirection.Rows, split.FindNamed<GridSplitter>("PART_Splitter").ResizeDirection);
            });

        // A closed panel must take its divider with it, or the middle of the window ends short
        // with a four-pixel scar where the panel used to be.
        [Fact]
        public Task An_empty_pane_takes_the_divider_with_it() => Realised(
            () => new SplitPane { First = null, Second = new TextBlock { Text = "all of it" } },
            split =>
            {
                Grid grid = split.FindNamed<Grid>("PART_Grid");

                Assert.False(split.FindNamed<GridSplitter>("PART_Splitter").IsVisible);
                Assert.Equal(0, grid.ColumnDefinitions[0].Width.Value);
                Assert.Equal(0, grid.ColumnDefinitions[1].Width.Value);
                Assert.True(grid.ColumnDefinitions[2].Width.IsStar);
            });

        // The divider is a number in a file, and this is the round trip. Dragging cannot be
        // simulated meaningfully here - the headless surface has no pointer to throw at a four
        // point target - so the drag is stood in for by what a drag does: the grid definition
        // takes a new absolute length.
        [Fact]
        public Task A_dragged_divider_is_remembered_under_its_key() => Session.Dispatch(() =>
        {
            var first = new SplitPane
            {
                PaneKey = "explorer",
                FixedSize = 200,
                First = new TextBlock(),
                Second = new TextBlock(),
            };

            var window = new ToolWindow { Width = 600, Height = 400, Content = first };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            first.FindNamed<Grid>("PART_Grid").ColumnDefinitions[0].Width = new GridLength(325);
            Assert.Equal(325, first.FixedSize);

            // Closing the window is the case that matters: a drag followed straight away by a
            // close would otherwise lose the last thing the user did.
            window.Close();

            Assert.Equal(325, PaneLayoutStore.Load("explorer")!.Size);

            var second = new SplitPane { PaneKey = "explorer", First = new TextBlock(), Second = new TextBlock() };
            var reopened = new ToolWindow { Width = 600, Height = 400, Content = second };
            reopened.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(325, second.FixedSize);
            reopened.Close();
        }, default);

        // THE DIVIDER IS A CONTROL, NOT A DECORATION, and this is what says so. Avalonia's
        // GridSplitter is a tab stop and moves on the arrow keys, which was found by the
        // whole-window accessibility guard rather than by reading the docs - so it gets a name and
        // this test holds both halves of the claim: reachable, and it does something when reached.
        [Fact]
        public Task A_divider_can_be_found_and_moved_from_the_keyboard() => Session.Dispatch(() =>
        {
            var split = new SplitPane
            {
                FixedSize = 200,
                DividerLabel = "Resize Explorer",
                First = new TextBlock(),
                Second = new TextBlock(),
            };

            var window = new ToolWindow { Width = 600, Height = 400, Content = split };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            GridSplitter splitter = split.FindNamed<GridSplitter>("PART_Splitter");
            Assert.True(splitter.IsTabStop);
            Assert.Equal("Resize Explorer", ControlAutomationPeer.CreatePeerForElement(splitter).GetName());

            Assert.True(splitter.Focus());
            window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, string.Empty);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(split.FixedSize < 200, $"The divider did not move: still {split.FixedSize}.");
            window.Close();
        }, default);

        // Opt-in, like every other persisted thing in the kit: no key, no file.
        [Fact]
        public Task A_pane_with_no_key_is_never_written_down() => Session.Dispatch(() =>
        {
            var split = new SplitPane { FixedSize = 200, First = new TextBlock(), Second = new TextBlock() };
            var window = new ToolWindow { Width = 600, Height = 400, Content = split };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            split.FindNamed<Grid>("PART_Grid").ColumnDefinitions[0].Width = new GridLength(325);
            window.Close();

            Assert.False(File.Exists(Path.Combine(_configDir, PaneLayoutStore.FileName)));
        }, default);

        // ------------------------------------------------------------------ side panel

        [Fact]
        public Task A_side_panel_shows_its_title_and_can_be_shut_from_its_own_header() => Realised(
            () => new SidePanel { Title = "Explorer", Content = new TextBlock { Text = "files" } },
            panel =>
            {
                Assert.Equal("Explorer", panel.FindNamed<TextBlock>("PART_Title").Text);

                Button close = panel.FindNamed<Button>("PART_Close");
                Assert.Equal("Close", ControlAutomationPeer.CreatePeerForElement(close).GetName());
                Assert.Equal("Explorer", ControlAutomationPeer.CreatePeerForElement(close).GetHelpText());

                ControlAutomationPeer.CreatePeerForElement(close).GetProvider<IInvokeProvider>()!.Invoke();

                Assert.False(panel.IsOpen);

                // And it actually goes away. A panel outside an AppWindow has nothing else to hide
                // it, so a close button that only moved a property would visibly do nothing.
                Assert.False(panel.IsVisible);
            });

        // Qt's toggleViewAction, and the reason it must be the same object: a View menu whose tick
        // is a second copy of the panel's state drifts the first time the close button is used.
        [Fact]
        public Task The_toggle_action_and_the_panel_are_one_state() => Realised(
            () => new SidePanel { Title = "Output" },
            panel =>
            {
                LunaAction toggle = panel.ToggleAction;

                Assert.Equal("Output", toggle.Text);
                Assert.True(toggle.IsCheckable);
                Assert.True(toggle.IsChecked);

                toggle.Invoke();
                Assert.False(panel.IsOpen);

                panel.IsOpen = true;
                Assert.True(toggle.IsChecked);

                // The menu entry is named after the panel, so renaming one renames the other.
                panel.Title = "Problems";
                Assert.Equal("Problems", toggle.Text);
            });

        [Fact]
        public Task A_panel_remembers_that_it_was_shut() => Session.Dispatch(() =>
        {
            var panel = new SidePanel { Title = "Explorer", PanelKey = "explorer" };
            var window = new ToolWindow { Width = 400, Height = 300, Content = panel };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            panel.IsOpen = false;
            window.Close();

            var again = new SidePanel { Title = "Explorer", PanelKey = "explorer" };
            var reopened = new ToolWindow { Width = 400, Height = 300, Content = again };
            reopened.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.False(again.IsOpen);
            reopened.Close();
        }, default);

        // ------------------------------------------------------------------ AppWindow

        // An AppWindow nobody has put anything in must look exactly like a ToolWindow. §9.1
        // refused a base class that changed behaviour by being inherited, and a shell that levied
        // an empty menu strip on every window deriving from it would be that again.
        [Fact]
        public Task An_empty_shell_shows_no_chrome() => Shown(() => new AppWindow(), window =>
        {
            Assert.False(window.MenuBar.IsVisible);
            Assert.False(window.ToolBar.IsVisible);
            Assert.False(window.StatusBar.IsVisible);
        });

        [Fact]
        public Task Setting_menus_shows_the_bar_and_binds_the_shortcuts() => Shown(() =>
        {
            var window = new AppWindow();
            window.SetMenus(new LunaMenu("File",
                new LunaAction("Open") { Shortcut = KeyGesture.Parse("Ctrl+O") },
                new LunaAction("Quit")));
            return window;
        }, window =>
        {
            Assert.True(window.MenuBar.IsVisible);
            Assert.Single(window.KeyBindings);
        });

        // A command taken out of the menu loses its key with it, or a window goes on answering a
        // keystroke for something it no longer offers.
        [Fact]
        public Task Replacing_the_menus_unbinds_the_keys_that_went_with_them() => Shown(() =>
        {
            var window = new AppWindow();
            window.SetMenus(new LunaMenu("File", new LunaAction("Open") { Shortcut = KeyGesture.Parse("Ctrl+O") }));
            return window;
        }, window =>
        {
            Assert.Single(window.KeyBindings);

            window.SetMenus(new LunaMenu("File", new LunaAction("Quit")));

            Assert.Empty(window.KeyBindings);
        });

        [Fact]
        public Task The_status_line_appears_when_something_is_put_in_it() => Shown(() => new AppWindow(), window =>
        {
            Assert.False(window.StatusBar.IsVisible);

            window.Status = "Ready.";

            Assert.True(window.StatusBar.IsVisible);
            Assert.Equal("Ready.", window.StatusBar.Status);

            // And stays, because a strip that came and went with the message would move every
            // control in the window each time an operation finished.
            window.Status = string.Empty;
            Assert.True(window.StatusBar.IsVisible);
        });

        [Fact]
        public Task A_panel_is_docked_to_the_side_it_names() => Shown(() =>
        {
            var window = new AppWindow { Central = new TextBlock { Text = "document" } };
            window.AddPanel(new SidePanel { Title = "Explorer", Side = PanelSide.Left });
            window.AddPanel(new SidePanel { Title = "Output", Side = PanelSide.Bottom });
            return window;
        }, window =>
        {
            SidePanel[] panels = window.GetVisualDescendants().OfType<SidePanel>().ToArray();

            Assert.Equal(2, panels.Length);
            Assert.Contains(panels, p => p.Title == "Explorer");
            Assert.Contains(panels, p => p.Title == "Output");
        });

        // Closing a panel takes it out of the layout rather than hiding it in place, which is the
        // difference between the document growing into the space and a gap where the panel was.
        [Fact]
        public Task Closing_a_panel_takes_it_out_of_the_layout() => Shown(() =>
        {
            var window = new AppWindow { Central = new TextBlock { Text = "document" } };
            window.AddPanel(new SidePanel { Title = "Explorer", Side = PanelSide.Left });
            return window;
        }, window =>
        {
            SidePanel panel = window.Panels[0];
            Assert.Single(window.GetVisualDescendants().OfType<SidePanel>());

            panel.ToggleAction.Invoke();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Empty(window.GetVisualDescendants().OfType<SidePanel>());

            panel.ToggleAction.Invoke();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Single(window.GetVisualDescendants().OfType<SidePanel>());
        });

        // One panel per side. Two on one edge is the tabbed-dock feature §26.12 records as absent,
        // and stacking them silently would be a worse answer than replacing.
        [Fact]
        public Task A_second_panel_on_one_side_replaces_the_first() => Shown(() =>
        {
            var window = new AppWindow();
            window.AddPanel(new SidePanel { Title = "Explorer", Side = PanelSide.Left });
            window.AddPanel(new SidePanel { Title = "Search", Side = PanelSide.Left });
            return window;
        }, window =>
        {
            Assert.Single(window.Panels);
            Assert.Equal("Search", window.Panels[0].Title);
        });

        [Fact]
        public Task The_shell_hands_back_a_toggle_for_every_panel() => Shown(() =>
        {
            var window = new AppWindow();
            window.AddPanel(new SidePanel { Title = "Explorer", Side = PanelSide.Left });
            window.AddPanel(new SidePanel { Title = "Output", Side = PanelSide.Bottom });
            return window;
        }, window =>
        {
            Assert.Equal(new[] { "Explorer", "Output" }, window.PanelToggles().Select(a => a.Text));
        });
    }
}
