using System;
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
    // ImageGrid and GridGeometry against the layout and motion the consumer measured from its reference - see docs/LunaP.md §104.
    public class ImageGridTests
    {
        // The consumer's base layout: a 1152 by 640 box, 192 by 160 items, 25.6 by 24 spacing, scale 1.2.
        private static GridGeometry Base(int count = 40, Size? spacing = null, double scale = 1.2, bool inwards = false, bool fractional = false, double w = 1152, double h = 640, double iw = 192) =>
            GridGeometry.Of(new Size(w, h), new Size(iw, 160), spacing ?? new Size(25.6, 24), scale, inwards, fractional, count);

        [Fact]
        public void Columns_are_those_whose_scaled_item_fits_and_start_from_the_left()
        {
            Assert.Equal(5, Base().Columns);
            Assert.Equal(2, Base(iw: 384).Columns);
            Assert.Equal(2, GridGeometry.Of(new Size(921.6, 640), new Size(256, 160), new Size(64, 24), 1.2, false, false, 40).Columns);
            Assert.Equal(2, GridGeometry.Of(new Size(793.6, 640), new Size(256, 160), new Size(0, 24), 1.2, false, false, 40).Columns);
            Assert.Equal(2, GridGeometry.Of(new Size(665.6, 640), new Size(256, 160), new Size(64, 24), 1.2, false, false, 40).Columns);
            Assert.Equal(3, Base(spacing: new Size(128, 80)).Columns);
            Assert.Equal(4, GridGeometry.Of(new Size(1280, 706.67), new Size(317.5, 238.33), new Size(3.0, 3.33), 1.2, true, true, 12).Columns);
            Assert.Equal(3, GridGeometry.Of(new Size(1280, 706.67), new Size(317.5, 238.33), new Size(3.0, 3.33), 1.2, false, true, 12).Columns);

            GridGeometry g = Base();
            Assert.Equal(19.2, g.CellRect(0, 0).X, 6);
            Assert.Equal(16, g.CellRect(0, 0).Y, 6);
            Assert.Equal(19.2 + 217.6 * 4, g.CellRect(4, 0).X, 6);
            Assert.Equal(16 + 184, g.CellRect(5, 0).Y, 6);
            Assert.Equal(new Rect(0, 0, 192, 160), Base(inwards: true).CellRect(0, 0));
        }

        [Fact]
        public void Omitted_spacing_is_half_an_item_s_growth_and_rows_are_counted_with_room_to_scale()
        {
            Size auto = GridGeometry.Of(new Size(1152, 640), new Size(192, 160), null, 1.2, false, false, 40).Spacing;
            Assert.Equal(19.2, auto.Width, 6);
            Assert.Equal(16, auto.Height, 6);
            auto = GridGeometry.Of(new Size(1152, 640), new Size(192, 160), null, 1.5, false, false, 40).Spacing;
            Assert.Equal(48, auto.Width, 6);
            Assert.Equal(40, auto.Height, 6);
            Assert.Equal(3, Base().WholeRows);
            Assert.Equal(2, Base(spacing: new Size(128, 80)).WholeRows);
            Assert.Equal(560, Base().ClipHeight, 6);
            Assert.Equal(432, Base(spacing: new Size(128, 80)).ClipHeight, 6);
            Assert.Equal(640, Base(fractional: true).ClipHeight);
            Assert.Equal(8, Base().Rows);
        }

        [Fact]
        public void The_selection_scrolls_only_past_the_last_row_shown_and_then_stays_on_it()
        {
            GridGeometry g = Base();
            Assert.Equal(new double[] { 0, 0, 0, 1, 2, 3, 4, 5 }, Enumerable.Range(0, 8).Select(r => g.ScrollFor(r * 5)));
            Assert.Equal(3, g.ScrollFor(29));
            GridGeometry abn = GridGeometry.Of(new Size(1280, 706.67), new Size(317.5, 238.33), new Size(3.0, 3.33), 1.2, true, true, 12);
            Assert.Equal(15.0, abn.ScrollFor(8) * abn.Pitch.Height, 1);
            Assert.Equal(0, abn.ScrollFor(7));
            Assert.Equal(new RelativePoint(0, 1, RelativeUnit.Relative), abn.Anchor(8, abn.ScrollFor(8)));
        }

        [Fact]
        public void Inward_items_keep_their_outer_edge_and_a_scrolled_bottom_row_its_bottom()
        {
            GridGeometry g = Base(inwards: true);
            Assert.Equal(new RelativePoint(0, 0, RelativeUnit.Relative), g.Anchor(0, 0));
            Assert.Equal(new RelativePoint(1, 0, RelativeUnit.Relative), g.Anchor(g.Columns - 1, 0));
            Assert.Equal(new RelativePoint(0.5, 0.5, RelativeUnit.Relative), g.Anchor(g.Columns + 1, 0));
            Assert.Equal(new RelativePoint(0.5, 0.5, RelativeUnit.Relative), g.Anchor(g.Columns * 2 + 1, 0));
            Assert.Equal(new RelativePoint(0.5, 1, RelativeUnit.Relative), g.Anchor(g.Columns * 3 + 1, 1));
            Assert.Equal(RelativePoint.Center, Base().Anchor(0, 0));
        }

        // Item red is red, the others lime and blue in turn, so a red run is that one item.
        private static CarouselItem[] Flat(int n, int red = 0) => Enumerable.Range(0, n)
            .Select(i => new CarouselItem(DrawnSupport.Flat(i == red ? "grid-red" : i % 2 == 0 ? "grid-blue" : "grid-lime", 40, 40, i == red ? Colors.Red : i % 2 == 0 ? Colors.Blue : Colors.Lime), $"Item {i}")).ToArray();

        private static (ToolWindow Window, ImageGrid Grid) Grid(ImageGrid grid, double w = 1152, double h = 640)
        {
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetSize(grid, new Size(1, 1));
            canvas.Children.Add(grid);
            return (Show(canvas, w, h), grid);
        }

        // The drawn extent of a colour along a row or a column of the frame.
        private static (int From, int To) Run(RenderedFrame f, Color c, int? y = null, int? x = null)
        {
            int from = -1, to = -1, n = y is not null ? f.Width : f.Height;
            for (int i = 0; i < n; i++)
            {
                if (!Dominant(At(f, x ?? i, y ?? i), c)) continue;
                if (from < 0) from = i;
                to = i;
            }
            return (from, to);
        }

        // A pixel whose strongest channel is the colour's own, however faded.
        private static bool Dominant(Color p, Color c)
        {
            int own = c == Colors.Red ? p.R : c == Colors.Blue ? p.B : p.G, other = c == Colors.Red ? Math.Max(p.G, p.B) : c == Colors.Blue ? Math.Max(p.R, p.G) : Math.Max(p.R, p.B);
            return own > 40 && other < 20;
        }

        [Fact]
        public Task The_selected_item_is_scaled_about_its_centre_and_the_others_fade_by_true_alpha() => UiTest.Run(() =>
        {
            var (window, grid) = Grid(new ImageGrid { Items = Flat(15), SelectedIndex = 0, ItemSize = new Size(192, 160), ItemSpacing = new Size(25.6, 24), ItemScale = 1.2, ImageFit = ImageFit.Fill, UnfocusedItemOpacity = 0.3 });
            RenderedFrame f = Frame(window);
            Assert.Equal(0, Run(f, Colors.Red, y: 100).From);
            Assert.InRange(Run(f, Colors.Red, y: 100).To, 229, 230);
            Assert.Equal(0, Run(f, Colors.Red, x: 100).From);
            Assert.InRange(Run(f, Colors.Red, x: 100).To, 191, 192);
            Color unfocused = At(f, 19.2 + 217.6 + 96, 96);
            Assert.InRange(unfocused.G, 72, 80);
            Assert.Equal(0, unfocused.R);
            Assert.Equal("Item 0", grid.Items![grid.SelectedIndex].Text);
            window.Close();
        });

        [Fact]
        public Task Unfocused_dimming_and_saturation_turn_an_item_grey_at_the_dimmed_luma() => UiTest.Run(() =>
        {
            var (window, _) = Grid(new ImageGrid { Items = Flat(3), SelectedIndex = 0, ItemSize = new Size(192, 160), ItemSpacing = new Size(25.6, 24), ItemScale = 1.2, ImageFit = ImageFit.Fill, UnfocusedItemDimming = 0.5, UnfocusedItemSaturation = 0 });
            Color lime = At(Frame(window), 19.2 + 217.6 + 96, 96);
            Assert.True(Math.Abs(lime.R - lime.G) <= 2 && Math.Abs(lime.G - lime.B) <= 2, lime.ToString());
            Assert.InRange(lime.G, (int)(0.5 * 0.587 * 255) - 12, (int)(0.5 * 0.587 * 255) + 12);
            window.Close();
        });

        [Fact]
        public Task Inward_scaling_keeps_the_first_column_s_left_edge_and_the_last_column_s_right_edge() => UiTest.Run(() =>
        {
            var (window, grid) = Grid(new ImageGrid { Items = Flat(15), SelectedIndex = 0, ItemSize = new Size(192, 160), ItemSpacing = new Size(25.6, 24), ItemScale = 1.2, ScaleInwards = true, ImageFit = ImageFit.Fill });
            Assert.Equal(0, Run(Frame(window), Colors.Red, y: 100).From);
            Assert.InRange(Run(Frame(window), Colors.Red, y: 100).To, 229, 230);
            GridGeometry g = grid.GeometryFor(grid.Bounds.Size);
            grid.Items = Flat(15, red: g.Columns - 1);
            grid.SelectedIndex = g.Columns - 1;
            (int from, int to) = Run(Frame(window), Colors.Red, y: 100);
            Assert.InRange(to, g.CellRect(g.Columns - 1, 0).Right - 2, g.CellRect(g.Columns - 1, 0).Right);
            Assert.InRange(from, g.CellRect(g.Columns - 1, 0).Right - 231, g.CellRect(g.Columns - 1, 0).Right - 229);
            window.Close();
        });

        [Fact]
        public Task Rows_past_the_whole_ones_are_cut_unless_fractional_and_the_rows_scroll_by_ScrollRow() => UiTest.Run(() =>
        {
            var (window, grid) = Grid(new ImageGrid { Items = Flat(40), SelectedIndex = 0, ItemSize = new Size(192, 160), ItemSpacing = new Size(25.6, 24), ItemScale = 1.2, ImageFit = ImageFit.Fill });
            double fourth = 16 + 3 * 184 + 20;
            Assert.Equal(Colors.Black, At(Frame(window), 115, fourth));
            grid.FractionalRows = true;
            Assert.NotEqual(Colors.Black, At(Frame(window), 115, fourth));
            grid.FractionalRows = false;
            grid.ScrollRow = 1;
            RenderedFrame scrolled = Frame(window);
            Assert.True(Dominant(At(scrolled, 115, 16 + 20), Colors.Lime));
            Assert.Equal(-1, Run(scrolled, Colors.Red, y: 16 + 20).From);
            grid.SelectedIndex = 25;
            grid.ScrollRow = double.NaN;
            Assert.Equal(3, grid.DrawnScrollRow);
            window.Close();
        });

        private static int Width(RenderedFrame f, Color c) => Run(f, c, y: 100).To - Run(f, c, y: 100).From + 1;

        [Fact]
        public Task A_move_between_items_scales_one_down_and_the_other_up_on_one_progress() => UiTest.Run(() =>
        {
            var (window, grid) = Grid(new ImageGrid { Items = Flat(15), SelectedIndex = 1, FocusFrom = 0, FocusProgress = 0.5, ItemSize = new Size(192, 160), ItemSpacing = new Size(25.6, 24), ItemScale = 1.2, ImageFit = ImageFit.Fill, UnfocusedItemOpacity = 0.3 });
            Assert.InRange(Width(Frame(window), Colors.Red), 210, 212);
            grid.FocusProgress = 1;
            Assert.InRange(Width(Frame(window), Colors.Red), 191, 193);
            grid.FocusProgress = 0;
            Assert.InRange(Width(Frame(window), Colors.Red), 229, 231);
            window.Close();
        });

        [Fact]
        public Task An_item_with_no_image_is_its_text_on_a_fill_covering_the_item() => UiTest.Run(() =>
        {
            var items = new[] { new CarouselItem(null, "Alpha"), new CarouselItem(null, "Beta") };
            var (window, _) = Grid(new ImageGrid { Items = items, SelectedIndex = 1, ItemSize = new Size(192, 160), ItemSpacing = new Size(25.6, 24), ItemScale = 1.2, TextBackground = Colors.Blue, TextColor = Colors.White, FontSize = 30 });
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Blue, At(f, 19.2 + 3, 16 + 3));
            Assert.Equal(Colors.Blue, At(f, 19.2 + 189, 16 + 157));
            Assert.Contains(Enumerable.Range(40, 140), x => Near(At(f, x, 16 + 80), Colors.White, 60));
            window.Close();
        });

        [Fact]
        public Task A_selector_and_an_item_background_are_drawn_in_their_layers() => UiTest.Run(() =>
        {
            var (window, grid) = Grid(new ImageGrid
            {
                Items = Flat(2), SelectedIndex = 0, ItemSize = new Size(192, 160), ItemSpacing = new Size(25.6, 24), ItemScale = 1, ImageFit = ImageFit.Fill, ImageRelativeScale = 0.5,
                ItemBackground = Colors.Yellow, SelectorColor = Colors.Magenta, SelectorLayer = GridSelectorLayer.Middle, SelectorRelativeScale = 0.75,
            });
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Yellow, At(f, 5, 5));
            Assert.Equal(Colors.Magenta, At(f, 30, 30));
            Assert.Equal(Colors.Red, At(f, 96, 80));
            Assert.Equal(Colors.Yellow, At(f, 217.6 + 30, 30));
            grid.SelectorLayer = GridSelectorLayer.Top;
            Assert.Equal(Colors.Magenta, At(Frame(window), 96, 80));
            window.Close();
        });
    }
}
