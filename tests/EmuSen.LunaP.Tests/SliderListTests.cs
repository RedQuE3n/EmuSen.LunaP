using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // SliderList - see docs/LunaP.md §97. A thousand numbers under headings, only the rows in view built, each value kept by its item.
    public class SliderListTests
    {
        private static List<object> Numbers(int count, int headingEvery = 100)
        {
            var items = new List<object>();
            for (int i = 0; i < count; i++)
            {
                if (i % headingEvery == 0) items.Add($"Section {i / headingEvery}");
                items.Add(new SliderItem($"Number {i}", 0, 1, 0.05, 0.5, 0.5) { Name = $"Number{i}", Tag = i });
            }
            return items;
        }

        private static (ToolWindow Window, SliderList List) Show(IEnumerable<object> items, double height = 400)
        {
            var list = new SliderList { ItemsSource = items.ToList() };
            var window = new ToolWindow { Width = 420, Height = height, Content = list };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
            return (window, list);
        }

        private static Slider SliderOf(SliderRow row) => row.GetVisualDescendants().OfType<Slider>().Single();

        private static void Key(Control target, Key key) =>
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

        private static ScrollViewer Scroll(SliderList list) => list.GetVisualDescendants().OfType<ScrollViewer>().First();

        [Fact]
        public Task A_thousand_numbers_build_only_the_rows_in_view_and_their_headings() => UiTest.Run(() =>
        {
            var (window, list) = Show(Numbers(1000));
            int rows = list.Realized.Count();
            Assert.InRange(rows, 1, 30);
            Assert.Equal(1000, list.Sliders.Count());
            Assert.Equal("Section 0", list.GetVisualDescendants().OfType<SectionHeader>().First().Text);
            Assert.Equal(new[] { "Number0", "Number1" }, list.Realized.Take(2).Select(r => r.Name));

            // Far down and back: the rows realised stay about one view's worth.
            Scroll(list).Offset = new Vector(0, Scroll(list).Extent.Height / 2);
            UiTest.Capture(window);
            Assert.InRange(list.Realized.Count(), 1, 30);
            Assert.DoesNotContain(list.Realized, r => r.Name == "Number0");
            window.Close();
        });

        [Fact]
        public Task A_person_s_move_raises_ValueChanged_with_the_item_and_a_value_set_in_code_raises_nothing() => UiTest.Run(() =>
        {
            var (window, list) = Show(Numbers(50));
            var changes = new List<(object? Tag, double Value)>();
            list.ValueChanged += item => changes.Add((item.Tag, item.Value));
            SliderItem third = list.Sliders.ElementAt(2);
            SliderRow row = list.RowFor(third)!;

            Key(SliderOf(row), Avalonia.Input.Key.Right);
            Assert.Equal(new[] { ((object?)2, 0.55) }, changes.Select(c => (c.Tag, System.Math.Round(c.Value, 6))));
            Assert.Equal(0.55, third.Value, 6);
            Assert.False(third.IsDefault);

            third.Value = 0.2;
            Assert.Equal(0.2, row.Value, 6);
            Assert.Single(changes);

            row.GetVisualDescendants().OfType<Button>().Single(b => b.FindAncestorOfType<Slider>() is null).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(0.5, third.Value, 6);
            Assert.True(third.IsDefault);
            Assert.Equal(2, changes.Count);
            window.Close();
        });

        // A value set on an item no row shows is what its row shows once it is scrolled to.
        [Fact]
        public Task An_item_scrolled_away_keeps_its_value_and_Reveal_brings_its_row() => UiTest.Run(() =>
        {
            var (window, list) = Show(Numbers(1000));
            SliderItem last = list.Sliders.Last();
            Assert.Null(list.RowFor(last));
            last.Value = 0.9;

            SliderRow row = list.Reveal(last)!;
            Assert.Equal("Number999", row.Name);
            Assert.Equal("Number 999", row.Label);
            Assert.Equal(0.9, row.Value, 6);
            Assert.Null(list.RowFor(list.Sliders.First()));
            Assert.Null(list.Reveal(new SliderItem("Not here", 0, 1, 0.1, 0, 0)));
            window.Close();
        });

        // The focused row outlives a scroll that would otherwise recycle its container - see docs/LunaP.md §97.3.
        [Fact]
        public Task A_focused_slider_keeps_the_focus_when_its_row_is_scrolled_away() => UiTest.Run(() =>
        {
            var (window, list) = Show(Numbers(1000));
            SliderItem first = list.Sliders.First();
            Slider slider = SliderOf(list.RowFor(first)!);
            Assert.True(slider.Focus(NavigationMethod.Directional));

            Scroll(list).Offset = new Vector(0, Scroll(list).Extent.Height / 2);
            UiTest.Capture(window);
            Assert.Same(slider, window.FocusManager!.GetFocusedElement());
            Key(slider, Avalonia.Input.Key.Right);
            Assert.Equal(0.55, first.Value, 6);
            window.Close();
        });

        [Fact]
        public Task New_items_start_at_the_top() => UiTest.Run(() =>
        {
            var (window, list) = Show(Numbers(1000));
            Scroll(list).Offset = new Vector(0, 5000);
            UiTest.Capture(window);
            Assert.True(Scroll(list).Offset.Y > 0);

            list.ItemsSource = Numbers(200);
            UiTest.Capture(window);
            Assert.Equal(0, Scroll(list).Offset.Y);
            Assert.Equal("Number0", list.Realized.First().Name);
            window.Close();
        });

        // The bar a thousand rows would make a hairline stays wide, its thumb 40 points at least, as GroupedList's does (§96).
        [Fact]
        public Task A_long_list_keeps_a_scroll_bar_that_can_be_seen_and_dragged() => UiTest.Run(() =>
        {
            var (window, list) = Show(Numbers(3000));
            ScrollViewer scroll = Scroll(list);
            Assert.False(scroll.AllowAutoHide);
            Assert.True(scroll.Extent.Height > 100 * scroll.Viewport.Height, $"extent {scroll.Extent.Height}, viewport {scroll.Viewport.Height}");
            Thumb thumb = list.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().First(b => b.Orientation == Avalonia.Layout.Orientation.Vertical)
                .GetVisualDescendants().OfType<Thumb>().Single();
            Assert.True(thumb.Bounds.Height >= 40, $"thumb {thumb.Bounds.Height}");
            window.Close();
        });

        [Fact]
        public Task Asking_for_a_row_before_the_list_is_shown_answers_null_and_holds_nothing() => UiTest.Run(() =>
        {
            var items = Numbers(20);
            var list = new SliderList { ItemsSource = items };
            SliderItem fifth = list.Sliders.ElementAt(4);
            Assert.Null(list.RowFor(fifth));
            Assert.Null(list.Reveal(fifth));
            fifth.Value = 0.8;

            var window = new ToolWindow { Width = 420, Height = 400, Content = list };
            window.Show();
            UiTest.Capture(window);
            Assert.Equal(0.8, list.Reveal(fifth)!.Value, 6);
            window.Close();
        });
    }
}
