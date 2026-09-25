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
    // NormalizedCanvas - see docs/LunaP.md §98.1: fractions of the panel, an origin, a rotation, and a depth order.
    public class NormalizedCanvasTests
    {
        private static Border Box(Color colour) => new() { Background = new SolidColorBrush(colour) };

        private static T Put<T>(NormalizedCanvas canvas, T child, Point pos, Size size, Point origin = default, double depth = 0) where T : Control
        {
            NormalizedCanvas.SetPosition(child, pos);
            NormalizedCanvas.SetSize(child, size);
            NormalizedCanvas.SetOrigin(child, origin);
            NormalizedCanvas.SetDepth(child, depth);
            canvas.Children.Add(child);
            return child;
        }

        [Fact]
        public Task Position_size_and_origin_are_fractions_of_the_panel_and_of_the_child() => UiTest.Run(() =>
        {
            var canvas = new NormalizedCanvas();
            Border centred = Put(canvas, Box(Colors.Red), new Point(0.5, 0.5), new Size(0.25, 0.5), new Point(0.5, 0.5));
            Border corner = Put(canvas, Box(Colors.Blue), new Point(1, 1), new Size(0.1, 0.1), new Point(1, 1));
            ToolWindow window = Show(canvas, 800, 500);

            Assert.Equal(new Rect(300, 125, 200, 250), centred.Bounds);
            Assert.Equal(new Rect(720, 450, 80, 50), corner.Bounds);
            window.Close();
        });

        [Fact]
        public Task Place_is_the_arithmetic_the_panel_uses() => UiTest.Run(() =>
        {
            Assert.Equal(new Rect(-50, 20, 100, 40), NormalizedCanvas.Place(new Size(1000, 400), new Point(0, 0.1), new Point(0.5, 0.5), new Size(100, 40)));
        });

        // A fraction written to eight places and read as a float is 800.00064 of 1920; layout rounding would ceil that to 801.
        [Fact]
        public Task Float_noise_in_a_fraction_does_not_become_a_whole_pixel() => UiTest.Run(() =>
        {
            var canvas = new NormalizedCanvas();
            Border panel = Put(canvas, Box(Colors.Red), new Point(0, 0), new Size((double)0.41666667f, 1));
            Border icon = Put(canvas, new Border { Child = new Border { Width = 10, Height = 10 } }, new Point(0.5, 0.5), new Size(0, (double)0.05f));
            ToolWindow window = Show(canvas, 1920, 800);
            Assert.Equal(800, panel.Bounds.Width);
            Assert.Equal(40, icon.Bounds.Height);
            window.Close();
        });

        // An axis of 0 takes what the child asks for, and MaxSize bounds that request.
        [Fact]
        public Task A_zero_axis_takes_the_childs_own_size_within_its_maximum() => UiTest.Run(() =>
        {
            string wide = Flat("canvas-wide", 100, 50, Colors.Lime);
            var canvas = new NormalizedCanvas();
            var fitted = Put(canvas, new FittedImage { Source = wide, Fit = ImageFit.Contain }, new Point(0, 0), default);
            NormalizedCanvas.SetMaxSize(fitted, new Size(0.5, 0.5));
            var byWidth = Put(canvas, new FittedImage { Source = wide }, new Point(0, 0.6), new Size(0.25, 0));
            ToolWindow window = Show(canvas, 800, 500);

            Assert.Equal(new Size(400, 200), fitted.Bounds.Size);
            Assert.Equal(new Size(200, 100), byWidth.Bounds.Size);
            window.Close();
        });

        // Depth decides what is on top; equal depths keep the order the children were added in.
        [Fact]
        public Task Depth_orders_drawing_and_ties_keep_the_order_of_Children() => UiTest.Run(() =>
        {
            var canvas = new NormalizedCanvas();
            Border red = Put(canvas, Box(Colors.Red), new Point(0, 0), new Size(1, 1), depth: 2);
            Border blue = Put(canvas, Box(Colors.Blue), new Point(0, 0), new Size(1, 1), depth: 1);
            ToolWindow window = Show(canvas, 100, 100);
            Assert.Equal(Colors.Red, At(Frame(window), 50, 50));

            NormalizedCanvas.SetDepth(red, 0.5);
            Assert.Equal(Colors.Blue, At(Frame(window), 50, 50));

            NormalizedCanvas.SetDepth(red, 1);
            Assert.Equal(Colors.Blue, At(Frame(window), 50, 50));
            window.Close();
        });

        [Fact]
        public Task Rotation_turns_a_child_about_its_rotation_origin() => UiTest.Run(() =>
        {
            var canvas = new NormalizedCanvas();
            Border bar = Put(canvas, Box(Colors.White), new Point(0.5, 0.5), new Size(0.8, 0.1), new Point(0.5, 0.5));
            ToolWindow window = Show(canvas, 200, 200);
            Assert.Equal(Colors.White, At(Frame(window), 25, 100));
            Assert.Equal(Colors.Black, At(Frame(window), 100, 25));

            NormalizedCanvas.SetRotation(bar, 90);
            RenderedFrame turned = Frame(window);
            Assert.Equal(Colors.Black, At(turned, 25, 100));
            Assert.Equal(Colors.White, At(turned, 100, 25));

            NormalizedCanvas.SetRotationOrigin(bar, new Point(0, 0.5));
            RenderedFrame aboutLeft = Frame(window);
            Assert.Equal(Colors.White, At(aboutLeft, 20, 150));
            window.Close();
        });
    }
}
