using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Motion;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // PadGlyph, the hint bar's button sets, and the scroll queries a host sleeps on - see docs/LunaP.md §103.
    public class PadGlyphTests
    {
        private const int Side = 64;

        private static readonly PadGlyphButton[] Buttons = Enum.GetValues<PadGlyphButton>();

        // One glyph, white on black, in a window of its own size; the frame's pixels as luminance.
        private static byte[] Glyph(PadFamily family, PadGlyphButton button)
        {
            var glyph = new PadGlyph { Family = family, Button = button, GlyphSize = Side, Color = Colors.White };
            var canvas = new Canvas { Width = Side, Height = Side, Children = { glyph } };
            ToolWindow window = Show(canvas, Side, Side);
            RenderedFrame f = Frame(window);
            window.Close();
            var lum = new byte[Side * Side];
            for (int y = 0; y < Side; y++)
                for (int x = 0; x < Side; x++) lum[y * Side + x] = At(f, x, y).G;
            return lum;
        }

        private static string Ascii(byte[] g) => string.Join("\n", Enumerable.Range(0, Side / 2).Select(y => new string(Enumerable.Range(0, Side).Select(x => g[y * 2 * Side + x] > 128 ? '#' : '.').ToArray())));

        private static bool Inked(byte[] g, double x, double y) => g[(int)y * Side + (int)x] > 128;

        private static int Differing(byte[] a, byte[] b) => a.Zip(b).Count(p => Math.Abs(p.First - p.Second) > 64);

        [Fact]
        public Task Every_button_of_every_family_draws_inside_its_square() => UiTest.Run(() =>
        {
            foreach (PadFamily family in Enum.GetValues<PadFamily>())
                foreach (PadGlyphButton button in Buttons)
                {
                    byte[] g = Glyph(family, button);
                    int ink = g.Count(v => v > 128);
                    Assert.True(ink > Side * Side / 40, $"{family} {button} drew {ink} pixels");
                    Assert.True(ink < Side * Side * 3 / 4, $"{family} {button} filled {ink} pixels");
                }
        });

        [Fact]
        public Task The_four_families_draw_each_face_button_and_shoulder_differently() => UiTest.Run(() =>
        {
            foreach (PadGlyphButton button in new[] { PadGlyphButton.South, PadGlyphButton.East, PadGlyphButton.West, PadGlyphButton.North, PadGlyphButton.LeftShoulder, PadGlyphButton.LeftTrigger, PadGlyphButton.Start, PadGlyphButton.Select })
            {
                byte[][] drawn = Enum.GetValues<PadFamily>().Select(f => Glyph(f, button)).ToArray();
                for (int i = 0; i < drawn.Length; i++)
                    for (int j = i + 1; j < drawn.Length; j++)
                        Assert.True(Differing(drawn[i], drawn[j]) > 20, $"{button}: {(PadFamily)i} and {(PadFamily)j} look alike");
            }
        });

        [Fact]
        public Task Face_buttons_are_named_and_drawn_by_position() => UiTest.Run(() =>
        {
            Assert.Equal("A", PadGlyph.Describe(PadFamily.Xbox, PadGlyphButton.South));
            Assert.Equal("B", PadGlyph.Describe(PadFamily.Nintendo, PadGlyphButton.South));
            Assert.Equal("A", PadGlyph.Describe(PadFamily.Nintendo, PadGlyphButton.East));
            Assert.Equal("X", PadGlyph.Describe(PadFamily.Nintendo, PadGlyphButton.North));
            Assert.Equal("Cross", PadGlyph.Describe(PadFamily.PlayStation, PadGlyphButton.South));
            Assert.Equal("Triangle", PadGlyph.Describe(PadFamily.PlayStation, PadGlyphButton.North));
            Assert.Equal("ZL", PadGlyph.Describe(PadFamily.Nintendo, PadGlyphButton.LeftTrigger));
            Assert.Equal("South button", PadGlyph.Describe(PadFamily.Generic, PadGlyphButton.South));

            // The generic set fills the dot at the button's place and only rings the others.
            double c = Side / 2.0, d = Side * 0.29;
            byte[] south = Glyph(PadFamily.Generic, PadGlyphButton.South), north = Glyph(PadFamily.Generic, PadGlyphButton.North);
            Assert.True(Inked(south, c, c + d) && !Inked(south, c, c - d), "south's dot filled, north's hollow");
            Assert.True(Inked(north, c, c - d) && !Inked(north, c, c + d), "north's dot filled, south's hollow");

            // A cross crosses the centre; a circle and a square leave it empty.
            Assert.True(Inked(Glyph(PadFamily.PlayStation, PadGlyphButton.South), c, c), Ascii(Glyph(PadFamily.PlayStation, PadGlyphButton.South)));
            Assert.False(Inked(Glyph(PadFamily.PlayStation, PadGlyphButton.East), c, c));
            Assert.False(Inked(Glyph(PadFamily.PlayStation, PadGlyphButton.West), c, c));

            // Nintendo's letter is cut out of a disc, so the disc's edge inside the ring is ink; the Xbox ring leaves it empty.
            Assert.True(Inked(Glyph(PadFamily.Nintendo, PadGlyphButton.South), c - Side * 0.36, c), Ascii(Glyph(PadFamily.Nintendo, PadGlyphButton.South)));
            Assert.False(Inked(Glyph(PadFamily.Xbox, PadGlyphButton.South), c - Side * 0.34, c));

            // The letters sit at the middle of their ring or disc: the ink's centre of mass is the glyph's centre, to a pixel and a half.
            foreach (PadFamily family in new[] { PadFamily.Xbox, PadFamily.Nintendo })
                foreach (PadGlyphButton button in new[] { PadGlyphButton.South, PadGlyphButton.East, PadGlyphButton.West, PadGlyphButton.North })
                {
                    byte[] g = Glyph(family, button);
                    double sum = 0, sx = 0, sy = 0;
                    for (int y = 0; y < Side; y++)
                        for (int x = 0; x < Side; x++) { sum += g[y * Side + x]; sx += g[y * Side + x] * (x + 0.5); sy += g[y * Side + x] * (y + 0.5); }
                    Assert.True(Math.Abs(sx / sum - c) < 1.5 && Math.Abs(sy / sum - c) < 1.5, $"{family} {button}: ink centred at ({sx / sum:F2}, {sy / sum:F2})\n{Ascii(g)}");
                }

            // The Xbox A and the Nintendo A are one letter at different places.
            Assert.True(Differing(Glyph(PadFamily.Xbox, PadGlyphButton.South), Glyph(PadFamily.Xbox, PadGlyphButton.East)) > 20, Ascii(Glyph(PadFamily.Xbox, PadGlyphButton.South)));
        });

        [Fact]
        public Task A_hint_bar_draws_a_named_button_in_its_family_and_a_change_moves_nothing() => UiTest.Run(() =>
        {
            var bar = new HintBar
            {
                Entries = new[] { new HintEntry("Launch") { Button = PadGlyphButton.South }, new HintEntry("Back") { Button = PadGlyphButton.East } },
                FontSize = 30, IconColor = Colors.White, TextColor = Colors.Red, PadFamily = PadFamily.Xbox,
            };
            var canvas = new NormalizedCanvas();
            canvas.Children.Add(bar);
            ToolWindow window = Show(canvas, 500, 80);
            IReadOnlyList<(Rect Icon, Rect Label)> before = bar.Layout(bar.Bounds.Size);
            RenderedFrame xbox = Frame(window);
            Assert.Equal("A Launch, B Back", Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(bar).GetName());

            bar.PadFamily = PadFamily.PlayStation;
            window.UpdateLayout();
            Assert.Equal(before, bar.Layout(bar.Bounds.Size));
            RenderedFrame ps = Frame(window);
            Assert.Equal("Cross Launch, Circle Back", Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(bar).GetName());

            int iconsDiffer = 0, labelsDiffer = 0;
            var where = new List<string>();
            for (int y = 0; y < xbox.Height; y++)
                for (int x = 0; x < xbox.Width; x++)
                {
                    if (At(xbox, x, y) == At(ps, x, y)) continue;
                    if (before.Any(b => b.Icon.Contains(new Point(x + 0.5, y + 0.5)))) iconsDiffer++;
                    else { labelsDiffer++; where.Add($"({x},{y})"); }
                }
            Assert.True(iconsDiffer > 50, $"{iconsDiffer} icon pixels changed");
            Assert.True(labelsDiffer == 0, $"{string.Join(" ", where.Take(40))} icons {string.Join(" ", before.Select(b => b.Icon))}");

            // An image file wins over the named button.
            string icon = Flat("hint-over-button", 10, 10, Colors.Lime);
            bar.Entries = new[] { new HintEntry("Launch", icon) { Button = PadGlyphButton.South } };
            window.UpdateLayout();
            Rect box = bar.Layout(bar.Bounds.Size)[0].Icon;
            Assert.Equal(Colors.Lime, At(Frame(window), box.Center.X, box.Center.Y));
            window.Close();
        });

        [Fact]
        public void A_loop_and_a_column_say_when_they_next_change()
        {
            static TimeSpan S(double s) => TimeSpan.FromSeconds(s);
            var loop = new TextScroll(S(3), 100, 50);
            Assert.Equal(S(3), loop.NextLoopChange(S(0), 150));
            Assert.Equal(S(3), loop.NextLoopChange(S(2.5), 150));
            Assert.Equal(S(4), loop.NextLoopChange(S(4), 150));
            // One cycle is 3 s still and 2 s moving (200 px at 100 px/s); 5.5 s is still again, until 8 s.
            Assert.InRange(Math.Abs((loop.NextLoopChange(S(5.5), 150).Value - S(8)).TotalMilliseconds), 0, 1);
            Assert.Null(new TextScroll(S(3), 0, 50).NextLoopChange(S(1), 150));

            var column = new TextScroll(S(2), 10, EndPause: S(4), FadeIn: S(1));
            Assert.Equal(S(2), column.NextRunChange(S(0), 30));
            Assert.Equal(S(3), column.NextRunChange(S(3), 30));
            // Moving 2-5 s, then still until 9 s, when the fade begins.
            Assert.InRange(Math.Abs((column.NextRunChange(S(6), 30).Value - S(9)).TotalMilliseconds), 0, 1);
            Assert.InRange(Math.Abs((column.NextRunChange(S(9.5), 30).Value - S(9.5)).TotalMilliseconds), 0, 1);
            Assert.InRange(Math.Abs((column.NextRunChange(S(10.5), 30).Value - S(12)).TotalMilliseconds), 0, 1);
            Assert.Null(column.NextRunChange(S(1), 0));

            // The queries agree with the offsets they describe: nothing moves before the time given, and it has moved just after.
            for (double t = 0; t < 30; t += 0.37)
            {
                TimeSpan next = loop.NextLoopChange(S(t), 150)!.Value;
                if (next > S(t)) Assert.Equal(loop.LoopOffset(S(t), 150), loop.LoopOffset(next - TimeSpan.FromMilliseconds(1), 150), 6);
                TimeSpan nextRun = column.NextRunChange(S(t), 30)!.Value;
                if (nextRun > S(t)) Assert.Equal(column.RunAt(S(t), 30), column.RunAt(nextRun - TimeSpan.FromMilliseconds(1), 30));
            }
        }

        [Fact]
        public Task A_text_that_fits_never_changes_and_one_that_does_not_changes_after_its_delay() => UiTest.Run(() =>
        {
            var scroll = new TextScroll(TimeSpan.FromSeconds(3), 60, 30);
            var fits = new FontText { Text = "Short", FontSize = 16, ScrollDirection = TextScrollDirection.Horizontal, Scroll = scroll };
            var wide = new FontText { Text = "A line far too long for the narrow box it has been given here", FontSize = 16, ScrollDirection = TextScrollDirection.Horizontal, Scroll = scroll };
            var list = new TextRowList { Items = new[] { new TextRow("Short"), new TextRow("A row far too long for the list it sits in, which is narrow") }, FontSize = 16, Marquee = scroll };
            var canvas = new NormalizedCanvas();
            foreach ((Control c, double y) in new (Control, double)[] { (fits, 0), (wide, 0.2), (list, 0.4) })
            {
                NormalizedCanvas.SetPosition(c, new Point(0, y));
                NormalizedCanvas.SetSize(c, new Size(1, 0.15));
                canvas.Children.Add(c);
            }
            NormalizedCanvas.SetSize(list, new Size(1, 0.5));
            ToolWindow window = Show(canvas, 200, 300);
            Assert.Null(fits.NextScrollChange(TimeSpan.Zero));
            Assert.Equal(TimeSpan.FromSeconds(3), wide.NextScrollChange(TimeSpan.FromSeconds(1)));
            Assert.Null(list.NextMarqueeChange(TimeSpan.Zero));
            list.SelectedIndex = 1;
            Assert.Equal(TimeSpan.FromSeconds(3), list.NextMarqueeChange(TimeSpan.Zero));
            Assert.Equal(TimeSpan.FromSeconds(3.5), list.NextMarqueeChange(TimeSpan.FromSeconds(3.5)));
            window.Close();
        });
    }
}
