using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // TileStrip<T> - see docs/LunaP.md §95: one row of tiles scrolling sideways, virtualised, with TileGrid's contract.
    public class TileStripTests
    {
        private sealed class Moment
        {
            public Moment(int id) => Id = id;
            public int Id { get; }
            public override string ToString() => $"Moment {Id:D4}";
        }

        private static Moment[] Moments(int count) => Enumerable.Range(0, count).Select(i => new Moment(i)).ToArray();

        private static TileStrip<Moment> Strip(int count, out Moment[] moments)
        {
            moments = Moments(count);
            var strip = new TileStrip<Moment> { Key = m => m.Id };
            strip.Refresh(moments);
            return strip;
        }

        // 800 wide: at the default 160-pixel tiles and 12 spacing, a little over four tiles in view.
        private static ToolWindow Show(Control content, double width = 800, double height = 300)
        {
            var window = new ToolWindow { Width = width, Height = height, Content = content };
            window.Show();
            Settle(window);
            return window;
        }

        private static void Settle(Window window)
        {
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
            Dispatcher.UIThread.RunJobs();
        }

        private static List<TileGridItem> Containers(Control strip) => strip.GetVisualDescendants().OfType<TileGridItem>().ToList();

        private static List<TileGridItem> Showing(Control strip) => Containers(strip).Where(c => c.IsVisible).ToList();

        private static string Shown(TileGridItem item) => ((TextBlock)item.Content!).Text ?? "";

        private static TileGridItem TileShowing(Control strip, string label) => Showing(strip).Single(c => Shown(c) == label);

        private static void Press(Window window, Key key, PhysicalKey physical)
        {
            window.KeyPress(key, RawInputModifiers.None, physical, string.Empty);
            window.KeyRelease(key, RawInputModifiers.None, physical, string.Empty);
            Settle(window);
        }

        // The bound: the tiles in view plus two either side, of five thousand.
        [Fact]
        public Task Five_thousand_items_realise_only_the_tiles_in_view() => UiTest.Run(() =>
        {
            TileStrip<Moment> strip = Strip(5000, out _);
            ToolWindow window = Show(strip);

            int inView = (int)Math.Ceiling(strip.Bounds.Width / (strip.TileWidth + strip.Spacing)) + 1;
            Assert.InRange(Containers(strip).Count, 1, inView + 2 * 2);
            Assert.Equal("Moment 0000", Shown(Showing(strip).OrderBy(c => c.Bounds.X).First()));
            Assert.True(Showing(strip).All(c => Math.Abs(c.Bounds.Y - strip.Spacing) < 0.5), "every tile sits on the one row");

            window.Close();
        });

        [Fact]
        public Task Scrolling_reuses_containers_and_rebinds_them() => UiTest.Run(() =>
        {
            TileStrip<Moment> strip = Strip(5000, out _);
            int binds = 0;
            Action<Control, Moment> original = strip.BindTile;
            strip.BindTile = (tile, moment) => { binds++; original(tile, moment); };

            ToolWindow window = Show(strip);
            HashSet<TileGridItem> before = Containers(strip).ToHashSet();
            int bindsAtStart = binds;

            ScrollViewer scroll = strip.FindNamed<ScrollViewer>("PART_Scroll");
            scroll.Offset = new Vector(1000 * 172, 0);
            Settle(window);

            HashSet<TileGridItem> after = Containers(strip).ToHashSet();
            Assert.Superset(before, after);
            // At the start there is no buffer to the left; in the middle there are two, and the tile the view's left edge cuts.
            Assert.InRange(after.Count, before.Count, before.Count + 3);

            // 172,000 less the leading spacing is 999.9 pitches, so tile 999 is the first counted in view and two buffer tiles lie before it.
            int[] shown = Showing(strip).Select(c => int.Parse(Shown(c)[7..])).OrderBy(n => n).ToArray();
            Assert.Equal(997, shown.First());
            Assert.True(binds - bindsAtStart <= after.Count, $"scrolling once bound {binds - bindsAtStart} tiles with {after.Count} containers");

            window.Close();
        });

        [Fact]
        public Task Select_and_refresh_never_raise_chose_and_refresh_keeps_the_selection_by_key() => UiTest.Run(() =>
        {
            TileStrip<Moment> strip = Strip(10, out Moment[] moments);
            var chosen = new List<int>();
            strip.Chose += m => chosen.Add(m.Id);
            ToolWindow window = Show(strip);

            strip.Select(moments[3]);
            strip.Refresh(Moments(10));
            Settle(window);

            Assert.Empty(chosen);
            Assert.Equal(3, strip.Selected!.Id);
            Assert.NotSame(moments[3], strip.Selected);
            Assert.Equal(3, strip.SelectedIndex);

            strip.Refresh(Moments(3));
            Assert.Null(strip.Selected);
            Assert.Equal(-1, strip.SelectedIndex);
            Assert.Empty(chosen);

            window.Close();
        });

        // Centred where the extent allows, so a step shows the neighbours on both sides - §95.1.
        [Fact]
        public Task Select_centres_the_tile_and_rings_it() => UiTest.Run(() =>
        {
            TileStrip<Moment> strip = Strip(5000, out Moment[] moments);
            ToolWindow window = Show(strip);

            strip.Select(moments[4000]);
            Settle(window);

            TileGridItem tile = TileShowing(strip, "Moment 4000");
            Assert.True(tile.IsSelected);
            Assert.True(tile.FindNamed<Border>("PART_Ring").IsVisible);
            ScrollViewer scroll = strip.FindNamed<ScrollViewer>("PART_Scroll");
            Assert.Equal(tile.Bounds.Center.X - scroll.Viewport.Width / 2, scroll.Offset.X, 1.0);

            // At the end the strip cannot centre it, and shows the end instead.
            strip.Select(moments[^1]);
            Settle(window);
            Assert.Equal(scroll.Extent.Width - scroll.Viewport.Width, scroll.Offset.X, 1.0);
            Assert.True(TileShowing(strip, "Moment 4999").IsSelected);

            window.Close();
        });

        [Fact]
        public Task The_ring_is_painted_around_the_selected_tile() => UiTest.Run(() =>
        {
            TileStrip<Moment> strip = Strip(4, out Moment[] moments);
            ToolWindow window = Show(strip);
            strip.Select(moments[1]);
            Settle(window);

            TileGridItem tile = TileShowing(strip, "Moment 0001");
            Point corner = tile.TranslatePoint(new Point(0, 0), window)!.Value;
            Application.Current!.TryGetResource("LunaAccentColor", Avalonia.Styling.ThemeVariant.Dark, out object? accent);
            Avalonia.Media.Color expected = (Avalonia.Media.Color)accent!;

            Avalonia.Media.Imaging.WriteableBitmap frame = window.CaptureRenderedFrame()!;
            using Avalonia.Platform.ILockedFramebuffer pixels = frame.Lock();
            int x = (int)corner.X - 4, y = (int)(corner.Y + tile.Bounds.Height / 2);
            byte[] bgra = new byte[4];
            System.Runtime.InteropServices.Marshal.Copy(pixels.Address + y * pixels.RowBytes + x * 4, bgra, 0, 4);

            bool bgr = pixels.Format == Avalonia.Platform.PixelFormat.Bgra8888;
            byte r = bgr ? bgra[2] : bgra[0], g = bgra[1], b = bgr ? bgra[0] : bgra[2];
            Assert.True(Math.Abs(r - expected.R) < 12 && Math.Abs(g - expected.G) < 12 && Math.Abs(b - expected.B) < 12,
                $"the ring's stroke at ({x},{y}) is #{r:X2}{g:X2}{b:X2}, not the accent #{expected.R:X2}{expected.G:X2}{expected.B:X2}");

            window.Close();
        });

        // One axis and no wrap: Left, Right, Home, End, and a page of the whole tiles in view; each move a Chose.
        [Fact]
        public Task Keys_step_page_and_go_to_the_ends_without_wrapping() => UiTest.Run(() =>
        {
            TileStrip<Moment> strip = Strip(100, out Moment[] moments);
            var chosen = new List<int>();
            strip.Chose += m => chosen.Add(m.Id);
            ToolWindow window = Show(strip);
            strip.Focus();

            Press(window, Key.Right, PhysicalKey.ArrowRight);
            Assert.Equal(0, strip.Selected!.Id);
            Press(window, Key.Right, PhysicalKey.ArrowRight);
            Press(window, Key.Left, PhysicalKey.ArrowLeft);
            Press(window, Key.Left, PhysicalKey.ArrowLeft);
            Assert.Equal(0, strip.Selected!.Id);

            Press(window, Key.PageDown, PhysicalKey.PageDown);
            int page = (int)Math.Floor((strip.FindNamed<ScrollViewer>("PART_Scroll").Viewport.Width - strip.Spacing) / (strip.TileWidth + strip.Spacing));
            Assert.Equal(page, strip.Selected!.Id);
            Press(window, Key.End, PhysicalKey.End);
            Assert.Equal(99, strip.Selected!.Id);
            Press(window, Key.Right, PhysicalKey.ArrowRight);
            Press(window, Key.Home, PhysicalKey.Home);
            Assert.Equal(new[] { 0, 1, 0, page, 99, 0 }, chosen);

            window.Close();
        });

        // A host's own input: Move steps as a user does, clamps at the ends, and says whether it moved.
        [Fact]
        public Task Move_steps_clamps_and_raises_chose_as_a_user_does() => UiTest.Run(() =>
        {
            TileStrip<Moment> strip = Strip(10, out Moment[] moments);
            var chosen = new List<int>();
            strip.Chose += m => chosen.Add(m.Id);

            Assert.True(strip.Move(-1));
            Assert.Equal(9, strip.Selected!.Id);
            Assert.False(strip.Move(1));
            Assert.True(strip.Move(-4));
            Assert.True(strip.Move(int.MinValue / 2));
            Assert.False(strip.Move(-1));
            Assert.Equal(new[] { 9, 5, 0 }, chosen);

            strip.Select(null);
            Assert.True(strip.Move(1));
            Assert.Equal(0, strip.Selected!.Id);
            Assert.False(new TileStrip<Moment>().Move(1));
        });

        [Fact]
        public Task Pressing_a_tile_chooses_it_and_double_tap_or_enter_activates() => UiTest.Run(() =>
        {
            TileStrip<Moment> strip = Strip(8, out Moment[] moments);
            var chosen = new List<int>();
            var opened = new List<int>();
            strip.Chose += m => chosen.Add(m.Id);
            strip.Activated += m => opened.Add(m.Id);
            ToolWindow window = Show(strip);

            TileGridItem tile = TileShowing(strip, "Moment 0002");
            Point at = tile.TranslatePoint(new Point(tile.Bounds.Width / 2, tile.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Settle(window);
            Assert.Same(moments[2], strip.Selected);
            Assert.True(strip.IsFocused);

            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Settle(window);
            Assert.Equal(new[] { 2 }, chosen);
            Assert.Equal(new[] { 2 }, opened);

            Press(window, Key.Enter, PhysicalKey.Enter);
            Assert.Equal(new[] { 2, 2 }, opened);

            window.Close();
        });

        [Fact]
        public Task It_is_a_named_list_of_named_items_that_reports_its_selection() => UiTest.Run(() =>
        {
            TileStrip<Moment> strip = Strip(6, out Moment[] moments);
            AutomationProperties.SetName(strip, "History");
            var chosen = new List<int>();
            strip.Chose += m => chosen.Add(m.Id);
            ToolWindow window = Show(strip);
            strip.Select(moments[2]);
            Settle(window);

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(strip);
            Assert.Equal(AutomationControlType.List, peer.GetAutomationControlType());
            Assert.Equal("History", peer.GetName());
            var selection = (ISelectionProvider)peer;
            AutomationPeer item = Assert.Single(selection.GetSelection());
            Assert.Equal("Moment 0002", item.GetName());

            AutomationPeer other = ControlAutomationPeer.CreatePeerForElement(TileShowing(strip, "Moment 0004"));
            ((ISelectionItemProvider)other).Select();
            Assert.Same(moments[4], strip.Selected);
            Assert.Equal(new[] { 4 }, chosen);

            window.Close();
        });

        [Fact]
        public Task Tiles_come_from_the_host_factory_and_binder() => UiTest.Run(() =>
        {
            var strip = new TileStrip<Moment>
            {
                CreateTile = () => new Border { Child = new TextBlock() },
                BindTile = (tile, m) => ((TextBlock)((Border)tile).Child!).Text = $"#{m.Id}",
            };
            strip.Refresh(Moments(3));
            ToolWindow window = Show(strip);

            string[] shown = Containers(strip).Where(c => c.IsVisible).OrderBy(c => c.Bounds.X)
                .Select(c => ((TextBlock)((Border)c.Content!).Child!).Text!).ToArray();
            Assert.Equal(new[] { "#0", "#1", "#2" }, shown);

            window.Close();
        });
    }
}
