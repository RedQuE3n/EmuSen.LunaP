using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // A world's body drawn from plain data: each shape's size, a planet's colours from its palette or its hue, and each feature drawn only when asked - see docs/LunaP.md §222.2.
    public class WorldGlyphTests
    {
        // The glyph drawn at a scale of 2 (200 pixels to its 100-unit box), on a transparent canvas with room round it for the ring and moonlet.
        private static RenderedFrame Drawn(WorldBody body, double hue = 38, bool focused = true)
        {
            var glyph = new WorldGlyph { Body = body, Hue = hue, Focused = focused };
            Size size = WorldGlyph.SizeOf(body.Shape, focused);
            glyph.Measure(size);
            glyph.Arrange(new Rect(size));
            double scale = 200 / size.Width;
            return DrawnSupport.Draw(320, 400, dc =>
            {
                using (dc.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(60, 60))) glyph.Render(dc);
            });
        }

        // A point of the 100-unit box as a pixel of Drawn's canvas.
        private static Color At(RenderedFrame f, double x, double y) => DrawnSupport.At(f, 60 + 2 * x, 60 + 2 * y);

        [Fact]
        public void Each_shape_has_its_size_at_rest_and_focused()
        {
            Assert.Equal(new Size(58, 58), WorldGlyph.SizeOf(WorldShape.Planet, false));
            Assert.Equal(new Size(84, 84), WorldGlyph.SizeOf(WorldShape.Planet, true));
            Assert.Equal(new Size(42, 68), WorldGlyph.SizeOf(WorldShape.Shards, false));
            Assert.Equal(new Size(60, 96), WorldGlyph.SizeOf(WorldShape.Gem, true));
            var glyph = new WorldGlyph { Body = new WorldBody(WorldShape.Gem) };
            Assert.Equal((42.0, 68.0), (glyph.Width, glyph.Height));
            glyph.Focused = true;
            Assert.Equal((60.0, 96.0), (glyph.Width, glyph.Height));
        }

        [Fact]
        public Task A_plain_planet_takes_its_colours_from_its_hue_and_a_palette_its_own() => UiTest.Run(() =>
        {
            (Color light, _, Color rim) = WorldGlyph.Colours(WorldBody.Plain, 212);
            Assert.Equal(HslColor.ToRgb(212, 0.85, 0.8, 1), light);
            Assert.Equal(HslColor.ToRgb(212, 0.6, 0.22, 1), rim);
            var palette = new WorldBody(WorldShape.Planet) { Palette = [Color.Parse("#DCF1FF"), Color.Parse("#4E9CEB"), Color.Parse("#1F3A7A")] };
            (Color l, Color m, Color r) = WorldGlyph.Colours(WorldBody.Plain, 212);
            RenderedFrame plain = Drawn(WorldBody.Plain, 212), same = Drawn(new WorldBody(WorldShape.Planet) { Palette = [l, m, r] }), own = Drawn(palette);
            foreach ((double x, double y) in new[] { (50.0, 50.0), (44.0, 44.0), (70.0, 80.0), (50.0, 93.0) }) Assert.Equal(At(plain, x, y), At(same, x, y));
            // Near the gradient's centre (36 %, 32 % of the disc's box) the light colour, at the rim's foot the rim colour.
            Color near = At(own, 44, 44), foot = At(own, 50, 93);
            // 11.7 units from the centre of a 65.1-unit radius: 37 % of the way from the light colour to the middle, which is at 48 %.
            double f = System.Math.Sqrt(6.32 * 6.32 + 9.84 * 9.84) / (0.74 * 88) / 0.48;
            Color top = Color.Parse("#DCF1FF"), middle = Color.Parse("#4E9CEB");
            byte Mix(byte a, byte b) => (byte)System.Math.Round(a + f * (b - a));
            Assert.True(DrawnSupport.Near(Color.FromRgb(Mix(top.R, middle.R), Mix(top.G, middle.G), Mix(top.B, middle.B)), near, 6), $"own near the centre {near}");
            Assert.True(DrawnSupport.Near(Color.Parse("#1F3A7A"), foot, 30), $"own rim {foot}");
        });

        [Fact]
        public Task A_feature_at_nought_draws_nothing_of_itself() => UiTest.Run(() =>
        {
            var bare = new WorldBody(WorldShape.Planet) { Palette = [Color.Parse("#FFF8FB"), Color.Parse("#F3CFE3"), Color.Parse("#A9789F")] };
            RenderedFrame without = Drawn(bare), craters = Drawn(bare with { Craters = 1 }), moon = Drawn(bare with { Moons = 1 }), moons = Drawn(bare with { Moons = 2 }), ring = Drawn(bare with { Ring = WorldRing.Big }), subtle = Drawn(bare with { Ring = WorldRing.Subtle }), cap = Drawn(bare with { Cap = 1 });
            Assert.Equal(At(without, 63, 40), At(Drawn(bare with { Craters = 0 }), 63, 40));
            Assert.True(DrawnSupport.Near(Color.Parse("#A9789F"), At(craters, 63, 40), 8), $"a crater {At(craters, 63, 40)}");
            Assert.False(DrawnSupport.Near(At(without, 63, 40), At(craters, 63, 40), 8));
            Assert.Equal(0, At(without, 93, 14).A);
            Assert.True(At(moon, 93, 14).A > 200, "the moonlet");
            Assert.Equal(0, At(moon, 5, 84).A);
            Assert.True(At(moons, 5, 84).A > 200, "the second moonlet");
            Assert.True(DrawnSupport.Covered(subtle) > DrawnSupport.Covered(without), "a subtle ring");
            Assert.True(DrawnSupport.Covered(ring) > DrawnSupport.Covered(subtle), "a big ring reaches further than a subtle one");
            Assert.Equal(0, At(without, -12, 52 + 64 * 0.2756).A);
            Assert.True(DrawnSupport.Covered(ring) > DrawnSupport.Covered(without), "the ring reaches past the disc");
            Assert.True(DrawnSupport.Near(Color.Parse("#FFF4F6"), At(cap, 50, 12), 12), $"the cap {At(cap, 50, 12)}");
        });

        // A badge is the ink's rounded square with its pixels cut in the dark ink, crisp; a path is stroked in the ink (docs/LunaP.md §222.3).
        [Fact]
        public Task A_mark_draws_a_badge_s_pixels_or_a_stroked_path() => UiTest.Run(() =>
        {
            var badge = new MarkGlyph { Mark = new PlanetMark { Pixels = ["X.......X", ".X.....X."] }, Ink = Brushes.White, Dark = Brushes.Black, Width = 110, Height = 110 };
            badge.Measure(new Size(110, 110));
            badge.Arrange(new Rect(0, 0, 110, 110));
            RenderedFrame f = DrawnSupport.Draw(110, 110, badge.Render);
            Assert.Equal(Colors.Black, DrawnSupport.At(f, 15, 20));
            Assert.Equal(Colors.White, DrawnSupport.At(f, 25, 20));
            Assert.Equal(Colors.Black, DrawnSupport.At(f, 25, 30));
            Assert.Equal(Colors.White, DrawnSupport.At(f, 55, 60));
            var path = new MarkGlyph { Mark = new PlanetMark { Stroke = "M2 12h20" }, Ink = Brushes.White, Width = 24, Height = 24 };
            path.Measure(new Size(24, 24));
            path.Arrange(new Rect(0, 0, 24, 24));
            RenderedFrame p = DrawnSupport.Draw(24, 24, path.Render);
            Assert.True(DrawnSupport.At(p, 12, 12).A > 200);
            Assert.Equal(0, DrawnSupport.At(p, 12, 4).A);
        });

        [Fact]
        public Task Shards_and_a_gem_draw_their_facets_in_the_hue_translucent_at_rest() => UiTest.Run(() =>
        {
            RenderedFrame rest = Drawn(new WorldBody(WorldShape.Gem), 250, false), focused = Drawn(new WorldBody(WorldShape.Gem), 250, true);
            // The pavilion's middle facet, ink 3, under the crown.
            Color restInk = At(rest, 50, 110), onInk = At(focused, 50, 110);
            Assert.InRange(restInk.A, 140, 200);
            Assert.Equal(255, onInk.A);
            Assert.True(DrawnSupport.Near(HslColor.ToRgb(250, 0.95, 0.9, 1), onInk, 8), $"focused facet {onInk}");
            Assert.True(DrawnSupport.Covered(Drawn(new WorldBody(WorldShape.Shards), 0, true)) > 0);
        });
    }
}
