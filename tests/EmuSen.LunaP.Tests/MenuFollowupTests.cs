using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // The filled button glyphs, a menu's scroll indicator, and the text popup with a real field - see docs/LunaP.md §195.
    public class MenuFollowupTests
    {
        private const int Side = 64;

        private static byte[] Glyph(PadFamily family, PadGlyphButton button, PadGlyphStyle style)
        {
            var glyph = new PadGlyph { Family = family, Button = button, Style = style, GlyphSize = Side, Color = Colors.White };
            ToolWindow window = Show(new Canvas { Width = Side, Height = Side, Children = { glyph } }, Side, Side);
            RenderedFrame f = Frame(window);
            window.Close();
            var lum = new byte[Side * Side];
            for (int y = 0; y < Side; y++)
                for (int x = 0; x < Side; x++) lum[y * Side + x] = At(f, x, y).G;
            return lum;
        }

        private static string Ascii(byte[] g) => string.Join("\n", Enumerable.Range(0, Side / 2).Select(y => new string(Enumerable.Range(0, Side).Select(x => g[y * 2 * Side + x] > 128 ? '#' : '.').ToArray())));

        private static bool Inked(byte[] g, double x, double y) => g[(int)y * Side + (int)x] > 128;

        private static int Ink(byte[] g) => g.Count(v => v > 128);

        private static int Differing(byte[] a, byte[] b) => a.Zip(b).Count(p => Math.Abs(p.First - p.Second) > 64);

        [Fact]
        public Task Every_button_of_every_family_draws_filled_inside_its_square() => UiTest.Run(() =>
        {
            foreach (PadFamily family in Enum.GetValues<PadFamily>())
                foreach (PadGlyphButton button in Enum.GetValues<PadGlyphButton>())
                {
                    int ink = Ink(Glyph(family, button, PadGlyphStyle.Filled));
                    Assert.True(ink > Side * Side / 40 && ink < Side * Side * 3 / 4, $"{family} {button} drew {ink} pixels");
                }
        });

        [Fact]
        public Task A_filled_face_button_is_a_disc_with_its_letter_or_shape_cut_out() => UiTest.Run(() =>
        {
            double c = Side / 2.0;
            foreach (PadFamily family in new[] { PadFamily.Xbox, PadFamily.PlayStation, PadFamily.Nintendo })
                foreach (PadGlyphButton button in new[] { PadGlyphButton.South, PadGlyphButton.East, PadGlyphButton.West, PadGlyphButton.North })
                {
                    byte[] filled = Glyph(family, button, PadGlyphStyle.Filled);
                    // The disc reaches 0.425 of the side from the centre and no further.
                    Assert.True(Inked(filled, c - Side * 0.38, c) && Inked(filled, c, c + Side * 0.38), $"{family} {button}: the disc's edge\n{Ascii(filled)}");
                    Assert.False(Inked(filled, c - Side * 0.47, c), $"{family} {button}: past the disc\n{Ascii(filled)}");
                    // Something is cut out of it: the disc alone would ink about 0.567 of the square.
                    int ink = Ink(filled), disc = (int)(Math.PI * Math.Pow(Side * 0.425, 2));
                    Assert.True(ink < disc * 0.93 && ink > disc * 0.6, $"{family} {button}: {ink} of a disc's {disc}\n{Ascii(filled)}");
                    Assert.True(Differing(filled, Glyph(family, button, PadGlyphStyle.Outline)) > 200, $"{family} {button}: filled looks outlined");
                }
            // A cross is cut through the middle of its disc; a circle leaves the middle solid.
            Assert.False(Inked(Glyph(PadFamily.PlayStation, PadGlyphButton.South, PadGlyphStyle.Filled), c, c));
            Assert.True(Inked(Glyph(PadFamily.PlayStation, PadGlyphButton.East, PadGlyphStyle.Filled), c, c));
        });

        [Fact]
        public Task Filled_an_unknown_pad_is_lettered_as_an_Xbox_pad_and_named_so() => UiTest.Run(() =>
        {
            foreach (PadGlyphButton button in Enum.GetValues<PadGlyphButton>())
                Assert.True(Differing(Glyph(PadFamily.Generic, button, PadGlyphStyle.Filled), Glyph(PadFamily.Xbox, button, PadGlyphStyle.Filled)) == 0, $"{button}");
            Assert.True(Differing(Glyph(PadFamily.Generic, PadGlyphButton.South, PadGlyphStyle.Filled), Glyph(PadFamily.Generic, PadGlyphButton.South, PadGlyphStyle.Outline)) > 200);
            Assert.Equal("A", PadGlyph.Describe(PadFamily.Generic, PadGlyphButton.South, PadGlyphStyle.Filled));
            Assert.Equal("South button", PadGlyph.Describe(PadFamily.Generic, PadGlyphButton.South, PadGlyphStyle.Outline));
            Assert.Equal("B", PadGlyph.Describe(PadFamily.Nintendo, PadGlyphButton.South, PadGlyphStyle.Filled));
        });

        [Fact]
        public Task Filled_the_three_printed_families_differ_on_every_face_and_middle_button() => UiTest.Run(() =>
        {
            PadFamily[] families = { PadFamily.Xbox, PadFamily.PlayStation, PadFamily.Nintendo };
            foreach (PadGlyphButton button in new[] { PadGlyphButton.South, PadGlyphButton.East, PadGlyphButton.West, PadGlyphButton.North, PadGlyphButton.Start, PadGlyphButton.Select, PadGlyphButton.LeftShoulder, PadGlyphButton.RightTrigger })
            {
                byte[][] drawn = families.Select(f => Glyph(f, button, PadGlyphStyle.Filled)).ToArray();
                for (int i = 0; i < drawn.Length; i++)
                    for (int j = i + 1; j < drawn.Length; j++)
                        Assert.True(Differing(drawn[i], drawn[j]) > 20, $"{button}: {families[i]} and {families[j]} look alike");
            }
            // Start and Select differ within each family.
            foreach (PadFamily family in families)
                Assert.True(Differing(Glyph(family, PadGlyphButton.Start, PadGlyphStyle.Filled), Glyph(family, PadGlyphButton.Select, PadGlyphStyle.Filled)) > 20, $"{family}");
        });

        [Fact]
        public Task A_filled_d_pad_cuts_an_arrow_into_each_arm_it_moves_along() => UiTest.Run(() =>
        {
            double c = Side / 2.0, arrow = Side * 0.33;
            byte[] upDown = Glyph(PadFamily.Xbox, PadGlyphButton.DPadUpDown, PadGlyphStyle.Filled);
            byte[] leftRight = Glyph(PadFamily.Xbox, PadGlyphButton.DPadLeftRight, PadGlyphStyle.Filled);
            byte[] all = Glyph(PadFamily.Xbox, PadGlyphButton.DPad, PadGlyphStyle.Filled);
            Assert.False(Inked(upDown, c, c - arrow), Ascii(upDown));
            Assert.False(Inked(upDown, c, c + arrow), Ascii(upDown));
            Assert.True(Inked(upDown, c - arrow, c) && Inked(upDown, c + arrow, c), Ascii(upDown));
            Assert.False(Inked(leftRight, c - arrow, c) || Inked(leftRight, c + arrow, c), Ascii(leftRight));
            Assert.True(Inked(leftRight, c, c - arrow) && Inked(leftRight, c, c + arrow), Ascii(leftRight));
            foreach ((double x, double y) in new[] { (c, c - arrow), (c, c + arrow), (c - arrow, c), (c + arrow, c) }) Assert.False(Inked(all, x, y), Ascii(all));
            Assert.True(Inked(all, c, c), "the middle stays solid");
        });

        [Fact]
        public Task A_hint_bar_draws_its_style_and_a_menu_panel_s_help_bar_is_filled_by_default() => UiTest.Run(() =>
        {
            HintEntry[] entries = { new("Select") { Button = PadGlyphButton.South } };
            RenderedFrame Bar(PadGlyphStyle style)
            {
                var bar = new HintBar { Entries = entries, FontSize = 40, PadFamily = PadFamily.Xbox, GlyphStyle = style, IconColor = Colors.White, TextColor = Colors.Black };
                ToolWindow window = Show(new Canvas { Width = 200, Height = 60, Background = Brushes.Black, Children = { bar } }, 200, 60);
                RenderedFrame f = Frame(window);
                window.Close();
                return f;
            }
            RenderedFrame outline = Bar(PadGlyphStyle.Outline), filled = Bar(PadGlyphStyle.Filled);
            int whiteOutline = 0, whiteFilled = 0;
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 40; x++)
                {
                    if (At(outline, x, y).G > 128) whiteOutline++;
                    if (At(filled, x, y).G > 128) whiteFilled++;
                }
            Assert.True(whiteFilled > whiteOutline * 1.6, $"filled {whiteFilled}, outlined {whiteOutline}");

            var panel = new MenuPanel { Title = "Menu", Hints = entries };
            Assert.Equal(PadGlyphStyle.Filled, panel.HintGlyphStyle);
            ToolWindow w = Show(panel, 1280, 800);
            Assert.Equal(PadGlyphStyle.Filled, panel.HelpBar.GlyphStyle);
            panel.HintGlyphStyle = PadGlyphStyle.Outline;
            Assert.Equal(PadGlyphStyle.Outline, panel.HelpBar.GlyphStyle);
            w.Close();
        });

        private static (MenuPanel Panel, ScrollViewer Rows, ToolWindow Window) Menu(int rows, double scale = 1, string? subtitle = null)
        {
            var list = new StackPanel();
            for (int i = 0; i < rows; i++) list.Children.Add(new MenuRow { Label = $"Row {i}" });
            var scroller = new ScrollViewer { Content = list, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden };
            var panel = new MenuPanel { Title = "UI Settings", Subtitle = subtitle, Child = scroller, ScrollIndicatorColor = Colors.Lime, PanelColor = Colors.Black };
            MenuPanel.SetScale(panel, scale);
            ToolWindow window = Show(panel, 1280 * scale, 800 * scale);
            return (panel, scroller, window);
        }

        private static int Lime(RenderedFrame f, Rect box)
        {
            int n = 0;
            for (int y = (int)box.Top; y < (int)Math.Ceiling(box.Bottom); y++)
                for (int x = (int)box.Left; x < (int)Math.Ceiling(box.Right); x++)
                    if (At(f, x, y) is { G: > 150, R: < 100, B: < 100 }) n++;
            return n;
        }

        [Fact]
        public Task The_scroll_indicator_shows_which_ways_the_rows_run_past_the_panel() => UiTest.Run(() =>
        {
            var (panel, rows, window) = Menu(30);
            (Rect up, Rect down) = panel.ScrollIndicatorBounds;
            Assert.Equal(MenuScrollIndicator.Down, panel.ScrollIndicator);
            RenderedFrame top = Frame(window);
            Assert.True(Lime(top, down) > 40, $"down {Lime(top, down)}");
            Assert.Equal(0, Lime(top, up));

            rows.Offset = new Vector(0, 54 * 5);
            window.UpdateLayout();
            Assert.Equal(MenuScrollIndicator.Both, panel.ScrollIndicator);
            RenderedFrame middle = Frame(window);
            Assert.True(Lime(middle, up) > 40 && Lime(middle, down) > 40);

            rows.Offset = new Vector(0, rows.Extent.Height);
            window.UpdateLayout();
            Assert.Equal(MenuScrollIndicator.Up, panel.ScrollIndicator);
            RenderedFrame bottom = Frame(window);
            Assert.True(Lime(bottom, up) > 40);
            Assert.Equal(0, Lime(bottom, down));

            // Nothing is drawn anywhere but in the two squares.
            int total = 0;
            for (int y = 0; y < 800; y += 1)
                for (int x = 0; x < 1280; x += 1)
                    if (!up.Contains(new Point(x, y)) && !down.Contains(new Point(x, y)) && At(middle, x, y) is { G: > 150, R: < 100, B: < 100 }) total++;
            Assert.Equal(0, total);

            panel.ShowsScrollIndicator = false;
            Assert.Equal(MenuScrollIndicator.None, panel.ScrollIndicator);
            Assert.Equal(0, Lime(Frame(window), up));
            window.Close();

            (panel, _, window) = Menu(3);
            Assert.Equal(MenuScrollIndicator.None, panel.ScrollIndicator);
            (up, down) = panel.ScrollIndicatorBounds;
            RenderedFrame few = Frame(window);
            Assert.Equal(0, Lime(few, up) + Lime(few, down));
            window.Close();
        });

        // The chevrons point the way the rows run: the lower pair's points are at the bottom of its square, the upper pair's at the top.
        [Fact]
        public Task Each_pair_points_the_way_its_rows_run() => UiTest.Run(() =>
        {
            var (panel, rows, window) = Menu(30);
            rows.Offset = new Vector(0, 54 * 5);
            window.UpdateLayout();
            RenderedFrame f = Frame(window);
            (Rect up, Rect down) = panel.ScrollIndicatorBounds;
            bool Lit(Rect box, double fy) => At(f, box.X + box.Width / 2, box.Y + box.Height * fy) is { G: > 150, R: < 100, B: < 100 };
            Assert.True(Lit(down, 0.84) && !Lit(down, 0.16), "the lower pair points down");
            Assert.True(Lit(up, 0.16) && !Lit(up, 0.84), "the upper pair points up");
            window.Close();
        });

        // Scrolling redraws the panel itself: a capture that repaints only what was invalidated shows the new pair.
        [Fact]
        public Task Scrolling_redraws_the_indicator_without_a_forced_repaint() => UiTest.Run(() =>
        {
            var (panel, rows, window) = Menu(30);
            (Rect up, _) = panel.ScrollIndicatorBounds;
            Assert.Equal(0, Lime(UiTest.Capture(window), up));
            rows.Offset = new Vector(0, 54 * 5);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(Lime(UiTest.Capture(window), up) > 40, "the upper pair appeared on the next frame");
            window.Close();
        });

        [Fact]
        public Task The_scroll_indicator_sits_at_the_title_s_right_where_the_reference_draws_it_at_any_scale() => UiTest.Run(() =>
        {
            foreach (double scale in new[] { 1.0, 1.5 })
            {
                var (panel, _, window) = Menu(30, scale);
                (Rect up, Rect down) = panel.ScrollIndicatorBounds;
                Assert.Equal(25 * scale, down.Width, 0.01);
                Assert.Equal(25 * scale, down.Height, 0.01);
                Assert.Equal(panel.PanelBounds.Right - 11 * scale, down.Right, 0.01);
                Assert.Equal(7 * scale, down.Top - up.Bottom, 0.01);
                Assert.Equal(panel.TitleBounds.Center.Y, (up.Top + down.Bottom) / 2, 0.01);
                window.Close();

                // With a subtitle the pair centres on the title's own line, 49 design pixels under the panel's top.
                (panel, _, window) = Menu(30, scale, "Aurora Drift (Synthetic).sfc [SNES]");
                (up, down) = panel.ScrollIndicatorBounds;
                Assert.Equal(panel.PanelBounds.Top + 49 * scale, (up.Top + down.Bottom) / 2, 0.01);
                window.Close();
            }
        });

        [Fact]
        public Task A_list_child_is_watched_through_the_scroller_its_template_holds() => UiTest.Run(() =>
        {
            var list = new ListBox { ItemsSource = Enumerable.Range(0, 30).Select(i => $"Entry {i}").ToArray() };
            var panel = new MenuPanel { Title = "Main Menu", Child = list };
            ToolWindow window = Show(panel, 1280, 800);
            Assert.Equal(MenuScrollIndicator.Down, panel.ScrollIndicator);
            list.ScrollIntoView(29);
            window.UpdateLayout();
            Assert.Equal(MenuScrollIndicator.Up, panel.ScrollIndicator);
            panel.Child = new StackPanel { Children = { new MenuRow { Label = "One" } } };
            window.UpdateLayout();
            Assert.Equal(MenuScrollIndicator.None, panel.ScrollIndicator);
            window.Close();
        });

        private static (ToolWindow Window, TextBox Box) TextWindow(string text)
        {
            var box = new TextBox { Text = text };
            ToolWindow window = Show(new StackPanel { Children = { box, new Button { Content = "Other" } } }, 1280, 800);
            box.Focus();
            return (window, box);
        }

        private static void Key(InputElement target, Key key) =>
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

        private static void Type(TextBox field, string text) =>
            field.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text, Source = field });

        [Fact]
        public Task The_text_popup_s_field_is_a_focused_text_box_and_Enter_keeps_what_was_typed() => UiTest.Run(() =>
        {
            var (window, box) = TextWindow("Aurora");
            MenuTextPopup popup = MenuTextPopup.Show(box, "Enter Name", "Enter  Done");
            Assert.True(popup.IsOpen);
            Assert.Same(popup, MenuTextPopup.OpenOver(window));
            Assert.Same(popup.Field, window.FocusManager!.GetFocusedElement());
            Assert.Equal("Aurora", popup.Field.Text);
            Assert.Equal(6, popup.Field.CaretIndex);

            Type(popup.Field, " Drift");
            Assert.Equal("Aurora Drift", popup.Field.Text);
            Assert.Equal("Aurora", box.Text);
            Key(popup.Field, Avalonia.Input.Key.Enter);

            Assert.False(popup.IsOpen);
            Assert.True(popup.Closed.Result);
            Assert.Equal("Aurora Drift", box.Text);
            Assert.Null(MenuTextPopup.OpenOver(window));
            Assert.Same(box, window.FocusManager!.GetFocusedElement());
            window.Close();
        });

        [Fact]
        public Task Escape_drops_what_was_typed_and_the_popup_is_titled_centred_and_shades_the_window() => UiTest.Run(() =>
        {
            var (window, box) = TextWindow("Aurora");
            MenuTextPopup popup = MenuTextPopup.Show(box, "Enter Name");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(640, popup.Bounds.Center.X, 1);
            Assert.Equal(400, popup.Bounds.Center.Y, 1);
            Assert.Equal("Enter Name", popup.Title);
            RenderedFrame f = Frame(window);
            Assert.True(At(f, 20, 20).R < 120, $"the window is shaded: {At(f, 20, 20)}");

            Type(popup.Field, "XYZ");
            Key(popup.Field, Avalonia.Input.Key.Escape);
            Assert.False(popup.IsOpen);
            Assert.False(popup.Closed.Result);
            Assert.Equal("Aurora", box.Text);

            // Cancel and Finish called directly do the same.
            popup = MenuTextPopup.Show(box, "Enter Name");
            popup.Field.Text = "Kept";
            popup.Finish();
            Assert.Equal("Kept", box.Text);
            popup = MenuTextPopup.Show(box, "Enter Name");
            popup.Field.Text = "Dropped";
            popup.Cancel();
            Assert.Equal("Kept", box.Text);
            window.Close();
        });
    }
}
