using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // ImageCarousel's wheel layout, content offset, clipping, growth point and reflections - see docs/LunaP.md §190.
    public class CarouselWheelTests
    {
        private static ToolWindow Fill(Control control, double w, double h)
        {
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetSize(control, new Size(1, 1));
            canvas.Children.Add(control);
            return Show(canvas, w, h);
        }

        private static CarouselItem[] Pictures(int n) => Enumerable.Range(0, n)
            .Select(i => new CarouselItem(Flat($"wheel-{i}", 200, 100, Color.FromRgb((byte)(20 * i + 40), 200, 0)), $"Item {i}")).ToArray();

        private static Point Centre(Control child)
        {
            Rect r = child.Bounds;
            Matrix m = Matrix.CreateTranslation(r.X, r.Y);
            if (child.RenderTransform is { } t) m = t.Value * m;
            return new Point(r.Width / 2, r.Height / 2).Transform(m);
        }

        private static void Near(Point expected, Point actual, double within = 0.01) =>
            Assert.True(Math.Abs(expected.X - actual.X) <= within && Math.Abs(expected.Y - actual.Y) <= within, $"expected {expected}, got {actual}");

        private static Point Turned(Point p, Point about, double degrees)
        {
            double a = degrees * Math.PI / 180, dx = p.X - about.X, dy = p.Y - about.Y;
            return new Point(about.X + dx * Math.Cos(a) - dy * Math.Sin(a), about.Y + dx * Math.Sin(a) + dy * Math.Cos(a));
        }

        [Fact]
        public Task A_vertical_wheel_turns_each_item_about_its_origin_by_its_distance_from_the_selection() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel { Items = Pictures(12), SelectedIndex = 6, Layout = CarouselLayout.Wheel, Orientation = Orientation.Vertical, ItemSize = new Size(256, 80), ItemScale = 1 };
            ToolWindow window = Fill(carousel, 512, 800);
            Point centre = new(256 - 128 - 3 * 256, 400);
            Near(new Point(256, 400), Centre(carousel.Shown.Single(s => s.Offset == 0).Child));
            Near(Turned(new Point(256, 400), centre, 7.5), Centre(carousel.Shown.Single(s => s.Offset == 1).Child));
            Near(Turned(new Point(256, 400), centre, -22.5), Centre(carousel.Shown.Single(s => s.Offset == -3).Child));
            Assert.Equal(17, carousel.Shown.Count);
            carousel.ItemsBefore = 2;
            carousel.ItemsAfter = 4;
            UiTest.Settle(carousel);
            Assert.Equal(Enumerable.Range(-2, 7), carousel.Shown.Select(s => s.Offset).OrderBy(o => o));
            window.Close();
        });

        [Fact]
        public Task A_horizontal_wheel_reads_its_origin_a_quarter_turn_anticlockwise() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel
            {
                Items = Pictures(12), SelectedIndex = 6, Layout = CarouselLayout.Wheel, ItemSize = new Size(128, 160), ItemScale = 1, WheelRotation = 30, WheelOrigin = new Point(-1, 1.5),
            };
            ToolWindow window = Fill(carousel, 1280, 800);
            Near(Turned(new Point(640, 400), new Point(640 + 1.0 * 160, 400 + 1.5 * 128), 30), Centre(carousel.Shown.Single(s => s.Offset == 1).Child));
            window.Close();
        });

        [Fact]
        public Task Upright_items_travel_round_the_wheel_without_turning() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel
            {
                Items = Pictures(12), SelectedIndex = 6, Layout = CarouselLayout.Wheel, Orientation = Orientation.Vertical, ItemSize = new Size(256, 80), ItemScale = 1,
                WheelRotation = -30, WheelOrigin = new Point(2, 3), ItemsUpright = true,
            };
            ToolWindow window = Fill(carousel, 1280, 800);
            Control next = carousel.Shown.Single(s => s.Offset == 1).Child;
            Matrix m = next.RenderTransform!.Value;
            Assert.Equal(1, m.M11, 9);
            Assert.Equal(0, m.M12, 9);
            Vector arm = new(-2 * 256, 0);
            Near(new Point(640, 400) + new Vector(arm.X * Math.Cos(-Math.PI / 6), arm.X * Math.Sin(-Math.PI / 6)) - arm, Centre(next));
            window.Close();
        });

        [Fact]
        public Task The_wheel_alignment_places_the_hub_and_a_side_alignment_shifts_every_item_by_half_the_growth() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel
            {
                Items = Pictures(12), SelectedIndex = 6, Layout = CarouselLayout.Wheel, Orientation = Orientation.Vertical, ItemSize = new Size(256, 80), ItemScale = 1,
                WheelHorizontalAlignment = HorizontalAlignment.Left,
            };
            ToolWindow window = Fill(carousel, 512, 800);
            Near(new Point(128, 400), Centre(carousel.Shown.Single(s => s.Offset == 0).Child));
            carousel.WheelHorizontalAlignment = HorizontalAlignment.Center;
            carousel.ItemSize = new Size(320, 80);
            carousel.ItemHorizontalAlignment = HorizontalAlignment.Left;
            carousel.ItemScale = 1.5;
            UiTest.Settle(carousel);
            Near(new Point(96 - 80 + 120, 400), Centre(carousel.Shown.Single(s => s.Offset == 0).Child), 0.5);
            window.Close();
        });

        [Fact]
        public Task A_row_is_offset_by_fractions_of_its_size_clips_to_its_box_and_grows_from_its_aligned_edge() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel { Items = Pictures(9), SelectedIndex = 4, MaxItemCount = 5, ItemSize = new Size(200, 160), ItemScale = 1.5, ContentOffset = new Point(0.25, 0.25) };
            Assert.True(carousel.ClipToBounds);
            ToolWindow window = Fill(carousel, 1000, 320);
            Assert.Equal(new Rect(400 + 250, 80 + 80, 200, 160), carousel.ItemRect(0, carousel.Bounds.Size));
            carousel.ContentOffset = default;
            carousel.ItemVerticalAlignment = VerticalAlignment.Top;
            UiTest.Settle(carousel);
            Control selected = carousel.Shown.Single(s => s.Offset == 0).Child;
            Near(new Point(500, 0), new Point(selected.Bounds.Width / 2, 0).Transform(selected.RenderTransform!.Value * Matrix.CreateTranslation(selected.Bounds.X, selected.Bounds.Y)));
            window.Close();
        });

        [Fact]
        public Task A_reflection_mirrors_its_image_beneath_it_and_fades_by_the_falloff() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel
            {
                Items = Pictures(5), SelectedIndex = 2, MaxItemCount = 5, ItemSize = new Size(200, 160), ItemScale = 1, UnfocusedItemOpacity = 1,
                Reflections = true, ReflectionOpacity = 1, ReflectionFalloff = 2,
            };
            ToolWindow window = Fill(carousel, 1000, 400);
            Assert.Equal(new Rect(400, 40, 200, 160), carousel.ItemRect(0, carousel.Bounds.Size));
            Assert.Equal(carousel.Shown.Count, carousel.ShownReflections.Count);
            RenderedFrame f = Frame(window);
            Assert.Equal(200, At(f, 500, 169).G);
            Assert.InRange(At(f, 500, 172).G, 180, 200);
            Assert.InRange(At(f, 500, 195).G, 90, 110);
            Assert.InRange(At(f, 500, 225).G, 0, 5);
            carousel.Orientation = Orientation.Vertical;
            UiTest.Settle(carousel);
            Assert.Empty(carousel.ShownReflections);
            window.Close();
        });
    }
}
