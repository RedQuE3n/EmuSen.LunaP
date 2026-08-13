using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // The light column, and the trap that decides how any of this may be asserted - see docs/LunaP.md §23.
    public class PaletteVariantTests : IDisposable
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PaletteVariantTests).GetTypeInfo().Assembly);

        // The harness pins Dark (§3.1), and these tests move it. Put it back, or every test that
        // runs afterwards inherits whatever this one left behind - the §20.2 hazard exactly.
        public void Dispose() => Session.Dispatch(
            () => Application.Current!.RequestedThemeVariant = ThemeVariant.Dark, default).GetAwaiter().GetResult();

        // COLOUR keys, not brushes, and that distinction is the finding.
        //
        // The brushes are declared once, outside the theme dictionaries, with their Color bound by
        // DynamicResource - so ONE brush instance serves both variants and reports whichever one is
        // currently active. `TryGetResource("LunaSurface", ThemeVariant.Light)` therefore hands back
        // the dark colour while the app is in dark mode, which looks like the light column not
        // working and is nothing of the sort. Measured:
        //
        //     v=Dark   color=#ff1e1e1e  brush=#ff1e1e1e
        //     v=Light  color=#fff3f3f3  brush=#ff1e1e1e     <- the brush, not the palette
        //
        // The Color keys are per-variant and honest, so they are what gets asserted here. What a
        // control actually PAINTS is asserted further down, live, which is the only way to see the
        // brush do its job.
        [Theory]
        [InlineData("LunaSurfaceColor", "#1E1E1E", "#F3F3F3")]
        [InlineData("LunaInputSurfaceColor", "#252526", "#FFFFFF")]
        [InlineData("LunaBorderColor", "#6E6E6E", "#8C8C8C")]
        [InlineData("LunaTextColor", "#D4D4D4", "#1F1F1F")]
        [InlineData("LunaMeterTextColor", "#DCDCDC", "#2A2A2A")]
        [InlineData("LunaMutedColor", "#808080", "#5F5F5F")]
        [InlineData("LunaSectionHeaderColor", "#9CDCFE", "#0A5A96")]
        [InlineData("LunaWarningColor", "#D08770", "#A34B1E")]
        [InlineData("LunaErrorColor", "#CD5C5C", "#B3261E")]
        [InlineData("LunaSuccessColor", "#2E8B57", "#1B6E3C")]
        [InlineData("LunaInfoColor", "#DAA520", "#7A5B00")]
        [InlineData("LunaNominalColor", "#32CD32", "#1B7A1B")]
        [InlineData("LunaBusyColor", "#FFD700", "#8A6300")]
        [InlineData("LunaHotColor", "#FF4500", "#B32D12")]
        public Task Every_colour_has_a_column_in_both_variants(string key, string dark, string light) =>
            Session.Dispatch(() =>
            {
                Assert.True(Application.Current!.TryGetResource(key, ThemeVariant.Dark, out object? d), key);
                Assert.True(Application.Current!.TryGetResource(key, ThemeVariant.Light, out object? l), key);

                Assert.Equal(Color.Parse(dark), Assert.IsType<Color>(d));
                Assert.Equal(Color.Parse(light), Assert.IsType<Color>(l));
            }, default);

        // The letterbox is the absence of a picture rather than a surface, so it does not lighten.
        [Fact]
        public Task The_void_stays_black_in_both_variants() => Session.Dispatch(() =>
        {
            foreach (ThemeVariant variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                Application.Current!.TryGetResource("LunaVoidColor", variant, out object? found);
                Assert.Equal(Colors.Black, Assert.IsType<Color>(found));
            }
        }, default);

        // What a control actually paints, which is the only assertion that exercises the brushes.
        [Fact]
        public Task A_window_repaints_when_the_variant_changes() => Session.Dispatch(() =>
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            var light = new ToolWindow { Width = 120, Height = 80 };
            light.Show();
            Color lightBackground = ((ISolidColorBrush)light.Background!).Color;
            light.Close();

            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            var dark = new ToolWindow { Width = 120, Height = 80 };
            dark.Show();
            Color darkBackground = ((ISolidColorBrush)dark.Background!).Color;
            dark.Close();

            Assert.Equal(Color.Parse("#F3F3F3"), lightBackground);
            Assert.Equal(Color.Parse("#1E1E1E"), darkBackground);
        }, default);

        // Dark by default, and this is the assertion that stops it quietly becoming "follow the
        // desktop" - which would be a behaviour change arriving inside a version bump for every
        // consumer on a light machine.
        [Fact]
        public void The_default_variant_is_dark_not_the_system() =>
            Assert.Equal(ThemeVariant.Dark, LunaTheme.Variant);

        // WCAG AA for body text is 4.5:1. The light column was picked against it rather than by
        // eye, and this is what holds it there - a re-derivation is only worth having if it is
        // readable, and "looks about right on my monitor" is how it stops being.
        [Theory]
        [InlineData("LunaTextColor")]
        [InlineData("LunaMeterTextColor")]
        [InlineData("LunaMutedColor")]
        [InlineData("LunaSectionHeaderColor")]
        [InlineData("LunaWarningColor")]
        [InlineData("LunaErrorColor")]
        [InlineData("LunaSuccessColor")]
        [InlineData("LunaInfoColor")]
        [InlineData("LunaNominalColor")]
        [InlineData("LunaBusyColor")]
        [InlineData("LunaHotColor")]
        public Task Every_light_foreground_clears_four_and_a_half_to_one(string key) => Session.Dispatch(() =>
        {
            Application.Current!.TryGetResource(key, ThemeVariant.Light, out object? fg);
            Application.Current!.TryGetResource("LunaSurfaceColor", ThemeVariant.Light, out object? bg);

            double ratio = Contrast((Color)fg!, (Color)bg!);
            Assert.True(ratio >= 4.5, $"{key} on the light surface is {ratio:F2}:1, below the 4.5:1 floor.");
        }, default);

        // THE DARK COLUMN IS HELD TO A LOWER BAR, AND THE REASON IS RECORDED RATHER THAN ROUNDED
        // OFF. LunaMuted on the dark surface measures 4.22:1 - short of 4.5. It is a value that has
        // shipped since before the toolkit had a name, and §2.1's rule is that changing a palette
        // literal is a deliberate one-line decision rather than something done in passing while
        // adding a light column. So it is measured, named, and left alone.
        [Theory]
        [InlineData("LunaTextColor", 4.5)]
        [InlineData("LunaMeterTextColor", 4.5)]
        [InlineData("LunaSectionHeaderColor", 4.5)]
        [InlineData("LunaMutedColor", 4.2)]
        public Task Every_dark_foreground_clears_its_floor(string key, double floor) => Session.Dispatch(() =>
        {
            Application.Current!.TryGetResource(key, ThemeVariant.Dark, out object? fg);
            Application.Current!.TryGetResource("LunaSurfaceColor", ThemeVariant.Dark, out object? bg);

            double ratio = Contrast((Color)fg!, (Color)bg!);
            Assert.True(ratio >= floor, $"{key} on the dark surface is {ratio:F2}:1, below {floor:F1}:1.");
        }, default);

        // The accent is a FILL rather than text, so it answers to WCAG 1.4.11's 3:1 for "visual
        // information required to identify user interface components" and not to 1.4.3's 4.5:1.
        // A checked CheckBox, a Slider's filled track and a focused TextBox border are all things
        // you must be able to see in order to use the control, and none of them is read as words.
        [Theory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public Task The_accent_clears_three_to_one_against_the_surface(string variantName) => Session.Dispatch(() =>
        {
            ThemeVariant variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

            Application.Current!.TryGetResource("LunaAccentColor", variant, out object? accent);
            Application.Current!.TryGetResource("LunaSurfaceColor", variant, out object? surface);

            double ratio = Contrast((Color)accent!, (Color)surface!);
            Assert.True(ratio >= 3.0, $"LunaAccent on the {variantName} surface is {ratio:F2}:1, below 3:1.");
        }, default);

        // THE PAIR THE ACCENT EXISTS AS ONE HALF OF, and it went unpinned when the two tokens were
        // added. LunaOnAccent is the tick inside a checked box and the knob of a switch that is on,
        // so it is drawn ON the accent and nowhere else - a floor against the SURFACE would be
        // measuring a pair that never appears on screen.
        //
        // 4.5:1 rather than 3:1, and that is not the accent's own bar being raised. A glyph is a
        // shape you read, which is 1.4.3 and not 1.4.11 - the tick in a checkbox is closer to text
        // than to the box around it. Both columns clear it: 4.51:1 dark, 6.31:1 light.
        //
        // Worth pinning because the comment on the token already claimed it was held to a floor
        // while nothing held it to anything, and because the two figures first written beside it
        // were 4.50 and 6.98 - the second measured by nothing. See docs/LunaP.md §48.3.
        [Theory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public Task What_sits_on_the_accent_clears_four_and_a_half_to_one(string variantName) => Session.Dispatch(() =>
        {
            ThemeVariant variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

            Application.Current!.TryGetResource("LunaOnAccentColor", variant, out object? onAccent);
            Application.Current!.TryGetResource("LunaAccentColor", variant, out object? accent);

            double ratio = Contrast((Color)onAccent!, (Color)accent!);
            Assert.True(ratio >= 4.5, $"LunaOnAccent on the {variantName} accent is {ratio:F2}:1, below 4.5:1.");
        }, default);

        // A DIFFERENT FLOOR FOR A DIFFERENT KIND OF THING. Everything above is WCAG 1.4.3, which
        // is about reading text. The border token is not text: it is the edge of a card, the rule
        // under a panel header, and the divider a splitter is dragged by - and that last one is a
        // control a keyboard can focus and move (§26.11), so it falls under 1.4.11, whose bar for
        // "visual information required to identify user interface components" is 3:1.
        //
        // Worth a test rather than a comment because the obvious value fails it: #3C3C3C is what
        // a dark theme reaches for and measures 1.51:1 on this surface, which is a divider that is
        // hardest to see for the people who most need to see it. See docs/LunaP.md §26.9.
        [Theory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public Task The_border_clears_three_to_one_against_the_surface(string variantName) => Session.Dispatch(() =>
        {
            ThemeVariant variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

            Application.Current!.TryGetResource("LunaBorderColor", variant, out object? border);
            Application.Current!.TryGetResource("LunaSurfaceColor", variant, out object? surface);

            double ratio = Contrast((Color)border!, (Color)surface!);
            Assert.True(ratio >= 3.0, $"LunaBorder on the {variantName} surface is {ratio:F2}:1, below 3:1.");
        }, default);

        // WCAG 2.x relative luminance, spelled out rather than pulled in: it is nine lines and a
        // dependency for nine lines is a decision this project would have to justify.
        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        private static double Luminance(Color c) =>
            0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

        private static double Channel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
    }
}
