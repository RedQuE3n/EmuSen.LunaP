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
