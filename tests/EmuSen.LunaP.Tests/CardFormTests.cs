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
            Assert.Equal(24 * u, box.Left, 0.5);
            Assert.Equal(width * AdaptiveLayout.CardShare, box.Width, 0.5);
            Assert.Equal(MenuScrollIndicator.None, panel.ScrollIndicator);
            Assert.True(panel.IsVisualAncestorOf(rows), "the content is in the card's panel");
            Rect paneRoom = panel.PaneBounds;
            Assert.True(paneRoom.Left >= box.Right && paneRoom.Width > width * 0.5, $"pane room {paneRoom} beside {box}");
            Assert.True(paneRoom.Contains(In(pane, panel)), $"the detail {In(pane, panel)} outside its room {paneRoom}");
            foreach (MenuRow r in rows.GetVisualDescendants().OfType<MenuRow>()) output.WriteLine($"{width}x{height}: row {In(r, panel)} in {box}");
            Assert.Equal(8, rows.GetVisualDescendants().OfType<MenuRow>().Count(r => r.IsEffectivelyVisible && box.Inflate(0.5).Contains(In(r, panel))));
            List<string> faults = FitAudit.Check(layout);
            foreach (string f in faults) output.WriteLine($"{width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

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
            Assert.False(layout.IsDetailOpen);
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
    }
}
