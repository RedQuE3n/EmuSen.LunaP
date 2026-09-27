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
