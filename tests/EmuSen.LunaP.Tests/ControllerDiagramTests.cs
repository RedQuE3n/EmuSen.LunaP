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
        // The drawings finished so far; the rest stand in with another's until theirs are drawn.
        public static TheoryData<ControllerLayout> Drawn() => new() { ControllerLayout.Snes, ControllerLayout.Nintendo64 };

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
            foreach (DiagramRegion region in diagram.Regions)
            {
                Rect box = diagram.RegionRect(region.Id).Inflate(12);
                diagram.SelectedRegion = region.Id;
                int ringed = Count(UiTest.Redraw(window), box, c => Near(c, accent, 30));
                Assert.True(ringed > 40, $"{region.Id} selected drew {ringed} accent pixels about it");
            }
            diagram.SelectedRegion = null;
            Assert.Equal(none, UiTest.Redraw(window).Hash);
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
            Assert.Equal(new[] { "StickUp", "StickRight", "StickDown", "StickLeft" }, diagram.Regions.Where(r => r.Stick == "Stick").Select(r => r.Id));
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
    }
}
