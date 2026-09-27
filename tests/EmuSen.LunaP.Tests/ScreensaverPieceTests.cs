using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // DimLayer and CrossFadeImage - see docs/LunaP.md §184.
    public class ScreensaverPieceTests
    {
        private static readonly Color Green = Color.FromRgb(40, 161, 61);

        private static ToolWindow Over(Color colour, Control layer) =>
            Show(new Grid { Children = { new Border { Background = new SolidColorBrush(colour) }, layer } }, 120, 80);

        private static void Near(int r, int g, int b, Color c)
        {
            Assert.InRange(c.R, r - 1, r + 1);
            Assert.InRange(c.G, g - 1, g + 1);
            Assert.InRange(c.B, b - 1, b + 1);
        }

        // The luma is 0.3 R + 0.59 G + 0.11 B, 113.7 for this green; ES-DE's Dim drew (45, 45, 45) from it at 0.4.
        [Fact]
        public Task No_saturation_is_the_grey_of_the_luma_and_the_brightness_scales_it() => UiTest.Run(() =>
        {
            var dim = new DimLayer { Saturation = 0, Brightness = 0.4 };
            ToolWindow window = Over(Green, dim);
            Near(45, 45, 45, At(Frame(window), 60, 40));
            Assert.True(dim.IsDimming);
            window.Close();
        });

        [Fact]
        public Task Half_saturation_is_halfway_to_the_grey_and_brightness_alone_keeps_the_hue() => UiTest.Run(() =>
        {
            var dim = new DimLayer { Saturation = 0.5 };
            ToolWindow window = Over(Green, dim);
            Near(77, 137, 87, At(Frame(window), 60, 40));
            dim.Saturation = 1;
            dim.Brightness = 0.5;
            Near(20, 80, 30, At(Frame(window), 60, 40));
            window.Close();
        });

        [Fact]
        public Task At_one_and_one_it_changes_nothing_and_values_are_clamped() => UiTest.Run(() =>
        {
            var dim = new DimLayer();
            ToolWindow window = Over(Green, dim);
            Assert.False(dim.IsDimming);
            Assert.Equal(Green, At(Frame(window), 60, 40));
            dim.Saturation = 3;
            dim.Brightness = 2;
            Assert.False(dim.IsDimming);
            Assert.Equal(Green, At(Frame(window), 60, 40));
            dim.Brightness = -1;
            Assert.Equal(Colors.Black, At(Frame(window), 60, 40));
            window.Close();
        });

        private static string Red => Flat("fade-red", 40, 40, Colors.Red);
        private static string Blue => Flat("fade-blue", 40, 40, Colors.Blue);

        [Fact]
        public Task A_new_picture_fades_in_over_the_old_one_and_the_old_one_goes_at_the_end() => UiTest.Run(() =>
        {
            var image = new CrossFadeImage { Fit = ImageFit.Fill };
            ToolWindow window = Over(Colors.Black, image);
            image.Show(Red);
            Assert.Equal(0, image.Progress);
            Assert.Null(image.Previous);
            image.Progress = 1;
            Assert.Equal(Colors.Red, At(Frame(window), 60, 40));

            image.Show(Blue);
            Assert.Equal(Red, image.Previous);
            Assert.Equal(Colors.Red, At(Frame(window), 60, 40));
            image.Progress = 0.5;
            Near(127, 0, 128, At(Frame(window), 60, 40));
            image.Progress = 1;
            Assert.Null(image.Previous);
            Assert.Equal(Colors.Blue, At(Frame(window), 60, 40));
            window.Close();
        });

        [Fact]
        public Task Over_nothing_it_fades_in_from_what_is_behind() => UiTest.Run(() =>
        {
            var image = new CrossFadeImage { Fit = ImageFit.Fill };
            ToolWindow window = Over(Colors.Black, image);
            image.Show(Red);
            image.Progress = 1;
            image.Show(Blue, overPrevious: false);
            Assert.Null(image.Previous);
            Assert.Equal(Colors.Black, At(Frame(window), 60, 40));
            image.Progress = 0.25;
            Assert.Equal(0.25, image.SourceOpacity);
            Near(0, 0, 64, At(Frame(window), 60, 40));
            image.Progress = 7;
            Assert.Equal(1, image.SourceOpacity);
            Assert.Equal(Colors.Blue, At(Frame(window), 60, 40));
            window.Close();
        });

        [Fact]
        public Task Both_pictures_take_the_fit_and_sample_at_high_quality() => UiTest.Run(() =>
        {
            var image = new CrossFadeImage();
            Assert.Equal(ImageFit.Contain, image.Fit);
            Assert.Equal(Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality, image.Interpolation);
            ToolWindow window = Over(Colors.Black, image);
            image.Show(Flat("fade-wide", 80, 20, Colors.Red));
            image.Progress = 1;
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Red, At(f, 60, 40));
            Assert.Equal(Colors.Black, At(f, 60, 5));
            image.Fit = ImageFit.Fill;
            Assert.Equal(Colors.Red, At(Frame(window), 60, 5));
            window.Close();
        });
    }
}
