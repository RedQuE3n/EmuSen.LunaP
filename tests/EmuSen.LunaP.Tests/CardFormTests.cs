using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Testing;
using Xunit;
using Xunit.Abstractions;

namespace EmuSen.LunaP.Tests
{
    // The Card form: the roles placed into a left panel over a scrim, the detail beside it, the header as its title row, and the header role in the other forms - see docs/LunaP.md §207.
    public class CardFormTests(ITestOutputHelper output)
    {
        private const string LongName = "The Legend of Zelda - Ocarina of Time - Master Quest (USA) (Debug Edition) (GameCube) (Rev 2)";

        public static TheoryData<int, int> Screens() => new() { { 1280, 800 }, { 1920, 1200 }, { 1280, 720 } };

        private static WriteableBitmap Cover() => new(new PixelSize(30, 42), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        private static (Window Window, AdaptiveLayout Layout, ListBox Rows, Control Pane, List<string> Invoked) Card(int width, int height, int rows = 8, AdaptiveForm form = AdaptiveForm.Card)
        {
            var invoked = new List<string>();
            var list = new ListBox { ItemsSource = Enumerable.Range(1, rows).Select(i => $"Row {i}").ToArray() };
            list.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new StackPanel());
            MenuRows.Apply(list);
            var pane = new StackPanel
            {
                Name = "Pane", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 8,
                Children = { new Border { Width = 320, Height = 240, Background = Brushes.DimGray }, new TextBlock { Text = "Slot 3 - Saved Sat 1 Nov, 14:32", Foreground = Brushes.White, FontSize = 22 * height / 800.0 } },
            };
            var layout = new AdaptiveLayout { Title = "Pause", Form = form, Content = list, Detail = pane, Header = new AdaptiveHeader(LongName, Picture: Cover()) };
            layout.SetSections(new[] { new AdaptiveSection("a", "First"), new AdaptiveSection("b", "Second") });
            layout.SetActions(new[] { new LunaAction("Screenshot", () => invoked.Add("Screenshot")) { PadButton = PadGlyphButton.West } });
            MenuPanel.SetScale(layout, height / 800.0);
            var window = new Window { Width = width, Height = height, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            list.SelectedIndex = 0;
            UiTest.Settle(layout);
            return (window, layout, list, pane, invoked);
        }

        private static Rect In(Control c, Control root) => new(c.TranslatePoint(default, root) ?? throw new InvalidOperationException(c.Name), c.Bounds.Size);

        [Theory]
        [MemberData(nameof(Screens))]
        public Task The_card_holds_eight_rows_whole_at_the_left_with_the_detail_beside_it_and_passes_the_fit_audit(int width, int height) => UiTest.Run(() =>
        {
            var (window, layout, rows, pane, _) = Card(width, height);
            double u = height / 800.0;
            MenuPanel panel = layout.CardPanel;
            Assert.True(panel.IsEffectivelyVisible);
            Rect box = panel.PanelBounds;
            Assert.Equal(0, box.Left, 0.5);
            Assert.Equal(width * AdaptiveLayout.CardShare, box.Width, 0.5);
            Assert.Equal(height, box.Height, 0.5);
            Assert.Equal(MenuScrollIndicator.None, panel.ScrollIndicator);
            Assert.True(panel.IsVisualAncestorOf(rows), "the content is in the card's panel");
            Rect paneRoom = panel.PaneBounds;
            Assert.True(paneRoom.Left >= box.Right + 64 * u - 0.5 && paneRoom.Right <= width - 64 * u + 0.5 && paneRoom.Width > width * 0.45, $"pane room {paneRoom} beside {box}");
            Assert.True(paneRoom.Contains(In(pane, panel)), $"the detail {In(pane, panel)} outside its room {paneRoom}");
            foreach (MenuRow r in rows.GetVisualDescendants().OfType<MenuRow>()) output.WriteLine($"{width}x{height}: row {In(r, panel)} in {box}");
            Assert.Equal(8, rows.GetVisualDescendants().OfType<MenuRow>().Count(r => r.IsEffectivelyVisible && box.Inflate(0.5).Contains(In(r, panel))));
            List<string> faults = FitAudit.Check(layout);
            foreach (string f in faults) output.WriteLine($"{width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

        // The design's card (§210): the panel the full height at the left edge, the title row and rows inside the safe area, the pane on a raised surface beside them, the help bar a band across the foot.
        [Theory]
        [MemberData(nameof(Screens))]
        public Task The_card_stands_where_the_design_puts_it(int width, int height) => UiTest.Run(() =>
        {
            var (window, layout, rows, _, _) = Card(width, height);
            double u = height / 800.0, panelWidth = width * AdaptiveLayout.CardShare;
            MenuPanel panel = layout.CardPanel;
            Assert.Equal(new Rect(0, 0, panelWidth, height), panel.PanelBounds, new RectComparer(0.5));
            Assert.Equal(new Rect(64 * u, 40 * u, panelWidth - 88 * u, 100 * u), panel.TitleBounds, new RectComparer(0.5));
            Rect list = In(rows, panel);
            Assert.Equal(64 * u, list.X, 0.5);
            Assert.Equal(156 * u, list.Y, 0.5);
            Assert.Equal(panelWidth - 88 * u, list.Width, 0.5);
            Assert.Equal(new Rect(panelWidth + 64 * u, 156 * u, width - panelWidth - 128 * u, height - 270 * u), panel.PaneBounds, new RectComparer(0.5));
            Rect help = In(panel.HelpBar, panel);
            Assert.Equal(64 * u, help.X, 0.5);
            // The band reaches the screen's foot, its entries centred in all of it (decided 2026-11-02 by the tester, docs/LunaP.md §210).
            Assert.True(help.Y >= height - 98 * u - 0.5 && help.Bottom <= height + 0.5, $"the help bar {help} in its band");
            Assert.Equal(height - 49 * u, help.Y + help.Height / 2, 0.5);
            Assert.Equal(LetterCase.None, panel.HelpBar.LetterCase);
            Border surface = panel.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "AdaptiveCardPane");
            Assert.Equal(panel.PaneBounds, In(surface, panel), new RectComparer(0.5));
            Assert.Equal(Color.Parse("#26262B"), ((ISolidColorBrush)surface.Background!).Color);
            Assert.Equal(8 * u, surface.CornerRadius.TopLeft, 0.01);
            MenuRow row = rows.GetVisualDescendants().OfType<MenuRow>().First();
            Assert.True(row.FilledMarks);
            Assert.Equal(Color.Parse("#8D8D91"), row.MutedColor);
            Assert.Equal(24, row.Inset);
            window.Close();
        });

        // Whatever is under the card, the brightest or the darkest, every word on it and on its pane keeps WCAG's 4.5:1 against what is actually behind it (§210).
        [Theory]
        [InlineData(1280, 800, "#FFFFFF")]
        [InlineData(1280, 800, "#000000")]
        [InlineData(1920, 1080, "#FFFFFF")]
        [InlineData(1920, 1080, "#000000")]
        public Task Every_word_on_the_card_reads_over_a_white_or_a_black_screen(int width, int height, string under) => UiTest.Run(() =>
        {
            var (window, layout, rows, pane, _) = Card(width, height);
            window.Background = new SolidColorBrush(Color.Parse(under));
            var quiet = new TextBlock { Text = "Saved 3 days ago", FontSize = 20 * height / 800.0 };
            quiet.Bind(TextBlock.ForegroundProperty, quiet.GetResourceObservable("LunaMuted"));
            ((StackPanel)pane).Children.Add(quiet);
            layout.SetActions(new[] { new LunaAction("Screenshot", () => { }) { PadButton = PadGlyphButton.West } });
            UiTest.Settle(layout);
            RenderedFrame frame = UiTest.Capture(window);
            MenuPanel panel = layout.CardPanel;
            var runs = new List<(string What, Color Ink, Rect Box)> { ("title", panel.TitleColor, panel.TitleBounds) };
            foreach (Visual v in layout.GetVisualDescendants().Where(v => v.IsEffectivelyVisible))
            {
                if (v is TextBlock { Text.Length: > 0 } t && t.Foreground is ISolidColorBrush b) runs.Add(($"\"{t.Text}\"", b.Color, In(t, window)));
                if (v is MenuRow r) runs.Add(($"row {r.Label}", r.IsHighlighted ? r.HighlightTextColor : r.TextColor, In(r, window)));
                if (v is MenuRow { MutedColor: { } m, IsHighlighted: false } q) runs.Add(($"row {q.Label}'s value", m, In(q, window)));
            }
            List<string> faults = [];
            foreach ((string what, Color ink, Rect box) in runs)
            {
                Color behind = MostOften(frame, box);
                double ratio = Contrast(ink, behind);
                output.WriteLine($"{width}x{height} over {under}: {what} {ink} on {behind} {ratio:F2}:1");
                if (ratio < 4.5) faults.Add($"{what} is {ratio:F2}:1, {ink} on {behind}");
            }
            window.Close();
            Assert.True(faults.Count == 0, string.Join("\n", faults));
        });

        // The colour most often under a box, which is what its words are read against.
        private static Color MostOften(RenderedFrame frame, Rect box)
        {
            var counts = new Dictionary<int, int>();
            for (int y = Math.Max(0, (int)box.Y); y < Math.Min(frame.Height, (int)box.Bottom); y++)
                for (int x = Math.Max(0, (int)box.X); x < Math.Min(frame.Width, (int)box.Right); x++)
                {
                    int i = (y * frame.Width + x) * 4, key = frame.Rgba[i] | frame.Rgba[i + 1] << 8 | frame.Rgba[i + 2] << 16;
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }
            int best = counts.Count == 0 ? 0 : counts.MaxBy(p => p.Value).Key;
            return Color.FromRgb((byte)best, (byte)(best >> 8), (byte)(best >> 16));
        }

        private static double Contrast(Color a, Color b)
        {
            static double L(byte v) { double c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
            static double Lum(Color c) => 0.2126 * L(c.R) + 0.7152 * L(c.G) + 0.0722 * L(c.B);
            double la = Lum(a), lb = Lum(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        private sealed class RectComparer(double slack) : IEqualityComparer<Rect>
        {
            public bool Equals(Rect a, Rect b) => Math.Abs(a.X - b.X) <= slack && Math.Abs(a.Y - b.Y) <= slack && Math.Abs(a.Width - b.Width) <= slack && Math.Abs(a.Height - b.Height) <= slack;
            public int GetHashCode(Rect r) => 0;
        }

        [Fact]
        public Task Navigation_search_and_the_action_buttons_are_not_drawn_and_the_actions_are_in_the_help_bar() => UiTest.Run(() =>
        {
            var (window, layout, _, _, _) = Card(1280, 800);
            Assert.Null(layout.HostOf(AdaptiveRole.Navigation));
            Assert.Null(layout.HostOf(AdaptiveRole.Actions));
            Assert.Null(layout.HostOf(AdaptiveRole.Header));
            Assert.NotNull(layout.HostOf(AdaptiveRole.Content));
            Assert.NotNull(layout.HostOf(AdaptiveRole.Detail));
            Assert.Equal(new[] { "Select", "Back", "Screenshot" }, layout.CardPanel.Hints!.Select(h => h.Label));
            Assert.Equal(new PadGlyphButton?[] { PadGlyphButton.South, PadGlyphButton.East, PadGlyphButton.West }, layout.CardPanel.Hints!.Select(h => h.Button));
            layout.AcceptWord = "Choose";
            layout.BackWord = "Resume";
            Assert.Equal(new[] { "Choose", "Resume", "Screenshot" }, layout.CardPanel.Hints!.Select(h => h.Label));
            Assert.False(layout.IsDetailOpen);
            window.Close();
        });

        // A slid card moves its panel's rows left past the edge and its help bar down past the foot, lays nothing out again and fades the pane where it stands; at rest the transforms are gone and the pane whole (docs/LunaP.md §218).
        [Fact]
        public Task A_slid_card_moves_its_rows_and_help_out_and_at_rest_is_laid_out_as_before() => UiTest.Run(() =>
        {
            var (window, layout, rows, pane, _) = Card(1280, 800);
            MenuPanel panel = layout.CardPanel;
            Rect rowsAt = In(rows, window), paneAt = In(pane, window), panelAt = panel.PanelBounds;
            HintBar help = panel.GetVisualDescendants().OfType<HintBar>().First();
            Rect helpAt = In(help, window);
            panel.SlideOut = 1;
            UiTest.Settle(layout);
            Assert.Equal(panelAt, panel.PanelBounds);
            Assert.True(rows.TranslatePoint(new Point(rows.Bounds.Width, 0), window)!.Value.X <= 0, "the rows are past the left edge");
            Assert.True(help.TranslatePoint(default, window)!.Value.Y >= 800, "the help bar is past the foot");
            Assert.Equal(paneAt, In(pane, window));
            Control paneHost = panel.Pane!;
            Assert.Equal(0.0, paneHost.Opacity);
            panel.SlideOut = 0.5;
            UiTest.Settle(layout);
            Assert.InRange(rows.TranslatePoint(default, window)!.Value.X, -panelAt.Right, rowsAt.X - 1);
            Assert.Equal(0.5, paneHost.Opacity);
            panel.SlideOut = 0;
            UiTest.Settle(layout);
            Assert.Null(rows.RenderTransform);
            Assert.Null(help.RenderTransform);
            Assert.Equal(1.0, paneHost.Opacity);
            Assert.Equal((rowsAt, helpAt), (In(rows, window), In(help, window)));
            window.Close();
        });

        // Left and Right's word comes second, after A and before B, as the pause card's pages draw it (docs/LunaP.md §217).
        [Fact]
        public Task The_side_word_follows_A_and_comes_before_B() => UiTest.Run(() =>
        {
            var (window, layout, _, _, _) = Card(1280, 800);
            layout.AcceptWord = "Go back";
            layout.SideWord = "Moment";
            Assert.Equal(new[] { "Go back", "Moment", "Back", "Screenshot" }, layout.CardPanel.Hints!.Select(h => h.Label));
            Assert.Equal(PadGlyphButton.DPadLeftRight, layout.CardPanel.Hints![1].Button);
            layout.SideWord = null;
            Assert.Equal(new[] { "Go back", "Back", "Screenshot" }, layout.CardPanel.Hints!.Select(h => h.Label));
            window.Close();
        });

        [Fact]
        public Task The_pad_s_shoulders_do_nothing_on_a_card_B_is_the_host_s_and_an_action_s_button_invokes_it() => UiTest.Run(() =>
        {
            var (window, layout, _, _, invoked) = Card(1280, 800);
            Assert.False(layout.Press(PadGlyphButton.RightShoulder));
            Assert.Equal("a", layout.SelectedSection);
            Assert.False(layout.Press(PadGlyphButton.South));
            Assert.False(layout.Press(PadGlyphButton.East));
            Assert.True(layout.Press(PadGlyphButton.West));
            Assert.Equal(new[] { "Screenshot" }, invoked);
            window.Close();
        });

        [Fact]
        public Task With_the_swap_the_help_bar_names_East_as_Select_and_South_as_Back() => UiTest.Run(() =>
        {
            var (window, layout, _, _, _) = Card(1280, 800);
            layout.SwapsAcceptAndBack = true;
            UiTest.Settle(layout);
            Assert.Equal(new PadGlyphButton?[] { PadGlyphButton.East, PadGlyphButton.South, PadGlyphButton.West }, layout.CardPanel.Hints!.Select(h => h.Button));
            window.Close();
        });

        [Theory]
        [MemberData(nameof(Screens))]
        public Task The_header_is_the_card_s_title_row_one_line_after_the_picture_shrunk_and_never_wrapped(int width, int height) => UiTest.Run(() =>
        {
            var (window, layout, _, _, _) = Card(width, height);
            MenuPanel panel = layout.CardPanel;
            Assert.Equal(LongName, panel.Title);
            Assert.NotNull(panel.TitleIcon);
            Assert.True(panel.TitleIconBounds.Width > 0);
            Assert.Equal(1, panel.TitleLines);
            Assert.True(panel.TitleDrawnSize < 32 * height / 800.0, "a long name shrinks");
            layout.Header = new AdaptiveHeader("Game Options", Subtitle: "‹ Cobalt Harbor");
            UiTest.Settle(layout);
            Assert.Equal("Game Options", panel.Title);
            Assert.Equal("‹ Cobalt Harbor", panel.Subtitle);
            Assert.Null(panel.TitleIcon);
            Assert.Null(panel.Footer);
            Assert.Empty(FitAudit.Check(layout));
            window.Close();
        });

        [Fact]
        public Task The_rows_of_a_card_are_in_Barlow_at_the_big_screen_s_row_size_in_their_own_case() => UiTest.Run(() =>
        {
            var (window, layout, rows, _, _) = Card(1280, 800);
            MenuRow row = rows.GetVisualDescendants().OfType<MenuRow>().First();
            Assert.Equal(26, row.TextSize);
            Assert.Equal(LetterCase.None, row.LetterCase);
            Assert.Equal(LunaFonts.BodyFile, row.FontPath);
            Assert.Equal(LunaFonts.CondensedFile, MenuPanel.GetFontPath(layout.CardPanel));
            window.Close();
        });

        [Fact]
        public Task A_card_turned_into_the_big_screen_and_back_keeps_its_content_and_detail_and_drops_the_card_s_styles() => UiTest.Run(() =>
        {
            var (window, layout, rows, pane, _) = Card(1280, 800);
            layout.Form = AdaptiveForm.BigScreen;
            UiTest.Settle(layout);
            Assert.False(layout.CardPanel.IsVisualAncestorOf(rows));
            Assert.True(layout.IsVisualAncestorOf(rows));
            Assert.False(pane.IsEffectivelyVisible);
            Assert.Equal(36, rows.GetVisualDescendants().OfType<MenuRow>().First().TextSize);
            Assert.NotNull(layout.HostOf(AdaptiveRole.Header));
            layout.Form = AdaptiveForm.Card;
            UiTest.Settle(layout);
            Assert.True(layout.CardPanel.IsVisualAncestorOf(rows));
            Assert.True(layout.CardPanel.IsVisualAncestorOf(pane));
            Assert.True(pane.IsEffectivelyVisible);
            window.Close();
        });

        [Theory]
        [InlineData(AdaptiveForm.Desktop)]
        [InlineData(AdaptiveForm.BigScreen)]
        public Task Outside_a_card_the_header_is_a_title_row_above_everything_and_null_hides_it(AdaptiveForm form) => UiTest.Run(() =>
        {
            var (window, layout, _, _, _) = Card(1280, 800, form: form);
            Control header = layout.HostOf(AdaptiveRole.Header)!;
            Assert.NotNull(header);
            Rect top = In(header, layout);
            foreach (AdaptiveRole role in new[] { AdaptiveRole.Navigation, AdaptiveRole.Content })
                if (layout.HostOf(role) is { } other) Assert.True(In(other, layout).Top >= top.Bottom - 0.5, $"{role} under the header");
            Assert.Contains(header.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == LongName && t.TextWrapping == TextWrapping.NoWrap);
            Assert.Contains(header.GetVisualDescendants().OfType<Image>(), i => i.Source is not null);
            Assert.Empty(FitAudit.Check(header, null, form == AdaptiveForm.BigScreen ? FitAudit.SmallestText : 0));
            layout.Header = null;
            UiTest.Settle(layout);
            Assert.Null(layout.HostOf(AdaptiveRole.Header));
            window.Close();
        });
        // The layout's child is the layers panel: the arranged form first, the List card or a cover above it when open; the Card form's dim is its scrim, painting LunaScrim across the layout beneath the card (§208.12).
        [Fact]
        public Task The_cards_scrim_paints_the_dim_across_the_layout_beneath_the_card() => UiTest.Run(() =>
        {
            var (window, layout, _, _, _) = Card(1280, 800);
            var layers = Assert.IsType<Panel>(layout.Child);
            Assert.Equal("AdaptiveLayers", layers.Name);
            var scrim = Assert.IsType<Panel>(layers.Children[0]);
            Assert.Equal("AdaptiveCardScrim", scrim.Name);
            Assert.Equal(Color.Parse("#8C000000"), Assert.IsAssignableFrom<ISolidColorBrush>(scrim.Background).Color);
            Assert.Equal(new Rect(0, 0, 1280, 800), In(scrim, layout));
            Assert.Single(layers.Children);
            window.Close();
        });
    }
}
