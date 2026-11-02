using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Testing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // A glyph Barlow lacks shapes in the kit's bundled face, never the machine's, so a line's height does not depend on what the process loaded first - see docs/LunaP.md §208.13.
    public class FontFallbackTests
    {
        private static string FaceOf(string text, FontFamily family)
        {
            var layout = new TextLayout(text, new Typeface(family), 20, Brushes.White);
            var run = Assert.IsType<ShapedTextRun>(Assert.Single(Assert.Single(layout.TextLines).TextRuns));
            return run.ShapedBuffer.GlyphTypeface.FamilyName;
        }

        [Theory]
        [InlineData("\u25B8", "Adwaita Mono")]
        [InlineData("\u25C2", "Adwaita Mono")]
        [InlineData("\u2713", "Adwaita Mono")]
        [InlineData("\u26A0", "Adwaita Mono")]
        [InlineData("\u25CF", "Adwaita Mono")]
        [InlineData("\u2605", "Adwaita Mono")]
        [InlineData("\u2610", "Adwaita Mono")]
        [InlineData("\u23F8", "Adwaita Mono")]
        [InlineData("\u2191", "Adwaita Mono")]
        [InlineData("\u2192", "Adwaita Mono")]
        public Task A_symbol_glyph_shapes_in_the_bundled_face_under_both_typefaces(string glyph, string face) => UiTest.Run(() =>
        {
            Assert.Equal(face, FaceOf(glyph, LunaFonts.Condensed));
            Assert.Equal(face, FaceOf(glyph, LunaFonts.Body));
        });

        [Fact]
        public Task Barlow_keeps_the_glyphs_it_has() => UiTest.Run(() =>
        {
            Assert.Equal("Barlow Condensed", FaceOf("\u00B7 \u2026 \u203A \u2014", LunaFonts.Condensed));
            Assert.Equal("Barlow", FaceOf("\u00B7 \u2026 \u203A \u2014", LunaFonts.Body));
        });

        [Fact]
        public Task Every_fallback_names_a_bundled_face_the_font_manager_resolves() => UiTest.Run(() =>
        {
            foreach (FontFallback fallback in LunaFonts.Fallbacks)
                Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(fallback.FontFamily), out GlyphTypeface? typeface) && typeface is not null, fallback.FontFamily.Name);
        });
    }
}
