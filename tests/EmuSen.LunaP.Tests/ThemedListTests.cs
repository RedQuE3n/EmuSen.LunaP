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
    // TextRowList and ImageCarousel at rest - see docs/LunaP.md §100.
    public class ThemedListTests
    {
        private static ToolWindow Fill(Control control, double w, double h)
        {
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetSize(control, new Size(1, 1));
            canvas.Children.Add(control);
            return Show(canvas, w, h);
        }

        private static TextRow[] Rows(int n) => Enumerable.Range(0, n).Select(i => new TextRow($"Game {i:D2}", i % 5 == 4)).ToArray();

        [Fact]
        public Task Rows_sit_at_the_pitch_and_the_selection_has_its_bar_and_background() => UiTest.Run(() =>
        {
            var list = new TextRowList
            {
                Items = Rows(5), SelectedIndex = 1, FontSize = 20, LineSpacing = 2, SelectorColor = Colors.Red, SelectorHeight = 10, SelectorOffsetY = 30,
                SelectedBackgroundColor = Colors.Blue, SelectedBackgroundMargins = new Thickness(0, 0, 0, 0), SelectedBackgroundCornerRadius = 0,
            };
            ToolWindow window = Fill(list, 300, 400);
            Assert.Equal(40, list.RowPitch);
            Assert.Equal(new Rect(0, 40, 300, 40), list.RowRect(1));
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Blue, At(f, 290, 45));
            Assert.Equal(Colors.Black, At(f, 290, 5));
            Assert.Equal(Colors.Black, At(f, 290, 85));

            list.SelectedBackgroundColor = Colors.Transparent;
            RenderedFrame bar = Frame(window);
            Assert.Equal(Colors.Red, At(bar, 290, 75));
            Assert.Equal(Colors.Black, At(bar, 290, 45));

            list.SelectedIndex = 99;
            Assert.Equal(Colors.Black, At(Frame(window), 290, 75));
            window.Close();
        });

        [Fact]
        public Task The_selected_background_reaches_past_the_list_by_its_margins() => UiTest.Run(() =>
        {
            var list = new TextRowList { Items = Rows(3), SelectedIndex = 0, FontSize = 20, SelectorColor = Colors.Transparent, SelectedBackgroundColor = Colors.Blue, SelectedBackgroundMargins = new Thickness(20, 0, 10, 0) };
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetPosition(list, new Point(0.25, 0));
            NormalizedCanvas.SetSize(list, new Size(0.5, 1));
            canvas.Children.Add(list);
            ToolWindow window = Show(canvas, 400, 200);
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Blue, At(f, 85, 15));
            Assert.Equal(Colors.Black, At(f, 75, 15));
            Assert.Equal(Colors.Blue, At(f, 305, 15));
            Assert.Equal(Colors.Black, At(f, 315, 15));
            window.Close();
        });

        [Fact]
        public Task Selected_primary_and_secondary_rows_take_their_colours() => UiTest.Run(() =>
        {
            var list = new TextRowList
            {
                Items = new[] { new TextRow("WWWWWW"), new TextRow("WWWWWW", true), new TextRow("WWWWWW") }, SelectedIndex = 2, FontSize = 30, LineSpacing = 1.5,
                PrimaryColor = Colors.Red, SecondaryColor = Colors.Lime, SelectedColor = Colors.Blue, SelectorColor = Colors.Transparent,
            };
            ToolWindow window = Fill(list, 300, 200);
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Red, Dominant(f, list.RowRect(0)));
            Assert.Equal(Colors.Lime, Dominant(f, list.RowRect(1)));
            Assert.Equal(Colors.Blue, Dominant(f, list.RowRect(2)));
            window.Close();
        });

        // Ten rows in a window of four: the selection stays on the second row until the end is reached.
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(2, 1)]
        [InlineData(5, 4)]
        [InlineData(9, 6)]
        public Task The_window_keeps_the_selection_near_its_middle_and_stops_at_the_ends(int selected, int first) => UiTest.Run(() =>
        {
            var list = new TextRowList { Items = Rows(10), SelectedIndex = selected, FontSize = 10, LineSpacing = 2 };
            ToolWindow window = Fill(list, 200, 85);
            Assert.Equal(4, list.VisibleRows);
            Assert.Equal(first, list.FirstVisible);
            window.Close();
        });

        private static CarouselItem[] Pictures(int n, string? missing = null) => Enumerable.Range(0, n)
            .Select(i => new CarouselItem(i.ToString() == missing ? "/nonexistent/lunap/c.png" : Flat($"carousel-{i}", 40, 80, Color.FromRgb((byte)(20 * i + 40), 0, 0)), $"Item {i}"))
            .ToArray();

        [Fact]
        public Task Items_are_spaced_by_length_over_MaxItemCount_about_a_centred_selection() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel { Items = Pictures(9), SelectedIndex = 4, MaxItemCount = 4, ItemScale = 1, ItemSize = new Size(100, 200) };
            ToolWindow window = Fill(carousel, 800, 400);
            Assert.Equal(200, carousel.Spacing);
            Assert.Equal(new Rect(350, 100, 100, 200), carousel.ItemRect(0, carousel.Bounds.Size));
            Assert.Equal(new Rect(550, 100, 100, 200), carousel.ItemRect(1, carousel.Bounds.Size));
            Control selected = carousel.Shown.Single(s => s.Offset == 0).Child;
            Assert.Equal(4, selected.Tag);
            Assert.Equal(new Rect(350, 100, 100, 200), selected.Bounds);
            Assert.Same(selected, carousel.Shown[^1].Child);
            window.Close();
        });

        [Fact]
        public Task Unfocused_items_fade_dim_and_desaturate_and_the_selection_scales() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel
            {
                Items = Pictures(5), SelectedIndex = 2, MaxItemCount = 5, ItemScale = 1.5, UnfocusedItemOpacity = 0.25, UnfocusedItemDimming = 0.5, UnfocusedItemSaturation = 0,
                ImageSaturation = 0.8,
            };
            ToolWindow window = Fill(carousel, 500, 200);
            var selected = (FittedImage)carousel.Shown.Single(s => s.Offset == 0).Child;
            var other = (FittedImage)carousel.Shown.First(s => s.Offset == 1).Child;
            Assert.Equal(1, selected.Opacity);
            Assert.Equal(0.25, other.Opacity);
            Assert.Equal(0.8, selected.Saturation, 6);
            Assert.Equal(0, other.Saturation);
            Assert.Equal(Colors.White, selected.Tint);
            Assert.Equal(Color.FromRgb(127, 127, 127), other.Tint);
            Assert.IsType<ScaleTransform>(selected.RenderTransform);
            Assert.Null(other.RenderTransform);
            window.Close();
        });

        [Fact]
        public Task A_wrapping_row_repeats_its_items_to_fill_the_row_and_an_unwrapping_one_stops() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel { Items = Pictures(9), SelectedIndex = 0, MaxItemCount = 5 };
            ToolWindow window = Fill(carousel, 500, 200);
            Assert.Equal(8, carousel.Shown.Single(s => s.Offset == -1).Child.Tag);
            Assert.Equal(9, carousel.Shown.Select(s => (int)s.Child.Tag!).Distinct().Count());

            carousel.Wraps = false;
            UiTest.Settle(carousel);
            Assert.DoesNotContain(carousel.Shown, s => s.Offset < 0);

            carousel.Wraps = true;
            carousel.Items = Pictures(4);
            UiTest.Settle(carousel);
            Assert.Equal(9, carousel.Shown.Count);
            Assert.Equal(3, carousel.Shown.Single(s => s.Offset == 3).Child.Tag);
            Assert.Equal(3, carousel.Shown.Single(s => s.Offset == -1).Child.Tag);
            window.Close();
        });

        [Fact]
        public Task An_item_without_an_image_shows_its_text_and_a_vertical_row_runs_down() => UiTest.Run(() =>
        {
            var carousel = new ImageCarousel { Items = Pictures(3, missing: "1"), SelectedIndex = 1, Orientation = Orientation.Vertical, MaxItemCount = 3, ItemScale = 1 };
            ToolWindow window = Fill(carousel, 200, 600);
            var text = Assert.IsType<FontText>(carousel.Shown.Single(s => s.Offset == 0).Child);
            Assert.Equal("Item 1", text.Text);
            Assert.Equal(new Rect(0, 200, 200, 200), carousel.ItemRect(0, carousel.Bounds.Size));
            Assert.Equal(new Rect(0, 400, 200, 200), carousel.ItemRect(1, carousel.Bounds.Size));
            window.Close();
        });

        private static Color Dominant(RenderedFrame f, Rect r) =>
            Enumerable.Range((int)r.X, (int)r.Width).SelectMany(x => Enumerable.Range((int)r.Y, (int)r.Height).Select(y => At(f, x, y)))
                .Where(c => c != Colors.Black).GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;
    }
}
