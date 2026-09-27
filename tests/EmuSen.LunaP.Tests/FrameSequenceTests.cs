using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // FrameSequenceImage: the GIF decoder and the frame-timing rules the consumer measured from its reference - see docs/LunaP.md §194.
    public class FrameSequenceTests
    {
        // Three interlaced 37 by 23 frames, pixel (x, y) of frame k the colour (3x + 5y + k) mod 4, delays 70, 200 and 300 ms, compressed by an ordinary encoder.
        private const string Pattern = "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";

        private static readonly Color[] Palette = [Color.FromRgb(230, 30, 30), Color.FromRgb(30, 200, 30), Color.FromRgb(30, 60, 230), Color.FromRgb(230, 210, 30)];

        private static string PatternFile()
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "pattern.gif");
            if (!File.Exists(path)) File.WriteAllBytes(path, Convert.FromBase64String(Pattern));
            return path;
        }

        [Fact]
        public Task Every_frame_decodes_to_its_pixels_and_the_first_delay_sets_the_pace() => UiTest.Run(() =>
        {
            var image = new FrameSequenceImage { Source = PatternFile(), Fit = ImageFit.Fill, Width = 37, Height = 23 };
            Assert.Equal(3, image.FrameCount);
            Assert.Equal(new Size(37, 23), image.IntrinsicSize);
            Assert.Equal(TimeSpan.FromMilliseconds(70), image.FrameDuration);
            ToolWindow window = Show(image, 37, 23);
            for (int k = 0; k < 3; k++)
            {
                image.Time = TimeSpan.FromMilliseconds(70 * (k + 1) + 10);
                Assert.Equal(k, image.FrameAt(image.Time));
                RenderedFrame f = Frame(window);
                for (int y = 0; y < 23; y += 3)
                    for (int x = 0; x < 37; x += 4)
                        Assert.Equal(Palette[(3 * x + 5 * y + k) % 4], At(f, x + 0.5, y + 0.5));
            }

            window.Close();
        });

        private static int[] Sequence(FrameSequenceImage image, int steps) => Enumerable.Range(0, steps).Select(s => image.FrameAt(TimeSpan.FromMilliseconds(70 * s + 1))).ToArray();

        // The first frame held for two frame times, then the direction's order; one pass of a bounce is a round trip; the last iteration holds.
        [Fact]
        public Task Directions_iterations_and_speed_follow_the_measured_order() => UiTest.Run(() =>
        {
            var three = new FrameSequenceImage { Source = PatternFile() };
            Assert.Equal([0, 0, 1, 2, 0, 1], Sequence(three, 6));
            three.Direction = FrameDirection.Reverse;
            Assert.Equal([0, 2, 1, 0, 2], Sequence(three, 5));
            three.Direction = FrameDirection.Alternate;
            Assert.Equal([0, 0, 1, 2, 1, 0, 1], Sequence(three, 7));
            three.Direction = FrameDirection.AlternateReverse;
            Assert.Equal([0, 2, 1, 0, 1, 2, 1], Sequence(three, 7));
            three.Direction = FrameDirection.Normal;
            three.IterationCount = 1;
            Assert.Equal([0, 0, 1, 2, 2, 2, 2], Sequence(three, 7));
            Assert.Null(three.NextFrameChange(TimeSpan.FromMilliseconds(70 * 4 + 1)));
            Assert.Equal(TimeSpan.FromMilliseconds(140), three.NextFrameChange(TimeSpan.FromMilliseconds(100)));
            three.Direction = FrameDirection.Alternate;
            three.IterationCount = 2;
            Assert.Equal([0, 0, 1, 2, 1, 0, 1, 2, 1, 0, 0, 0], Sequence(three, 12));
            three.IterationCount = 0;
            three.Direction = FrameDirection.Normal;
            three.Speed = 2;
            Assert.Equal(1, three.FrameAt(TimeSpan.FromMilliseconds(71)));
        });

        // Forty 8 by 8 frames written by an ordinary encoder as changed rectangles over the last frame (disposal 1) and as whole frames cleared after each (disposal 2).
        private const string KeepEach = "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";
        private const string ClearEach = "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";

        private static int Want(int k, int x, int y) => x < 4 || k % 3 == 0 ? (x / 4 + y / 4 * 2 + k) % 4 : (x + y) % 4;

        // Frames are composited on demand from every sixteenth frame's kept canvas, so jumps back and forth must give what stepping gives.
        [Theory]
        [InlineData("keep", KeepEach)]
        [InlineData("clear", ClearEach)]
        public Task Frames_composited_on_demand_match_in_any_order(string name, string gif) => UiTest.Run(() =>
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "long-" + name + ".gif");
            File.WriteAllBytes(path, Convert.FromBase64String(gif));
            var image = new FrameSequenceImage { Source = path, Fit = ImageFit.Fill, Width = 8, Height = 8 };
            Assert.Equal(40, image.FrameCount);
            ToolWindow window = Show(image, 8, 8);
            foreach (int k in new[] { 0, 1, 2, 39, 5, 17, 16, 31, 30, 3, 38 })
            {
                image.Time = TimeSpan.FromMilliseconds(50 * (k + 1) + 10);
                Assert.Equal(k, image.FrameAt(image.Time));
                RenderedFrame f = Frame(window);
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                        Assert.Equal(Palette[Want(k, x, y)], At(f, x + 0.5, y + 0.5));
            }

            window.Close();
        });

        // One image block: its rectangle, its colour indices in display order, a transparent index or -1, its disposal method, and whether it is stored interlaced.
        private sealed record Part(int Left, int Top, int W, int H, int[] Indices, int Transparent = -1, int Disposal = 1, bool Interlaced = false);

        // Writes a GIF of the palette and parts, every code a clear code and a pixel so that no compression is needed.
        private static string Encode(string name, int w, int h, params Part[] parts)
        {
            var b = new System.Collections.Generic.List<byte>();
            b.AddRange("GIF89a"u8.ToArray());
            b.AddRange([(byte)w, 0, (byte)h, 0, 0x81, 0, 0]);
            foreach (Color c in Palette) b.AddRange([c.R, c.G, c.B]);
            foreach (Part p in parts)
            {
                byte packed = (byte)(p.Disposal << 2 | (p.Transparent >= 0 ? 1 : 0));
                b.AddRange([0x21, 0xF9, 4, packed, 5, 0, (byte)Math.Max(0, p.Transparent), 0]);
                b.AddRange([0x2C, (byte)p.Left, 0, (byte)p.Top, 0, (byte)p.W, 0, (byte)p.H, 0, (byte)(p.Interlaced ? 0x40 : 0), 2]);
                var rows = Enumerable.Range(0, p.H).ToList();
                if (p.Interlaced) rows = new[] { (0, 8), (4, 8), (2, 4), (1, 2) }.SelectMany(s => Enumerable.Range(0, p.H).Where(y => y >= s.Item1 && (y - s.Item1) % s.Item2 == 0)).ToList();
                var bits = new System.Collections.Generic.List<byte>();
                int acc = 0, n = 0;
                void Put(int code) { acc |= code << n; n += 3; while (n >= 8) { bits.Add((byte)acc); acc >>= 8; n -= 8; } }
                foreach (int y in rows)
                    for (int x = 0; x < p.W; x++) { Put(4); Put(p.Indices[y * p.W + x]); }
                Put(5);
                if (n > 0) bits.Add((byte)acc);
                for (int i = 0; i < bits.Count; i += 255) { int len = Math.Min(255, bits.Count - i); b.Add((byte)len); b.AddRange(bits.GetRange(i, len)); }
                b.Add(0);
            }

            b.Add(0x3B);
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, name + ".gif");
            File.WriteAllBytes(path, b.ToArray());
            return path;
        }

        private static int[] Fill(int n, int index) => Enumerable.Repeat(index, n).ToArray();

        private static Color[] Shown(string path, int frames, int frame)
        {
            var image = new FrameSequenceImage { Source = path, Fit = ImageFit.Fill, Width = 8, Height = 8, Time = TimeSpan.FromMilliseconds(50 * (frame + 1) + 10) };
            ToolWindow window = Show(image, 8, 8);
            Assert.Equal(frames, image.FrameCount);
            RenderedFrame f = Frame(window);
            window.Close();
            return Enumerable.Range(0, 64).Select(i => At(f, i % 8 + 0.5, i / 8 + 0.5)).ToArray();
        }

        // Disposal 2 clears the rectangle to nothing, disposal 3 restores what was under it, and a transparent index leaves the frame beneath.
        [Fact]
        public Task Disposal_and_transparency_compose_as_the_format_says() => UiTest.Run(() =>
        {
            string two = Encode("dispose-2", 8, 8, new Part(0, 0, 8, 8, Fill(64, 0)), new Part(0, 0, 4, 4, Fill(16, 2), Disposal: 2), new Part(4, 4, 4, 4, Fill(16, 1)));
            Color[] after2 = Shown(two, 3, 2);
            Assert.Equal(Colors.Black, after2[0]);
            Assert.Equal(Palette[0], after2[7]);
            Assert.Equal(Palette[1], after2[63]);
            string three = Encode("dispose-3", 8, 8, new Part(0, 0, 8, 8, Fill(64, 0)), new Part(0, 0, 4, 4, Fill(16, 2), Disposal: 3), new Part(4, 4, 4, 4, Fill(16, 1)));
            Assert.Equal(Palette[0], Shown(three, 3, 2)[0]);
            int[] holes = Enumerable.Range(0, 64).Select(i => i % 2 == 0 ? 3 : 1).ToArray();
            string clear = Encode("transparent", 8, 8, new Part(0, 0, 8, 8, Fill(64, 0)), new Part(0, 0, 8, 8, holes, Transparent: 3));
            Color[] t = Shown(clear, 2, 1);
            Assert.Equal(Palette[0], t[0]);
            Assert.Equal(Palette[1], t[1]);
        });

        // Interlaced rows are stored 0, 8, … then 4, … then 2, 6, … then the odd rows; each row here has its own colour.
        [Fact]
        public Task Interlaced_rows_land_in_their_place() => UiTest.Run(() =>
        {
            int[] rows = Enumerable.Range(0, 64).Select(i => i / 8 % 4).ToArray();
            string path = Encode("interlaced", 8, 8, new Part(0, 0, 8, 8, rows, Interlaced: true));
            Color[] shown = Shown(path, 1, 0);
            for (int y = 0; y < 8; y++) Assert.Equal(Palette[y % 4], shown[y * 8 + 3]);
        });

        // A jump back recomposes from the canvas kept before frame 16; a frame there that restores what was under it must not survive into frame 17.
        [Fact]
        public Task A_restoring_frame_at_a_kept_canvas_is_undone_after_a_jump_back() => UiTest.Run(() =>
        {
            var parts = new System.Collections.Generic.List<Part> { new(0, 0, 8, 8, Fill(64, 0)) };
            for (int k = 1; k < 16; k++) parts.Add(new Part(7, 7, 1, 1, Fill(1, k % 4)));
            parts.Add(new Part(0, 0, 4, 4, Fill(16, 2), Disposal: 3));
            for (int k = 17; k < 20; k++) parts.Add(new Part(7, 0, 1, 1, Fill(1, 3)));
            string path = Encode("restore-at-key", 8, 8, parts.ToArray());
            var image = new FrameSequenceImage { Source = path, Fit = ImageFit.Fill, Width = 8, Height = 8 };
            ToolWindow window = Show(image, 8, 8);
            image.Time = TimeSpan.FromMilliseconds(50 * 20 + 10);
            Frame(window);
            image.Time = TimeSpan.FromMilliseconds(50 * 18 + 10);
            Assert.Equal(17, image.FrameAt(image.Time));
            Assert.Equal(Palette[0], At(Frame(window), 0.5, 0.5));
            image.Time = TimeSpan.FromMilliseconds(50 * 17 + 10);
            Assert.Equal(16, image.FrameAt(image.Time));
            Assert.Equal(Palette[2], At(Frame(window), 0.5, 0.5));
            window.Close();
        });

        [Fact]
        public Task A_missing_or_broken_file_shows_nothing() => UiTest.Run(() =>
        {
            Directory.CreateDirectory(Folder);
            string broken = Path.Combine(Folder, "broken.gif");
            File.WriteAllBytes(broken, [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0]);
            Assert.Equal(0, new FrameSequenceImage { Source = broken }.FrameCount);
            Assert.Equal(0, new FrameSequenceImage { Source = "/nonexistent/lunap/a.gif" }.FrameCount);
        });
    }
}
