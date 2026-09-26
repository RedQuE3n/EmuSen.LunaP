using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // FittedImage - see docs/LunaP.md §98.2: the four fits, the tint and gradient, saturation, corners and sampling.
    public class FittedImageTests
    {
        private static ToolWindow Host(FittedImage image, double w, double h)
        {
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetSize(image, new Size(1, 1));
            canvas.Children.Add(image);
            return Show(canvas, w, h);
        }

        private static string Halves => Png("halves", 200, 100, (x, _) => x < 100 ? Colors.Red : Colors.Blue);

        [Fact]
        public Task Contain_fits_inside_and_centres_and_measures_to_the_fitted_size() => UiTest.Run(() =>
        {
            var image = new FittedImage { Source = Halves, Fit = ImageFit.Contain };
            ToolWindow window = Host(image, 200, 200);
            RenderedFrame f = Frame(window);
            Assert.Equal(new Rect(0, 50, 200, 100), image.DrawnRect);
            Assert.Equal(Colors.Black, At(f, 100, 20));
            Assert.Equal(Colors.Red, At(f, 50, 100));
            image.Measure(new Size(100, 1000));
            Assert.Equal(new Size(100, 50), image.DesiredSize);
            window.Close();
        });

        [Fact]
        public Task Fill_stretches_to_the_box() => UiTest.Run(() =>
        {
            var image = new FittedImage { Source = Halves, Fit = ImageFit.Fill };
            ToolWindow window = Host(image, 200, 200);
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Red, At(f, 50, 10));
            Assert.Equal(Colors.Blue, At(f, 150, 190));
            window.Close();
        });

        // A square box over a 2:1 picture shows one half or the other depending on where it crops.
        [Fact]
        public Task Cover_fills_the_box_and_crops_at_CropPosition() => UiTest.Run(() =>
        {
            var image = new FittedImage { Source = Halves, Fit = ImageFit.Cover, CropPosition = new Point(0, 0.5) };
            ToolWindow window = Host(image, 100, 100);
            RenderedFrame left = Frame(window);
            Assert.Equal(Colors.Red, At(left, 10, 50));
            Assert.Equal(Colors.Red, At(left, 90, 50));

            image.CropPosition = new Point(1, 0.5);
            RenderedFrame right = Frame(window);
            Assert.Equal(Colors.Blue, At(right, 10, 50));
            Assert.Equal(Colors.Blue, At(right, 90, 50));
            Assert.Equal(new Rect(-100, 0, 200, 100), FittedImage.Cover(new Rect(0, 0, 100, 100), new Size(200, 100), new Point(1, 0.5)));
            window.Close();
        });

        [Fact]
        public Task Tile_repeats_from_the_aligned_corner() => UiTest.Run(() =>
        {
            string checker = Png("checker", 2, 2, (x, y) => (x + y) % 2 == 0 ? Colors.White : Colors.Red);
            var image = new FittedImage { Source = checker, Fit = ImageFit.Tile, TileSize = new Size(20, 20), Interpolation = BitmapInterpolationMode.None };
            ToolWindow window = Host(image, 100, 100);
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.White, At(f, 2, 2));
            Assert.Equal(Colors.Red, At(f, 12, 2));
            Assert.Equal(Colors.White, At(f, 22, 2));
            Assert.Equal(Colors.Red, At(f, 32, 2));

            image.TileSize = new Size(30, 0);
            image.TileHorizontalAlignment = HorizontalAlignment.Right;
            RenderedFrame right = Frame(window);
            Assert.Equal(Colors.Red, At(right, 97, 2));
            Assert.Equal(Colors.White, At(right, 79, 2));
            Assert.Equal(Colors.Red, At(right, 2, 2));
            window.Close();
        });

        [Fact]
        public Task Tint_multiplies_every_channel_alpha_included() => UiTest.Run(() =>
        {
            string white = Flat("white", 10, 10, Colors.White);
            var image = new FittedImage { Source = white, Fit = ImageFit.Fill, Tint = Color.FromArgb(255, 255, 128, 0) };
            ToolWindow window = Host(image, 50, 50);
            Assert.True(Near(Color.FromRgb(255, 128, 0), At(Frame(window), 25, 25), 2));

            image.Tint = Color.FromArgb(128, 255, 255, 255);
            Assert.True(Near(Color.FromRgb(128, 128, 128), At(Frame(window), 25, 25), 3));
            window.Close();
        });

        [Fact]
        public Task TintEnd_runs_a_gradient_across_in_the_direction_given() => UiTest.Run(() =>
        {
            string white = Flat("white-wide", 100, 100, Colors.White);
            var image = new FittedImage { Source = white, Fit = ImageFit.Fill, Tint = Colors.Red, TintEnd = Colors.Blue };
            ToolWindow window = Host(image, 100, 100);
            RenderedFrame across = Frame(window);
            Assert.True(Near(Colors.Red, At(across, 1, 50), 8));
            Assert.True(Near(Colors.Blue, At(across, 98, 50), 8));

            image.TintDirection = Orientation.Vertical;
            RenderedFrame down = Frame(window);
            Assert.True(Near(Colors.Red, At(down, 50, 1), 8));
            Assert.True(Near(Colors.Blue, At(down, 50, 98), 8));
            window.Close();
        });

        [Fact]
        public Task Saturation_zero_is_Rec601_grey() => UiTest.Run(() =>
        {
            string red = Flat("red", 10, 10, Colors.Red);
            var image = new FittedImage { Source = red, Fit = ImageFit.Fill, Saturation = 0 };
            ToolWindow window = Host(image, 50, 50);
            Assert.True(Near(Color.FromRgb(76, 76, 76), At(Frame(window), 25, 25), 2));
            image.Saturation = 0.5;
            Color half = At(Frame(window), 25, 25);
            Assert.True(Near(Color.FromRgb(166, 38, 38), half, 3), half.ToString());
            window.Close();
        });

        [Fact]
        public Task CornerRadius_clips_the_corners() => UiTest.Run(() =>
        {
            string white = Flat("white-corner", 10, 10, Colors.White);
            var image = new FittedImage { Source = white, Fit = ImageFit.Fill };
            ToolWindow window = Host(image, 100, 100);
            Assert.Equal(Colors.White, At(Frame(window), 1, 1));
            image.CornerRadius = 30;
            Assert.Equal(Colors.Black, At(Frame(window), 1, 1));
            Assert.Equal(Colors.White, At(Frame(window), 50, 1));
            window.Close();
        });

        // Two pixels blown up to a hundred: nearest keeps two flat colours, linear blends between them.
        [Fact]
        public Task Interpolation_None_is_nearest_and_the_default_blends() => UiTest.Run(() =>
        {
            string pair = Png("pair", 2, 1, (x, _) => x == 0 ? Colors.Black : Colors.White);
            var image = new FittedImage { Source = pair, Fit = ImageFit.Fill };
            ToolWindow window = Host(image, 100, 20);
            Color blended = At(Frame(window), 48, 10);
            Assert.InRange(blended.R, 40, 215);

            image.Interpolation = BitmapInterpolationMode.None;
            Assert.Equal(Colors.Black, At(Frame(window), 48, 10));
            Assert.Equal(Colors.White, At(Frame(window), 52, 10));
            window.Close();
        });

        [Fact]
        public Task A_missing_file_draws_nothing_and_says_so() => UiTest.Run(() =>
        {
            var image = new FittedImage { Source = "/nonexistent/lunap/picture.png", Fit = ImageFit.Fill };
            ToolWindow window = Host(image, 50, 50);
            Assert.True(image.IsMissing);
            Assert.Equal(Colors.Black, At(Frame(window), 25, 25));
            Assert.Equal(default, image.IntrinsicSize);
            window.Close();
        });

        // An SVG source is drawn at the size it is shown, so it stays sharp; a refused one is a crossed box.
        [Fact]
        public Task An_svg_source_is_rasterised_at_its_drawn_size_and_a_refused_one_is_crossed_out() => UiTest.Run(() =>
        {
            string square = Svg("image-square", "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><rect x='0' y='0' width='10' height='10' fill='#00ff00'/></svg>");
            string refused = Svg("image-refused", "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><text x='0' y='5'>no</text></svg>");
            var image = new FittedImage { Source = square, Fit = ImageFit.Fill, Tint = Colors.White };
            ToolWindow window = Host(image, 80, 80);
            Assert.Equal(new Size(10, 10), image.IntrinsicSize);
            Assert.Equal(Colors.Lime, At(Frame(window), 79, 79));

            image.Source = refused;
            RenderedFrame crossed = Frame(window);
            Assert.True(image.IsRefused);
            Assert.Equal(EmuSen.LunaP.Theme.LunaPalette.Error.Color, At(crossed, 40, 40));
            window.Close();
        });
    }
}
