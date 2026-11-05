using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;
using Xunit.Abstractions;

namespace EmuSen.LunaP.Tests
{
    // The chooser of worlds and the hub and gallery at night: rows that fit, the pad's walk, the glow following the focus, the heading's neighbours, and the fit audit at every size - see docs/LunaP.md §221.
    public class PlanetLayoutTests(ITestOutputHelper output)
    {
        private static readonly (string Key, string Name, double Hue)[] Worlds =
        [
            ("nes", "NES", 8), ("snes", "Super Nintendo", 280), ("n64", "Nintendo 64", 140), ("gb", "Game Boy", 95), ("gbc", "Game Boy Color", 320), ("sms", "Master System", 220),
            ("genesis", "Genesis", 0), ("mcd", "Sega CD", 190), ("psx", "PlayStation", 230), ("ps2", "PlayStation 2", 250), ("dc", "Dreamcast", 25),
        ];

        private static readonly PlanetMark Crescent = new() { Stroke = "M15 3.5a8.5 8.5 0 1 0 0 17a7 7 0 1 1 0-17z" };
        private static readonly PlanetMark Badge = new() { Pixels = ["X.......X", ".X.....X.", ".XXXXXXX.", "XX.XXX.XX", "XXXXXXXXX", ".XX...XX.", ".X.....X.", "XX.....XX"] };

        // A body and an engine line for some worlds, the longest libretro name among them; the rest are plain and say no engine (docs/LunaP.md §222).
        private static readonly Dictionary<string, (WorldBody? Body, PlanetEngine? Engine)> Kinds = new()
        {
            ["nes"] = (new WorldBody(WorldShape.Planet) { Palette = [Color.Parse("#FFF8FB"), Color.Parse("#F3CFE3"), Color.Parse("#A9789F")], Craters = 0.28 }, new PlanetEngine("Moon", "EmuSen", true) { Mark = Crescent }),
            ["snes"] = (null, new PlanetEngine("Venus", "EmuSen", true) { Mark = Crescent }),
            ["n64"] = (null, new PlanetEngine("Mupen64Plus-Next", "libretro", false) { Mark = Badge }),
            ["genesis"] = (new WorldBody(WorldShape.Shards), new PlanetEngine("Nephrite", "EmuSen", true) { Mark = Crescent }),
            ["ps2"] = (new WorldBody(WorldShape.Gem), new PlanetEngine("PCSX2", "libretro", false) { Mark = Badge }),
        };

        private static List<CoverItem> Covers(string world) =>
            Enumerable.Range(0, 4).Select(i => new CoverItem($"{world}/{i}", $"Granite Choir {i}") { Aspect = new[] { 0.7, 1.4, 1.0, 0.72 }[i], Mark = "SNES" }).ToList();

        private static (Window Window, PlanetLayout Layout, List<string> Said) Planets(int width, int height, bool lateScale = false, int count = 11)
        {
            var said = new List<string>();
            var layout = new PlanetLayout { Title = "Games", Subtitle = "Choose a console", Count = "11 consoles · 6,424 games" };
            layout.SetTiles([
                new PlanetItem("all", "All games") { Status = "6,424 games", Facts = "6,424 games", Icon = "M4 5h7v6H4zM13 5h7v6h-7zM4 13h7v6H4zM13 13h7v6h-7z", Covers = Covers("all") },
                new PlanetItem("fav", "Favourites") { Status = "12 games", Icon = "M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z" },
                new PlanetItem("recent", "Recently played") { Status = "Last: Super Metroid" },
            ]);
            layout.SetPlanets(Worlds.Take(count).Select(w => new PlanetItem(w.Key, w.Name)
            {
                Hue = w.Hue, Status = "795 games", Title = w.Name, Facts = "Nintendo · 1991 · 795 games", Covers = Covers(w.Key),
                Body = Kinds.GetValueOrDefault(w.Key).Body, Engine = Kinds.GetValueOrDefault(w.Key).Engine,
            }));
            layout.HintsFor = _ => [new PageHint(PadGlyphButton.South, "Open"), new PageHint(PadGlyphButton.East, "Back", "back"), new PageHint(PadGlyphButton.North, "Favourites", "fav"), new PageHint(PadGlyphButton.West, "Random game", "random")];
            layout.ItemChosen += k => said.Add("chose " + k);
            layout.HintInvoked += k => said.Add("hint " + k);
            MenuPanel.SetScale(layout, (lateScale ? 800 : height) / PlanetLayout.DesignHeight);
            var window = new Window { Width = lateScale ? 1280 : width, Height = lateScale ? 800 : height, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            if (lateScale)
            {
                window.Width = width;
                window.Height = height;
                UiTest.Settle(layout);
                MenuPanel.SetScale(layout, height / PlanetLayout.DesignHeight);
                UiTest.Settle(layout);
            }
            return (window, layout, said);
        }

        private static readonly FitAudit.Allowance Lines = new("a world's words are one line cut with an ellipsis, as the design draws them", c => c.Name is PlanetLayout.StatusName or HubLayout.StatusName);

        private static readonly FitAudit.Allowance FanTitles = new("a fanned cover's generated title is cut to the cover; the title is the item's name, said under the rows", c => c.Name == "CoverTitle");

        private static Control Named(Control root, string name) => root.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);

        [Fact]
        public Task Eleven_planets_share_two_centred_rows_six_and_five() => UiTest.Run(() =>
        {
            var (window, layout, _) = Planets(1280, 800);
            Assert.Equal(6, layout.Columns);
            Assert.Equal([6, 5], layout.Rows.Select(r => r.Count));
            double u = 1;
            Rect first = Named(layout, "PlanetCell.nes").Bounds, last = Named(layout, "PlanetCell.sms").Bounds;
            Point a = Named(layout, "PlanetCell.nes").TranslatePoint(default, layout)!.Value, b = Named(layout, "PlanetCell.sms").TranslatePoint(default, layout)!.Value;
            Assert.Equal(1280 / 2.0, (a.X + b.X + last.Width) / 2 * u, 1.5);
            Point c = Named(layout, "PlanetCell.genesis").TranslatePoint(default, layout)!.Value;
            Assert.Equal(164, c.X, 1.5);
            Assert.Equal(PlanetLayout.RowTop + PlanetLayout.RowStep, c.Y, 1.5);
            Assert.True(first.Width > 0);
            window.Close();
        });

        [Fact]
        public Task The_pad_walks_from_the_tiles_down_the_rows_and_A_and_the_hints_raise_their_keys() => UiTest.Run(() =>
        {
            var (window, layout, said) = Planets(1280, 800);
            Assert.Equal("all", layout.Focused);
            Assert.False(layout.Move(0, -1));
            Assert.True(layout.Move(1, 0));
            Assert.Equal("fav", layout.Focused);
            Assert.True(layout.Move(0, 1));
            Assert.Contains(layout.Focused, new[] { "n64", "gb" });
            Assert.True(layout.Move(0, 1));
            Assert.Contains(layout.Focused, new[] { "genesis", "mcd", "psx" });
            Assert.False(layout.Move(0, 1));
            Assert.True(layout.Move(0, -1));
            Assert.True(layout.Move(0, -1));
            Assert.Contains(layout.Focused, new[] { "all", "fav" });
            layout.FocusOn("dc");
            Assert.False(layout.Move(1, 0));
            Assert.True(layout.Press(PadGlyphButton.South));
            Assert.True(layout.Press(PadGlyphButton.West));
            Assert.False(layout.Press(PadGlyphButton.Select));
            Assert.Equal(["chose dc", "hint random"], said);
            window.Close();
        });

        [Fact]
        public Task The_focused_planet_grows_and_the_sky_glows_in_its_hue_a_tile_keeping_the_last() => UiTest.Run(() =>
        {
            var (window, layout, _) = Planets(1280, 800);
            Assert.Equal(8, layout.Sky.GlowHue);
            layout.FocusOn("snes");
            UiTest.Settle(layout);
            Assert.Equal(PlanetLayout.FocusedPlanet, Named(layout, "Planet.snes").Bounds.Width, 0.5);
            Assert.Equal(PlanetLayout.Planet, Named(layout, "Planet.nes").Bounds.Width, 0.5);
            Assert.Equal(280, layout.Sky.GlowHue);
            Assert.Equal("SUPER NINTENDO", ((TextBlock)Named(layout, "PlanetFocusTitle")).Text);
            Assert.Equal(4, Named(layout, "PlanetFan").GetVisualChildren().Count());
            layout.FocusOn("recent");
            Assert.Equal(280, layout.Sky.GlowHue);
            layout.Night = false;
            Assert.DoesNotContain(layout.GetVisualDescendants(), c => c is LunaSky);
            window.Close();
        });

        // Each world drawn as its body says, a world with none as the plain planet, each at its shape's size (docs/LunaP.md §222.2).
        [Fact]
        public Task Each_world_is_drawn_as_its_body_at_its_shape_s_size() => UiTest.Run(() =>
        {
            var (window, layout, _) = Planets(1280, 800);
            layout.FocusOn("ps2");
            UiTest.Settle(layout);
            WorldGlyph Glyph(string key) => (WorldGlyph)Named(layout, "Planet." + key);
            Assert.Equal((WorldShape.Gem, 60.0, 96.0), (Glyph("ps2").Body.Shape, Glyph("ps2").Bounds.Width, Glyph("ps2").Bounds.Height));
            Assert.Equal((WorldShape.Shards, 42.0, 68.0), (Glyph("genesis").Body.Shape, Glyph("genesis").Bounds.Width, Glyph("genesis").Bounds.Height));
            Assert.Same(WorldBody.Plain, Glyph("dc").Body);
            Assert.Equal(0.28, Glyph("nes").Body.Craters);
            Assert.True(Glyph("ps2").Focused);
            Assert.False(Glyph("genesis").Focused);
            Assert.Equal(PlanetLayout.RowTop + PlanetLayout.PlanetArea / 2, Named(layout, "PlanetOrbit").TranslatePoint(default, layout)!.Value.Y, 1.5);
            window.Close();
        });

        [Fact]
        public Task The_focused_cell_alone_carries_three_sparkles() => UiTest.Run(() =>
        {
            var (window, layout, _) = Planets(1280, 800);
            Assert.DoesNotContain(layout.GetVisualDescendants(), c => c.Name == "PlanetSparkle");
            layout.FocusOn("snes");
            UiTest.Settle(layout);
            Assert.Equal(3, Named(layout, "PlanetCell.snes").GetVisualDescendants().Count(c => c.Name == "PlanetSparkle"));
            Assert.Equal(3, layout.GetVisualDescendants().Count(c => c.Name == "PlanetSparkle"));
            window.Close();
        });

        // The engine line: the name and tag in the world's hue for the host's own engine, in the theme's tokens otherwise, brighter when focused (docs/LunaP.md §222.3).
        [Fact]
        public Task The_engine_line_names_the_engine_and_its_tag_in_the_hue_or_in_the_tokens() => UiTest.Run(() =>
        {
            var (window, layout, _) = Planets(1280, 800);
            static Color Ink(Control c) => Assert.IsAssignableFrom<ISolidColorBrush>(((TextBlock)c).Foreground).Color;
            Control snes = Named(layout, "PlanetCell.snes"), n64 = Named(layout, "PlanetCell.n64");
            Assert.Equal("VENUS", ((TextBlock)Named(Named(snes, "PlanetEngine"), PlanetLayout.StatusName)).Text);
            Assert.Equal("EMUSEN", ((TextBlock)Named(snes, PlanetLayout.EngineTagName + "Word")).Text);
            Assert.Equal(HslColor.ToRgb(280, 0.35, 0.7, 1), Ink(Named(Named(snes, "PlanetEngine"), PlanetLayout.StatusName)));
            Assert.Equal(HslColor.ToRgb(280, 0.45, 0.52, 1), Assert.IsAssignableFrom<ISolidColorBrush>(((Border)Named(snes, PlanetLayout.EngineTagName)).BorderBrush).Color);
            Assert.Equal("LIBRETRO", ((TextBlock)Named(n64, PlanetLayout.EngineTagName + "Word")).Text);
            Assert.Equal(Color.Parse("#D4D4D4"), Ink(Named(Named(n64, "PlanetEngine"), PlanetLayout.StatusName)));
            Assert.Null(((Border)Named(n64, PlanetLayout.EngineTagName)).Background);
            Assert.DoesNotContain(Named(layout, "PlanetCell.dc").GetVisualDescendants(), c => c.Name == PlanetLayout.EngineTagName);
            var symbol = (MarkGlyph)Named(snes, PlanetLayout.EngineSymbolName);
            Assert.Equal(HslColor.ToRgb(280, 0.35, 0.7, 1), Assert.IsAssignableFrom<ISolidColorBrush>(symbol.Ink).Color);
            Assert.Equal(15, symbol.Bounds.Width, 0.5);
            var badge = (MarkGlyph)Named(n64, PlanetLayout.EngineSymbolName);
            Assert.Same(Badge, badge.Mark);
            Assert.Equal((16.0, Color.Parse("#D4D4D4"), Color.Parse("#26262B")), (badge.Bounds.Width, Assert.IsAssignableFrom<ISolidColorBrush>(badge.Ink).Color, Assert.IsAssignableFrom<ISolidColorBrush>(badge.Dark).Color));
            layout.FocusOn("snes");
            UiTest.Settle(layout);
            Assert.Equal(HslColor.ToRgb(280, 0.85, 0.8, 1), Ink(Named(Named(Named(layout, "PlanetCell.snes"), "PlanetEngine"), PlanetLayout.StatusName)));
            window.Close();
        });

        [Fact]
        public Task A_third_row_scrolls_in_as_the_focus_reaches_it() => UiTest.Run(() =>
        {
            var (window, layout, _) = Planets(1280, 800);
            layout.SetPlanets(Enumerable.Range(0, 15).Select(i => new PlanetItem("w" + i, "World " + i) { Hue = i * 20 }));
            Assert.Equal(3, layout.Rows.Count);
            Assert.DoesNotContain(layout.GetVisualDescendants(), c => c.Name == "PlanetCell.w14");
            layout.FocusOn("w14");
            UiTest.Settle(layout);
            Assert.Contains(layout.GetVisualDescendants(), c => c.Name == "PlanetCell.w14");
            Assert.DoesNotContain(layout.GetVisualDescendants(), c => c.Name == "PlanetCell.w0");
            window.Close();
        });

        public static TheoryData<int, int, bool> Sizes() => new() { { 1280, 800, false }, { 1920, 1080, false }, { 1280, 720, false }, { 3840, 2160, true } };

        [Theory]
        [MemberData(nameof(Sizes))]
        public Task The_planets_fit_at_every_size(int width, int height, bool late) => UiTest.Run(() =>
        {
            var (window, layout, _) = Planets(width, height, late);
            foreach (string key in new[] { "all", "gbc", "dc", "n64", "genesis" })
            {
                layout.FocusOn(key);
                UiTest.Settle(layout);
                List<string> faults = FitAudit.Check(layout, [Lines, FanTitles]);
                foreach (string f in faults) output.WriteLine($"{width}x{height} {key}: {f}");
                Assert.Empty(faults);
            }
            double u = height / PlanetLayout.DesignHeight;
            Control band = Named(layout, "PlanetHelpBar");
            Assert.Equal(742 * u, band.TranslatePoint(default, layout)!.Value.Y, 1.5);
            Assert.Equal(height, band.TranslatePoint(new Point(0, band.Bounds.Height), layout)!.Value.Y, 1.0);
            Assert.Equal(612 * u, Named(layout, "PlanetFan").TranslatePoint(default, layout)!.Value.Y, 1.5);
            foreach (string key in Kinds.Keys)
            {
                Control cell = Named(layout, "PlanetCell." + key), tag = Named(cell, PlanetLayout.EngineTagName);
                Rect inCell = new Rect(tag.Bounds.Size).TransformToAABB(tag.TransformToVisual(cell)!.Value);
                Assert.True(inCell.X >= 8 - 0.5 && inCell.Right <= PlanetLayout.Cell - 8 + 0.5 && inCell.Bottom <= PlanetLayout.CellHeight + 0.5, $"{width}x{height} {key}: the tag at {inCell} leaves its tile");
                Control symbol = Named(cell, PlanetLayout.EngineSymbolName);
                Rect mark = new Rect(symbol.Bounds.Size).TransformToAABB(symbol.TransformToVisual(cell)!.Value);
                Assert.True(mark.X >= 8 - 0.5 && mark.Right <= inCell.X, $"{width}x{height} {key}: the symbol at {mark} leaves its tile or meets the tag");
                TextBlock word = (TextBlock)Named(tag, PlanetLayout.EngineTagName + "Word");
                Assert.False(word.TextLayout.TextLines.Any(l => l.HasCollapsed), $"{key}'s tag is cut");
            }
            window.Close();
        });

        [Theory]
        [MemberData(nameof(Sizes))]
        public Task The_hub_and_the_gallery_fit_at_night_at_every_size(int width, int height, bool late) => UiTest.Run(() =>
        {
            var hub = new HubLayout { Form = AdaptiveForm.BigScreen, Night = true, Mark = "EMUSEN", Status = "2 controllers · 21:42" };
            hub.Continue = new HubContinue("Granite Choir", "Super Nintendo · Paused 12 minutes ago") { Hue = 280 };
            hub.SetShelf(Covers("snes").Select(c => c with { Hue = 280 }).Concat(Covers("nes").Select(c => c with { Hue = 8 })));
            hub.SetPlaces([new HubPlace("games", "Games") { Status = "11 consoles" }, new HubPlace("scraper", "Scraper") { Status = "Up to date" }, new HubPlace("power", "Power")]);
            hub.FocusStart();
            var gallery = new GalleryLayout { Night = true, Trail = "Games", Hue = 280, Count = "795 games" };
            gallery.SetSections([new AdaptiveSection("nes", "NES"), new AdaptiveSection("snes", "Super Nintendo"), new AdaptiveSection("n64", "Nintendo 64")], "snes");
            gallery.SetItems(Covers("snes").Concat(Covers("n64")).Select(c => c with { Hue = 280 }));
            foreach (Control layout in new Control[] { hub, gallery })
            {
                MenuPanel.SetScale(layout, (late ? 800 : height) / 800.0);
                var window = new Window { Width = late ? 1280 : width, Height = late ? 800 : height, Content = layout };
                window.Show();
                UiTest.Settle(layout);
                if (late)
                {
                    window.Width = width;
                    window.Height = height;
                    UiTest.Settle(layout);
                    MenuPanel.SetScale(layout, height / 800.0);
                    UiTest.Settle(layout);
                }
                List<string> faults = FitAudit.Check(layout, [Lines]);
                foreach (string f in faults) output.WriteLine($"{layout.GetType().Name} {width}x{height}: {f}");
                Control band = Named(layout, layout is HubLayout ? "HubHelpBar" : "GalleryHelpBar");
                Assert.Equal(height, band.TranslatePoint(new Point(0, band.Bounds.Height), layout)!.Value.Y, 1.0);
                Assert.Empty(faults);
                Assert.Contains(layout.GetVisualDescendants(), c => c is LunaSky);
                window.Content = null;
                window.Close();
            }
        });

        // The focused Continue panel's ring clears RECENT by at least the mockup's 8 units, at every audited size (docs/LunaP.md §221.4).
        [Theory]
        [MemberData(nameof(Sizes))]
        public Task The_focused_Continue_ring_ends_above_the_shelf_s_label(int width, int height, bool late) => UiTest.Run(() =>
        {
            var hub = new HubLayout { Form = AdaptiveForm.BigScreen, Night = true, Mark = "EMUSEN", Status = "2 controllers · 21:42" };
            hub.Continue = new HubContinue("Granite Choir", "Super Nintendo · Paused 12 minutes ago") { Hue = 280 };
            hub.SetShelf(Covers("snes").Select(c => c with { Hue = 280 }));
            hub.SetPlaces([new HubPlace("games", "Games"), new HubPlace("power", "Power")]);
            hub.FocusStart();
            MenuPanel.SetScale(hub, (late ? 800 : height) / 800.0);
            var window = new Window { Width = late ? 1280 : width, Height = late ? 800 : height, Content = hub };
            window.Show();
            UiTest.Settle(hub);
            if (late)
            {
                window.Width = width;
                window.Height = height;
                UiTest.Settle(hub);
                MenuPanel.SetScale(hub, height / 800.0);
                UiTest.Settle(hub);
            }
            Assert.Equal(HubRow.Continue, hub.Focused.Row);
            double u = height / 800.0;
            Control panel = Named(hub, "HubContinuePanel"), label = Named(hub, "HubRecentTitle");
            Assert.True(panel.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "HubRing" && c.IsVisible));
            double ringFoot = panel.TranslatePoint(new Point(0, panel.Bounds.Height), hub)!.Value.Y, labelTop = label.TranslatePoint(default, hub)!.Value.Y;
            window.Close();
            Assert.True(labelTop - ringFoot >= 8 * u - 0.5, $"{width}x{height}: the ring ends at {ringFoot:0.0}, RECENT starts at {labelTop:0.0}");
        });

        [Fact]
        public Task The_hub_at_night_keeps_its_places_and_walk_and_glows_in_the_focus_s_hue() => UiTest.Run(() =>
        {
            var hub = new HubLayout { Form = AdaptiveForm.BigScreen, Night = true, Mark = "EMUSEN" };
            hub.Continue = new HubContinue("Granite Choir", "Paused") { Hue = 250 };
            hub.SetShelf(Covers("snes").Select(c => c with { Hue = 280 }));
            hub.SetPlaces([new HubPlace("games", "Games"), new HubPlace("power", "Power")]);
            hub.FocusStart();
            MenuPanel.SetScale(hub, 1);
            var window = new Window { Width = 1280, Height = 800, Content = hub };
            window.Show();
            UiTest.Settle(hub);
            Assert.Equal(250, hub.Sky.GlowHue);
            Assert.Equal(HubLayout.ContinueTop, Named(hub, "HubContinue").TranslatePoint(default, hub)!.Value.Y, 1.5);
            Assert.Contains(hub.GetVisualDescendants(), c => c.Name == "HubContinuePlanet");
            Assert.Contains(hub.GetVisualDescendants(), c => c.Name == "HubMark");
            Assert.True(hub.Move(0, 1));
            Assert.Equal(new HubFocus(HubRow.Shelf, "snes/0"), hub.Focused);
            Assert.Equal(280, hub.Sky.GlowHue);
            Assert.Equal(HubLayout.NightLift, Named(hub, "HubTile.snes/0") is CoverCard card ? card.Lift : 0);
            Assert.True(hub.Move(0, 1));
            Assert.Equal(HubRow.Places, hub.Focused.Row);
            Assert.Equal(250, hub.Sky.GlowHue);
            window.Close();
        });

        [Fact]
        public Task The_gallery_s_heading_names_the_sections_the_shoulders_lead_to() => UiTest.Run(() =>
        {
            var gallery = new GalleryLayout { Night = true, Trail = "Games", Hue = 280, Count = "795 games" };
            gallery.SetSections([new AdaptiveSection("nes", "NES"), new AdaptiveSection("snes", "Super Nintendo"), new AdaptiveSection("n64", "Nintendo 64")], "snes");
            gallery.SetItems(Covers("snes"));
            var window = new Window { Width = 1280, Height = 800, Content = gallery };
            window.Show();
            UiTest.Settle(gallery);
            Assert.Equal(("NES", "Nintendo 64"), gallery.Neighbours);
            Assert.Equal("NES", ((TextBlock)Named(gallery, "GalleryPrevious")).Text);
            Assert.Equal("Nintendo 64", ((TextBlock)Named(gallery, "GalleryNext")).Text);
            Assert.Equal("SUPER NINTENDO", ((TextBlock)Named(gallery, "GallerySection")).Text);
            Assert.Equal(280, gallery.Sky.GlowHue);
            Assert.True(gallery.Press(PadGlyphButton.RightShoulder));
            UiTest.Settle(gallery);
            Assert.Equal(("Super Nintendo", "NES"), gallery.Neighbours);
            Assert.Equal("NES", ((TextBlock)Named(gallery, "GalleryNext")).Text);
            window.Close();
        });
    }
}
