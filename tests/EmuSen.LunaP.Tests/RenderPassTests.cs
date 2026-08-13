using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.VisualTree;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // When Avalonia actually draws, which UiTest.Redraw depends on and which nothing else here can
    // see - see docs/LunaP.md §38.
    //
    // WHY THIS EXISTS AS A TEST RATHER THAN AS A PARAGRAPH. §37 fixed a macOS-only render failure,
    // and the obvious fix for the hazard it left behind was "capture the window twice and keep the
    // second frame". That is wrong, and wrong in the worst way available: it compiles, it reads
    // correctly, it costs a second capture, and it changes nothing, because a capture of an
    // unchanged window copies the frame that is already there instead of drawing a new one. It
    // would have shipped as a fix and been believed.
    //
    // The only reason we know otherwise is a measurement, and a measurement nobody can repeat is a
    // claim. So the measurement is these assertions.
    //
    // HOW A DRAW IS COUNTED. Control.Render does not draw - it records a draw list, which is why it
    // is called once and stays called once no matter how many frames are produced. A custom draw
    // operation runs during the real Skia pass, so counting it counts draws. Everything here is
    // stated in those units.
    //
    // THIS IS A PIN ON AVALONIA 12.1.0, not on anything LunaP owns. If a future Avalonia redraws
    // clean windows, or propagates invalidation down the tree, these turn red - and that is the
    // point, because UiTest.Redraw would then be doing unnecessary work at best and the reasoning
    // in its comment would be stale at worst.
    public class RenderPassTests
    {
        private sealed class DrawCounter : ICustomDrawOperation
        {
            public required Counted Owner { get; init; }

            public Rect Bounds { get; init; }
            public void Dispose() { }
            public bool HitTest(Point p) => false;

            // Never equal, so Avalonia can never decide this operation is unchanged and skip it.
            // A draw that was skipped for being identical would be counted as "no draw happened"
            // and would make every assertion below lie in the same direction.
            public bool Equals(ICustomDrawOperation? other) => false;

            public void Render(ImmediateDrawingContext context) => Owner.Draws++;
        }

        // Counts the passes that actually reached the rasteriser, and the ones that only rebuilt a
        // draw list, so a failure says which of the two changed.
        private sealed class Counted : Control
        {
            public int Draws;
            public int Records;

            public Counted()
            {
                Width = 120;
                Height = 80;
            }

            public override void Render(DrawingContext context)
            {
                Records++;
                context.Custom(new DrawCounter { Owner = this, Bounds = new Rect(0, 0, 120, 80) });
            }
        }

        private sealed class Rig
        {
            public required Counted Leaf { get; init; }
            public required Border Ancestor { get; init; }
            public required Window Window { get; init; }

            public int Capture()
            {
                _ = Window.CaptureRenderedFrame();
                return Leaf.Draws;
            }
        }

        private static Rig Show()
        {
            var leaf = new Counted();
            var ancestor = new Border { Child = new StackPanel { Children = { leaf } } };
            var window = new ToolWindow { Width = 320, Height = 220, Content = ancestor };
            window.Show();

            var rig = new Rig { Leaf = leaf, Ancestor = ancestor, Window = window };
            rig.Capture();
            return rig;
        }

        // The finding UiTest.Redraw is built on. Sabotage: none needed - deleting the invalidation
        // loop from Redraw makes this the behaviour Redraw has, and RenderPassTests.A_redraw_draws
        // below turns red.
        [Fact]
        public Task Capturing_an_unchanged_window_does_not_draw_it_again() => UiTest.Run(() =>
        {
            Rig rig = Show();
            int after = rig.Capture();
            int later = rig.Capture();

            Assert.Equal(1, after);
            Assert.Equal(1, later);
        });

        // Records, not draws: proof that the count above is not simply stuck. A clean capture
        // rebuilds nothing and draws nothing, and both halves of that are asserted.
        [Fact]
        public Task A_clean_capture_does_not_rebuild_the_draw_list_either() => UiTest.Run(() =>
        {
            Rig rig = Show();
            rig.Capture();

            Assert.Equal(1, rig.Leaf.Records);
        });

        // The trap that makes the tree walk necessary rather than paranoid: dirtying a parent does
        // not dirty its children, because every visual owns its own draw list.
        [Fact]
        public Task Invalidating_an_ancestor_does_not_redraw_what_it_contains() => UiTest.Run(() =>
        {
            Rig rig = Show();

            rig.Ancestor.InvalidateVisual();
            Assert.Equal(1, rig.Capture());

            ((StackPanel)rig.Ancestor.Child!).InvalidateVisual();
            Assert.Equal(1, rig.Capture());

            // The control that owns the draw list is the one that has to be told.
            rig.Leaf.InvalidateVisual();
            Assert.Equal(2, rig.Capture());
        });

        // What UiTest.Redraw promises, through the public method rather than through a copy of its
        // implementation - so deleting the invalidation loop turns this red.
        [Fact]
        public Task A_redraw_draws() => UiTest.Run(() =>
        {
            Rig rig = Show();

            UiTest.Redraw(rig.Window);
            Assert.Equal(2, rig.Leaf.Draws);

            UiTest.Redraw(rig.Window);
            Assert.Equal(3, rig.Leaf.Draws);
        });

        // The property the baseline path actually needs, and the one that would have caught §37 on
        // macOS had it existed: from a redraw onwards, the picture is settled. On Linux and Windows
        // this passes because nothing was ever unsettled; on macOS it passes only because Redraw
        // does a real draw pass, which is the whole reason §38 had to establish what a real draw is.
        [Fact]
        public Task A_redrawn_window_renders_the_same_way_every_time_after() => UiTest.Run(() =>
        {
            var window = new ToolWindow
            {
                Width = 320,
                Height = 220,
                Content = new TextBlock { Text = "Redraw settles the picture", FontSize = 17.5 },
            };

            window.Show();

            RenderedFrame first = UiTest.Redraw(window);
            RenderedFrame second = UiTest.Redraw(window);

            Assert.True(first.Hash == second.Hash,
                $"Two redraws of one unchanged window differed: {first.Hash:X16} vs {second.Hash:X16}. "
                + "See docs/LunaP.md §38.");
        });
    }
}
