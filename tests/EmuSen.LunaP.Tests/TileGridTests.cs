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
    // TileGrid<T> - see docs/LunaP.md §88.2. Selection by key, the Chose contract, two-dimensional
    // keyboard navigation measured against Columns, and the virtualisation bound that is the reason
    // the control exists.
    public class TileGridTests
    {
        private sealed class Game
        {
            public Game(int id, string title)
            {
                Id = id;
                Title = title;
            }

            public int Id { get; }
            public string Title { get; }
            public override string ToString() => Title;
        }

        private static Game[] Library(int count) =>
            Enumerable.Range(0, count).Select(i => new Game(i, $"Game {i:D4}")).ToArray();

        private static TileGrid<Game> Grid(int count, out Game[] games)
        {
            games = Library(count);
            var grid = new TileGrid<Game> { Key = g => g.Id };
            grid.Refresh(games);
            return grid;
        }

        // 800 x 600, which at the default 160 x 200 tiles and 20 spacing is four columns and a little
        // under three rows - small enough that a realised count of thousands is unmistakable.
        private static ToolWindow Show(Control content, double width = 800, double height = 600)
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

        private static List<TileGridItem> Containers(Control grid) => grid.GetVisualDescendants().OfType<TileGridItem>().ToList();

        private static List<TileGridItem> Showing(Control grid) => Containers(grid).Where(c => c.IsVisible).ToList();

        private static string Shown(TileGridItem item) => ((TextBlock)item.Content!).Text ?? "";

        private static void Press(Window window, Key key, PhysicalKey physical)
        {
            window.KeyPress(key, RawInputModifiers.None, physical, string.Empty);
            window.KeyRelease(key, RawInputModifiers.None, physical, string.Empty);
            Settle(window);
        }

        private static Point CentreOf(Visual visual, Window window) =>
            visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

        private static TileGridItem TileShowing(Control grid, string title) => Showing(grid).Single(c => Shown(c) == title);

        // THE BOUND. Five thousand items, and the realised count is the rows on screen plus one
        // buffer row either side, times the columns - measured as 16 here, where a ListBox over a
        // WrapPanel realises all 5,000 (§88.2). Asserted as a formula over Columns rather than as 16,
        // so a change to the tile size or the window does not turn a correct control red.
        [Fact]
        public Task Five_thousand_items_realise_only_the_rows_in_view() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(5000, out _);
            ToolWindow window = Show(grid);

            int rowsInView = (int)Math.Ceiling(grid.Bounds.Height / (grid.TileHeight + grid.Spacing)) + 1;
            int bound = (rowsInView + 2 * 1) * grid.Columns;

            Assert.Equal(4, grid.Columns);
            Assert.InRange(Containers(grid).Count, 1, bound);
            Assert.Equal("Game 0000", Shown(Showing(grid).OrderBy(c => c.Bounds.Y).ThenBy(c => c.Bounds.X).First()));

            window.Close();
        });

        // Scrolling reuses the containers it has rather than making more, and what they show follows
        // the scroll. The same instances before and after is the recycling claim stated exactly.
        [Fact]
        public Task Scrolling_reuses_containers_and_rebinds_them() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(5000, out _);
            int binds = 0;
            Action<Control, Game> original = grid.BindTile;
            grid.BindTile = (tile, game) =>
            {
                binds++;
                original(tile, game);
            };

            ToolWindow window = Show(grid);
            HashSet<TileGridItem> before = Containers(grid).ToHashSet();
            int bindsAtStart = binds;

            ScrollViewer scroll = grid.FindNamed<ScrollViewer>("PART_Scroll");
            scroll.Offset = new Vector(0, 600 * 220);
            Settle(window);

            // Every container from the top is still here, reused. A few more may exist: at the top
            // there is no buffer row above, and scrolled into the middle there is, so the pool grows
            // by at most that one row and no further.
            HashSet<TileGridItem> after = Containers(grid).ToHashSet();
            Assert.Superset(before, after);
            Assert.InRange(after.Count, before.Count, before.Count + grid.Columns * 2);

            // An offset of 132,000 is inside row 599 (row 600 starts at 20 + 600 x 220), and the first
            // realised row is the buffer row above that: 598, whose first item is 598 x 4.
            int[] shown = Showing(grid).Select(c => int.Parse(Shown(c)[5..])).OrderBy(n => n).ToArray();
            Assert.Equal(598 * grid.Columns, shown.First());
            Assert.True(binds - bindsAtStart <= after.Count,
                $"Scrolling once bound {binds - bindsAtStart} tiles with only {after.Count} containers.");

            window.Close();
        });

        // Space left over across a row is shared between every gap, edges included, and no gap is
        // below Spacing.
        [Fact]
        public Task Leftover_width_is_shared_evenly_between_the_columns() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(12, out _);
            ToolWindow window = Show(grid);

            double[] xs = Showing(grid).Select(c => c.Bounds.X).Distinct().OrderBy(x => x).ToArray();
            double width = Containers(grid)[0].GetVisualParent()!.Bounds.Width;
            double gap = (width - grid.Columns * grid.TileWidth) / (grid.Columns + 1);

            Assert.Equal(grid.Columns, xs.Length);
            Assert.True(gap >= grid.Spacing, $"gap {gap} is below Spacing");
            for (int i = 0; i < xs.Length; i++) Assert.Equal(gap + i * (grid.TileWidth + gap), xs[i], 3);

            window.Close();
        });

        [Fact]
        public Task Select_and_refresh_never_raise_chose() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(20, out Game[] games);
            int chose = 0;
            grid.Chose += _ => chose++;
            ToolWindow window = Show(grid);

            grid.Select(games[5]);
            Assert.Same(games[5], grid.Selected);

            // Rebuilt from scratch, so reference identity would lose it; Key keeps it.
            Game[] rebuilt = Library(20);
            grid.Refresh(rebuilt);
            Settle(window);

            Assert.Same(rebuilt[5], grid.Selected);
            Assert.True(TileShowing(grid, "Game 0005").IsSelected);
            Assert.Equal(0, chose);

            // Gone from the list: nothing selected, and still no Chose.
            grid.Refresh(rebuilt.Where(g => g.Id != 5).ToArray());
            Settle(window);
            Assert.Null(grid.Selected);
            Assert.DoesNotContain(Containers(grid), c => c.IsSelected);
            Assert.Equal(0, chose);

            window.Close();
        });

        [Fact]
        public Task Select_scrolls_the_tile_into_view_and_rings_it() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(5000, out Game[] games);
            ToolWindow window = Show(grid);

            grid.Select(games[4000]);
            Settle(window);

            TileGridItem tile = TileShowing(grid, "Game 4000");
            Assert.True(tile.IsSelected);
            Assert.Contains(TileGridItem.SelectedClass, tile.Classes);
            Assert.True(tile.FindNamed<Border>("PART_Ring").IsVisible);

            // Inside the viewport, not merely realised in the buffer row.
            ScrollViewer scroll = grid.FindNamed<ScrollViewer>("PART_Scroll");
            Assert.True(tile.Bounds.Top >= scroll.Offset.Y && tile.Bounds.Bottom <= scroll.Offset.Y + scroll.Viewport.Height,
                $"tile {tile.Bounds} is outside the viewport at {scroll.Offset.Y}+{scroll.Viewport.Height}");

            window.Close();
        });

        // The ring is OpenEmu's: outset 6, stroke 4, radius 3, in the accent (§88.2).
        [Fact]
        public Task The_ring_is_outset_six_pixels_in_the_accent() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(4, out Game[] games);
            ToolWindow window = Show(grid);
            grid.Select(games[1]);
            Settle(window);

            TileGridItem tile = TileShowing(grid, "Game 0001");
            Border ring = tile.FindNamed<Border>("PART_Ring");

            Assert.Equal(new Rect(-6, -6, grid.TileWidth + 12, grid.TileHeight + 12), ring.Bounds);
            Assert.Equal(new Thickness(4), ring.BorderThickness);
            Assert.Equal(new CornerRadius(3), ring.CornerRadius);
            Application.Current!.TryGetResource("LunaAccent", Avalonia.Styling.ThemeVariant.Dark, out object? accent);
            Assert.Equal(((Avalonia.Media.ISolidColorBrush)accent!).Color, ((Avalonia.Media.ISolidColorBrush)ring.BorderBrush!).Color);

            window.Close();
        });

        // THE PROPERTY TEST ABOVE PASSED WHILE NO RING WAS EVER DRAWN, which is why this one reads
        // pixels. TileGridItem clips to its bounds, and a 4 px stroke outset by 6 lies wholly outside
        // them, so the ring had the right size, colour and visibility and painted nothing at all. A
        // consumer found it in its first screenshot (§88.11). The sample is the middle of the stroke's
        // left edge, 4 px outside the tile.
        [Fact]
        public Task The_ring_is_painted_outside_the_tile_it_surrounds() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(4, out Game[] games);
            ToolWindow window = Show(grid);
            grid.Select(games[1]);
            Settle(window);

            TileGridItem tile = TileShowing(grid, "Game 0001");
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
                $"the ring's stroke at ({x},{y}) is #{r:X2}{g:X2}{b:X2} ({pixels.Format}), not the accent #{expected.R:X2}{expected.G:X2}{expected.B:X2}");

            window.Close();
        });

        // Two-dimensional, and every step is a user's change: Chose each time.
        [Fact]
        public Task Arrow_keys_move_by_one_and_by_a_row_of_columns() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(10, out Game[] games);
            var chosen = new List<int>();
            grid.Chose += g => chosen.Add(g.Id);
            ToolWindow window = Show(grid);
            grid.Focus();
            int columns = grid.Columns;
            Assert.Equal(4, columns);

            Press(window, Key.Right, PhysicalKey.ArrowRight);   // nothing selected: the first tile
            Press(window, Key.Right, PhysicalKey.ArrowRight);   // 1
            Press(window, Key.Down, PhysicalKey.ArrowDown);     // 1 + columns
            Press(window, Key.Down, PhysicalKey.ArrowDown);     // 9, the last row
            Press(window, Key.Down, PhysicalKey.ArrowDown);     // nowhere below: stays, no Chose
            Press(window, Key.Up, PhysicalKey.ArrowUp);         // 5
            Press(window, Key.Left, PhysicalKey.ArrowLeft);     // 4
            Press(window, Key.Left, PhysicalKey.ArrowLeft);     // 3, wrapping to the row above
            Press(window, Key.End, PhysicalKey.End);            // 9
            Press(window, Key.Home, PhysicalKey.Home);          // 0

            Assert.Equal(new[] { 0, 1, 1 + columns, 1 + 2 * columns, 1 + columns, columns, columns - 1, 9, 0 }, chosen);
            Assert.Same(games[0], grid.Selected);

            window.Close();
        });

        // Down from a full row onto a shorter one lands on the last tile rather than refusing.
        [Fact]
        public Task Down_onto_a_short_last_row_goes_to_the_last_tile() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(6, out Game[] games);
            ToolWindow window = Show(grid);
            grid.Focus();
            grid.Select(games[3]);

            Press(window, Key.Down, PhysicalKey.ArrowDown);

            Assert.Same(games[5], grid.Selected);
            window.Close();
        });

        [Fact]
        public Task Page_down_moves_a_viewport_of_rows() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(400, out Game[] games);
            ToolWindow window = Show(grid);
            grid.Focus();
            grid.Select(games[0]);
            Settle(window);

            ScrollViewer scroll = grid.FindNamed<ScrollViewer>("PART_Scroll");
            int rows = Math.Max(1, (int)Math.Floor(scroll.Viewport.Height / (grid.TileHeight + grid.Spacing)));

            Press(window, Key.PageDown, PhysicalKey.PageDown);
            Assert.Same(games[rows * grid.Columns], grid.Selected);
            Assert.True(TileShowing(grid, games[rows * grid.Columns].Title).IsSelected);

            Press(window, Key.PageUp, PhysicalKey.PageUp);
            Assert.Same(games[0], grid.Selected);

            window.Close();
        });

        [Fact]
        public Task Pressing_a_tile_chooses_it_and_double_tap_or_enter_activates() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(8, out Game[] games);
            var chosen = new List<string>();
            var opened = new List<string>();
            grid.Chose += g => chosen.Add(g.Title);
            grid.Activated += g => opened.Add(g.Title);
            ToolWindow window = Show(grid);

            Point at = CentreOf(TileShowing(grid, "Game 0002"), window);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Settle(window);

            Assert.Same(games[2], grid.Selected);
            Assert.Equal(new[] { "Game 0002" }, chosen);
            Assert.True(grid.IsFocused);

            // A second press on the same tile is a double-tap and no change of selection.
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            Settle(window);

            Assert.Equal(new[] { "Game 0002" }, chosen);
            Assert.Equal(new[] { "Game 0002" }, opened);

            Press(window, Key.Enter, PhysicalKey.Enter);
            Assert.Equal(new[] { "Game 0002", "Game 0002" }, opened);

            window.Close();
        });

        // The right button selects first, so a context menu opened by it reads the tile clicked.
        [Fact]
        public Task A_right_press_selects_before_the_context_menu_opens() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(8, out Game[] games);
            Game? seenByMenu = null;
            var menu = new ContextMenu { ItemsSource = new[] { "Rename" } };
            menu.Opening += (_, _) => seenByMenu = grid.Selected;
            grid.ContextMenu = menu;
            ToolWindow window = Show(grid);

            Point at = CentreOf(TileShowing(grid, "Game 0006"), window);
            window.MouseDown(at, MouseButton.Right);
            window.MouseUp(at, MouseButton.Right);
            Settle(window);

            Assert.Same(games[6], grid.Selected);
            Assert.Same(games[6], seenByMenu);

            menu.Close();
            window.Close();
        });

        // A press in the gap between tiles changes nothing - Chose could not report a cleared one.
        [Fact]
        public Task A_press_between_tiles_keeps_the_selection() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(8, out Game[] games);
            int chose = 0;
            grid.Chose += _ => chose++;
            ToolWindow window = Show(grid);
            grid.Select(games[1]);

            window.MouseDown(grid.TranslatePoint(new Point(5, 5), window)!.Value, MouseButton.Left);
            Settle(window);

            Assert.Same(games[1], grid.Selected);
            Assert.Equal(0, chose);
            window.Close();
        });

        [Fact]
        public Task It_is_a_named_list_of_named_items_that_reports_its_selection() => UiTest.Run(() =>
        {
            TileGrid<Game> grid = Grid(8, out Game[] games);
            grid.Label = g => g.Title.ToUpperInvariant();
            AutomationProperties.SetName(grid, "Library");
            int chose = 0;
            grid.Chose += _ => chose++;
            ToolWindow window = Show(grid);
            grid.Select(games[3]);
            Settle(window);

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(grid);
            Assert.Equal(AutomationControlType.List, peer.GetAutomationControlType());
            Assert.Equal("Library", peer.GetName());
            Assert.True(peer.IsControlElement());

            List<AutomationPeer> items = Descendants(peer).Where(p => p.GetAutomationControlType() == AutomationControlType.ListItem).ToList();
            Assert.Equal(games.Select(g => g.Title.ToUpperInvariant()), items.Select(p => p.GetName()).OrderBy(n => n));

            AutomationPeer selected = Assert.Single(peer.GetProvider<ISelectionProvider>()!.GetSelection());
            Assert.Equal("GAME 0003", selected.GetName());
            Assert.True(selected.GetProvider<ISelectionItemProvider>()!.IsSelected);

            // A reader's Select is a person choosing, so it raises Chose.
            items.Single(p => p.GetName() == "GAME 0006").GetProvider<ISelectionItemProvider>()!.Select();
            Assert.Same(games[6], grid.Selected);
            Assert.Equal(1, chose);

            window.Close();
        });

        // Refresh re-binds every realised tile even when handed the SAME objects, because a model
        // edited in place - a renamed game, a new cover - is the ordinary reason to refresh, and a
        // container that skipped BindTile because its item "had not changed" would show the old
        // title. The first draft of this suite could not see that: every Refresh it made passed new
        // objects, which re-bind anyway. §88.8 records the sabotage that found the hole.
        [Fact]
        public Task Refresh_rebinds_tiles_whose_model_changed_in_place() => UiTest.Run(() =>
        {
            var names = new Dictionary<int, string> { [0] = "Before", [1] = "Other" };
            var ids = new[] { new Game(0, "x"), new Game(1, "y") };
            var grid = new TileGrid<Game> { Label = g => names[g.Id] };
            grid.Refresh(ids);
            ToolWindow window = Show(grid);
            Assert.Contains(Showing(grid), c => Shown(c) == "Before");

            names[0] = "After";
            grid.Refresh(ids);
            Settle(window);

            Assert.Contains(Showing(grid), c => Shown(c) == "After");
            Assert.DoesNotContain(Showing(grid), c => Shown(c) == "Before");
            window.Close();
        });

        [Fact]
        public Task Tiles_come_from_the_host_factory_and_binder() => UiTest.Run(() =>
        {
            var grid = new TileGrid<Game>
            {
                CreateTile = () => new Border { Child = new TextBlock() },
                BindTile = (tile, game) => ((TextBlock)((Border)tile).Child!).Text = "#" + game.Id,
            };
            grid.Refresh(Library(3));
            ToolWindow window = Show(grid);

            Assert.Equal(new[] { "#0", "#1", "#2" },
                Showing(grid).Select(c => ((TextBlock)((Border)c.Content!).Child!).Text).OrderBy(t => t));
            window.Close();
        });

        private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
        {
            foreach (AutomationPeer child in peer.GetChildren())
            {
                yield return child;
                foreach (AutomationPeer deeper in Descendants(child)) yield return deeper;
            }
        }
    }
}
