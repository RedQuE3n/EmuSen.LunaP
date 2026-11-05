using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // The unified interface's tokens: colours in both variants and on the big screen, type, spacing, focus, motion and sound - see docs/LunaP.md §206.
    public class InterfaceTokenTests
    {
        public static readonly TheoryData<string, string, string> Columns = new()
        {
            { "LunaSurfaceColor", "#1B1B1E", "#F3F3F3" },
            { "LunaSurfaceRaisedColor", "#26262B", "#FFFFFF" },
            { "LunaInputSurfaceColor", "#0E0E10", "#FFFFFF" },
            { "LunaScrimColor", "#8C000000", "#8C000000" },
            { "LunaBorderColor", "#707070", "#8C8C8C" },
            { "LunaTextColor", "#D4D4D4", "#1F1F1F" },
            { "LunaTextStrongColor", "#EEEEF0", "#000000" },
            { "LunaMutedColor", "#8D8D91", "#5F5F5F" },
            { "LunaAccentColor", "#4C9AE8", "#005FB8" },
            { "LunaOnAccentColor", "#0E0E10", "#FFFFFF" },
            { "LunaAttentionColor", "#DAA520", "#7A5B00" },
            { "LunaSuccessColor", "#35A064", "#1B6E3C" },
            { "LunaErrorColor", "#D37070", "#B3261E" },
            { "LunaOnArtColor", "#FFFFFF", "#FFFFFF" },
            { "LunaOnArtScrimColor", "#B8000000", "#B8000000" },
            { "LunaRuleColor", "#333337", "#D6D6D6" },
        };

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Every_token_has_the_value_of_its_column_on_the_desktop_and_the_big_screen(string key, string dark, string light) => UiTest.Run(() =>
        {
            Assert.True(Application.Current!.TryGetResource(key, ThemeVariant.Dark, out object? d), key);
            Assert.True(Application.Current!.TryGetResource(key, ThemeVariant.Light, out object? l), key);
            Assert.Equal(Color.Parse(dark), d);
            Assert.Equal(Color.Parse(light), l);
            Assert.True(MenuLook.CreatePalette(light: false).TryGetResource(key, null, out object? bd), key);
            Assert.True(MenuLook.CreatePalette(light: true).TryGetResource(key, null, out object? bl), key);
            Assert.Equal(Color.Parse(dark), bd);
            Assert.Equal(Color.Parse(light), bl);
        });

        // Fluent reads these as colours, so the big screen's palette copies them from its column; missing, a sheet painted Fluent's dark defaults (§206.1).
        [Theory]
        [InlineData("ScrollBarThumbBackgroundColor", "LunaBorderColor")]
        [InlineData("SystemChromeLowColor", "LunaSurfaceRaisedColor")]
        public Task The_big_screen_gives_Fluents_colour_keys_its_columns_tokens(string key, string token) => UiTest.Run(() =>
        {
            foreach (bool light in new[] { false, true })
            {
                ResourceDictionary palette = MenuLook.CreatePalette(light);
                Assert.True(palette.TryGetResource(key, null, out object? got), key);
                Assert.True(palette.TryGetResource(token, null, out object? want), token);
                Assert.IsType<Color>(got);
                Assert.Equal(want, got);
            }
        });

        // Foreground, background and the floor: 4.5 for words (WCAG 1.4.3), 3 for a control's edge or fill (1.4.11).
        public static readonly TheoryData<string, string, string, double> Pairs = PairsInBothVariants();

        private static TheoryData<string, string, string, double> PairsInBothVariants()
        {
            var data = new TheoryData<string, string, string, double>();
            string[] grounds = ["LunaSurface", "LunaSurfaceRaised", "LunaInputSurface"];
            (string, double)[] inks =
            [
                ("LunaText", 4.5), ("LunaTextStrong", 4.5), ("LunaMuted", 4.5), ("LunaAttention", 4.5), ("LunaSuccess", 4.5), ("LunaError", 4.5),
                ("LunaAccent", 3), ("LunaBorder", 3),
            ];
            foreach (string variant in new[] { "Dark", "Light" })
            {
                foreach ((string ink, double floor) in inks)
                    foreach (string ground in grounds) data.Add(variant, ink, ground, floor);
                data.Add(variant, "LunaOnAccent", "LunaAccent", 4.5);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(Pairs))]
        public Task Every_pair_of_tokens_clears_its_contrast_floor(string variant, string ink, string ground, double floor) => UiTest.Run(() =>
        {
            ThemeVariant v = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            double ratio = Contrast(ColourOf(ink + "Color", v), ColourOf(ground + "Color", v));
            Assert.True(ratio >= floor, $"{ink} on {ground} ({variant}) is {ratio:F2}:1, below {floor}:1.");
        });

        [Theory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public Task Words_over_art_clear_four_and_a_half_to_one_on_white_art_under_the_scrim(string variant) => UiTest.Run(() =>
        {
            ThemeVariant v = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            Color scrim = ColourOf("LunaOnArtScrimColor", v);
            double a = scrim.A / 255.0;
            byte under = (byte)Math.Round(255 * (1 - a) + scrim.R * a);
            double ratio = Contrast(ColourOf("LunaOnArtColor", v), Color.FromRgb(under, under, under));
            Assert.True(ratio >= 4.5, $"OnArt over white art is {ratio:F2}:1.");
        });

        [Fact]
        public Task The_big_screen_stays_dark_on_a_light_desktop_and_takes_light_when_asked() => UiTest.Run(() =>
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            try
            {
                var darkPanel = new MenuPanel { Width = 300, Height = 200 };
                var lightPanel = new MenuPanel { Width = 300, Height = 200 };
                MenuLook.SetIsLight(lightPanel, true);
                var probeDark = new Border { [!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("LunaSurface") };
                var probeLight = new Border { [!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("LunaSurface") };
                darkPanel.Child = probeDark;
                lightPanel.Child = probeLight;
                ToolWindow window = Show(new StackPanel { Children = { darkPanel, lightPanel } }, 400, 500);
                Assert.Equal(Color.Parse("#1B1B1E"), ((ISolidColorBrush)probeDark.Background!).Color);
                Assert.Equal(Color.Parse("#F3F3F3"), ((ISolidColorBrush)probeLight.Background!).Color);
                Assert.Equal(Color.Parse("#26262B"), new MenuRow().BarColor);
                window.Close();
            }
            finally { Application.Current.RequestedThemeVariant = ThemeVariant.Dark; }
        });

        [Fact]
        public Task A_highlighted_row_draws_the_raised_bar_with_an_accent_bar_at_its_left() => UiTest.Run(() =>
        {
            var row = new MenuRow { Label = "Resume", IsHighlighted = true, Width = 400, Height = 54, TextSize = 26 };
            ToolWindow window = Show(new Border { Child = row }, 400, 54);
            RenderedFrame f = Frame(window);
            Assert.Equal(Color.Parse("#4C9AE8"), At(f, 1, 20));
            Assert.Equal(Color.Parse("#26262B"), At(f, 8, 20));
            Assert.NotEqual(Color.Parse("#4C9AE8"), At(f, 5, 20));
            window.Close();
        });

        [Fact]
        public Task The_metrics_are_the_tokens_of_their_form() => UiTest.Run(() =>
        {
            var big = new LunaMetrics { Form = LunaForm.BigScreen };
            var desk = new LunaMetrics();
            Assert.Equal(new double[] { 44, 32, 26, 22, 20 }, new[] { "Display", "Title", "Row", "Body", "Caption" }.Select(k => (double)big["Luna" + k + "FontSize"]!));
            Assert.Equal(new double[] { 24, 16, 14, 13, 11 }, new[] { "Display", "Title", "Strong", "Body", "Caption" }.Select(k => (double)desk["Luna" + k + "FontSize"]!));
            Assert.Equal(54d, big["LunaRowHeight"]);
            Assert.Equal(44d, big["LunaControlHeight"]);
            Assert.Equal(new Thickness(64, 40), big["LunaSafeArea"]);
            Assert.Equal(new Thickness(3), big["LunaFocusRingThickness"]);
            Assert.Equal(4d, big["LunaFocusRingOffset"]);
            Assert.Equal(new Thickness(64, 40), LunaTokens.SafeAreaOf(new Size(1280, 800)));
            Assert.Equal(new Thickness(96, 60), LunaTokens.SafeAreaOf(new Size(1920, 1200)));
            Assert.True(Application.Current!.TryGetResource("LunaBodyFontSize", null, out object? body));
            Assert.Equal(13d, body);
        });

        [Fact]
        public Task Barlow_and_Barlow_Condensed_are_bundled_with_their_licence_and_every_window_writes_in_Barlow() => UiTest.Run(() =>
        {
            Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(LunaFonts.Body), out GlyphTypeface? body));
            Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(LunaFonts.Condensed, weight: FontWeight.Medium), out GlyphTypeface? condensed));
            Assert.Equal("Barlow", body!.FamilyName);
            Assert.StartsWith("Barlow Condensed", condensed!.FamilyName);
            Assert.Equal(FontWeight.Medium, condensed.Weight);
            foreach (string file in new[] { LunaFonts.BodyFile, LunaFonts.BodyMediumFile, LunaFonts.CondensedFile, LunaFonts.CondensedMediumFile })
                Assert.NotNull(FontFiles.Load(file));
            Assert.Contains("SIL Open Font License", File.ReadAllText(LunaFonts.LicenceFile));
            var words = new TextBlock { Text = "Resume" };
            ToolWindow window = Show(words, 200, 60);
            Assert.Equal("Barlow", words.FontFamily.Name.Split('#').Last());
            Assert.Equal(13d, words.FontSize);
            window.Close();
        });

        [Fact]
        public void The_motion_tokens_have_their_durations_and_reduced_motion_stops_movement() 
        {
            int[] ms = [0, 120, 200, 160, 320, 400, 150, 1500];
            Assert.Equal(ms, Enum.GetValues<LunaMotionToken>().Select(t => (int)LunaMotion.Duration(t).TotalMilliseconds));
            Assert.IsType<Avalonia.Animation.Easings.QuadraticEaseIn>(LunaMotion.Easing(LunaMotionToken.Exit));
            Assert.IsType<Avalonia.Animation.Easings.CubicEaseOut>(LunaMotion.Easing(LunaMotionToken.Pan));
            Assert.True(LunaMotion.Breathes);
            LunaMotion.Reduced = true;
            try
            {
                Assert.All(Enum.GetValues<LunaMotionToken>(), t => Assert.Equal(TimeSpan.Zero, LunaMotion.Duration(t)));
                Assert.Equal(TimeSpan.FromMilliseconds(120), LunaMotion.FadeDuration(LunaMotionToken.Enter));
                Assert.Equal(TimeSpan.Zero, LunaMotion.FadeDuration(LunaMotionToken.Instant));
                Assert.False(LunaMotion.Breathes);
            }
            finally { LunaMotion.Reduced = false; }
        }

        [Fact]
        public void Nine_cues_are_synthesised_at_sixty_percent_and_only_saved_and_loaded_play_over_a_game()
        {
            Assert.Equal(9, LunaSounds.Cues.Count);
            Assert.Equal(0.6, LunaSounds.DefaultVolume);
            foreach (LunaSoundCue cue in LunaSounds.Cues)
            {
                byte[] samples = LunaSounds.Samples(cue);
                Assert.Equal(0, samples.Length % 8);
                Assert.Contains(samples, b => b != 0);
                Assert.Same(samples, LunaSounds.Samples(cue));
            }
            Assert.Equal([LunaSoundCue.Saved, LunaSoundCue.Loaded], LunaSounds.Cues.Where(LunaSounds.PlaysOverGameplay));
            Assert.Equal(2, LunaSounds.BellsOf(LunaSoundCue.Refused).Count);
        }

        private static Color ColourOf(string key, ThemeVariant variant)
        {
            Assert.True(Application.Current!.TryGetResource(key, variant, out object? found), key);
            return (Color)found!;
        }

        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        private static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

        private static double Channel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
    }
}
