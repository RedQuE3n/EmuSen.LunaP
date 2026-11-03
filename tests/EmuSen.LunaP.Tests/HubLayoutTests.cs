using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Avalonia.Headless;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;
using Xunit.Abstractions;

namespace EmuSen.LunaP.Tests
{
    // A hub and a gallery in their forms: where the parts go, the fit audit at each size, the pad's walk, whole covers only, and search with the keyboard docked - see docs/LunaP.md §212.
    public class HubLayoutTests(ITestOutputHelper output)
    {
        public static TheoryData<int, int> Screens() => new() { { 1280, 800 }, { 1920, 1200 }, { 1920, 1080 }, { 1280, 720 } };

        private static WriteableBitmap Picture(int w, int h) => new(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        private static readonly (string Name, double Aspect, string Mark, bool Art)[] Games =
        [
            ("Basalt Run", 1.0, "PS", true), ("Ivory Signal", 0.7, "PS2", false), ("Ember Circuit", 1.4, "N64", true), ("Cobalt Harbor", 1.0, "PS", true),
            ("Dune Relay", 0.7, "MD", false), ("Kestrel Run", 1.4, "SNES", true), ("Hollow Comet", 0.9, "GB", true), ("Aurora Drift", 1.4, "SNES", false),
            ("Juniper Vault", 0.9, "GBC", true), ("Lumen Garden", 0.7, "SCD", true), ("Granite Choir", 1.4, "SNES", true), ("Fable of Tiles", 0.72, "NES", false),
        ];

        private static List<CoverItem> Covers(int count = 12) => Enumerable.Range(0, count).Select(i =>
        {
            var g = Games[i % Games.Length];
            return new CoverItem($"g{i}", g.Name) { Aspect = g.Aspect, Mark = g.Mark, Picture = g.Art ? Picture(200, 200) : null, IsFavourite = i == 2, Facts = "Super Nintendo · 1994 · Studio A", Status = "Super Nintendo · 1994" };
        }).ToList();

        private static readonly HubPlace[] PlaceList =
        [
            new("games", "Games") { Status = "1,284 games" }, new("scraper", "Scraper") { Status = "41 no art" }, new("packages", "Packages") { Status = "3 changed" },
            new("library", "Library") { Status = "2 suspect dumps", NeedsAttention = true }, new("cores", "Cores") { Status = "1 to approve", NeedsAttention = true },
            new("settings", "Settings") { Status = "Dark · 60% sound" }, new("power", "Power") { Status = "Exit" },
        ];

        private static (Window Window, HubLayout Hub, List<string> Said) Hub(int width, int height, AdaptiveForm form = AdaptiveForm.BigScreen, bool continued = true, int covers = 12)
        {
            var said = new List<string>();
            var hub = new HubLayout { Form = form, Status = "2 controllers · 21:42", Title = "Home" };
            if (continued) hub.Continue = new HubContinue("Granite Choir", "Super Nintendo · Paused 12 minutes ago · Slot 3 saved 1 h ago", Picture(320, 224));
            hub.SetShelf(Covers(covers));
            hub.SetPlaces(form == AdaptiveForm.Desktop ? PlaceList.Take(7).Append(new HubPlace("big", "Big Picture") { Status = "Open full screen" }) : PlaceList);
            hub.HintsFor = f => f.Row switch
            {
                HubRow.Continue => [new PageHint(PadGlyphButton.South, "Resume"), new PageHint(PadGlyphButton.West, "Game page", "page"), new PageHint(PadGlyphButton.Select, "Search", "search")],
                HubRow.Shelf => [new PageHint(PadGlyphButton.South, "Play"), new PageHint(PadGlyphButton.West, "Game page", "page"), new PageHint(PadGlyphButton.North, "Favourite", "favourite")],
                _ => [new PageHint(PadGlyphButton.South, "Open"), new PageHint(PadGlyphButton.Start, "Home", "home")],
            };
            hub.ContinueChosen += () => said.Add("continue");
            hub.TileChosen += k => said.Add("tile " + k);
            hub.PlaceChosen += k => said.Add("place " + k);
            hub.HintInvoked += k => said.Add("hint " + k);
            hub.FocusStart();
            if (form != AdaptiveForm.Desktop) MenuPanel.SetScale(hub, height / HubLayout.DesignHeight);
            var window = new Window { Width = width, Height = height, Content = hub };
            window.Show();
            UiTest.Settle(hub);
            return (window, hub, said);
        }

        private static readonly FitAudit.Allowance PlaceLines = new("a place's live line is one line cut with an ellipsis", c => c.Name == HubLayout.StatusName);

        private static Rect At(Control root, string name, double u)
        {
            Control c = root.GetVisualDescendants().OfType<Control>().First(x => x.Name == name);
            return new Rect(c.TranslatePoint(default, root)!.Value, c.Bounds.Size * u);
        }

        private static void Picture(Window window, string name)
        {
            if (Environment.GetEnvironmentVariable("EMUSEN_UI_DUMP") is not { Length: > 0 }) return;
            using WriteableBitmap? frame = window.CaptureRenderedFrame();
            if (frame is not null) UiTest.Dump(name, frame);
        }

        [Theory]
        [MemberData(nameof(Screens))]
        public Task The_hub_fits_the_big_screen_with_its_parts_where_the_design_puts_them(int width, int height) => UiTest.Run(() =>
        {
            var (window, hub, _) = Hub(width, height);
            double u = height / HubLayout.DesignHeight;
            Picture(window, $"hub-{width}x{height}");
            Assert.Equal(new Rect(HubLayout.Side * u, HubLayout.ContinueTop * u, HubLayout.ContinueWidth * u, HubLayout.ContinueHeight * u), At(hub, "HubContinue", u), new RectComparer(1.5));
            Assert.Equal(HubLayout.ShelfTop * u, At(hub, "HubShelf", 1).Y, 1.5);
            Assert.Equal(HubLayout.PlacesTop * u, At(hub, "HubPlaces", 1).Y, 1.5);
            Assert.Equal(702 * u, At(hub, "HubHelpBar", 1).Y, 1.5);
            // Whole covers only: eight across 1280 units, nine across the 16:9 screen's 1422.
            Assert.Equal(width * 800 / height >= 1400 ? 9 : 8, hub.ShelfShown.Count);
            List<string> faults = FitAudit.Check(hub, [PlaceLines]);
            foreach (string f in faults) output.WriteLine($"{width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

        [Theory]
        [InlineData(1080, 772)]
        [InlineData(1720, 1148)]
        public Task The_hub_fits_the_desktop_content_area(int width, int height) => UiTest.Run(() =>
        {
            var (window, hub, _) = Hub(width, height, AdaptiveForm.Desktop);
            Picture(window, $"hub-desktop-{width}x{height}");
            Assert.Equal(width - 2 * HubLayout.DesktopGutter, At(hub, "HubContinue", 1).Width, 1.5);
            Assert.Equal(HubLayout.DesktopContinueHeight, At(hub, "HubContinue", 1).Height, 1.5);
            List<string> faults = FitAudit.Check(hub, [PlaceLines], smallest: 0);
            foreach (string f in faults) output.WriteLine($"{width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

        [Fact]
        public Task The_pad_walks_from_Continue_down_the_shelf_to_the_places_and_back() => UiTest.Run(() =>
        {
            var (window, hub, said) = Hub(1280, 800);
            Assert.Equal(new HubFocus(HubRow.Continue, null), hub.Focused);
            Assert.False(hub.Move(1, 0));
            Assert.True(hub.Move(0, 1));
            Assert.Equal(new HubFocus(HubRow.Shelf, "g0"), hub.Focused);
            for (int i = 0; i < 8; i++) Assert.True(hub.Move(1, 0));
            Assert.Equal("g8", hub.Focused.Key);
            // The ninth cover is reached whole: the window moves a cover at a time.
            Assert.Equal("g1", hub.ShelfShown[0]);
            Assert.True(hub.Move(0, 1));
            Assert.Equal(HubRow.Places, hub.Focused.Row);
            Assert.Equal("power", hub.Focused.Key);
            Assert.True(hub.Press(PadGlyphButton.South));
            Assert.True(hub.Move(0, -1));
            Assert.Equal(HubRow.Shelf, hub.Focused.Row);
            Assert.True(hub.Move(0, -1));
            Assert.True(hub.Press(PadGlyphButton.South));
            Assert.False(hub.Move(0, -1));
            Assert.Equal(["place power", "continue"], said);
            window.Close();
        });

        [Fact]
        public Task With_nothing_left_the_focus_starts_on_the_first_cover_and_the_top_shows_it() => UiTest.Run(() =>
        {
            var (window, hub, said) = Hub(1280, 800, continued: false);
            Assert.Equal(new HubFocus(HubRow.Shelf, "g0"), hub.Focused);
            Assert.Equal("Basalt Run", hub.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "HubTitle").Text);
            Assert.True(hub.Move(1, 0));
            Assert.Equal("Ivory Signal", hub.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "HubTitle").Text);
            Assert.True(hub.Press(PadGlyphButton.West));
            Assert.True(hub.Press(PadGlyphButton.South));
            Assert.False(hub.Press(PadGlyphButton.Select));
            Assert.Equal(["hint page", "tile g1"], said);
            window.Close();
        });

        [Fact]
        public Task The_help_bar_is_generated_from_what_is_focused() => UiTest.Run(() =>
        {
            var (window, hub, _) = Hub(1280, 800);
            Assert.Equal(["Resume", "Game page", "Search"], hub.Hints.Select(h => h.Label));
            hub.Move(0, 1);
            Assert.Equal(["Play", "Game page", "Favourite"], hub.Hints.Select(h => h.Label));
            Assert.Equal("Basalt Run", hub.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "HubShelfTitle").Text);
            window.Close();
        });

        [Fact]
        public Task A_place_that_needs_the_person_is_drawn_in_the_attention_colour_and_a_long_line_is_cut() => UiTest.Run(() =>
        {
            var (window, hub, _) = Hub(1280, 800);
            Control cores = hub.GetVisualDescendants().OfType<Control>().First(c => c.Name == "HubPlace.cores");
            TextBlock line = cores.GetVisualDescendants().OfType<TextBlock>().Last();
            Assert.Equal("⚠ 1 to approve", line.Text);
            Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, line.TextTrimming);
            Assert.Equal(Avalonia.Media.Color.Parse("#DAA520"), ((Avalonia.Media.ISolidColorBrush)line.Foreground!).Color);
            window.Close();
        });

        private static (Window Window, GalleryLayout Gallery, List<string> Said) Gallery(int width, int height, int covers = 12)
        {
            var said = new List<string>();
            var gallery = new GalleryLayout { Count = "1,284 games" };
            gallery.SetSections([new AdaptiveSection("all", "All"), new AdaptiveSection("fav", "Favourites"), new AdaptiveSection("recent", "Recent"), new AdaptiveSection("sg", "SG-1000"),
                new AdaptiveSection("sms", "Master System"), new AdaptiveSection("nes", "NES"), new AdaptiveSection("md", "Genesis"), new AdaptiveSection("gb", "Game Boy"), new AdaptiveSection("snes", "Super Nintendo")]);
            gallery.SetItems(Covers(covers));
            gallery.HintsFor = f => f.InKeyboard
                ? [new PageHint(PadGlyphButton.South, "Type"), new PageHint(PadGlyphButton.East, "Delete"), new PageHint(PadGlyphButton.North, "Space"), new PageHint(PadGlyphButton.West, "Done"), new PageHint(PadGlyphButton.Shoulders, "Move to grid"), new PageHint(PadGlyphButton.Start, "Home", "home")]
                : [new PageHint(PadGlyphButton.South, "Play"), new PageHint(PadGlyphButton.West, "Game page", "page"), new PageHint(PadGlyphButton.North, "Favourite", "favourite"), new PageHint(PadGlyphButton.Shoulders, "Console"), new PageHint(PadGlyphButton.Select, "List", "list"), new PageHint(PadGlyphButton.Start, "Home", "home")];
            gallery.ItemChosen += k => said.Add("item " + k);
            gallery.HintInvoked += k => said.Add("hint " + k);
            gallery.SectionChosen += k => said.Add("section " + k);
            gallery.SearchChanged += t => said.Add("search " + t);
            gallery.SearchClosed += done => said.Add("closed " + done);
            MenuPanel.SetScale(gallery, height / GalleryLayout.DesignHeight);
            var window = new Window { Width = width, Height = height, Content = gallery };
            window.Show();
            UiTest.Settle(gallery);
            return (window, gallery, said);
        }

        [Theory]
        [MemberData(nameof(Screens))]
        public Task The_gallery_draws_whole_rows_of_covers_centred_and_fits(int width, int height) => UiTest.Run(() =>
        {
            var (window, gallery, _) = Gallery(width, height);
            double u = height / GalleryLayout.DesignHeight;
            Picture(window, $"gallery-{width}x{height}");
            int columns = width * 800 / height >= 1400 ? 6 : 5;
            Assert.Equal(columns, gallery.Columns);
            Assert.Equal(2 * columns, gallery.Shown.Count);
            Rect grid = At(gallery, "GalleryGrid", 1);
            Assert.Equal(GalleryLayout.GridTop * u, grid.Y, 1.5);
            Assert.Equal(width / 2.0, grid.X + grid.Width * u / 2, 1.5);
            List<string> faults = FitAudit.Check(gallery);
            foreach (string f in faults) output.WriteLine($"{width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

        [Fact]
        public Task The_pad_moves_by_a_cover_and_a_row_and_the_rows_move_whole() => UiTest.Run(() =>
        {
            var (window, gallery, said) = Gallery(1280, 800);
            Assert.True(gallery.Move(0, 1));
            Assert.Equal("g5", gallery.Focused.Key);
            Assert.True(gallery.Move(0, 1));
            Assert.Equal("g10", gallery.Focused.Key);
            Assert.Equal("g5", gallery.Shown[0]);
            Assert.False(gallery.Move(0, 1));
            Assert.True(gallery.Move(1, 0));
            Assert.True(gallery.Press(PadGlyphButton.South));
            Assert.True(gallery.Press(PadGlyphButton.RightShoulder));
            Assert.True(gallery.Press(PadGlyphButton.Select));
            Assert.Equal(["item g11", "section fav", "hint list"], said);
            Assert.Equal("Fable of Tiles", gallery.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "GalleryName").Text);
            window.Close();
        });

        [Theory]
        [MemberData(nameof(Screens))]
        public Task Search_docks_the_keyboard_under_one_row_of_covers_and_fits(int width, int height) => UiTest.Run(() =>
        {
            var (window, gallery, said) = Gallery(width, height);
            gallery.OpenSearch("gra", [KeyboardLayout.Search, KeyboardLayout.Symbols]);
            UiTest.Settle(gallery);
            double u = height / GalleryLayout.DesignHeight;
            Picture(window, $"gallery-search-{width}x{height}");
            Assert.Equal(GalleryLayout.KeyboardTop * u, At(gallery, "GalleryKeyboard", 1).Y, 1.5);
            Assert.Equal(GalleryLayout.SearchTop * u, At(gallery, "GallerySearch", 1).Y, 1.5);
            Assert.Equal(1, gallery.RowsShown);
            Assert.True(gallery.Focused.InKeyboard);
            List<string> faults = FitAudit.Check(gallery);
            foreach (string f in faults) output.WriteLine($"{width}x{height}: {f}");
            window.Close();
            Assert.Empty(faults);
        });

        [Fact]
        public Task The_docked_keyboard_types_moves_to_the_grid_and_closes_on_B_with_nothing_left() => UiTest.Run(() =>
        {
            var (window, gallery, said) = Gallery(1280, 800);
            gallery.OpenSearch("", [KeyboardLayout.Search, KeyboardLayout.Symbols]);
            gallery.Move(0, 2);
            gallery.Move(4, 0);
            Assert.True(gallery.Press(PadGlyphButton.South));
            Assert.Equal("g", gallery.SearchText);
            Assert.True(gallery.Press(PadGlyphButton.North));
            Assert.Equal("g ", gallery.SearchText);
            Assert.Equal(["Type", "Delete", "Space", "Done", "Move to grid", "Home"], gallery.Hints.Select(h => h.Label));
            Assert.True(gallery.Press(PadGlyphButton.RightShoulder));
            Assert.False(gallery.Focused.InKeyboard);
            Assert.Equal("Play", gallery.Hints[0].Label);
            Assert.True(gallery.Press(PadGlyphButton.South));
            Assert.True(gallery.Press(PadGlyphButton.LeftShoulder));
            Assert.True(gallery.Press(PadGlyphButton.East));
            Assert.True(gallery.Press(PadGlyphButton.East));
            Assert.True(gallery.IsSearchOpen);
            Assert.True(gallery.Press(PadGlyphButton.East));
            UiTest.Settle(gallery);
            Assert.False(gallery.IsSearchOpen);
            Assert.Equal(["search g", "search g ", "item g0", "search g", "search ", "closed False"], said);
            window.Close();
        });

        [Fact]
        public Task X_finishes_the_search_and_keeps_its_text() => UiTest.Run(() =>
        {
            var (window, gallery, said) = Gallery(1280, 800);
            gallery.OpenSearch("gran", [KeyboardLayout.Search]);
            Assert.True(gallery.Press(PadGlyphButton.West));
            UiTest.Settle(gallery);
            Assert.False(gallery.IsSearchOpen);
            Assert.Equal("gran", gallery.SearchText);
            Assert.Equal(["closed True"], said);
            Assert.Equal(2, gallery.RowsShown);
            window.Close();
        });

        private sealed class RectComparer(double slack) : IEqualityComparer<Rect>
        {
            public bool Equals(Rect a, Rect b) => Math.Abs(a.X - b.X) <= slack && Math.Abs(a.Y - b.Y) <= slack && Math.Abs(a.Width - b.Width) <= slack && Math.Abs(a.Height - b.Height) <= slack;
            public int GetHashCode(Rect r) => 0;
        }
    }
}
