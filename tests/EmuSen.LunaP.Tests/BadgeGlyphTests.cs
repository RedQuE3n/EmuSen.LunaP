using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // BadgeGlyph, ControllerGlyph, BadgeStrip's drawn entries and overlays, and ScrollLetterOverlay - see docs/LunaP.md §180.
    public class BadgeGlyphTests
    {
        private const int Side = 64;

        private static byte[] Luminance(Control glyph, int w = Side, int h = Side)
        {
            var canvas = new Canvas { Width = w, Height = h, Background = Brushes.Black, Children = { glyph } };
            ToolWindow window = Show(canvas, w, h);
            RenderedFrame f = Frame(window);
            window.Close();
            var lum = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) lum[y * w + x] = At(f, x, y).G;
            return lum;
        }

        private static byte[] Badge(BadgeKind kind) => Luminance(new BadgeGlyph { Kind = kind, GlyphSize = Side, Color = Colors.White });

        private static byte[] Controller(ControllerShape shape) => Luminance(new ControllerGlyph { Shape = shape, GlyphSize = Side, Color = Colors.White });

        private static int Ink(byte[] g) => g.Count(v => v > 128);

        private static int Differing(byte[] a, byte[] b) => a.Zip(b).Count(p => Math.Abs(p.First - p.Second) > 64);

        [Fact]
        public Task Every_badge_and_every_controller_draws_inside_its_square_and_none_fills_it() => UiTest.Run(() =>
        {
            foreach (BadgeKind kind in Enum.GetValues<BadgeKind>())
            {
                int ink = Ink(Badge(kind));
                Assert.True(ink > Side * Side / 40 && ink < Side * Side / 2, $"{kind} drew {ink} pixels");
            }
            foreach (ControllerShape shape in Enum.GetValues<ControllerShape>())
            {
                int ink = Ink(Controller(shape));
                Assert.True(ink > Side * Side / 40 && ink < Side * Side / 2, $"{shape} drew {ink} pixels");
            }
        });

        // Every drawing is told from every other of its set: a badge's symbol differs from each other badge's, a controller's outline from each other's.
        [Fact]
        public Task Each_badge_and_each_controller_differs_from_every_other() => UiTest.Run(() =>
        {
            var badges = Enum.GetValues<BadgeKind>().Select(k => (Name: k.ToString(), Pixels: Badge(k))).ToList();
            var controllers = Enum.GetValues<ControllerShape>().Select(s => (Name: s.ToString(), Pixels: Controller(s))).ToList();
            foreach (var set in new[] { badges, controllers })
                for (int i = 0; i < set.Count; i++)
                    for (int j = i + 1; j < set.Count; j++)
                        Assert.True(Differing(set[i].Pixels, set[j].Pixels) > 40, $"{set[i].Name} and {set[j].Name} look alike");
        });

        // The plate: every badge but the link carries the same rounded square, so a strip of them reads as one set; the controller badge is the plate alone.
        [Fact]
        public Task Every_badge_but_the_link_stands_on_the_plate_and_the_controller_badge_is_the_plate_alone() => UiTest.Run(() =>
        {
            byte[] plate = Badge(BadgeKind.Controller);
            bool Inked(byte[] g, int x, int y) => g[y * Side + x] > 128;
            foreach (BadgeKind kind in Enum.GetValues<BadgeKind>())
            {
                byte[] g = Badge(kind);
                bool edge = Inked(g, Side / 2, (int)(Side * 0.07) + 1) || Inked(g, Side / 2, (int)(Side * 0.07));
                Assert.Equal(kind != BadgeKind.FolderLink, edge);
            }
            for (int y = Side / 4; y < Side * 3 / 4; y++)
                for (int x = Side / 4; x < Side * 3 / 4; x++) Assert.False(Inked(plate, x, y), $"the controller plate is inked at {x},{y}");
        });

        [Fact]
        public void Badges_and_controllers_are_named_for_a_screen_reader()
        {
            Assert.Equal("Favorite", BadgeGlyph.Describe(BadgeKind.Favorite));
            Assert.Equal("Kids' game", BadgeGlyph.Describe(BadgeKind.KidGame));
            Assert.Equal("Alternative emulator", BadgeGlyph.Describe(BadgeKind.AltEmulator));
            Assert.Equal("Folder link", BadgeGlyph.Describe(BadgeKind.FolderLink));
            Assert.Equal("NES controller", ControllerGlyph.Describe(ControllerShape.Nes));
            Assert.Equal("Nintendo 64 controller", ControllerGlyph.Describe(ControllerShape.Nintendo64));
            Assert.Equal("Unknown controller", ControllerGlyph.Describe(ControllerShape.Unknown));
        }

        private static (RenderedFrame Frame, BadgeStrip Strip) Strip(IReadOnlyList<BadgeEntry> entries, Action<BadgeStrip>? set = null)
        {
            var strip = new BadgeStrip { Entries = entries, Lines = 1, ItemsPerLine = 4, Width = 400, Height = 100, Tint = Colors.White };
            set?.Invoke(strip);
            var canvas = new Canvas { Width = 400, Height = 100, Background = Brushes.Black, Children = { strip } };
            ToolWindow window = Show(canvas, 400, 100);
            RenderedFrame f = Frame(window);
            window.Close();
            return (f, strip);
        }

        private static int InkIn(RenderedFrame f, Rect r)
        {
            int n = 0;
            for (int y = (int)r.Top; y < (int)r.Bottom; y++)
                for (int x = (int)r.Left; x < (int)r.Right; x++)
                    if (At(f, x, y).G > 128) n++;
            return n;
        }

        // Entries win over Icons; a file wins over the drawing; a cell holds its badge's drawing in the square fitted to it.
        [Fact]
        public Task A_strip_draws_each_entry_s_file_else_its_drawing_and_entries_win_over_icons() => UiTest.Run(() =>
        {
            string red = Flat("badge-red", 10, 10, Colors.Red);
            (RenderedFrame f, BadgeStrip strip) = Strip(new[] { new BadgeEntry(BadgeKind.Favorite), new BadgeEntry(BadgeKind.Completed) { IconPath = red } },
                s => s.Icons = new[] { red, red, red });
            Assert.Equal(2, strip.Cells(new Size(400, 100)).Count);
            Assert.Equal(Colors.Red, At(f, 150, 50));
            Assert.Equal(Colors.White, At(f, 50, 50));
            Assert.Equal(Colors.Black, At(f, 250, 50));

            (RenderedFrame missing, _) = Strip(new[] { new BadgeEntry(BadgeKind.Favorite) { IconPath = "/nonexistent/emusen/badge.png" } });
            Assert.Equal(Colors.White, At(missing, 50, 50));
        });

        // A controller stands on its badge at ControllerPosition, ControllerSize of the badge's width; a folder link likewise; a file wins over each drawing.
        [Fact]
        public Task A_controller_and_a_folder_link_are_drawn_over_their_badges_where_and_as_large_as_the_strip_says() => UiTest.Run(() =>
        {
            var plain = Strip(new[] { new BadgeEntry(BadgeKind.Controller), new BadgeEntry(BadgeKind.Folder) }).Frame;
            var over = Strip(new[] { new BadgeEntry(BadgeKind.Controller) { Controller = ControllerShape.Nes }, new BadgeEntry(BadgeKind.Folder) { Linked = true } }).Frame;
            var centre = new Rect(25, 25, 50, 50);
            Assert.True(InkIn(over, centre) > InkIn(plain, centre) + 100, $"{InkIn(over, centre)} against {InkIn(plain, centre)}");
            Assert.True(InkIn(over, centre.Translate(new Vector(100, 0))) > InkIn(plain, centre.Translate(new Vector(100, 0))) + 60);
            // The drawn link is cut out of the drawn folder beneath it: some of the folder's ink is gone where the link crosses.
            int cut = 0;
            for (int y = 0; y < 100; y++)
                for (int x = 100; x < 200; x++)
                    if (At(plain, x, y).G > 128 && At(over, x, y).G < 64) cut++;
            Assert.True(cut > 10, $"{cut} pixels of the folder were cut");

            var moved = Strip(new[] { new BadgeEntry(BadgeKind.Controller) { Controller = ControllerShape.Nes } }, s => { s.ControllerPosition = new Point(0.5, 0.2); s.ControllerSize = 0.3; }).Frame;
            Assert.True(InkIn(moved, new Rect(30, 55, 40, 30)) < InkIn(over, new Rect(30, 55, 40, 30)));
            Assert.True(InkIn(moved, new Rect(35, 8, 30, 24)) > 20);

            string blue = Flat("controller-blue", 10, 10, Colors.Blue);
            var file = Strip(new[] { new BadgeEntry(BadgeKind.Controller) { Controller = ControllerShape.Nes, ControllerIconPath = blue } }).Frame;
            Assert.Equal(Colors.Blue, At(file, 50, 50));
            string green = Flat("link-green", 10, 10, Colors.Lime);
            var link = Strip(new[] { new BadgeEntry(BadgeKind.Folder) { Linked = true, LinkIconPath = green } }).Frame;
            Assert.Equal(Colors.Lime, At(link, 50, 50));

            // No controller named, no overlay; a link asked of a badge that is no folder is not drawn.
            var none = Strip(new[] { new BadgeEntry(BadgeKind.Favorite) { Linked = true, Controller = ControllerShape.Snes } }).Frame;
            var star = Strip(new[] { new BadgeEntry(BadgeKind.Favorite) }).Frame;
            Assert.Equal(0, Enumerable.Range(0, 100).Sum(x => Enumerable.Range(0, 100).Count(y => At(none, x, y) != At(star, x, y))));
        });

        [Fact]
        public Task The_scroll_overlay_shades_its_box_and_shows_letters_or_a_star_and_nothing_when_empty() => UiTest.Run(() =>
        {
            RenderedFrame Render(ScrollLetterOverlay o)
            {
                o.Width = 300;
                o.Height = 200;
                var canvas = new Canvas { Width = 300, Height = 200, Background = Brushes.White, Children = { o } };
                ToolWindow window = Show(canvas, 300, 200);
                RenderedFrame f = Frame(window);
                window.Close();
                return f;
            }

            var empty = new ScrollLetterOverlay();
            Assert.False(empty.Showing);
            RenderedFrame blank = Render(empty);
            Assert.Equal(Colors.White, At(blank, 5, 5));
            Assert.Equal(Colors.White, At(blank, 150, 100));

            var letters = new ScrollLetterOverlay { Letters = "Co", Foreground = Colors.Red, LetterSize = 80 };
            Assert.True(letters.Showing);
            RenderedFrame withLetters = Render(letters);
            Color corner = At(withLetters, 5, 5);
            Assert.True(corner.R < 200 && corner.R > 100, $"the corner is {corner}");
            int red = 0;
            for (int y = 60; y < 140; y++)
                for (int x = 90; x < 210; x++)
                    if (At(withLetters, x, y) is { R: > 200, G: < 60 }) red++;
            Assert.True(red > 200, $"{red} red pixels");

            RenderedFrame withStar = Render(new ScrollLetterOverlay { Letters = "Co", Star = true, Foreground = Colors.Red, LetterSize = 80 });
            Assert.Equal(At(withLetters, 5, 5), At(withStar, 5, 5));
            Assert.Equal(Colors.Red, At(withStar, 150, 100));
            int differ = 0;
            for (int y = 60; y < 140; y++)
                for (int x = 90; x < 210; x++)
                    if (At(withLetters, x, y) != At(withStar, x, y)) differ++;
            Assert.True(differ > 300, $"{differ} pixels differ");
        });
    }
}
