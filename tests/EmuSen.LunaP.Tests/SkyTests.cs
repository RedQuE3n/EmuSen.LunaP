using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using Xunit;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // LunaSky: the night gradient, Moonrise's stars held still, and the glow easing to the focus's hue - see docs/LunaP.md §221.2.
    [Collection(KitSwitches.Name)]
    public class SkyTests
    {
        private static TimeSpan _now;

        private static LunaSky Sky() => new() { Clock = () => _now, GlowRadii = new Size(756, 450), GlowStrength = 0.2 };

        private static RenderedFrame Drawn(LunaSky sky, int width, int height)
        {
            sky.Measure(new Size(width, height));
            sky.Arrange(new Rect(0, 0, width, height));
            return Draw(width, height, sky.Render);
        }

        [Fact]
        public Task A_frame_is_the_same_on_two_draws_and_the_seed_places_the_stars() => UiTest.Run(() =>
        {
            _now = TimeSpan.Zero;
            LunaSky a = Sky(), b = Sky(), c = Sky();
            c.Seed = 7;
            Assert.Equal(SplashTimeline.Moonrise.Seed, a.Seed);
            Assert.Equal(SplashTimeline.Moonrise.StarCount, a.StarCount);
            byte[] first = Drawn(a, 640, 400).Rgba;
            Assert.Equal(first, Drawn(b, 640, 400).Rgba);
            Assert.NotEqual(first, Drawn(c, 640, 400).Rgba);
            // The night gradient: dark at the top, bluer at the foot.
            RenderedFrame f = Drawn(a, 640, 400);
            Color top = At(f, 3, 1), foot = At(f, 3, 398);
            Assert.True(top.B < 20 && foot.B > 50 && foot.B > foot.R, $"top {top}, foot {foot}");
        });

        [Fact]
        public Task The_glow_eases_the_shorter_way_round_and_rests_after_the_Pan_token() => UiTest.Run(() =>
        {
            LunaMotion.Reduced = false;
            _now = TimeSpan.FromSeconds(10);
            LunaSky sky = Sky();
            sky.GlowHue = 350;
            Assert.Equal(350, sky.ShownHue!.Value, 3);
            sky.GlowHue = 20;
            _now += TimeSpan.FromMilliseconds(100);
            double mid = sky.ShownHue!.Value;
            Assert.True(mid > 350 || mid < 20, $"through 0, not back through 180: {mid}");
            Assert.False(sky.Settled);
            _now += LunaMotion.Nominal(LunaMotionToken.Pan);
            Assert.Equal(20, sky.ShownHue!.Value, 3);
            Assert.True(sky.Settled);
            RenderedFrame f = Drawn(sky, 1280, 800);
            Color centre = At(f, 640, 615), plain = At(Drawn(Sky(), 1280, 800), 640, 615);
            Assert.True(centre.R - plain.R > 10 && centre.R - plain.R > centre.B - plain.B, $"an orange glow over the night: {plain} became {centre}");
        });

        [Fact]
        public Task Under_reduced_motion_the_glow_holds_still_and_the_new_hue_is_drawn_at_once() => UiTest.Run(() =>
        {
            LunaMotion.Reduced = true;
            try
            {
                _now = TimeSpan.FromSeconds(20);
                LunaSky sky = Sky();
                sky.GlowHue = 280;
                sky.GlowHue = 140;
                Assert.True(sky.Settled);
                Assert.Equal(140, sky.ShownHue!.Value, 3);
                sky.GlowCentre = new Point(0.2, 0.3);
                Assert.True(sky.Settled);
            }
            finally { LunaMotion.Reduced = false; }
        });

        [Fact]
        public Task No_glow_is_drawn_without_a_hue() => UiTest.Run(() =>
        {
            _now = TimeSpan.Zero;
            LunaSky sky = Sky();
            Assert.Null(sky.ShownHue);
            Assert.Equal(default, sky.GlowBounds());
        });
    }
}
