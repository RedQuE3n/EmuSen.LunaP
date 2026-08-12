using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Fluent;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // What LunaP's controls tell a screen reader - see docs/LunaP.md §24.
    //
    // THE ASSERTION THAT MATTERS IS IsControlElement, not the name. A control with an empty name is
    // a control somebody forgot to label; a control outside the control view is one assistive
    // technology never reaches, and no amount of naming it helps. Every LunaP control was in the
    // second category before §24 (§24.1), and the reason was invisible from the code: Avalonia's
    // default peer reports IsControlElement = false, and its template parts are hidden too on the
    // assumption that the control speaks for them.
    public class AccessibilityTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AccessibilityTests).GetTypeInfo().Assembly);

        // Every LunaP control, with the type it should report. A control added to the kit and not
        // added here is a control this file cannot speak for, which is the point of listing them.
        public static TheoryData<string, AutomationControlType> Controls => new()
        {
            { nameof(MeterRow), AutomationControlType.ProgressBar },
            { nameof(MeterList), AutomationControlType.Group },
            { nameof(EmptyState), AutomationControlType.Text },
            { nameof(FieldRow), AutomationControlType.Group },
            { nameof(PathPickerRow), AutomationControlType.Group },
            { nameof(FilterBar), AutomationControlType.Group },
            { nameof(ConsolePane), AutomationControlType.Group },
            { nameof(StatusBar), AutomationControlType.StatusBar },
            { nameof(ButtonBar), AutomationControlType.ToolBar },
            { nameof(RgbaImageView), AutomationControlType.Image },

            // The shell (§26). A menu bar is not here and that is deliberate rather than an
            // omission: MenuBar derives from Avalonia's Menu, which already has a peer of its own
            // reporting Menu and implementing the patterns that go with it. Overriding that to
            // satisfy a list would be replacing a working answer with a Luna-shaped one.
            { nameof(ToolBar), AutomationControlType.ToolBar },
            { nameof(Card), AutomationControlType.Group },
            { nameof(SplitPane), AutomationControlType.Pane },
            { nameof(SidePanel), AutomationControlType.Pane },
        };

        [Theory]
        [MemberData(nameof(Controls))]
        public Task Every_control_is_in_the_control_view_and_says_what_it_is(
            string name, AutomationControlType expected) => Session.Dispatch(() =>
        {
            Control control = Build(name);
            using var host = Host(control);

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(control);

            Assert.True(peer.IsControlElement(),
                $"{name} is outside the control view, so a screen reader never reaches it.");
            Assert.Equal(expected, peer.GetAutomationControlType());
        }, default);

        // The name each control derives from the property it already had. These are the strings a
        // reader hears, so the mapping is worth pinning by value rather than by "is not empty".
        [Theory]
        [InlineData(nameof(MeterRow), "CPU")]
        [InlineData(nameof(EmptyState), "No cores loaded")]
        [InlineData(nameof(FieldRow), "Save folder")]
        [InlineData(nameof(PathPickerRow), "Choose a save folder")]
        [InlineData(nameof(StatusBar), "Applied 12 cheats")]
        [InlineData(nameof(LunaSwitch), "Enable rewind")]
        [InlineData(nameof(Card), "Emulation")]
        [InlineData(nameof(SidePanel), "Explorer")]
        public Task A_control_names_itself_from_the_property_it_already_had(string name, string expected) =>
            Session.Dispatch(() =>
            {
                Control control = Build(name);
                using var host = Host(control);

                Assert.Equal(expected, ControlAutomationPeer.CreatePeerForElement(control).GetName());
            }, default);

        // A meter's reading is the item status, deliberately apart from its name - §24.2.
        [Fact]
        public Task A_meter_reports_its_reading_as_status_not_as_part_of_its_name() => Session.Dispatch(() =>
        {
            var meter = new MeterRow { Label = "CPU", Percent = 62, ValueText = "62.0%" };
            using var host = Host(meter);

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(meter);

            Assert.Equal("CPU", peer.GetName());
            Assert.Equal("62.0%", peer.GetItemStatus());
        }, default);

        // The status follows the property rather than being captured when the peer was built. A
        // dashboard updates several times a second, and a peer holding the reading from whenever
        // the window opened would be worse than no reading at all.
        [Fact]
        public Task A_meters_status_follows_the_property_rather_than_being_captured() => Session.Dispatch(() =>
        {
            var meter = new MeterRow { Label = "CPU", Percent = 10, ValueText = "10.0%" };
            using var host = Host(meter);

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(meter);
            Assert.Equal("10.0%", peer.GetItemStatus());

            meter.ValueText = "91.0%";
            Assert.Equal("91.0%", peer.GetItemStatus());
        }, default);

        // Only one progress bar per meter row. The template's own bar is AccessibilityView="Raw"
        // so it does not appear beside the row saying the same number with no name attached.
        [Fact]
        public Task A_meter_row_does_not_also_expose_a_nameless_progress_bar() => Session.Dispatch(() =>
        {
            var meter = new MeterRow { Label = "CPU", Percent = 62, ValueText = "62.0%" };
            using var host = Host(meter);

            ProgressBar bar = meter.GetVisualDescendants().OfType<ProgressBar>().Single();

            Assert.False(ControlAutomationPeer.CreatePeerForElement(bar).IsControlElement(),
                "The template's ProgressBar is in the control view, so a meter announces twice.");
        }, default);

        // THE CALLER'S NAME BEATS THE CONTROL'S OWN. A toolkit that ignored AutomationProperties
        // would be worse than one that set nothing, because the caller's fix would look applied.
        [Fact]
        public Task An_explicit_name_beats_the_controls_own() => Session.Dispatch(() =>
        {
            var meter = new MeterRow { Label = "CPU", ValueText = "62.0%" };
            AutomationProperties.SetName(meter, "Processor load");
            var sw = new LunaSwitch { Label = "Enable rewind" };
            AutomationProperties.SetName(sw, "Rewind buffer");

            using var host = Host(meter, sw);

            Assert.Equal("Processor load", ControlAutomationPeer.CreatePeerForElement(meter).GetName());
            Assert.Equal("Rewind buffer", ControlAutomationPeer.CreatePeerForElement(sw).GetName());
        }, default);

        // A switch's label is in OnContent/OffContent rather than Content (§14.1), which is where
        // Avalonia's ToggleButton peer looks - so the label was invisible until §24 put it back.
        [Fact]
        public Task A_switch_announces_its_label_and_keeps_its_toggle() => Session.Dispatch(() =>
        {
            var sw = new LunaSwitch { Label = "Enable rewind" };
            using var host = Host(sw);

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(sw);

            Assert.Equal("Enable rewind", peer.GetName());

            // The reason this peer subclasses Avalonia's rather than using LunaAutomationPeer: a
            // named switch nothing can read the state of would trade one silence for another.
            Assert.NotNull(peer.GetProvider<Avalonia.Automation.Provider.IToggleProvider>());
        }, default);

        // A field's label is a sibling of the control it labels, which is exactly the pairing a
        // screen reader cannot infer - §24.2.
        [Fact]
        public Task A_field_row_lends_its_label_to_the_control_inside_it() => Session.Dispatch(() =>
        {
            var box = new TextBox();
            var field = new FieldRow { Label = "Save folder", Content = box };
            using var host = Host(field);

            Assert.Equal("Save folder", ControlAutomationPeer.CreatePeerForElement(box).GetName());
        }, default);

        // LabeledBy rather than writing Name, so a caller who has named their own control keeps it.
        [Fact]
        public Task A_field_row_does_not_overwrite_a_name_the_caller_set() => Session.Dispatch(() =>
        {
            var box = new TextBox();
            AutomationProperties.SetName(box, "Where states go");
            var field = new FieldRow { Label = "Save folder", Content = box };
            using var host = Host(field);

            Assert.Equal("Where states go", ControlAutomationPeer.CreatePeerForElement(box).GetName());
        }, default);

        // Swapping the content re-pairs it. A settings page that rebuilds a row's editor would
        // otherwise end up with a labelled row containing an unlabelled control.
        [Fact]
        public Task A_field_row_re_labels_content_that_is_swapped_in_later() => Session.Dispatch(() =>
        {
            var field = new FieldRow { Label = "Save folder", Content = new TextBox() };
            using var host = Host(field);

            var replacement = new TextBox();
            field.Content = replacement;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal("Save folder", ControlAutomationPeer.CreatePeerForElement(replacement).GetName());
        }, default);

        // Four picker rows on a settings page were four buttons all called "Browse...". The visible
        // word stays the name - voice control needs it to - and BrowseTitle becomes the help text.
        [Fact]
        public Task Browse_buttons_are_told_apart_by_help_text_not_by_renaming_them() => Session.Dispatch(() =>
        {
            var saves = new PathPickerRow { BrowseTitle = "Choose a save folder" };
            var roms = new PathPickerRow { BrowseTitle = "Choose a ROM folder" };
            using var host = Host(saves, roms);

            Button savesButton = saves.GetVisualDescendants().OfType<Button>().Single();
            Button romsButton = roms.GetVisualDescendants().OfType<Button>().Single();

            AutomationPeer savesPeer = ControlAutomationPeer.CreatePeerForElement(savesButton);
            AutomationPeer romsPeer = ControlAutomationPeer.CreatePeerForElement(romsButton);

            Assert.Equal("Browse...", savesPeer.GetName());
            Assert.Equal("Browse...", romsPeer.GetName());
            Assert.Equal("Choose a save folder", savesPeer.GetHelpText());
            Assert.Equal("Choose a ROM folder", romsPeer.GetHelpText());
        }, default);

        // A status line is read rather than sought, which is what a live region is for.
        [Fact]
        public Task The_status_bar_is_a_live_region() => Session.Dispatch(() =>
        {
            var status = new StatusBar { Status = "Applied 12 cheats" };
            using var host = Host(status);

            Assert.Equal(AutomationLiveSetting.Polite,
                ControlAutomationPeer.CreatePeerForElement(status).GetLiveSetting());
        }, default);

        // A caller can turn it off, because a status line that updates continuously is a live
        // region that never stops talking. The style sets a default, not a policy.
        [Fact]
        public Task A_caller_can_turn_the_status_live_region_off() => Session.Dispatch(() =>
        {
            var status = new StatusBar { Status = "62 fps" };
            AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Off);
            using var host = Host(status);

            Assert.Equal(AutomationLiveSetting.Off,
                ControlAutomationPeer.CreatePeerForElement(status).GetLiveSetting());
        }, default);

        // Not a LunaAutomationPeer, and that is the finding rather than an exemption. MenuBar
        // derives from Avalonia's Menu, whose own peer already reports Menu and is in the control
        // view - measured here rather than assumed, because §24.1 is a whole section about a
        // toolkit that assumed exactly this and was wrong about nine controls.
        [Fact]
        public Task A_menu_bar_is_in_the_tree_through_Avalonias_own_peer() => Session.Dispatch(() =>
        {
            var bar = new MenuBar();
            bar.SetMenus(new EmuSen.LunaP.Commands.LunaMenu("File", new EmuSen.LunaP.Commands.LunaAction("Open")));
            using var host = Host(bar);

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(bar);

            Assert.True(peer.IsControlElement(), "The menu bar is outside the control view.");
            Assert.Equal(AutomationControlType.Menu, peer.GetAutomationControlType());
        }, default);

        // THE WHOLE-WINDOW GUARD, AGAIN, OVER THE SHELL. §24.1's nine missing controls were found
        // by walking a window and asking, and the shell is a whole new window's worth of chrome to
        // walk. A toolbar button whose action had no text, or a splitter that took focus with
        // nothing to say, would be caught here and nowhere else.
        [Fact]
        public Task Nothing_the_keyboard_can_reach_in_a_shell_is_unnamed() => Session.Dispatch(() =>
        {
            var open = new EmuSen.LunaP.Commands.LunaAction("Open ROM...");
            var grid = new EmuSen.LunaP.Commands.LunaAction("Grid") { IsCheckable = true };

            // The caller's own controls are named by the caller - that is not the shell's job and
            // never was. Leaving them bare here would test this file's carelessness rather than
            // the toolkit's.
            var window = new AppWindow { Width = 640, Height = 480, Central = new TextBox().AccessibleName("Document") };
            window.SetMenus(new EmuSen.LunaP.Commands.LunaMenu("File", open));
            window.SetToolBar(open, EmuSen.LunaP.Commands.LunaAction.Separator(), grid);
            window.AddPanel(new SidePanel { Title = "Explorer", Content = new TextBox().AccessibleName("Filter") });
            window.Status = "Ready.";
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var unnamed = new List<string>();
            foreach (Visual v in window.GetVisualDescendants())
            {
                if (v is not InputElement e || !e.Focusable || !e.IsTabStop || !e.IsEffectivelyVisible) continue;

                AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement((Control)v);
                if (string.IsNullOrWhiteSpace(peer.GetName())) unnamed.Add(v.GetType().Name);
            }

            window.Close();

            Assert.True(unnamed.Count == 0, "Focusable but unnamed: " + string.Join(", ", unnamed));
        }, default);

        // THE WHOLE-WINDOW GUARD, and the one most likely to catch a control added later. Anything
        // the keyboard can land on must say what it is: an unnamed tab stop is a dead end for
        // somebody who cannot see where the focus went.
        [Fact]
        public Task Nothing_the_keyboard_can_reach_is_unnamed() => Session.Dispatch(() =>
        {
            var console = new ConsolePane { Prompt = "> " };
            var dropdown = new Dropdown();
            AutomationProperties.SetName(dropdown, "Console");

            var panel = new StackPanel();
            panel.Children.Add(new FieldRow { Label = "Save folder", Content = new TextBox() });
            panel.Children.Add(new PathPickerRow { BrowseTitle = "Choose a ROM folder" });
            panel.Children.Add(new FilterBar { Placeholder = "Search games", FacetLabel = "Console:", ShowFacet = true });
            panel.Children.Add(console);
            panel.Children.Add(dropdown);
            panel.Children.Add(new LunaSwitch { Label = "Enable rewind" });

            using var host = Host(panel);

            // A console with no output has no output to name itself with - see §24.5. Giving it a
            // line is what the control is for, not a workaround for the assertion.
            console.AppendLine("emusen ready");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var unnamed = new List<string>();
            foreach (Visual v in host.Window.GetVisualDescendants())
            {
                // IsEffectivelyVisible is load-bearing: a ComboBox template carries a hidden TextBox
                // for its editable mode, and counting it reports a tab stop no keyboard can reach.
                if (v is not InputElement e || !e.Focusable || !e.IsTabStop || !e.IsEffectivelyVisible) continue;

                AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement((Control)v);
                if (string.IsNullOrWhiteSpace(peer.GetName())) unnamed.Add(v.GetType().Name);
            }

            Assert.True(unnamed.Count == 0,
                "Focusable but unnamed: " + string.Join(", ", unnamed));
        }, default);

        // The fluent surface, which exists because the attached form was used zero times across
        // three repositories - §24.3.
        [Fact]
        public Task The_fluent_helpers_set_what_their_names_say() => Session.Dispatch(() =>
        {
            var label = new TextBlock { Text = "Save folder" };
            var box = new TextBox().LabeledBy(label);
            var button = new Button { Content = "Browse..." }.HelpText("Choose a save folder");
            var region = new TextBlock { Text = "Ready" }.LiveRegion();
            var icon = new TextBlock { Text = "*" }.Decorative();
            var named = new Dropdown().AccessibleName("Console");

            using var host = Host(label, box, button, region, icon, named);

            Assert.Equal("Save folder", ControlAutomationPeer.CreatePeerForElement(box).GetName());
            Assert.Equal("Choose a save folder", ControlAutomationPeer.CreatePeerForElement(button).GetHelpText());
            Assert.Equal(AutomationLiveSetting.Polite, ControlAutomationPeer.CreatePeerForElement(region).GetLiveSetting());
            Assert.False(ControlAutomationPeer.CreatePeerForElement(icon).IsControlElement());
            Assert.Equal("Console", ControlAutomationPeer.CreatePeerForElement(named).GetName());
        }, default);

        // A blank or whitespace name does not shadow the caller's - LunaAutomationPeer.Read.
        [Fact]
        public Task A_control_with_nothing_to_say_reports_no_name_rather_than_blank() => Session.Dispatch(() =>
        {
            var meter = new MeterRow { Label = "   " };
            using var host = Host(meter);

            Assert.True(string.IsNullOrEmpty(ControlAutomationPeer.CreatePeerForElement(meter).GetName()));
        }, default);

        private static Control Build(string name) => name switch
        {
            nameof(MeterRow) => new MeterRow { Label = "CPU", Percent = 62, ValueText = "62.0%" },
            nameof(MeterList) => new MeterList(),
            nameof(EmptyState) => new EmptyState { Message = "No cores loaded", Detail = "Open a ROM to begin." },
            nameof(FieldRow) => new FieldRow { Label = "Save folder", Hint = "Where states are written", Content = new TextBox() },
            nameof(PathPickerRow) => new PathPickerRow { BrowseTitle = "Choose a save folder" },
            nameof(FilterBar) => new FilterBar { Placeholder = "Search games" },
            nameof(ConsolePane) => new ConsolePane { Prompt = "> " },
            nameof(StatusBar) => new StatusBar { Status = "Applied 12 cheats" },
            nameof(ButtonBar) => new ButtonBar(),
            nameof(RgbaImageView) => new RgbaImageView(),
            nameof(LunaSwitch) => new LunaSwitch { Label = "Enable rewind" },
            nameof(ToolBar) => Loaded(new ToolBar()),
            nameof(Card) => new Card { Header = "Emulation", Content = new TextBlock { Text = "inside" } },
            nameof(SplitPane) => new SplitPane { First = new TextBlock(), Second = new TextBlock() },
            nameof(SidePanel) => new SidePanel { Title = "Explorer", Content = new TextBlock() },
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No builder for this control."),
        };

        // A toolbar with one command in it. Empty, it is a run of nothing, and a peer over a
        // control with no items would pass this file's assertions while telling a reader nothing.
        private static ToolBar Loaded(ToolBar bar)
        {
            bar.SetActions(new EmuSen.LunaP.Commands.LunaAction("Open"));
            return bar;
        }

        // A real window with a real template pass, because a peer over a control that never got a
        // template reports whatever the unstyled control would - which is §3.1's trap again.
        private static Showing Host(params Control[] controls)
        {
            var panel = new StackPanel();
            foreach (Control c in controls) panel.Children.Add(c);

            var window = new ToolWindow { Width = 640, Height = 640, Content = panel };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            return new Showing(window);
        }

        private sealed class Showing : IDisposable
        {
            public Showing(Window window) => Window = window;

            public Window Window { get; }

            public void Dispose() => Window.Close();
        }
    }
}
