using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Motion;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // Glide, and the drawn controls whose state is a function of a time value - see docs/LunaP.md §102.
    public class MotionTests
    {
        private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

        private static ToolWindow Fill(Control control, double w, double h)
        {
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetSize(control, new Size(1, 1));
            canvas.Children.Add(control);
            return Show(canvas, w, h);
        }

        [Fact]
        public void A_glide_holds_its_ends_and_eases_between_them()
        {
            var glide = new Glide(2, 6, Ms(100), Ms(200), new CubicEaseOut());
            Assert.Equal(2, glide.ValueAt(Ms(0)));
            Assert.Equal(2, glide.ValueAt(Ms(100)));
            Assert.Equal(2 + 4 * (1 - Math.Pow(0.5, 3)), glide.ValueAt(Ms(200)), 9);
            Assert.Equal(6, glide.ValueAt(Ms(300)));
            Assert.Equal(6, glide.ValueAt(Ms(5000)));
            Assert.False(glide.IsSettledAt(Ms(299)));
            Assert.True(glide.IsSettledAt(Ms(300)));
            Assert.Equal(Ms(300), glide.End);
            Assert.Equal(3, new Glide(2, 6, Ms(0), Ms(400)).ValueAt(Ms(100)), 9);
            Assert.Equal(6, new Glide(2, 6, Ms(100), TimeSpan.Zero).ValueAt(Ms(100)));
            Assert.Equal(5, Glide.At(5).ValueAt(Ms(-10)));
        }

        [Fact]
        public void A_glide_turned_mid_move_starts_from_where_it_had_got_to()
        {
            var first = new Glide(0, 10, Ms(0), Ms(100));
            Glide second = first.Toward(-10, Ms(25), Ms(200));
            Assert.Equal(2.5, second.From, 9);
            Assert.Equal(2.5, second.ValueAt(Ms(25)), 9);
            Assert.Equal(-10, second.ValueAt(Ms(225)));
            Assert.Equal(Ms(25), second.Start);
        }

        private static CarouselItem[] Pictures(int n) => Enumerable.Range(0, n)
            .Select(i => new CarouselItem(Flat($"motion-{i}", 40, 80, Color.FromRgb((byte)(20 * i + 40), (byte)(10 * i), 200)), $"Item {i}")).ToArray();

        [Fact]
        public Task A_fractional_position_places_items_between_their_slots_and_weights_focus_by_distance() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel { Items = Pictures(9), SelectedIndex = 4, MaxItemCount = 4, ItemScale = 1.5, UnfocusedItemOpacity = 0.2, ItemSize = new Size(100, 200) };
            ToolWindow window = Fill(carousel, 800, 400);
            carousel.Position = 4.25;
            UiTest.Settle(carousel);
            Control four = carousel.Shown.Single(s => (int)s.Child.Tag! == 4).Child;
            Control five = carousel.Shown.Single(s => (int)s.Child.Tag! == 5).Child;
            Assert.Equal(new Rect(300, 100, 100, 200), four.Bounds);
            Assert.Equal(new Rect(500, 100, 100, 200), five.Bounds);
            Assert.Equal(0.2 + 0.8 * 0.75, four.Opacity, 9);
            Assert.Equal(0.2 + 0.8 * 0.25, five.Opacity, 9);
            Assert.Equal(1 + 0.5 * 0.75, Assert.IsType<ScaleTransform>(four.RenderTransform).ScaleX, 9);
            Assert.Equal(1 + 0.5 * 0.25, Assert.IsType<ScaleTransform>(five.RenderTransform).ScaleX, 9);
            Assert.Same(four, carousel.Shown[^1].Child);

            carousel.Position = 4.75;
            UiTest.Settle(carousel);
            five = carousel.Shown.Single(s => (int)s.Child.Tag! == 5).Child;
            Assert.Same(five, carousel.Shown[^1].Child);
            Assert.Equal(new Rect(400, 100, 100, 200), five.Bounds);
            window.Close();
        });

        [Fact]
        public Task A_whole_position_draws_what_the_selection_at_rest_draws() => UiTest.Run(() =>
        {
            var still = new ImageCarousel { Items = Pictures(7), SelectedIndex = 3, MaxItemCount = 3.5, UnfocusedItemOpacity = 0.4 };
            ToolWindow a = Fill(still, 600, 200);
            RenderedFrame rest = Frame(a);
            a.Close();

            var moving = new ImageCarousel { Items = Pictures(7), SelectedIndex = 2, MaxItemCount = 3.5, UnfocusedItemOpacity = 0.4, Position = 2.6 };
            ToolWindow b = Fill(moving, 600, 200);
            Frame(b);
            moving.SelectedIndex = 3;
            moving.Position = 3;
            UiTest.Settle(moving);
            Assert.Equal(0, Differing(rest, Frame(b)));
            moving.Position = 10;
            UiTest.Settle(moving);
            Assert.Equal(0, Differing(rest, Frame(b)));
            b.Close();
        });

        [Fact]
        public void A_loop_waits_moves_one_period_and_waits_again()
        {
            var scroll = new TextScroll(Ms(1000), 100, Gap: 50);
            Assert.Equal(0, scroll.LoopOffset(Ms(500), 250));
            Assert.Equal(0, scroll.LoopOffset(Ms(1000), 250));
            Assert.Equal(50, scroll.LoopOffset(Ms(1500), 250), 6);
            Assert.Equal(290, scroll.LoopOffset(Ms(3900), 250), 6);
            Assert.Equal(0, scroll.LoopOffset(Ms(4000), 250), 6);
            Assert.Equal(0, scroll.LoopOffset(Ms(4500), 250), 6);
            Assert.Equal(50, scroll.LoopOffset(Ms(5500), 250), 6);
            Assert.Equal(0, new TextScroll(Ms(0), 0).LoopOffset(Ms(9000), 250));
        }

        [Fact]
        public void A_column_waits_moves_to_its_end_pauses_and_fades_in_from_the_top()
        {
            var scroll = new TextScroll(Ms(1000), 50, EndPause: Ms(2000), FadeIn: Ms(400));
            Assert.Equal((0, 1), scroll.RunAt(Ms(900), 100));
            Assert.Equal(25, scroll.RunAt(Ms(1500), 100).Offset, 6);
            Assert.Equal((100, 1), scroll.RunAt(Ms(3000), 100));
            Assert.Equal((100, 1), scroll.RunAt(Ms(4900), 100));
            (double offset, double opacity) = scroll.RunAt(Ms(5100), 100);
            Assert.Equal(0, offset);
            Assert.Equal(0.25, opacity, 6);
            Assert.Equal((0, 1), scroll.RunAt(Ms(5400), 100));
            Assert.Equal(25, scroll.RunAt(Ms(6900), 100).Offset, 6);
            Assert.Equal((0, 1), scroll.RunAt(Ms(99999), 0));
        }

        private static (int Left, int Top) Ink(RenderedFrame f, int x0, int y0, int x1, int y1)
        {
            int left = int.MaxValue, top = int.MaxValue;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (At(f, x, y).R > 128) { left = Math.Min(left, x); top = Math.Min(top, y); }
            return (left, top);
        }

        [Fact]
        public Task A_horizontal_text_holds_then_slides_left_with_its_copy_following() => UiTest.Run(() =>
        {
            var text = new FontText
            {
                Text = "WWWWWWWWWWWWWWWWWWWW", FontSize = 20, Foreground = Brushes.White, ScrollDirection = TextScrollDirection.Horizontal,
                Scroll = new TextScroll(Ms(1000), 40, Gap: 30),
            };
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetPosition(text, new Point(0.1, 0.1));
            NormalizedCanvas.SetSize(text, new Size(0.5, 0.5));
            canvas.Children.Add(text);
            ToolWindow window = Show(canvas, 400, 100);
            int still = Ink(Frame(window), 0, 0, 400, 100).Left;
            text.ScrollTime = Ms(500);
            Assert.Equal(still, Ink(Frame(window), 0, 0, 400, 100).Left);
            text.ScrollTime = Ms(1250);
            Assert.Equal(10, text.ScrollOffset, 6);
            Assert.Equal(40, Ink(Frame(window), 0, 0, 400, 100).Left);
            Assert.Equal(0, Ink(Frame(window), 0, 0, 40, 100).Left == int.MaxValue ? 0 : 1);
            window.Close();
        });

        [Fact]
        public Task A_vertical_text_moves_up_and_fades_back_in() => UiTest.Run(() =>
        {
            var text = new FontText
            {
                Text = string.Join("\n", Enumerable.Range(0, 12).Select(i => "Line")), FontSize = 10, LineSpacing = 2, Foreground = Brushes.White,
                ScrollDirection = TextScrollDirection.Vertical, Scroll = new TextScroll(Ms(1000), 20, EndPause: Ms(1000), FadeIn: Ms(500)),
            };
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetSize(text, new Size(1, 1));
            canvas.Children.Add(text);
            ToolWindow window = Show(canvas, 200, 100);
            int top = Ink(Frame(window), 0, 0, 200, 100).Top;
            text.ScrollTime = Ms(2000);
            Assert.Equal(20, text.ScrollOffset, 6);
            Assert.Equal(top, Ink(Frame(window), 0, 0, 200, 100).Top + 0);
            text.ScrollTime = Ms(1000 + 7000 + 1000 + 250);
            Assert.Equal(0, text.ScrollOffset);
            RenderedFrame faded = Frame(window);
            text.ScrollTime = Ms(1000 + 7000 + 1000 + 600);
            RenderedFrame full = Frame(window);
            Assert.True(Enumerable.Range(0, 200).Max(x => (int)At(faded, x, top + 3).R) < Enumerable.Range(0, 200).Max(x => (int)At(full, x, top + 3).R));
            window.Close();
        });

        [Fact]
        public Task A_vertical_text_with_whole_lines_shows_no_part_line_at_the_bottom() => UiTest.Run(() =>
        {
            var text = new FontText
            {
                Text = string.Join("\n", Enumerable.Range(0, 8).Select(i => "HHHH")), FontSize = 20, LineSpacing = 1.5, Foreground = Brushes.White,
                ScrollDirection = TextScrollDirection.Vertical, Scroll = new TextScroll(Ms(1000), 20),
            };
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetSize(text, new Size(1, 1));
            canvas.Children.Add(text);
            ToolWindow window = Show(canvas, 200, 100);
            Assert.NotEqual(int.MaxValue, Ink(Frame(window), 0, 90, 200, 100).Top);
            text.ScrollWholeLines = true;
            Assert.Equal(int.MaxValue, Ink(Frame(window), 0, 90, 200, 100).Top);
            text.ScrollTime = Ms(1000 + 7400);
            Assert.Equal(8 * 30 - 90 - 2, text.ScrollOffset, 6);
            window.Close();
        });

        [Fact]
        public Task Only_the_selected_row_scrolls_and_only_when_it_is_too_wide() => UiTest.Run(() =>
        {
            var list = new TextRowList
            {
                Items = [new TextRow("IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII"), new TextRow("IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII"), new TextRow("II")],
                FontSize = 20, LineSpacing = 1.5, PrimaryColor = Colors.White, SelectedColor = Colors.White, SelectorColor = Colors.Transparent,
                SelectedIndex = 0, Marquee = new TextScroll(Ms(1000), 40, Gap: 30), HorizontalMargin = 10,
            };
            ToolWindow window = Fill(list, 200, 90);
            RenderedFrame still = Frame(window);
            int restLeft = Ink(still, 0, 0, 200, 30).Left;
            Assert.True(restLeft > 10);
            Assert.Equal(restLeft, Ink(still, 0, 30, 200, 60).Left);
            list.MarqueeTime = Ms(1500);
            Assert.Equal(20, list.MarqueeOffset, 6);
            RenderedFrame f = Frame(window);
            for (int x = 12; x < 160; x++)
                for (int y = 0; y < 30; y++)
                    Assert.Equal(At(still, x + 20, y), At(f, x, y));
            Assert.True(Ink(f, 0, 0, 200, 30).Left >= 10);
            Assert.Equal(restLeft, Ink(f, 0, 30, 200, 60).Left);
            list.SelectedIndex = 2;
            Assert.Equal(0, list.MarqueeOffset);
            window.Close();
        });

        private static int Differing(RenderedFrame a, RenderedFrame b)
        {
            int n = 0;
            for (int i = 0; i < a.Rgba.Length; i += 4)
                if (a.Rgba[i] != b.Rgba[i] || a.Rgba[i + 1] != b.Rgba[i + 1] || a.Rgba[i + 2] != b.Rgba[i + 2]) n++;
            return n;
        }
    }
}
