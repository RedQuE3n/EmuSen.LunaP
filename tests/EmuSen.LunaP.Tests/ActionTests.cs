using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Settings;

namespace EmuSen.LunaP.Tests
{
    // LunaAction, ActionGroup and the menu builder - see docs/LunaP.md §26.3 and §26.4.
    //
    // Most of this needs no window at all, which is itself worth noticing: the command layer is
    // plain objects, and the only reason any of it touches Avalonia is the KeyGesture it carries
    // and the controls it is turned into. A command model that could only be tested through a
    // rendered menu would be a command model welded to one.
    public class ActionTests : IDisposable
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ActionTests).GetTypeInfo().Assembly);

        private readonly List<string> _diagnostics = new();

        public ActionTests() => LunaSettings.Diagnostics = _diagnostics.Add;

        public void Dispose() => LunaSettings.Diagnostics = null;

        [Fact]
        public void An_action_runs_its_handler_when_invoked()
        {
            int ran = 0;
            var action = new LunaAction("Save", () => ran++);

            action.Invoke();

            Assert.Equal(1, ran);
        }

        // Qt's order, and the only one that makes a one-line handler possible: a checkable action
        // has already flipped by the time the handler asks what it is now.
        [Fact]
        public void A_checkable_action_has_already_flipped_when_its_handler_runs()
        {
            bool? seen = null;
            var action = new LunaAction("Grid", self => seen = self.IsChecked) { IsCheckable = true };

            action.Invoke();

            Assert.True(seen);
            Assert.True(action.IsChecked);

            action.Invoke();

            Assert.False(seen);
            Assert.False(action.IsChecked);
        }

        // The whole reason CanExecute exists here. A disabled action reached through a stale
        // toolbar button or a key binding that outlived its window must do NOTHING - not the
        // handler, and not the state change either, which would leave a menu ticked for a command
        // that never ran.
        [Fact]
        public void A_disabled_action_neither_runs_nor_flips()
        {
            int ran = 0;
            var action = new LunaAction("Grid", () => ran++) { IsCheckable = true, IsEnabled = false };

            action.Invoke();
            ((ICommand)action).Execute(null);

            Assert.Equal(0, ran);
            Assert.False(action.IsChecked);
            Assert.False(((ICommand)action).CanExecute(null));
        }

        // A stock Avalonia Button listens to this and to nothing else, so an action whose enabled
        // state moved silently would grey out in the menu and stay clickable on the toolbar.
        [Fact]
        public void Disabling_an_action_tells_ICommand_listeners()
        {
            var action = new LunaAction("Save");
            int raised = 0;
            action.CanExecuteChanged += (_, _) => raised++;

            action.IsEnabled = false;
            action.IsEnabled = false;

            Assert.Equal(1, raised);
        }

        [Fact]
        public void Changing_the_label_tells_the_surfaces_showing_it()
        {
            var action = new LunaAction("Pause");
            int changed = 0;
            action.Changed += _ => changed++;

            action.Text = "Resume";

            Assert.Equal(1, changed);
            Assert.Equal("Resume", action.Text);
        }

        [Fact]
        public void A_group_unchecks_everything_else()
        {
            var group = new ActionGroup();
            LunaAction dark = group.Add("Dark");
            LunaAction light = group.Add("Light");
            LunaAction solar = group.Add("Solarised");

            dark.IsChecked = true;
            Assert.Same(dark, group.Checked);

            light.Invoke();

            Assert.True(light.IsChecked);
            Assert.False(dark.IsChecked);
            Assert.False(solar.IsChecked);
            Assert.Same(light, group.Checked);
        }

        // The difference between a radio set and a row of switches. Clicking the one that is
        // already on is not a request to have none of them on.
        [Fact]
        public void A_grouped_action_cannot_be_unchecked_by_invoking_it_again()
        {
            var group = new ActionGroup();
            LunaAction dark = group.Add("Dark");
            group.Add("Light");

            dark.Invoke();
            dark.Invoke();

            Assert.True(dark.IsChecked);
            Assert.Same(dark, group.Checked);
        }

        [Fact]
        public void Joining_a_group_makes_an_action_checkable()
        {
            var action = new LunaAction("Dark");
            Assert.False(action.IsCheckable);

            new ActionGroup().Add(action);

            Assert.True(action.IsCheckable);
        }

        [Fact]
        public void A_separator_is_not_a_command()
        {
            LunaAction separator = LunaAction.Separator();
            int ran = 0;
            separator.Invoked += _ => ran++;

            separator.Invoke();

            Assert.True(separator.IsSeparator);
            Assert.Equal(0, ran);
            Assert.False(((ICommand)separator).CanExecute(null));
        }

        // The walk the shortcut binder does. Submenus are reached and separators are not returned,
        // because binding a key to a divider is binding a key to nothing.
        [Fact]
        public void A_menu_walks_its_submenus_and_skips_separators()
        {
            var nested = new LunaMenu("Recent", new LunaAction("smw.sfc"), new LunaAction("zelda.sfc"));
            var file = new LunaMenu("File",
                new LunaAction("Open"),
                LunaAction.Separator(),
                new LunaAction("Recent") { Submenu = nested },
                new LunaAction("Quit"));

            string[] names = file.Commands().Select(a => a.Text).ToArray();

            Assert.Equal(new[] { "Open", "Recent", "smw.sfc", "zelda.sfc", "Quit" }, names);
        }

        [Fact]
        public Task Binding_shortcuts_puts_one_key_binding_on_the_window_per_gesture() => Session.Dispatch(() =>
        {
            var window = new Window();
            var menu = new LunaMenu("File",
                new LunaAction("Open") { Shortcut = KeyGesture.Parse("Ctrl+O") },
                new LunaAction("Save") { Shortcut = KeyGesture.Parse("Ctrl+S") },
                new LunaAction("About"));

            IReadOnlyList<KeyBinding> bound = Menus.BindShortcuts(window, new[] { menu });

            // Two, not three: an action with no shortcut binds nothing.
            Assert.Equal(2, bound.Count);
            Assert.Equal(2, window.KeyBindings.Count);

            Menus.Unbind(window, bound);
            Assert.Empty(window.KeyBindings);
        }, default);

        // TWO COMMANDS ON ONE KEY, which Avalonia resolves by running the first and ignoring the
        // second - in silence, while the menu goes on showing the shortcut beside both of them.
        [Fact]
        public Task A_shortcut_claimed_twice_is_reported_and_only_the_first_is_bound() => Session.Dispatch(() =>
        {
            var window = new Window();
            var menu = new LunaMenu("File",
                new LunaAction("Save") { Shortcut = KeyGesture.Parse("Ctrl+S") },
                new LunaAction("Store") { Shortcut = KeyGesture.Parse("Ctrl+S") });

            IReadOnlyList<KeyBinding> bound = Menus.BindShortcuts(window, new[] { menu });

            Assert.Single(bound);
            Assert.Contains(_diagnostics, d => d.Contains("Save") && d.Contains("Store"));
        }, default);

        // The arrangement this whole design is for - one action in the menu AND on the toolbar -
        // must not be reported as a collision, or the message that catches a real one gets ignored.
        [Fact]
        public Task The_same_action_in_two_places_is_not_a_conflict() => Session.Dispatch(() =>
        {
            var window = new Window();
            var save = new LunaAction("Save") { Shortcut = KeyGesture.Parse("Ctrl+S") };

            IReadOnlyList<KeyBinding> bound = Menus.BindShortcuts(window, new[] { save, save });

            Assert.Single(bound);
            Assert.Empty(_diagnostics);
        }, default);

        [Fact]
        public Task A_context_menu_is_items_and_separators_in_the_order_given() => Session.Dispatch(() =>
        {
            ContextMenu menu = Menus.Context(
                new LunaAction("Copy"),
                LunaAction.Separator(),
                new LunaAction("Delete"));

            Control[] items = menu.ItemsSource!.Cast<Control>().ToArray();

            Assert.IsType<ActionMenuItem>(items[0]);
            Assert.IsType<Separator>(items[1]);
            Assert.IsType<ActionMenuItem>(items[2]);
        }, default);
    }
}
