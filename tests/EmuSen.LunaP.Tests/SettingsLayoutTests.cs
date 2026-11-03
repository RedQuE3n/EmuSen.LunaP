using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
            new("crt", "CRT shader") { Kind = PageRowKind.Steps, Status = "Balanced" },
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

        private sealed class RectComparer(double slack) : IEqualityComparer<Rect>
        {
            public bool Equals(Rect a, Rect b) => Math.Abs(a.X - b.X) <= slack && Math.Abs(a.Y - b.Y) <= slack && Math.Abs(a.Width - b.Width) <= slack && Math.Abs(a.Height - b.Height) <= slack;
            public int GetHashCode(Rect r) => 0;
        }
    }
}
