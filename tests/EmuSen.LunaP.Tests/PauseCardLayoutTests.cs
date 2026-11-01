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
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Testing;
using Xunit;
using Xunit.Abstractions;

namespace EmuSen.LunaP.Tests
{
    // A menu anchored left with a pane beside it, a cover in its title, a backdrop that dims without blurring, and the detail beside the content on the big screen - see docs/LunaP.md §205.
    public class PauseCardLayoutTests(ITestOutputHelper output)
    {
        private const string LongName = "The Legend of Zelda - Ocarina of Time - Master Quest (USA) (Debug Edition) (GameCube) (Rev 2)";

        public static TheoryData<int, int> Screens() => new() { { 1280, 800 }, { 1920, 1200 }, { 1280, 720 } };

        private static WriteableBitmap Cover() => new(new PixelSize(30, 42), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        private static (Window Window, MenuPanel Panel, Control Pane) Card(int width, int height, int rows = 8)
        {
            var list = new StackPanel();
            for (int i = 0; i < rows; i++) list.Children.Add(new MenuRow { Label = $"Row {i + 1}", Value = i == 2 ? "Slot 3" : null });
            double u = height / 800.0;
            var pane = new StackPanel
            {
                Name = "Pane", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 8,
                Children = { new Border { Width = 200, Height = 150, Background = Brushes.DimGray }, new TextBlock { Text = "Slot 3 - Saved Sat 1 Nov, 14:32", Foreground = Brushes.White, FontSize = 22 * u } },
            };
            var panel = new MenuPanel
            {
                Anchor = MenuAnchor.Left, WidthFraction = 0.4, TitleIcon = Cover(), Title = LongName, TitleSize = 40, TitleMinScale = 0.55, LetterCase = LetterCase.None,
                Child = new ScrollViewer { Content = list }, Pane = pane,
                Hints = new[] { new HintEntry("Select") { Button = PadGlyphButton.South }, new HintEntry("Back") { Button = PadGlyphButton.East }, new HintEntry("Screenshot") { Button = PadGlyphButton.West } },
            };
            MenuPanel.SetScale(panel, u);
            var window = new Window { Width = width, Height = height, Content = panel };
            window.Show();
            UiTest.Settle(panel);
            return (window, panel, pane);
        }

        [Theory]
        [MemberData(nameof(Screens))]
        public Task A_left_card_holds_eight_rows_whole_with_the_pane_beside_it_and_passes_the_fit_audit(int width, int height) => UiTest.Run(() =>
        {
            var (window, panel, pane) = Card(width, height);
            double u = height / 800.0;
            Rect box = panel.PanelBounds;
            Assert.Equal(24 * u, box.Left, 0.5);
            Assert.Equal(Math.Min(width * 0.4, height * 1.05), box.Width, 0.5);
            Assert.Equal(MenuScrollIndicator.None, panel.ScrollIndicator);
            Rect paneRoom = panel.PaneBounds;
            Assert.True(paneRoom.Left >= box.Right && paneRoom.Right <= width && paneRoom.Width > width * 0.5, $"pane room {paneRoom} beside {box}");
            Rect drawn = new(pane.TranslatePoint(default, panel)!.Value, pane.Bounds.Size);
            Assert.True(paneRoom.Contains(drawn), $"pane {drawn} outside its room {paneRoom}");
            Rect help = new(panel.HelpBar.TranslatePoint(default, panel)!.Value, panel.HelpBar.Bounds.Size);
            Assert.True(help.Top >= paneRoom.Bottom && help.Top > box.Bottom, $"help {help} under pane {paneRoom} and panel {box}");
            List<string> faults = FitAudit.Check(panel);
            foreach (string f in faults) output.WriteLine($"{width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

        [Theory]
        [MemberData(nameof(Screens))]
        public Task The_title_is_one_line_after_the_cover_shrunk_and_never_wrapped(int width, int height) => UiTest.Run(() =>
        {
            var (window, panel, _) = Card(width, height);
            double u = height / 800.0;
            Rect icon = panel.TitleIconBounds;
            Assert.True(icon.Width > 0 && panel.TitleBounds.Contains(icon), $"icon {icon} in band {panel.TitleBounds}");
            Assert.Equal(1, panel.TitleLines);
            Assert.True(panel.TitleDrawnSize < 40 * u, $"title drawn at {panel.TitleDrawnSize}, not shrunk");
            Assert.True(panel.TitleDrawnSize >= 40 * u * 0.55 - 0.01);
            // A name that fits shrunk takes less room after the cover than without it.
            panel.Title = "Super Mario 64 (USA) (Rev 1)";
            UiTest.Settle(panel);
            double withCover = panel.TitleDrawnSize;
            panel.TitleIcon = null;
            UiTest.Settle(panel);
            Assert.Equal(default, panel.TitleIconBounds);
            Assert.True(withCover < panel.TitleDrawnSize - 1, $"with the cover {withCover}, without {panel.TitleDrawnSize}");
            window.Close();
        });

        [Fact]
        public Task The_centred_panel_is_unchanged_and_its_pane_takes_the_room_right_of_it() => UiTest.Run(() =>
        {
            var pane = new Border { Width = 100, Height = 80, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var panel = new MenuPanel { Title = "Main Menu", Child = new StackPanel { Children = { new MenuRow { Label = "One" } } }, Pane = pane };
            var window = new Window { Width = 1280, Height = 800, Content = panel };
            window.Show();
            UiTest.Settle(panel);
            Assert.Equal(640, panel.PanelBounds.Center.X, 0.01);
            Assert.Equal(panel.PanelBounds.Right + 24, panel.PaneBounds.Left, 0.01);
            Assert.Equal(1280 - 24, panel.PaneBounds.Right, 0.01);
            Assert.Equal(panel.PaneBounds.TopLeft, new Rect(pane.TranslatePoint(default, panel)!.Value, pane.Bounds.Size).TopLeft);
            window.Close();
        });

        [Fact]
        public Task A_backdrop_that_does_not_blur_only_shades_and_blurs_again_when_the_flag_is_set() => UiTest.Run(() =>
        {
            var screen = new Border { Background = Brushes.SteelBlue };
            var backdrop = new BlurBackdrop { Target = screen, Blurs = false };
            var window = new Window { Width = 200, Height = 200, Content = new Grid { Children = { screen, backdrop } } };
            window.Show();
            UiTest.Settle(backdrop);
            Assert.False(backdrop.IsBlurring);
            Assert.Null(screen.Effect);
            backdrop.Blurs = true;
            Assert.True(backdrop.IsBlurring);
            backdrop.Blurs = false;
            Assert.Null(screen.Effect);
            window.Close();
        });

        private static (Window Window, AdaptiveLayout Layout) Beside(AdaptiveForm form, int width, int height)
        {
            var layout = new AdaptiveLayout { Title = "Pause", Form = form, DetailPlacement = AdaptiveDetailPlacement.Beside };
            layout.SetActions(new[] { new Commands.LunaAction("Screenshot", () => { }) { PadButton = PadGlyphButton.West } });
            layout.Content = new ListBox { ItemsSource = new[] { "Resume", "Rewind", "Save", "Load" } };
            layout.Detail = new StackPanel { Children = { new TextBlock { Text = "Slot 3 - Saved Sat 1 Nov, 14:32" }, new Button { Content = "Older" } } };
            var window = new Window { Width = width, Height = height, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            return (window, layout);
        }

        [Theory]
        [MemberData(nameof(Screens))]
        public Task On_the_big_screen_the_detail_stands_beside_the_content_and_passes_the_fit_audit(int width, int height) => UiTest.Run(() =>
        {
            var (window, layout) = Beside(AdaptiveForm.BigScreen, width, height);
            Control content = layout.HostOf(AdaptiveRole.Content)!, detail = layout.HostOf(AdaptiveRole.Detail)!;
            Assert.NotNull(detail);
            Rect c = new(content.TranslatePoint(default, window)!.Value, content.Bounds.Size), d = new(detail.TranslatePoint(default, window)!.Value, detail.Bounds.Size);
            Assert.True(c.Right <= d.Left && Math.Abs(c.Top - d.Top) < 0.5, $"content {c} beside detail {d}");
            Assert.Equal(0.4, c.Width / (c.Width + d.Width + 16), 2);
            List<string> faults = FitAudit.Check(layout);
            foreach (string f in faults) output.WriteLine($"{width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

        [Fact]
        public Task Beside_the_content_A_opens_no_page_the_help_says_Select_and_the_walk_reaches_the_detail() => UiTest.Run(() =>
        {
            var (window, layout) = Beside(AdaptiveForm.BigScreen, 1280, 800);
            Assert.Equal("Select", layout.Hints[0].Label);
            Assert.True(layout.FocusRole(AdaptiveRole.Content));
            Assert.False(layout.Press(PadGlyphButton.South));
            Assert.False(layout.IsDetailOpen);
            Assert.False(layout.OpenDetail());
            Assert.True(layout.MoveFocus(1));
            Assert.Equal(AdaptiveRole.Detail, layout.FocusedRole);
            Assert.True(layout.Press(PadGlyphButton.East));
            Assert.Equal(AdaptiveRole.Content, layout.FocusedRole);

            // The page placement is still the default, and the desktop ignores the placement.
            layout.DetailPlacement = AdaptiveDetailPlacement.Page;
            UiTest.Settle(layout);
            Assert.Null(layout.HostOf(AdaptiveRole.Detail));
            Assert.Equal("Open", layout.Hints[0].Label);
            layout.DetailPlacement = AdaptiveDetailPlacement.Beside;
            layout.Form = AdaptiveForm.Desktop;
            UiTest.Settle(layout);
            Control content = layout.HostOf(AdaptiveRole.Content)!, detail = layout.HostOf(AdaptiveRole.Detail)!;
            Assert.True(detail.TranslatePoint(default, window)!.Value.Y >= content.TranslatePoint(default, window)!.Value.Y + content.Bounds.Height - 0.5, "the desktop keeps the detail beneath");
            window.Close();
        });
    }
}
