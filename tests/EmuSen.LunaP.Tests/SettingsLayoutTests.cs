using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;
using Xunit.Abstractions;

namespace EmuSen.LunaP.Tests
{
    // A settings screen: the strip, whole rows, the pane with its previews and the help band where the design puts them, the trail of pages, stepping, and the help bar from the focus - see docs/LunaP.md §213.
    public class SettingsLayoutTests(ITestOutputHelper output)
    {
        public static TheoryData<int, int> Screens() => new() { { 1280, 800 }, { 1920, 1200 }, { 1920, 1080 }, { 1280, 720 } };

        private static readonly AdaptiveSection[] Sections =
            [new("controllers", "Controllers"), new("display", "Display"), new("sound", "Sound"), new("consoles", "Consoles"), new("library", "Library"), new("emusen", "EmuSen")];

        private static List<PageRow> Display() =>
        [
            new("shape", "Picture shape") { Kind = PageRowKind.Steps, Status = "Console's", Sentence = "The screen each console was made for." },
            new("crt", "CRT shader") { Kind = PageRowKind.Steps, Status = "Balanced", Choices = ["Off", "Performance", "Balanced", "Accurate"] },
            new("levers", "Shader levers") { Kind = PageRowKind.Opens },
            new("crop", "Crop overscan") { Kind = PageRowKind.Steps, Status = "Console's" },
            new("variant", "Light or dark") { Kind = PageRowKind.Steps, Status = "Dark" },
            new("volume", "Volume") { Kind = PageRowKind.Steps, Status = "80%" },
            new("reset", "Reset") { Kind = PageRowKind.Asks },
            new("cards", "Memory cards") { Kind = PageRowKind.Opens, IsEnabled = false },
        ];

        private static List<PageRow> Many(int n) => Enumerable.Range(0, n).Select(i => new PageRow($"c{i}", $"Console {i}") { Kind = PageRowKind.Opens }).ToList();

        private static (Window Window, SettingsLayout Layout, List<string> Said) Screen(int width, int height, IEnumerable<PageRow>? rows = null)
        {
            var said = new List<string>();
            var layout = new SettingsLayout();
            layout.SetSections(Sections, "display");
            layout.SetRows(rows ?? Display());
            layout.PaneFor = key => key switch
            {
                "crt" => new SettingsPane([new SettingsLine("Balanced: the mask and the bloom at half strength.")]) { Preview = new SettingsPicture(null, "Granite Choir · Balanced CRT") },
                "volume" => new SettingsPane([new SettingsLine("The game's volume.")]) { Preview = new SettingsMeter(0.8) },
                "variant" => new SettingsPane([new SettingsLine("Dark is the big screen's default.")]) { Preview = new SettingsVariants("Dark", "Light") },
                "cards" => new SettingsPane([new SettingsLine("No firmware needed", SettingsTone.Success) { Mark = "✓" }, new SettingsLine("What every game shares.", SettingsTone.Muted)]),
                _ => null,
            };
            layout.HintsFor = key => key == "levers"
                ? [new PageHint(PadGlyphButton.South, "Open"), new PageHint(PadGlyphButton.East, "Back")]
                : [new PageHint(PadGlyphButton.South, "Change"), new PageHint(PadGlyphButton.DPadLeftRight, "Value"), new PageHint(PadGlyphButton.Shoulders, "Section"), new PageHint(PadGlyphButton.East, "Back"), new PageHint(PadGlyphButton.West, "Reset", "reset")];
            layout.SectionChosen += k => said.Add("section " + k);
            layout.RowChosen += k => said.Add("row " + k);
            layout.RowStepped += (k, by) => said.Add($"step {k} {by}");
            layout.HintInvoked += k => said.Add("hint " + k);
            MenuPanel.SetScale(layout, height / SettingsLayout.DesignHeight);
            var window = new Window { Width = width, Height = height, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            return (window, layout, said);
        }

        private static Rect At(Control root, string name, double u)
        {
            Control c = root.GetVisualDescendants().OfType<Control>().First(x => x.Name == name);
            return new Rect(c.TranslatePoint(default, root)!.Value, c.Bounds.Size * u);
        }

        private static void Picture(Window window, string name)
        {
            if (Environment.GetEnvironmentVariable("EMUSEN_UI_DUMP") is not { Length: > 0 }) return;
            using WriteableBitmap? frame = window.CaptureRenderedFrame();
            if (frame is not null) UiTest.Dump(name, frame);
        }

        [Theory]
        [MemberData(nameof(Screens))]
        public Task The_screen_fits_with_its_parts_where_the_design_puts_them(int width, int height) => UiTest.Run(() =>
        {
            var (window, layout, _) = Screen(width, height);
            double u = height / SettingsLayout.DesignHeight, units = width / u;
            layout.FocusOn("crt");
            UiTest.Settle(layout);
            Picture(window, $"settings-{width}x{height}");
            Rect pane = At(layout, "SettingsPane", u);
            Assert.Equal(new Rect((units - 64 - 416) * u, 108 * u, 416 * u, 578 * u), pane, new RectComparer(1.5));
            Rect rows = At(layout, "SettingsRows", u);
            Assert.Equal(64 * u, rows.X, 1.5);
            Assert.Equal(108 * u, rows.Y, 1.5);
            Assert.Equal(pane.X - 32 * u, rows.Right, 1.5);
            Assert.Equal(40 * u, At(layout, "SettingsSections", 1).Y, 1.5);
            Assert.Equal(702 * u, At(layout, "SettingsHelpBar", 1).Y, 1.5);
            Assert.Equal(54 * u, At(layout, "SettingsRow.crt", u).Height, 1.5);
            Assert.Equal(new Size(368 * u, 276 * u), At(layout, "SettingsPicture", u).Size);
            List<string> faults = FitAudit.Check(layout, []);
            foreach (string f in faults) output.WriteLine($"{width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

        [Fact]
        public Task The_desktop_form_draws_switches_dropdowns_and_the_pane_only_for_a_picture() => UiTest.Run(() =>
        {
            var said = new List<string>();
            var layout = new SettingsLayout { Form = AdaptiveForm.Desktop };
            layout.SetSections(Sections, "display");
            layout.SetRows(Display().Prepend(new PageRow("art", "Background art on Home") { Kind = PageRowKind.Steps, Status = "On", Choices = ["On", "Off"] }));
            layout.PaneFor = key => key == "crt" ? new SettingsPane([new SettingsLine("Balanced.")]) { Preview = new SettingsPicture(null, "Granite Choir") } : null;
            layout.RowStepped += (k, by) => said.Add($"step {k} {by}");
            layout.BackRequested += () => said.Add("back");
            var window = new Window { Width = 960, Height = 640, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            bool Has(string name) => layout.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name);
            Assert.Single(layout.GetVisualDescendants().OfType<LunaSwitch>());
            Assert.Contains(layout.GetVisualDescendants().OfType<ComboBox>(), b => (string?)b.SelectedItem == "Balanced");
            Assert.False(Has("SettingsPane"));
            layout.FocusOn("crt");
            UiTest.Settle(layout);
            Assert.True(Has("SettingsPane"));
            layout.GetVisualDescendants().OfType<LunaSwitch>().Single().IsChecked = false;
            Assert.Equal(["step art 1"], said);
            layout.SetTrail(["Display", "Shader levers"]);
            UiTest.Settle(layout);
            Picture(window, "settings-desktop-960x640");
            layout.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SettingsBack").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(["step art 1", "back"], said);
            Assert.Empty(FitAudit.Check(layout, [], smallest: 0));
            window.Close();
        });

        [Fact]
        public Task Rows_are_whole_ten_at_a_time_and_the_window_moves_a_row_at_a_time() => UiTest.Run(() =>
        {
            var (window, layout, _) = Screen(1280, 800, Many(14));
            Assert.Equal(10, layout.RowsShown);
            Assert.Equal(Enumerable.Range(0, 10).Select(i => $"c{i}"), layout.Shown);
            for (int i = 0; i < 10; i++) layout.Move(0, 1);
            Assert.Equal("c10", layout.Focused);
            Assert.Equal("c1", layout.Shown[0]);
            Assert.True(layout.Move(0, -1));
            Assert.Equal("c1", layout.Shown[0]);
            for (int i = 0; i < 20; i++) layout.Move(0, 1);
            Assert.Equal("c13", layout.Focused);
            Assert.Equal("c4", layout.Shown[0]);
            Assert.False(layout.Move(0, 1));
            window.Close();
        });

        [Fact]
        public Task Left_and_Right_step_only_a_Steps_row_and_A_passes_over_a_row_not_enabled() => UiTest.Run(() =>
        {
            var (window, layout, said) = Screen(1280, 800);
            Assert.True(layout.Move(1, 0));
            layout.FocusOn("levers");
            Assert.False(layout.Move(-1, 0));
            Assert.True(layout.Press(PadGlyphButton.South));
            layout.FocusOn("cards");
            Assert.True(layout.Press(PadGlyphButton.South));
            Assert.Equal(["step shape 1", "row levers"], said);
            window.Close();
        });

        [Fact]
        public Task The_trail_takes_the_strip_and_the_shoulders_are_the_host_s_under_it() => UiTest.Run(() =>
        {
            var (window, layout, said) = Screen(1280, 800);
            Assert.True(layout.Press(PadGlyphButton.RightShoulder));
            Assert.Equal("sound", layout.SelectedSection);
            layout.SetTrail(["Consoles", "Super Nintendo"]);
            UiTest.Settle(layout);
            Assert.False(layout.Press(PadGlyphButton.RightShoulder));
            Assert.False(layout.Press(PadGlyphButton.East));
            var strip = (Panel)layout.GetVisualDescendants().OfType<Control>().First(c => c.Name == "SettingsSections");
            Assert.Equal(["CONSOLES", "SUPER NINTENDO"], strip.Children.OfType<Avalonia.Controls.Primitives.ToggleButton>().Select(b => (string)b.Content!));
            Assert.Equal(true, strip.Children.OfType<Avalonia.Controls.Primitives.ToggleButton>().Last().IsChecked);
            Assert.Equal(["section sound"], said);
            window.Close();
        });

        [Fact]
        public Task The_pane_draws_each_preview_and_falls_back_to_the_row_s_sentence() => UiTest.Run(() =>
        {
            var (window, layout, _) = Screen(1280, 800);
            string Words() => string.Join(" | ", layout.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "SettingsLine").Select(t => t.Inlines?.Count > 0 ? string.Concat(t.Inlines.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text)) : t.Text));
            bool Has(string name) => layout.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name);
            Assert.Equal("The screen each console was made for.", Words());
            layout.FocusOn("volume");
            Assert.True(Has("SettingsMeter"));
            layout.FocusOn("variant");
            Assert.True(Has("SettingsVariants"));
            Assert.True(Has("SettingsVariant.Light"));
            layout.FocusOn("cards");
            Assert.Equal("✓ No firmware needed | What every game shares.", Words());
            Assert.False(Has("SettingsPicture"));
            window.Close();
        });

        [Fact]
        public Task The_help_bar_is_generated_from_the_focus_and_a_hint_s_key_is_raised() => UiTest.Run(() =>
        {
            var (window, layout, said) = Screen(1280, 800);
            Assert.Equal(["Change", "Value", "Section", "Back", "Reset"], layout.Hints.Select(h => h.Label));
            Assert.True(layout.Press(PadGlyphButton.West));
            layout.FocusOn("levers");
            Assert.Equal(["Open", "Back"], layout.Hints.Select(h => h.Label));
            Assert.False(layout.Press(PadGlyphButton.West));
            Assert.Equal(["hint reset"], said);
            window.Close();
        });

        // §213.7: a page of content in place of the rows, its card, tabs, body and caption, the pane asked for its focus, the directions the host's.
        [Theory]
        [MemberData(nameof(Screens))]
        public Task A_page_of_content_is_one_card_with_its_tabs_body_and_caption_and_the_directions_are_the_hosts(int width, int height) => UiTest.Run(() =>
        {
            var (window, layout, said) = Screen(width, height);
            double u = height / SettingsLayout.DesignHeight;
            var moved = new List<string>();
            var tabs = new List<int>();
            layout.ContentMoved += (dx, dy) => moved.Add($"{dx},{dy}");
            layout.ContentTabChosen += at => tabs.Add(at);
            layout.PaneFor = key => key == "bind:Y" ? new SettingsPane([new SettingsLine("Bound to the pad's West button.")]) : null;
            layout.SetTrail(["Controllers", "Controller bindings"]);
            var body = new Border { Name = "Drawing" };
            layout.SetContent(new SettingsContent("bind:Y", body) { Tabs = ["Player 1", "Super Nintendo", "Genesis"], Chosen = 1, Caption = "Left and Right move between the drawing's regions." });
            UiTest.Settle(layout);
            Picture(window, $"settings-content-{width}x{height}");

            Rect card = At(layout, "SettingsContent", u), pane = At(layout, "SettingsPane", u);
            Assert.Equal(new Rect(64 * u, 108 * u, pane.X - 32 * u - 64 * u, 578 * u), card, new RectComparer(1.5));
            Assert.Equal(44 * u, At(layout, "SettingsContentTabs", u).Height, 1.5);
            Assert.Equal(380 * u, At(layout, "SettingsContentBody", u).Height, 1.5);
            Assert.Same(body, layout.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SettingsContentBody").Child);
            Assert.Contains(layout.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Bound to the pad's West button.");
            Assert.DoesNotContain(layout.GetVisualDescendants().OfType<Control>(), c => c.Name == "SettingsRows");

            // The directions are the host's, never a row's step; A chooses the content's focus.
            Assert.True(layout.Move(-1, 0));
            Assert.True(layout.Move(0, 1));
            Assert.Equal(["-1,0", "0,1"], moved);
            Assert.True(layout.Press(PadGlyphButton.South));
            Assert.Equal(["row bind:Y"], said);
            layout.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().First(b => (string?)b.Content == "GENESIS").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Primitives.ToggleButton.ClickEvent));
            Assert.Equal([2], tabs);
            Assert.Empty(FitAudit.Check(layout, []));

            // Null gives the rows back, with their focus.
            layout.SetContent(null);
            UiTest.Settle(layout);
            Assert.Null(layout.Content);
            Assert.Contains(layout.GetVisualDescendants().OfType<Control>(), c => c.Name == "SettingsRows");
            window.Close();
        });

        [Fact]
        public Task The_desktop_draws_a_page_of_content_with_its_pane_though_it_has_no_picture() => UiTest.Run(() =>
        {
            var layout = new SettingsLayout { Form = AdaptiveForm.Desktop };
            layout.SetSections(Sections, "controllers");
            layout.SetTrail(["Controllers", "Controller bindings"]);
            layout.PaneFor = key => new SettingsPane([new SettingsLine("Bound to the pad's West button.")]);
            layout.SetContent(new SettingsContent("bind:Y", new Border { Name = "Drawing", MinHeight = 200 }) { Tabs = ["Player 1", "Super Nintendo"], Chosen = 1, Caption = "A rebinds the focused one." });
            var window = new Window { Width = 960, Height = 640, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            Picture(window, "settings-content-desktop-960x640");
            bool Has(string name) => layout.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name);
            Assert.True(Has("SettingsContent") && Has("SettingsContentTabs") && Has("Drawing") && Has("SettingsContentCaption") && Has("SettingsPane"));
            Assert.Empty(FitAudit.Check(layout, [], smallest: 0));
            window.Close();
        });

        private static AutomationPeer PeerOf(Control root, string name) =>
            ControlAutomationPeer.CreatePeerForElement(root.GetVisualDescendants().OfType<Control>().First(c => c.Name == name));

        private static string? Chosen(Control root, string name) =>
            PeerOf(root, name).GetProvider<ISelectionProvider>() is { } list ? string.Join("|", list.GetSelection().Select(p => p.GetName())) : null;

        // What a platform bridge reads as the focus: the peer of the element the focus manager holds.
        private static AutomationPeer? Focus(Window window) =>
            window.FocusManager?.GetFocusedElement() is Control c ? ControlAutomationPeer.CreatePeerForElement(c) : null;

        // The reader's view of the big screen (§213.8): the focused row selected and holding the focus, told on every move, and the chosen section selected.
        [Fact]
        public Task The_focused_row_and_the_chosen_section_are_reported_selected_and_the_focus_follows_the_pad() => UiTest.Run(() =>
        {
            var (window, layout, said) = Screen(1280, 800);
            var told = new List<object?>();
            window.AddHandler(InputElement.GotFocusEvent, (_, e) => told.Add(e.Source));
            layout.Focus();
            UiTest.Settle(layout);
            Assert.Equal("Picture shape, Console's", Chosen(layout, "SettingsRows"));
            Assert.Equal("Picture shape, Console's", Focus(window)?.GetName());
            Assert.Equal("Display", Chosen(layout, "SettingsSections"));
            foreach (string expected in new[] { "CRT shader, Balanced", "Shader levers", "Crop overscan, Console's" })
            {
                int before = told.Count;
                Assert.True(layout.Move(0, 1));
                UiTest.Settle(layout);
                Assert.True(told.Count > before, "the move was not told");
                Assert.DoesNotContain(layout, told.Skip(before));
                AutomationPeer focused = Focus(window)!;
                Assert.Equal(expected, focused.GetName());
                Assert.True(focused.HasKeyboardFocus());
                Assert.True(focused.GetProvider<ISelectionItemProvider>()?.IsSelected, "the focused row is not selected");
                Assert.Equal(expected, Chosen(layout, "SettingsRows"));
                Assert.Single(PeerOf(layout, "SettingsRows").GetChildren(), p => p.GetProvider<ISelectionItemProvider>()?.IsSelected == true);
            }
            layout.SetRows(Display().Select(r => r.Key == "crop" ? r with { Status = "None" } : r));
            UiTest.Settle(layout);
            Assert.Equal("Crop overscan, None", Focus(window)?.GetName());
            PeerOf(layout, "SettingsRow.volume").GetProvider<ISelectionItemProvider>()!.Select();
            UiTest.Settle(layout);
            Assert.Equal("volume", layout.Focused);
            Assert.Equal("Volume, 80%", Focus(window)?.GetName());
            Assert.True(layout.StepSection(1));
            UiTest.Settle(layout);
            Assert.Equal("Sound", Chosen(layout, "SettingsSections"));
            Assert.Equal(AutomationControlType.TabItem, PeerOf(layout, "SettingsSections").GetChildren().First().GetAutomationControlType());
            layout.SetContent(new SettingsContent("bind:Y", new Border { Name = "Drawing" }));
            UiTest.Settle(layout);
            Assert.Same(layout, window.FocusManager!.GetFocusedElement());
            window.Close();
        });

        // The desktop form's rows are real controls: the focused one is what the reader is given.
        [Fact]
        public Task The_desktop_form_reports_its_focused_control() => UiTest.Run(() =>
        {
            var layout = new SettingsLayout { Form = AdaptiveForm.Desktop };
            layout.SetSections(Sections, "display");
            layout.SetRows(Display());
            var window = new Window { Width = 960, Height = 640, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            Control row = layout.GetVisualDescendants().OfType<Control>().First(c => c.Name == "SettingsRow.levers");
            Assert.True(row.Focus());
            Assert.Equal("Shader levers", Focus(window)?.GetName());
            Assert.True(Focus(window)!.HasKeyboardFocus());
            ComboBox crt = layout.GetVisualDescendants().OfType<ComboBox>().First(b => (string?)b.SelectedItem == "Balanced");
            Assert.True(crt.Focus());
            Assert.Equal("CRT shader", Focus(window)?.GetName());
            window.Close();
        });

        private sealed class RectComparer(double slack) : IEqualityComparer<Rect>
        {
            public bool Equals(Rect a, Rect b) => Math.Abs(a.X - b.X) <= slack && Math.Abs(a.Y - b.Y) <= slack && Math.Abs(a.Width - b.Width) <= slack && Math.Abs(a.Height - b.Height) <= slack;
            public int GetHashCode(Rect r) => 0;
        }
    }
}
