using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // LunaSplash, SplashTimeline and the chime - see docs/LunaP.md §219.
    public class SplashTests
    {
        private static readonly SplashBody[] Thirteen =
            Enumerable.Range(0, 13).Select(i => new SplashBody($"console {i}", new[] { 8, 280, 140, 95, 320, 205, 220, 0, 190, 30, 230, 250, 25 }[i])).ToArray();

        private static LunaSplash Splash(double seconds) => new() { Bodies = Thirteen, Title = "EMUSEN", Caption = "13 CONSOLES", Time = seconds };

        private static RenderedFrame Drawn(LunaSplash splash, int width, int height)
        {
            splash.Measure(new Size(width, height));
            splash.Arrange(new Rect(0, 0, width, height));
            return Draw(width, height, splash.Render);
        }

        [Fact]
        public void The_timeline_is_the_approved_drafts_stretched_by_its_pace()
        {
            SplashTimeline m = SplashTimeline.Moonrise;
            Assert.Equal((1.55, 8.0), (m.Pace, m.Length));
            Assert.Equal(new SplashSpan(0, 0.9), m.Stars);
            Assert.Equal(new SplashSpan(0.4, 1.6), m.Stroke);
            Assert.Equal(new SplashSpan(1.2, 2.0), m.Fill);
            Assert.Equal(new SplashSpan(3.45, 4.15), m.Words);
            Assert.Equal((new SplashSpan(2.35, 3.65), new SplashSpan(2.4, 3.7), 34.0), (m.Sweep, m.Line, m.FlareWidth));
            Assert.Equal(m.Length / m.Pace, m.DraftTime(99), 9);
            Assert.Equal(1.0 / m.Pace, m.DraftTime(1), 9);
            Assert.Equal(0.5, new SplashSpan(1, 3).At(2));
            Assert.Equal((0, 1), (new SplashSpan(1, 3).At(0), new SplashSpan(1, 3).At(4)));
        }

        [Fact]
        public void Time_is_clamped_and_Finish_draws_the_finished_frame()
        {
            var splash = Splash(-1);
            Assert.Equal(0, splash.Time);
            Assert.False(splash.AtEnd);
            splash.Time = 99;
            Assert.Equal(SplashTimeline.Moonrise.Length, splash.Time);
            splash.Time = 2;
            splash.Finish();
            Assert.True(splash.AtEnd);
        }

        // The draft page's generator gave these for the seed, read from Chrome on 2026-11-04.
        [Fact]
        public void The_generator_is_the_draft_pages_mulberry32()
        {
            double[] chrome = [0.68727025506086648, 0.11158405826427042, 0.30390220414847136, 0.91567308828234673, 0.51794449822045863];
            Assert.Equal(chrome, SplashRandom.First(SplashTimeline.Moonrise.Seed, 5));
        }

        [Fact]
        public Task A_frame_is_the_same_on_every_draw_and_the_start_is_only_sky() => UiTest.Run(() =>
        {
            RenderedFrame a = Drawn(Splash(2.2), 640, 360), b = Drawn(Splash(2.2), 640, 360);
            Assert.Equal(a.Rgba, b.Rgba);

            RenderedFrame start = Drawn(Splash(0), 640, 360);
            Assert.True(Near(Color.Parse("#05060f"), At(start, 320, 1), 3));
            Assert.True(Near(Color.Parse("#141b45"), At(start, 320, 359), 4));
            Assert.True(Near(At(start, 10, 180), At(start, 630, 180), 1), "nothing but the sky's gradient at the start");
        });

        // The wordmark in Barlow Condensed Light, the draft's 300, bundled beside Regular and Medium (decided 2026-11-04).
        [Fact]
        public Task The_wordmark_is_set_in_barlow_condensed_light() => UiTest.Run(() =>
        {
            Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(LunaFonts.Condensed, FontStyle.Normal, FontWeight.Light), out GlyphTypeface? light));
            Assert.Equal(FontWeight.Light, light!.Weight);
            Assert.True(File.Exists(LunaFonts.CondensedLightFile));
            var splash = Splash(0);
            splash.Finish();
            splash.Measure(new Size(1280, 720));
            splash.Arrange(new Rect(0, 0, 1280, 720));
            double Width(FontWeight weight)
            {
                using var layout = new Avalonia.Media.TextFormatting.TextLayout("EMUSEN", new Typeface(LunaFonts.Condensed, FontStyle.Normal, weight), 78, null, letterSpacing: 10);
                return layout.WidthIncludingTrailingWhitespace;
            }
            Assert.NotEqual(Width(FontWeight.Light), Width(FontWeight.Normal), 1);
            Assert.Equal(Width(FontWeight.Light), splash.Parts().Single(p => p.Name == "title").Bounds.Width, 1);
        });

        // The splash leaves over the motion tokens' fade, 150 ms, which the host runs and cuts under reduced motion.
        [Fact]
        public void The_splash_fades_out_over_150_ms() => Assert.Equal(0.15, SplashTimeline.Moonrise.FadeOut);

        // The moon's face lit at the end, the row of planets drawn in their hues, the title in its colour.
        [Fact]
        public Task The_finished_frame_has_its_crescent_its_row_and_its_title() => UiTest.Run(() =>
        {
            var splash = Splash(0);
            splash.Finish();
            RenderedFrame f = Drawn(splash, 1280, 720);
            Color lit = At(f, 640 - 80, 260);
            Assert.True(lit.R > 200 && lit.G > 195, $"the crescent's lit edge, got {lit}");
            Assert.True(At(f, 640 + 40, 260).R < 40, "the shadow covers the moon's right");
            Rect first = splash.Parts().Single(p => p.Name == "console 0").Bounds;
            Color planet = At(f, first.Center.X, first.Center.Y);
            Assert.True(planet.R > planet.B, $"NES's red hue, got {planet}");
            // The spring ends at 1 - cos(2.4π)e^-5, 0.998 of the way, as the draft's does.
            Assert.All(splash.Parts().Where(p => p.Name.StartsWith("console")), p => Assert.Equal(720 * 0.66, p.Bounds.Center.Y, tolerance: 1.0));
            Assert.Equal(["moon", "halo", .. Thirteen.Select(b => b.Name), "title", "caption"], splash.Parts().Select(p => p.Name));
        });

        // Every part of the finished frame inside the control, the last size set after the first layout, as a window resized to 4K is.
        [Theory]
        [InlineData(1280, 800)]
        [InlineData(1920, 1080)]
        [InlineData(1280, 720)]
        [InlineData(3840, 2160)]
        public Task The_finished_frame_fits_at_every_size(int width, int height) => UiTest.Run(() =>
        {
            var splash = Splash(0);
            splash.Finish();
            ToolWindow window = Show(splash, 1280, 800);
            window.Width = width;
            window.Height = height;
            UiTest.Settle(splash);
            UiTest.Redraw(window);
            Assert.Equal(new Size(width, height), splash.Bounds.Size);
            var box = new Rect(splash.Bounds.Size);
            Assert.All(splash.Parts().Where(p => p.Name != "halo"), p => Assert.True(box.Contains(p.Bounds), $"{p.Name} at {p.Bounds} is outside {box}"));
            Assert.Equal(width * 0.78 > 980 * height / 720.0 ? 980 * height / 720.0 : width * 0.78,
                splash.Parts().Last(p => p.Name.StartsWith("console")).Bounds.Center.X - splash.Parts().First(p => p.Name.StartsWith("console")).Bounds.Center.X, tolerance: width * 0.005);
            window.Close();
        });

        // The sweep reaches the row's ends 60 units in from its own, at a steady speed, 2.35 to 3.65 draft seconds.
        [Fact]
        public Task Each_planets_bell_rings_as_the_sweep_reaches_it() => UiTest.Run(() =>
        {
            var splash = Splash(0);
            splash.Measure(new Size(1280, 720));
            splash.Arrange(new Rect(0, 0, 1280, 720));
            IReadOnlyList<double> strikes = splash.StrikeTimes();
            Assert.Equal(13, strikes.Count);
            Assert.Equal((2.35 + 60.0 / 1100 * 1.3) * SplashTimeline.Moonrise.Pace, strikes[0], 9);
            Assert.Equal((2.35 + 1040.0 / 1100 * 1.3) * SplashTimeline.Moonrise.Pace, strikes[12], 9);
            Assert.True(strikes.Zip(strikes.Skip(1)).All(p => p.Second > p.First));

            SplashChime chime = SplashTimeline.Moonrise.Chime;
            double[] pentatonic = [392, 440, 493.88, 587.33, 659.25, 783.99, 880, 987.77, 1174.66, 1318.51, 1567.98, 1760, 1975.53];
            for (int i = 0; i < 13; i++) Assert.Equal(pentatonic[i], chime.NoteOf(i), 0.02);
            Assert.Equal(2349.32, chime.NoteOf(13), 0.05);

            byte[] bytes = LunaSounds.Render(chime, strikes);
            float Sample(int frame) => BitConverter.ToSingle(bytes, frame * 8);
            int frames = bytes.Length / 8;
            // The power at one pitch over a stretch, by Goertzel's recurrence.
            double Tone(double from, double to, double hz)
            {
                double c = 2 * Math.Cos(2 * Math.PI * hz / LunaSounds.Rate), s1 = 0, s2 = 0;
                for (int i = (int)Math.Max(0, from * LunaSounds.Rate); i < (int)(to * LunaSounds.Rate); i++) (s2, s1) = (s1, Sample(i) + c * s1 - s2);
                return s1 * s1 + s2 * s2 - c * s1 * s2;
            }
            for (int n = 0; n < 13; n++)
            {
                double at = strikes[n] - strikes[0];
                Assert.True(Tone(at, at + 0.05, chime.NoteOf(n)) > 10 * Tone(at - 0.05, at, chime.NoteOf(n)), $"bell {n} struck at {strikes[n]} s");
            }
            Assert.Equal(bytes, LunaSounds.Render(chime, strikes));
            double loudest = Enumerable.Range(0, frames).Max(i => Math.Abs(Sample(i)));
            // At about the cues' level, as decided 2026-11-05: between the quietest cue's peak and the loudest's.
            double[] cuePeaks = LunaSounds.Cues.Select(c => { byte[] cue = LunaSounds.Samples(c); return (double)Enumerable.Range(0, cue.Length / 8).Max(i => Math.Abs(BitConverter.ToSingle(cue, i * 8))); }).ToArray();
            Assert.InRange(loudest, cuePeaks.Min() * 0.8, cuePeaks.Max() * 1.25);
            Assert.True(Enumerable.Range(frames - 4800, 4800).Max(i => Math.Abs(Sample(i))) < loudest * 0.01, "the last 100 ms is 40 dB under the loudest");
            Assert.Equal(BitConverter.ToSingle(bytes, 8 * 30000), BitConverter.ToSingle(bytes, 8 * 30000 + 4));
            Assert.Empty(LunaSounds.Render(chime, []));
        });

        // Mid-sweep the planet under the light flares, its halo brighter than when the sweep has passed.
        [Fact]
        public Task A_planet_flares_as_the_sweep_passes() => UiTest.Run(() =>
        {
            var splash = Splash(0);
            splash.Measure(new Size(1280, 720));
            splash.Arrange(new Rect(0, 0, 1280, 720));
            double strike = splash.StrikeTimes()[6];
            splash.Time = strike;
            Rect planet = splash.Parts().Single(p => p.Name == "console 6").Bounds;
            Point halo = new(planet.Center.X, planet.Center.Y - planet.Height * 1.4);
            Color lit = At(Drawn(splash, 1280, 720), halo.X, halo.Y);
            splash.Time = strike + 0.6;
            Color after = At(Drawn(splash, 1280, 720), halo.X, halo.Y);
            Assert.True(lit.R + lit.G + lit.B > after.R + after.G + after.B + 60, $"flare {lit} against {after}");
        });

        // The control's frames for the comparison with the draft, written only when the variable names a folder.
        [Fact]
        public Task The_comparison_frames_are_written_on_request() => UiTest.Run(() =>
        {
            if (Environment.GetEnvironmentVariable("EMUSEN_SPLASH_FRAMES") is not { Length: > 0 } folder) return;
            Directory.CreateDirectory(folder);
            foreach ((string name, double t) in new[] { ("1.0", 1.0), ("2.0", 2.0), ("3.0", 3.0), ("6.0", 6.0), ("final", SplashTimeline.Moonrise.Length) })
            {
                var splash = Splash(t);
                splash.Measure(new Size(1280, 720));
                splash.Arrange(new Rect(0, 0, 1280, 720));
                using var target = new RenderTargetBitmap(new PixelSize(1280, 720));
                using (DrawingContext dc = target.CreateDrawingContext()) splash.Render(dc);
                target.Save(Path.Combine(folder, $"built-{name}.png"));
            }
        });
    }
}
