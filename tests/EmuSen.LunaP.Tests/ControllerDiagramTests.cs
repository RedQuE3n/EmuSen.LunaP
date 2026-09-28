using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // ControllerDiagram: regions, hit tests, pressed and stick drawing, labels and moving between regions - see docs/LunaP.md §198.
    public class ControllerDiagramTests
    {
        public static TheoryData<ControllerLayout> Drawn() => new(Enum.GetValues<ControllerLayout>());

        private static (ToolWindow Window, ControllerDiagram Diagram) Shown(ControllerLayout layout, double w = 1100, double h = 640)
        {
            var diagram = new ControllerDiagram { Layout = layout };
            foreach (DiagramRegion r in diagram.Regions) diagram.SetBinding(r.Id, r.Id.Length <= 2 ? r.Id : "K", "South");
            var window = new ToolWindow { Width = w, Height = h, Content = diagram, Background = Brushes.Black };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
            return (window, diagram);
        }

        private static Color Pixel(ToolWindow window, Point p) => At(UiTest.Redraw(window), p.X, p.Y);

        private static int Count(RenderedFrame f, Rect box, Func<Color, bool> match)
        {
            int n = 0;
            for (int y = Math.Max(0, (int)box.Y); y < Math.Min(f.Height, (int)box.Bottom); y++)
                for (int x = Math.Max(0, (int)box.X); x < Math.Min(f.Width, (int)box.Right); x++)
                    if (match(At(f, x, y))) n++;
            return n;
        }

        [Theory]
        [MemberData(nameof(Drawn))]
        public Task A_point_in_each_region_hits_that_region_and_the_corners_hit_nothing(ControllerLayout layout) => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(layout);
            Assert.NotEmpty(diagram.Regions);
            Assert.Equal(diagram.Regions.Count, diagram.Regions.Select(r => r.Id).Distinct().Count());
            foreach (DiagramRegion region in diagram.Regions)
            {
                Point? at = diagram.PointIn(region.Id);
                Assert.True(at is not null, $"{region.Id} has no point inside it");
                Assert.Equal(region.Id, diagram.RegionAt(at!.Value));
                Assert.True(diagram.RegionRect(region.Id).Contains(at.Value));
            }
            foreach (Point corner in new[] { new Point(2, 2), new Point(1098, 2), new Point(2, 638), new Point(1098, 638) })
                Assert.Null(diagram.RegionAt(corner));
            Assert.Null(diagram.PointIn("NoSuchRegion"));
            window.Close();
        });

        // Pressed is drawn in the accent, and letting go draws the region as it was.
        [Theory]
        [MemberData(nameof(Drawn))]
        public Task A_pressed_region_is_drawn_in_the_accent_and_released_as_it_was(ControllerLayout layout) => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(layout);
            diagram.Resources["LunaAccentColor"] = Color.Parse("#FF00FF");
            Color accent = Color.Parse("#FF00FF");
            foreach (DiagramRegion region in diagram.Regions)
            {
                Rect box = diagram.RegionRect(region.Id);
                RenderedFrame before = UiTest.Redraw(window);
                diagram.SetPressed(region.Id, true);
                Assert.True(diagram.IsPressed(region.Id));
                RenderedFrame lit = UiTest.Redraw(window);
                int was = Count(before, box, c => Near(c, accent, 30)), now = Count(lit, box, c => Near(c, accent, 30));
                Assert.True(now > was + 150, $"{region.Id} pressed drew {now} accent pixels of {box.Width * box.Height:0}, {was} before");
                diagram.SetPressed(region.Id, false);
                Assert.True(before.Hash == UiTest.Redraw(window).Hash, $"{region.Id} is drawn differently once let go");
            }
            Assert.Empty(diagram.Pressed);
            window.Close();
        });

        [Fact]
        public Task A_stick_s_knob_moves_by_its_position_and_stops_at_its_travel() => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(ControllerLayout.Nintendo64);
            string stick = Assert.Single(diagram.Sticks);
            Point rest = diagram.StickKnobCentre(stick)!.Value;

            diagram.SetStick(stick, 1, 0);
            Point right = diagram.StickKnobCentre(stick)!.Value;
            diagram.SetStick(stick, 3, 0);
            Assert.Equal(new Vector(1, 0), diagram.StickPosition(stick));
            Assert.Equal(right, diagram.StickKnobCentre(stick)!.Value);
            diagram.SetStick(stick, -0.5, 0.5);
            Point half = diagram.StickKnobCentre(stick)!.Value;
            Assert.True(right.X > rest.X + 5 && Math.Abs(right.Y - rest.Y) < 0.01);
            Assert.Equal(rest.X - (right.X - rest.X) / 2, half.X, 3);
            Assert.Equal(rest.Y + (right.X - rest.X) / 2, half.Y, 3);

            // The knob is drawn where it is said to be: its middle turns the accent's colour of the dot drawn there.
            diagram.Resources["LunaAccentColor"] = Color.Parse("#00FF00");
            Assert.True(Near(Pixel(window, half), Color.Parse("#00FF00"), 40));
            Assert.Null(diagram.StickKnobCentre("NoStick"));
            window.Close();
        });

        // Every region reaches every other by the four directions, so a pad alone can choose any of them.
        [Theory]
        [MemberData(nameof(Drawn))]
        public Task Every_region_reaches_every_other_by_moving_the_selection(ControllerLayout layout) => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(layout);
            NavigationDirection[] ways = { NavigationDirection.Up, NavigationDirection.Down, NavigationDirection.Left, NavigationDirection.Right };
            string start = diagram.Regions[0].Id;
            var reached = new HashSet<string> { start };
            var queue = new Queue<string>(new[] { start });
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                foreach (NavigationDirection way in ways)
                {
                    diagram.Select(at);
                    if (!diagram.MoveSelection(way)) continue;
                    string to = diagram.SelectedRegion!;
                    Assert.Equal(diagram.Neighbour(at, way), to);
                    Assert.Equal(to, diagram.RegionOf(TopLevel.GetTopLevel(diagram)!.FocusManager!.GetFocusedElement()));
                    if (reached.Add(to)) queue.Enqueue(to);
                }
            }
            Assert.Equal(diagram.Regions.Select(r => r.Id).OrderBy(x => x), reached.OrderBy(x => x));
            window.Close();
        });

        [Fact]
        public Task Moving_goes_to_the_nearest_region_that_way_and_stops_at_the_edge() => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(ControllerLayout.Snes);
            Assert.Equal("A", diagram.Neighbour("Y", NavigationDirection.Right));
            Assert.Equal("X", diagram.Neighbour("B", NavigationDirection.Up));
            Assert.Equal("Down", diagram.Neighbour("Up", NavigationDirection.Down));
            Assert.Equal("Up", diagram.Neighbour("Down", NavigationDirection.Up));
            Assert.Equal("Start", diagram.Neighbour("Select", NavigationDirection.Right));
            Assert.Null(diagram.Neighbour("L", NavigationDirection.Left));
            diagram.Select("L");
            Assert.False(diagram.MoveSelection(NavigationDirection.Left));
            Assert.Equal("L", diagram.SelectedRegion);
            window.Close();
        });

        // Labels: one per region, none overlapping another or the edge, at a small, a middling and a large size.
        [Theory]
        [InlineData(ControllerLayout.Snes, 700, 440)]
        [InlineData(ControllerLayout.Snes, 1100, 640)]
        [InlineData(ControllerLayout.Snes, 1920, 1080)]
        [InlineData(ControllerLayout.Nintendo64, 760, 480)]
        [InlineData(ControllerLayout.Nintendo64, 1100, 640)]
        [InlineData(ControllerLayout.Nintendo64, 1920, 1080)]
        [InlineData(ControllerLayout.Nintendo64, 1000, 420)]
        public Task Every_region_has_a_label_and_no_label_overlaps_another(ControllerLayout layout, double w, double h) => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(layout, w, h);
            var boxes = new List<(string Id, Rect Box)>();
            foreach (DiagramRegion region in diagram.Regions)
            {
                Button label = diagram.LabelOf(region.Id)!;
                Assert.True(label.Focusable);
                Assert.Equal(region.Id, diagram.RegionOf(label));
                Rect box = label.Bounds;
                Assert.True(box.Width > 20 && box.Height > 10, $"{region.Id}'s label is {box}");
                Assert.True(box.X >= -0.5 && box.Y >= -0.5 && box.Right <= w + 0.5 && box.Bottom <= h + 0.5, $"{region.Id}'s label {box} leaves {w}x{h}");
                boxes.Add((region.Id, box));
            }
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    Assert.False(boxes[i].Box.Deflate(0.5).Intersects(boxes[j].Box.Deflate(0.5)), $"{boxes[i].Id} {boxes[i].Box} overlaps {boxes[j].Id} {boxes[j].Box}");
            window.Close();
        });

        // Z is drawn behind the middle grip: a click on the shell over it hits nothing, and a click where it shows hits it.
        [Fact]
        public Task A_region_drawn_behind_the_shell_is_hit_only_where_it_shows() => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(ControllerLayout.Nintendo64);
            Rect z = diagram.RegionRect("Z");
            Assert.Equal("Z", diagram.RegionAt(diagram.PointIn("Z")!.Value));
            Assert.Null(diagram.RegionAt(new Point(z.Right - 2, z.Center.Y)));
            window.Close();
        });

        // The selected region is ringed in the accent, and the ring goes with the selection.
        [Theory]
        [MemberData(nameof(Drawn))]
        public Task The_selected_region_is_ringed_and_the_ring_goes_with_the_selection(ControllerLayout layout) => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(layout);
            Color accent = Color.Parse("#FF00FF");
            diagram.Resources["LunaAccentColor"] = accent;
            ulong none = UiTest.Redraw(window).Hash;
            // Counted on the half of the region away from its label, where its line does not reach, so only a ring is counted.
            static bool Magenta(Color c) => c.R > c.G + 80 && c.B > c.G + 80;
            foreach (DiagramRegion region in diagram.Regions)
            {
                Rect r = diagram.RegionRect(region.Id).Inflate(12);
                Rect far = region.Side switch
                {
                    DiagramSide.Left => new Rect(r.Center.X, r.Y, r.Width / 2, r.Height),
                    DiagramSide.Right => new Rect(r.X, r.Y, r.Width / 2, r.Height),
                    DiagramSide.Top => new Rect(r.X, r.Center.Y, r.Width, r.Height / 2),
                    _ => new Rect(r.X, r.Y, r.Width, r.Height / 2),
                };
                diagram.SelectedRegion = null;
                int was = Count(UiTest.Redraw(window), far, Magenta);
                diagram.SelectedRegion = region.Id;
                int ringed = Count(UiTest.Redraw(window), far, Magenta);
                Assert.True(ringed > was + 20, $"{region.Id} selected drew {ringed} accent pixels on its far half, {was} before");
            }
            diagram.SelectedRegion = null;
            Assert.Equal(none, UiTest.Redraw(window).Hash);
            window.Close();
        });

        // Each label's line runs from the label to its region, and meets a button at the edge facing the label, not through its letter.
        [Theory]
        [InlineData(ControllerLayout.Snes, 1100, 640)]
        [InlineData(ControllerLayout.Snes, 1700, 500)]
        [InlineData(ControllerLayout.Snes, 700, 900)]
        [InlineData(ControllerLayout.Nintendo64, 1100, 640)]
        [InlineData(ControllerLayout.Nintendo64, 1700, 500)]
        [InlineData(ControllerLayout.Nintendo64, 700, 900)]
        public Task Each_label_s_line_runs_from_the_label_to_its_region(ControllerLayout layout, double w, double h) => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(layout, w, h);
            foreach (DiagramRegion region in diagram.Regions)
            {
                (Point from, _, Point to) = diagram.LeaderOf(region.Id)!.Value;
                Rect label = diagram.LabelOf(region.Id)!.Bounds.Inflate(1);
                Assert.True(label.Contains(from), $"{region.Id}'s line starts at {from}, off its label {label}");
                Assert.True(diagram.RegionRect(region.Id).Inflate(2).Contains(to), $"{region.Id}'s line ends at {to}, off its region {diagram.RegionRect(region.Id)}");
            }
            foreach (string lettered in layout == ControllerLayout.Snes ? new[] { "X", "Y", "A", "B" } : new[] { "A", "B" })
            {
                Rect box = diagram.RegionRect(lettered);
                Point meets = diagram.LeaderOf(lettered)!.Value.To;
                Assert.True(((Vector)(meets - box.Center)).Length > Math.Min(box.Width, box.Height) * 0.35, $"{lettered}'s line meets it at {meets}, near its middle {box.Center}");
            }
            Assert.Null(diagram.LeaderOf("NoSuchRegion"));
            window.Close();
        });

        // No line runs across the controller: each leaves it by the shortest way that crosses no other button, so the length of it over the drawing is short.
        [Theory]
        [MemberData(nameof(Drawn))]
        public Task No_line_runs_across_the_drawing(ControllerLayout layout) => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(layout);
            Rect drawn = default;
            for (double y = 0; y < 640; y += 4)
                for (double x = 0; x < 1100; x += 4)
                    if (diagram.IsOnDrawing(new Point(x, y))) drawn = drawn == default ? new Rect(x, y, 1, 1) : drawn.Union(new Rect(x, y, 1, 1));
            double across = Math.Max(drawn.Width, drawn.Height);
            var worst = new List<string>();
            foreach (DiagramRegion region in diagram.Regions)
            {
                (Point from, Point elbow, Point to) = diagram.LeaderOf(region.Id)!.Value;
                double over = 0;
                foreach ((Point a, Point b) in new[] { (from, elbow), (elbow, to) })
                {
                    double length = ((Vector)(b - a)).Length;
                    for (double t = 0; t < length; t += 1)
                    {
                        Point p = a + (b - a) * (t / length);
                        if (diagram.IsOnDrawing(p) && diagram.RegionAt(p) != region.Id) over += 1;
                    }
                }
                if (over > across * 0.35) worst.Add($"{region.Id} crosses {over:0} px of a {across:0} px drawing");
            }
            Assert.Empty(worst);
            window.Close();
        });

        // A line's way out is no longer over the drawing than any straight way along the axes or diagonals that crosses no other button, and crosses no button itself.
        [Theory]
        [MemberData(nameof(Drawn))]
        public Task Each_line_leaves_by_the_shortest_way_that_crosses_no_other_button(ControllerLayout layout) => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(layout);
            Rect box = default;
            for (double y = 0; y < 640; y += 4)
                for (double x = 0; x < 1100; x += 4)
                    if (diagram.IsOnDrawing(new Point(x, y))) box = box == default ? new Rect(x, y, 1, 1) : box.Union(new Rect(x, y, 1, 1));
            double size = Math.Max(box.Width, box.Height);
            string? last = null;

            // How far a straight way from a point at an angle runs before it has left the drawing's box.
            double Reach(Point p, double a)
            {
                double dx = Math.Cos(a), dy = Math.Sin(a), t = double.MaxValue;
                if (Math.Abs(dx) > 1e-9) t = Math.Min(t, ((dx > 0 ? box.Right + 2 : box.Left - 2) - p.X) / dx);
                if (Math.Abs(dy) > 1e-9) t = Math.Min(t, ((dy > 0 ? box.Bottom + 2 : box.Top - 2) - p.Y) / dy);
                return t;
            }

            // Over the drawing along a straight path, and whether it crosses a button other than its own.
            (double Over, bool Crosses) Walk(string region, Point a, Point b)
            {
                double over = 0, length = ((Vector)(b - a)).Length;
                int crossed = 0;
                string? hit = null;
                for (double t = 0; t < length; t += 1)
                {
                    Point p = a + (b - a) * (t / length);
                    string? at = diagram.RegionAt(p);
                    if (at is not null && at != region) { crossed++; hit = at; }
                    if (diagram.IsOnDrawing(p) && at != region) over += 1;
                }
                last = hit;
                return (over, crossed > 2);
            }

            var worse = new List<string>();
            foreach (DiagramRegion region in diagram.Regions)
            {
                (_, Point via, Point to) = diagram.LeaderOf(region.Id)!.Value;
                (double over, bool crosses) = Walk(region.Id, to, via);
                if (crosses) worse.Add($"{region.Id}'s line crosses {last}");
                Point from = diagram.PointIn(region.Id)!.Value;
                double straight = Enumerable.Range(0, 8).Select(i => i * Math.PI / 4).Select(a => from + new Vector(Math.Cos(a), Math.Sin(a)) * Reach(from, a))
                    .Select(end => Walk(region.Id, from, end)).Where(w => !w.Crosses).Select(w => w.Over).DefaultIfEmpty(double.MaxValue).Min();
                if (over > straight + size * 0.05) worse.Add($"{region.Id} runs {over:0} px over the drawing where a straight way runs {straight:0}");
            }
            Assert.Empty(worse);
            window.Close();
        });

        // No row or column takes more labels than it has room for: six in a row, eight in a column.
        [Theory]
        [MemberData(nameof(Drawn))]
        public Task No_band_takes_more_labels_than_its_room(ControllerLayout layout) => UiTest.Run(() =>
        {
            var diagram = new ControllerDiagram { Layout = layout };
            foreach (DiagramSide side in Enum.GetValues<DiagramSide>())
            {
                int count = diagram.Regions.Count(r => r.Side == side);
                Assert.True(count <= (side is DiagramSide.Top or DiagramSide.Bottom ? 6 : 8), $"{layout}'s {side} takes {count} labels");
            }
        });

        // A cross's arm is hit inside the cross's rounded end, not in the square corner of the box it is cut from.
        [Fact]
        public Task A_cross_s_arm_is_hit_only_inside_the_cross() => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(ControllerLayout.Snes, 1600, 900);
            foreach (string arm in new[] { "Up", "Down", "Left", "Right" })
            {
                Rect box = diagram.RegionRect(arm);
                Point corner = arm switch
                {
                    "Up" => box.TopLeft + new Vector(1, 1),
                    "Down" => box.BottomRight - new Vector(1, 1),
                    "Left" => box.BottomLeft + new Vector(1, -1),
                    _ => box.TopRight + new Vector(-1, 1),
                };
                Assert.NotEqual(arm, diagram.RegionAt(corner));
                Assert.Equal(arm, diagram.RegionAt(diagram.PointIn(arm)!.Value));
            }
            window.Close();
        });

        // A label is never much smaller than the text around it: the same space with larger text around it gives taller labels.
        [Fact]
        public Task Labels_follow_the_size_of_the_text_around_them() => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(ControllerLayout.Snes, 1100, 640);
            double small = diagram.LabelOf("A")!.Bounds.Height;
            window.FontSize = 26;
            UiTest.Redraw(window);
            double large = diagram.LabelOf("A")!.Bounds.Height;
            Assert.True(large > small * 1.15, $"labels were {small:0.0} high with 14-pixel text and {large:0.0} with 26");
            window.Close();
        });

        [Fact]
        public Task A_label_says_its_bindings_and_a_click_on_it_or_on_the_drawing_chooses_its_region() => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(ControllerLayout.Snes);
            var chosen = new List<string>();
            diagram.RegionInvoked += (_, e) => chosen.Add(e.Region);

            diagram.SetBinding("A", "X", "East");
            Assert.Equal(new DiagramBinding("X", "East"), diagram.BindingOf("A"));
            Button label = diagram.LabelOf("A")!;
            Assert.Equal("A", Avalonia.Automation.AutomationProperties.GetName(label));
            Assert.Equal("Key X, pad East", Avalonia.Automation.AutomationProperties.GetHelpText(label));
            diagram.SetBinding("A", null, null);
            Assert.Equal("Key none, pad none", Avalonia.Automation.AutomationProperties.GetHelpText(label));

            label.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new[] { "A" }, chosen);
            Assert.Equal("A", diagram.SelectedRegion);

            Point on = diagram.TranslatePoint(diagram.PointIn("Start")!.Value, window)!.Value;
            window.MouseDown(on, MouseButton.Left);
            window.MouseUp(on, MouseButton.Left);
            Assert.Equal(new[] { "A", "Start" }, chosen);
            Assert.Equal("Start", diagram.SelectedRegion);

            // Off, the labels take no focus and a click on the drawing chooses nothing.
            diagram.IsInteractive = false;
            Assert.False(diagram.LabelOf("A")!.Focusable);
            window.MouseDown(on, MouseButton.Left);
            window.MouseUp(on, MouseButton.Left);
            Assert.Equal(2, chosen.Count);
            window.Close();
        });

        [Fact]
        public Task The_arrow_keys_move_between_regions_from_a_focused_label() => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(ControllerLayout.Snes);
            diagram.Select("Y", NavigationMethod.Directional);
            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal("A", diagram.SelectedRegion);
            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal("X", diagram.SelectedRegion);
            window.Close();
        });

        [Fact]
        public Task Changing_the_layout_rebuilds_the_regions_and_forgets_what_was_pressed() => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(ControllerLayout.Snes);
            diagram.SetPressed("X", true);
            diagram.SelectedRegion = "X";
            diagram.Layout = ControllerLayout.Nintendo64;
            Assert.Contains(diagram.Regions, r => r.Id == "CUp");
            Assert.DoesNotContain(diagram.Regions, r => r.Id == "X");
            Assert.Empty(diagram.Pressed);
            Assert.Null(diagram.SelectedRegion);
            Assert.Equal(new[] { "StickUp", "StickDown", "StickLeft", "StickRight" }, diagram.Regions.Where(r => r.Stick == "Stick").Select(r => r.Id));
            window.Close();
        });

        // Without labels the drawing fills the control; without keys a label is one line; a caption of the host's is printed in place of the drawing's.
        [Fact]
        public Task Labels_keys_captions_and_the_stick_ring_are_the_host_s_to_turn_off_or_on() => UiTest.Run(() =>
        {
            (ToolWindow window, ControllerDiagram diagram) = Shown(ControllerLayout.Gamepad);
            double labelled = diagram.RegionRect("A").Width;
            double tall = diagram.LabelOf("A")!.Bounds.Width;
            diagram.ShowsKeys = false;
            UiTest.Redraw(window);
            Assert.True(diagram.LabelOf("A")!.Bounds.Width <= tall);
            diagram.ShowsLabels = false;
            UiTest.Redraw(window);
            Assert.False(diagram.LabelOf("A")!.IsVisible);
            Assert.True(diagram.RegionRect("A").Width > labelled * 1.3, $"the drawing grew from {labelled:0} to {diagram.RegionRect("A").Width:0}");
            Assert.Null(diagram.LeaderOf("A"));

            ulong plain = UiTest.Redraw(window).Hash;
            diagram.SetCaption("A", "Z");
            ulong captioned = UiTest.Redraw(window).Hash;
            Assert.NotEqual(plain, captioned);
            diagram.SetCaption("A", null);
            Assert.Equal(plain, UiTest.Redraw(window).Hash);

            diagram.StickRing = 0.5;
            Assert.NotEqual(plain, UiTest.Redraw(window).Hash);
            window.Close();
        });

        // Groups of labels that would overlap are pushed apart about the middle of their wishes, and kept inside the span.
        [Fact]
        public void Labels_are_spread_about_their_wishes_without_overlapping()
        {
            double[] at = ControllerDiagram.Spread(new double[] { 100, 105, 110 }, new double[] { 20, 20, 20 }, 4, 0, 1000);
            Assert.Equal(new double[] { 81, 105, 129 }, at);
            Assert.Equal(new double[] { 0, 24 }, ControllerDiagram.Spread(new double[] { -50, -40 }, new double[] { 20, 20 }, 4, 0, 1000));
            Assert.Equal(new double[] { 956, 980 }, ControllerDiagram.Spread(new double[] { 990, 995 }, new double[] { 20, 20 }, 4, 0, 1000));
            Assert.Equal(new double[] { 10, 500 }, ControllerDiagram.Spread(new double[] { 10, 500 }, new double[] { 20, 20 }, 4, 0, 1000));
        }

        // The drawings read at any size: each fills its space with more than a few colours, from a thumbnail to a large screen.
        [Theory]
        [InlineData(ControllerLayout.Snes)]
        [InlineData(ControllerLayout.Nintendo64)]
        public Task A_drawing_is_drawn_at_every_size(ControllerLayout layout) => UiTest.Run(() =>
        {
            foreach ((int w, int h) in new[] { (360, 240), (1100, 640), (2560, 1440) })
            {
                (ToolWindow window, ControllerDiagram diagram) = Shown(layout, w, h);
                Assert.True(UiTest.Redraw(window).DistinctColours(40) >= 40, $"{layout} at {w}x{h} drew few colours");
                window.Close();
            }
        });

        // Writes each drawing to EMUSEN_DIAGRAM_DUMP, idle and with every third region lit, for a person to look at.
        [Fact]
        public Task Pictures_for_review() => UiTest.Run(() =>
        {
            if (Environment.GetEnvironmentVariable("EMUSEN_DIAGRAM_DUMP") is not { Length: > 0 } folder) return;
            Directory.CreateDirectory(folder);
            foreach (ControllerLayout layout in Enum.GetValues<ControllerLayout>())
            {
                (ToolWindow window, ControllerDiagram diagram) = Shown(layout);
                window.CaptureRenderedFrame()!.Save(Path.Combine(folder, $"{layout}-idle.png"));
                foreach (DiagramRegion r in diagram.Regions.Where((_, i) => i % 3 == 0)) diagram.SetPressed(r.Id, true);
                foreach (string stick in diagram.Sticks) diagram.SetStick(stick, 0.7, -0.5);
                diagram.Select(diagram.Regions[1].Id);
                window.CaptureRenderedFrame()!.Save(Path.Combine(folder, $"{layout}-lit.png"));
                window.Close();
            }
        });

        // In the look the labels are set close, so their words stay larger in a short space; outside it they are as they were (§196.10).
        [Fact]
        public Task A_diagram_in_the_look_sets_its_labels_close_and_keeps_their_words_larger() => UiTest.Run(() =>
        {
            ControllerDiagram Make()
            {
                var d = new ControllerDiagram { Layout = ControllerLayout.Nintendo64 };
                foreach (DiagramRegion r in d.Regions) d.SetBinding(r.Id, r.Id.Length <= 2 ? r.Id : "K", "Right Shoulder");
                return d;
            }
            ControllerDiagram plain = Make(), look = Make();
            var lookHost = new Border { Width = 1400, Height = 450, Child = look };
            MenuLook.SetIsOn(lookHost, true);
            var window = new ToolWindow { Width = 1400, Height = 900, FontSize = 20, Background = Brushes.Black,
                Content = new StackPanel { Children = { new Border { Width = 1400, Height = 450, Child = plain }, lookHost } } };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);

            Assert.False(plain.CompactLabels);
            Assert.True(look.CompactLabels);
            Assert.True(look.LabelTextSize > plain.LabelTextSize * 1.25, $"{look.LabelTextSize:0.0} in the look, {plain.LabelTextSize:0.0} outside it");
            var boxes = look.Regions.Select(r => (r.Id, Box: look.LabelOf(r.Id)!.Bounds)).ToList();
            foreach ((string id, Rect box) in boxes)
                Assert.True(box.X >= -0.5 && box.Y >= -0.5 && box.Right <= 1400.5 && box.Bottom <= 450.5, $"{id}'s label {box} leaves the drawing");
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    Assert.False(boxes[i].Box.Deflate(0.5).Intersects(boxes[j].Box.Deflate(0.5)), $"{boxes[i].Id} {boxes[i].Box} overlaps {boxes[j].Id} {boxes[j].Box}");
            window.Close();
        });
    }
}
