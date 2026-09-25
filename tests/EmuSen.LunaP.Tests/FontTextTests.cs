using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // FontText and FontFiles - see docs/LunaP.md §98.3: a typeface from a file, cached and never registered, and the layout rules.
    public class FontTextTests
    {
        private static Size Measured(FontText text, double w = double.PositiveInfinity, double h = double.PositiveInfinity)
        {
            text.Measure(new Size(w, h));
            return text.DesiredSize;
        }

        [Fact]
        public Task The_typeface_comes_from_the_file_named() => UiTest.Run(() =>
        {
            var thin = new FontText { Text = "Mississippi", FontSize = 40, FontPath = Font("Inter-Thin") };
            var bold = new FontText { Text = "Mississippi", FontSize = 40, FontPath = Font("Inter-Bold") };
            Assert.True(Measured(bold).Width > Measured(thin).Width + 5, $"{Measured(bold).Width} against {Measured(thin).Width}");
            Assert.Equal("Inter", FontFiles.Load(Font("Inter-Bold"))!.FamilyName);
        });

        [Fact]
        public Task A_font_file_is_read_once_and_never_reaches_the_font_manager() => UiTest.Run(() =>
        {
            string path = Font("Inter-Light");
            GlyphTypeface first = FontFiles.Load(path)!;
            int count = FontFiles.Count;
            Assert.Same(first, FontFiles.Load(path));
            Assert.Same(first, FontFiles.Load(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, ".", "Inter-Light.ttf")));
            Assert.Equal(count, FontFiles.Count);
            Assert.DoesNotContain(FontManager.Current.SystemFonts, f => f.Name.Contains("FontFiles"));
            Assert.False(FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily("fonts:EmuSen.LunaP.FontFiles#Inter")), out _));
        });

        [Fact]
        public Task A_missing_or_unreadable_font_falls_back_to_the_default_typeface() => UiTest.Run(() =>
        {
            Assert.Null(FontFiles.Load("/nonexistent/lunap/font.ttf"));
            Assert.Null(FontFiles.Load(Flat("not-a-font", 2, 2, Colors.Red)));
            var text = new FontText { Text = "Fallback", FontSize = 20, FontPath = "/nonexistent/lunap/font.ttf" };
            Assert.True(Measured(text).Width > 20);
        });

        [Fact]
        public Task One_line_that_does_not_fit_ends_in_the_ellipsis() => UiTest.Run(() =>
        {
            var text = new FontText { Text = "A title far too long for its box", FontSize = 20, Wrap = false };
            Measured(text, 120);
            Assert.True(text.IsTruncated);
            string line = Assert.Single(text.LaidOutLines());
            Assert.EndsWith("…", line);
            Assert.True(text.DesiredSize.Width <= 120);

            text.Ellipsis = "...";
            Measured(text, 120);
            Assert.EndsWith("...", text.LaidOutLines()[0]);
        });

        [Fact]
        public Task Wrapping_breaks_at_spaces_and_a_height_limit_ellipsises_the_last_line_that_fits() => UiTest.Run(() =>
        {
            var text = new FontText { Text = "one two three four five six seven eight nine ten", FontSize = 20, LineSpacing = 1.5 };
            Measured(text, 150);
            Assert.True(text.LineCount > 2);
            Assert.All(text.LaidOutLines(), l => Assert.DoesNotContain("  ", l));
            Assert.Equal(text.LineCount * 30, text.DesiredSize.Height, 3);

            Measured(text, 150, 65);
            Assert.Equal(2, text.LineCount);
            Assert.True(text.IsTruncated);
            Assert.EndsWith("…", text.LaidOutLines()[1]);
        });

        [Theory]
        [InlineData(LetterCase.Upper, "THE LEGEND OF ZELDA")]
        [InlineData(LetterCase.Lower, "the legend of zelda")]
        [InlineData(LetterCase.Capitalize, "The Legend Of Zelda")]
        [InlineData(LetterCase.None, "the legend of Zelda")]
        public Task LetterCase_applies_before_layout(LetterCase letterCase, string expected) => UiTest.Run(() =>
        {
            var text = new FontText { Text = "the legend of Zelda", LetterCase = letterCase };
            Measured(text);
            Assert.Equal(expected, text.LaidOutLines().Single());
        });

        // White text on black: the ink's left edge moves with the alignment.
        [Fact]
        public Task Alignment_places_each_line_across_the_width_and_the_block_down_the_height() => UiTest.Run(() =>
        {
            var text = new FontText { Text = "Ink", FontSize = 30, Foreground = Brushes.White };
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetSize(text, new Size(1, 1));
            canvas.Children.Add(text);
            ToolWindow window = Show(canvas, 300, 200);

            (double left, double top) = Ink(Frame(window));
            Assert.InRange(left, 0, 10);
            Assert.InRange(top, 0, 20);

            text.TextAlignment = TextAlignment.Right;
            text.TextVerticalAlignment = VerticalAlignment.Bottom;
            (left, top) = Ink(Frame(window));
            Assert.InRange(left, 240, 300);
            Assert.InRange(top, 150, 200);

            text.TextAlignment = TextAlignment.Center;
            text.TextVerticalAlignment = VerticalAlignment.Center;
            (left, top) = Ink(Frame(window));
            Assert.InRange(left, 120, 150);
            Assert.InRange(top, 80, 100);
            window.Close();
        });

        [Fact]
        public Task The_background_fills_behind_the_padding_with_its_corner_radius() => UiTest.Run(() =>
        {
            var text = new FontText { Text = "12:00", FontSize = 20, Background = Brushes.Red, Padding = new Thickness(10), BackgroundCornerRadius = 12 };
            var canvas = new NormalizedCanvas();
            canvas.Children.Add(text);
            ToolWindow window = Show(canvas, 300, 200);
            Assert.Equal(text.LineHeight + 20, text.Bounds.Height, 3);
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Red, At(f, 5, text.Bounds.Height / 2));
            Assert.Equal(Colors.Black, At(f, 0, 0));
            window.Close();
        });

        private static (double Left, double Top) Ink(RenderedFrame f)
        {
            double left = double.MaxValue, top = double.MaxValue;
            for (int y = 0; y < f.Height; y++)
                for (int x = 0; x < f.Width; x++)
                    if (f.Rgba[(y * f.Width + x) * 4] > 128)
                    {
                        left = System.Math.Min(left, x);
                        top = System.Math.Min(top, y);
                    }

            return (left, top);
        }
    }
}
