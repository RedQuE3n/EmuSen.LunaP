using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;
using Xunit.Abstractions;

namespace EmuSen.LunaP.Tests
{
    // An item's page in both forms: the hero and its poster slot, the actions, the sections, rows, tiles and text, the pad's walk and the help bar - see docs/LunaP.md §209.
    public class PageLayoutTests(ITestOutputHelper output)
    {
        public static TheoryData<int, int> Screens() => new() { { 1280, 800 }, { 1920, 1200 }, { 1920, 1080 }, { 1280, 720 } };

        private static readonly string[] Contents = ["text", "rows", "tiles", "question", "covers"];

        public static TheoryData<string, int, int> Cases()
        {
            var data = new TheoryData<string, int, int>();
            foreach (string c in Contents)
                foreach ((int w, int h) in new[] { (1280, 800), (1920, 1200), (1920, 1080), (1280, 720) }) data.Add(c, w, h);
            return data;
        }

        private static WriteableBitmap Picture(int w, int h) => new(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        private static PageContent ContentOf(string which) => which switch
        {
            "text" => new PageContent.Text(string.Join(" ", Enumerable.Repeat("A game made for the tests, with a description long enough to wrap over several lines of the overview.", 6)), Picture(320, 224)),
            "rows" => new PageContent.Rows(Enumerable.Range(1, 9).Select(i => new PageRow($"r{i}", $"Row {i}") { Status = i == 1 ? "Diamond (EmuSen, default)" : "Off", Kind = i == 1 ? PageRowKind.Steps : PageRowKind.Opens, Choices = i == 1 ? ["Diamond (EmuSen, default)", "Other"] : null }).ToList()),
            "tiles" => new PageContent.Tiles(Enumerable.Range(1, 8).Select(i => new PageTile($"t{i}", $"Slot {i}") { Status = "Marble Gallery · 2 h ago", Picture = i < 5 ? Picture(256, 224) : null, IsEmpty = i >= 5, IsMarked = i == 3 }).ToList(), 200, 150),
            "question" => new PageContent.Rows([new PageRow("m1", "Granite Choir (1994, Super Nintendo)"), new PageRow("m2", "Granite Choir II (1996, Super Nintendo)"), new PageRow("other", "Search by another name…") { Kind = PageRowKind.Text }, new PageRow("no", "No")], "Fetches the name, the facts, the cover and the screenshots of the match chosen."),
            _ => new PageContent.Tiles(Enumerable.Range(1, 6).Select(i => new PageTile($"c{i}", $"Cover {i}") { Caption = "Granite Choir", Mark = "SNES" }).ToList(), 150, 200),
        };

        private static (Window Window, PageLayout Page, List<string> Said) Page(int width, int height, string content = "rows", AdaptiveForm form = AdaptiveForm.BigScreen)
        {
            var said = new List<string>();
            var page = new PageLayout { Form = form };
            page.Hero = new PageHero("Granite Choir", "Super Nintendo · 1994 · Studio A · Publisher P · 14 h 20 min · Last played yesterday", Picture(1280, 360), null, 4 / 3.0, "SNES");
            page.SetActions([
                new PageAction("play", "Play") { PadButton = PadGlyphButton.South, IsMain = true },
                new PageAction("resume", "Resume") { Value = "Slot 3 · 3 days ago", Choices = ["Slot 1 · 2 h ago", "Slot 3 · 3 days ago"] },
                new PageAction("favourite", "♥ Favourite") { PadButton = PadGlyphButton.North },
            ]);
            page.SetSections([new AdaptiveSection("overview", "Overview"), new AdaptiveSection("states", "States", Status: "4"), new AdaptiveSection("options", "Options"), new AdaptiveSection("media", "Media"), new AdaptiveSection("details", "Details")]);
            page.Content = ContentOf(content);
            page.HintsFor = f => [new PageHint(PadGlyphButton.South, f.InActions ? "Play" : "Choose"), new PageHint(PadGlyphButton.North, "Favourite", "favourite"), new PageHint(PadGlyphButton.Shoulders, "Section"), new PageHint(PadGlyphButton.East, "Back"), new PageHint(PadGlyphButton.Start, "Home", "home")];
            page.Back = () => { said.Add("back"); return true; };
            page.ActionInvoked += k => said.Add("action " + k);
            page.ActionStepped += (k, by) => said.Add($"step {k} {by}");
            page.RowChosen += k => said.Add("row " + k);
            page.RowStepped += (k, by) => said.Add($"rowstep {k} {by}");
            page.TileChosen += k => said.Add("tile " + k);
            page.SectionChosen += k => said.Add("section " + k);
            page.HintInvoked += k => said.Add("hint " + k);
            page.ViewerStepped += by => said.Add($"viewer {by}");
            MenuPanel.SetScale(page, height / PageLayout.DesignHeight);
            var window = new Window { Width = width, Height = height, Content = page };
            window.Show();
            UiTest.Settle(page);
            return (window, page, said);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public Task Every_content_fits_the_big_screen_whole_at_each_size(string content, int width, int height) => UiTest.Run(() =>
        {
            var (window, page, _) = Page(width, height, content);
            page.FocusContent();
            UiTest.Settle(page);
            List<string> faults = FitAudit.Check(page);
            foreach (string f in faults) output.WriteLine($"{content} {width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

        [Theory]
        [InlineData("text")]
        [InlineData("rows")]
        [InlineData("tiles")]
        public Task Every_content_fits_the_desktop_content_area(string content) => UiTest.Run(() =>
        {
            var (window, page, _) = Page(1080, 772, content, AdaptiveForm.Desktop);
            if (content == "rows") page.Content = new PageContent.Rows(((PageContent.Rows)page.Content!).Items.Take(5).Select(r => r with { Sentence = "What the row changes, in a sentence." }).ToList());
            UiTest.Settle(page);
            List<string> faults = FitAudit.Check(page, smallest: 0);
            foreach (string f in faults) output.WriteLine($"{content}: {f}");
            Assert.NotNull(page.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "PageBack"));
            window.Close();
            Assert.Empty(faults);
        });

        [Theory]
        [MemberData(nameof(Screens))]
        public Task The_hero_the_cover_the_actions_and_the_content_are_where_the_design_puts_them(int width, int height) => UiTest.Run(() =>
        {
            var (window, page, _) = Page(width, height);
            double u = height / PageLayout.DesignHeight;
            Rect At(string name) { Control c = page.GetVisualDescendants().OfType<Control>().First(x => x.Name == name); return new Rect(c.TranslatePoint(default, page)!.Value, c.Bounds.Size * u); }
            Assert.Equal(new Rect(0, 0, width, PageLayout.HeroHeight * u), At("PagePoster"), new RectComparer(1.5));
            Assert.Equal(PageLayout.Side * u, At("PageCover").X, 1.5);
            Assert.Equal(180 * u, At("PageCover").Y, 1.5);
            Assert.True(At("PageCover").Bottom <= PageLayout.ContentTop * u - 8 * u, "the cover stays clear of the content");
            Assert.Equal(PageLayout.TitleLeft * u, At("PageActions").X, 1.5);
            Assert.Equal(PageLayout.ContentTop * u, At("PageContent").Y, 1.5);
            Assert.Equal(PageLayout.HelpBarTop * u, At("PageHelpBar").Y, 1.5);
            window.Close();
        });

        [Fact]
        public Task The_pad_walks_the_actions_steps_resume_and_goes_down_into_the_rows_and_back() => UiTest.Run(() =>
        {
            var (window, page, said) = Page(1280, 800);
            Assert.Equal(new PageFocus(true, "play"), page.Focused);
            Assert.True(page.Move(1, 0));
            Assert.Equal("resume", page.Focused.Key);
            Assert.True(page.Move(-1, 0), "Left on Resume steps it back to Slot 1");
            Assert.Equal("step resume -1", said[^1]);
            Assert.True(page.Move(1, 0), "past Resume's last value Right moves on");
            Assert.Equal("favourite", page.Focused.Key);
            Assert.False(page.Move(1, 0));
            Assert.True(page.Press(PadGlyphButton.South));
            Assert.Equal("action favourite", said[^1]);
            Assert.True(page.Move(0, 1));
            Assert.Equal(new PageFocus(false, "r1"), page.Focused);
            Assert.True(page.Move(1, 0));
            Assert.Equal("rowstep r1 1", said[^1]);
            for (int i = 0; i < 6; i++) page.Move(0, 1);
            Assert.Equal("r7", page.Focused.Key);
            UiTest.Settle(page);
            Assert.Contains(page.GetVisualDescendants().OfType<MenuRow>(), r => r.Name == "PageRow.r7" && r.IsHighlighted);
            Assert.Equal(4, page.GetVisualDescendants().OfType<MenuRow>().Count());
            Assert.True(page.Press(PadGlyphButton.South));
            Assert.Equal("row r7", said[^1]);
            for (int i = 0; i < 7; i++) page.Move(0, -1);
            Assert.True(page.Focused.InActions);
            Assert.Equal("Play", page.Hints[0].Label);
            window.Close();
        });

        [Fact]
        public Task The_shoulders_step_the_sections_a_deeper_page_declines_them_and_B_is_the_hosts() => UiTest.Run(() =>
        {
            var (window, page, said) = Page(1280, 800);
            Assert.True(page.Press(PadGlyphButton.RightShoulder));
            Assert.Equal("section states", said[^1]);
            Assert.Equal("states", page.SelectedSection);
            Assert.True(page.Press(PadGlyphButton.LeftShoulder));
            Assert.True(page.Press(PadGlyphButton.LeftShoulder));
            Assert.Equal("details", page.SelectedSection);
            page.ShowDeeper("Options", "Cheats", new PageContent.Rows([new PageRow("c1", "Infinite lives") { Kind = PageRowKind.Steps, Status = "On" }]));
            Assert.True(page.IsDeeper);
            Assert.False(page.Press(PadGlyphButton.RightShoulder));
            Assert.Equal(new PageFocus(false, "c1"), page.Focused);
            UiTest.Settle(page);
            Assert.Equal(new[] { "OPTIONS", "CHEATS" }, page.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Select(b => (string)b.Content!).ToArray());
            Assert.True(page.Press(PadGlyphButton.East));
            Assert.Equal("back", said[^1]);
            Assert.True(page.Press(PadGlyphButton.North));
            Assert.Equal("hint favourite", said[^1]);
            Assert.False(page.Press(PadGlyphButton.West), "a button no hint gives a key is declined");
            window.Close();
        });

        [Fact]
        public Task Tiles_step_across_whole_and_up_returns_to_the_actions() => UiTest.Run(() =>
        {
            var (window, page, said) = Page(1280, 800, "tiles");
            page.FocusContent();
            for (int i = 0; i < 6; i++) Assert.True(page.Move(1, 0));
            UiTest.Settle(page);
            Assert.Equal("t7", page.Focused.Key);
            Assert.Equal(5, page.GetVisualDescendants().OfType<Panel>().Count(p => p.Name?.StartsWith("PageTile.", StringComparison.Ordinal) == true));
            Assert.Empty(FitAudit.Check(page));
            Assert.True(page.Press(PadGlyphButton.South));
            Assert.Equal("tile t7", said[^1]);
            Assert.True(page.Move(0, -1));
            Assert.True(page.Focused.InActions);
            window.Close();
        });

        [Fact]
        public Task The_viewer_covers_the_page_steps_with_left_and_right_and_closes_on_B() => UiTest.Run(() =>
        {
            var (window, page, said) = Page(1280, 800, "tiles");
            page.Back = () => false;
            page.ShowViewer(Picture(512, 448), "Screenshot 2 of 5 · 1 November 2026 · 512 × 448");
            UiTest.Settle(page);
            Assert.True(page.IsViewerOpen);
            Assert.Empty(FitAudit.Check(page));
            Assert.True(page.Move(1, 0));
            Assert.Equal("viewer 1", said[^1]);
            Assert.True(page.Press(PadGlyphButton.East));
            Assert.False(page.IsViewerOpen);
            window.Close();
        });

        [Fact]
        public Task A_section_status_is_heard_with_its_name_and_the_poster_slot_waits_for_a_video_surface() => UiTest.Run(() =>
        {
            var (window, page, _) = Page(1280, 800);
            Assert.Contains(page.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "States, 4");
            Assert.False(page.Poster.PlaysClip);
            Assert.Equal(TimeSpan.FromSeconds(1.5), PosterSlot.PosterDelay);
            Assert.False(page.Poster.IsHitTestVisible);
            window.Close();
        });

        private sealed class RectComparer(double slack) : IEqualityComparer<Rect>
        {
            public bool Equals(Rect a, Rect b) => Math.Abs(a.X - b.X) <= slack && Math.Abs(a.Y - b.Y) <= slack && Math.Abs(a.Width - b.Width) <= slack && Math.Abs(a.Height - b.Height) <= slack;
            public int GetHashCode(Rect r) => 0;
        }
    }
}
