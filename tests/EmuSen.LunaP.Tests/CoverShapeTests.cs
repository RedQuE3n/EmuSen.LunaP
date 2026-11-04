using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;
using Xunit.Abstractions;

namespace EmuSen.LunaP.Tests
{
    // A cover is drawn whole at its own picture's shape, never cropped or stretched, and mixed shapes share the row's foot - see docs/LunaP.md §212.13.
    public class CoverShapeTests(ITestOutputHelper output)
    {
        private const int Band = 12;

        private static void Dump(Window window, string name)
        {
            if (Environment.GetEnvironmentVariable("EMUSEN_UI_DUMP") is not { Length: > 0 }) return;
            using WriteableBitmap? frame = window.CaptureRenderedFrame();
            if (frame is not null) UiTest.Dump(name, frame);
        }

        // A picture whose four edges are bands of their own colour, so a crop on any side removes that side's colour.
        internal static WriteableBitmap Banded(int w, int h)
        {
            var bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using ILockedFramebuffer frame = bitmap.Lock();
            var row = new byte[w * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    (byte r, byte g, byte b) = y < Band ? ((byte)255, (byte)0, (byte)0) : y >= h - Band ? ((byte)0, (byte)255, (byte)0)
                        : x < Band ? ((byte)0, (byte)0, (byte)255) : x >= w - Band ? ((byte)255, (byte)255, (byte)0) : ((byte)128, (byte)128, (byte)128);
                    row[x * 4] = b; row[x * 4 + 1] = g; row[x * 4 + 2] = r; row[x * 4 + 3] = 255;
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, frame.Address + y * frame.RowBytes, row.Length);
            }
            return bitmap;
        }

        private static Color At(RenderedFrame f, double x, double y)
        {
            int i = ((int)y * f.Width + (int)x) * 4;
            return Color.FromArgb(f.Rgba[i + 3], f.Rgba[i], f.Rgba[i + 1], f.Rgba[i + 2]);
        }

        private static bool Near(Color c, byte r, byte g, byte b) => Math.Abs(c.R - r) < 60 && Math.Abs(c.G - g) < 60 && Math.Abs(c.B - b) < 60;

        // Each edge's band is seen just inside the drawn cover's edge: nothing cropped, nothing letterboxed inside the box.
        private static List<string> Whole(RenderedFrame frame, Rect cover, string what)
        {
            var faults = new List<string>();
            double inset = 2, cx = cover.Center.X, cy = cover.Center.Y;
            (string Side, double X, double Y, byte R, byte G, byte B)[] probes =
            [
                ("top", cx, cover.Top + inset, 255, 0, 0), ("bottom", cx, cover.Bottom - inset - 1, 0, 255, 0),
                ("left", cover.Left + inset, cy, 0, 0, 255), ("right", cover.Right - inset - 1, cy, 255, 255, 0),
            ];
            foreach (var p in probes)
            {
                Color c = At(frame, p.X, p.Y);
                if (!Near(c, p.R, p.G, p.B)) faults.Add($"{what}: the {p.Side} edge at ({p.X:0.0},{p.Y:0.0}) is {c}, not its band");
            }
            return faults;
        }

        public static TheoryData<int, int, string> Shapes() => new()
        {
            { 140, 200, "tall 0.70" }, { 180, 200, "Game Boy 0.90" }, { 280, 200, "landscape 1.40" },
        };

        [Theory]
        [MemberData(nameof(Shapes))]
        public Task A_cover_is_drawn_whole_at_its_picture_s_own_shape(int w, int h, string shape) => UiTest.Run(() =>
        {
            foreach (bool big in new[] { true, false })
            {
                // The item's box shape is the inverse of the picture's, as a table in the other unit would give it.
                var card = new CoverCard { Big = big, CellWidth = 180, CellHeight = 240, Item = new CoverItem("g", "Granite Choir") { Picture = Banded(w, h), Aspect = (double)h / w }, Margin = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                var window = new Window { Width = 220, Height = 280, Content = card, Background = Brushes.Black };
                window.Show();
                UiTest.Settle(card);
                Rect cover = card.CoverBounds.Translate(new Vector(20, 20));
                Assert.Equal((double)w / h, card.CoverBounds.Width / card.CoverBounds.Height, 2);
                Assert.Equal(240, card.CoverBounds.Bottom, 1);
                Assert.True(card.CoverBounds.Width <= 180.5 && card.CoverBounds.Height <= 240.5, $"{shape}: {card.CoverBounds} leaves the cell");
                List<string> faults = Whole(UiTest.Redraw(window), cover, $"{shape}, {(big ? "big screen" : "desktop")}");
                foreach (string f in faults) output.WriteLine(f);
                window.Close();
                Assert.Empty(faults);
            }
        });

        [Fact]
        public Task A_generated_card_keeps_its_item_s_box_shape() => UiTest.Run(() =>
        {
            var card = new CoverCard { CellWidth = 180, CellHeight = 240, Item = new CoverItem("g", "Fable of Tiles") { Aspect = 1.4, Mark = "SNES" } };
            Assert.Equal(1.4, card.CoverBounds.Width / card.CoverBounds.Height, 2);
            Assert.Equal(new Rect(0, 240 - 180 / 1.4, 180, 180 / 1.4), card.CoverBounds);
        });

        [Fact]
        public Task A_game_page_s_cover_takes_its_picture_s_shape() => UiTest.Run(() =>
        {
            var page = new PageLayout { Form = AdaptiveForm.BigScreen, Hero = new PageHero("Granite Choir", "Super Nintendo", null, Banded(280, 200), 0.73, "SNES") };
            MenuPanel.SetScale(page, 1);
            var window = new Window { Width = 1280, Height = 800, Content = page };
            window.Show();
            UiTest.Settle(page);
            Control cover = page.GetVisualDescendants().OfType<Control>().First(c => c.Name == "PageCover");
            Assert.Equal(1.4, cover.Bounds.Width / cover.Bounds.Height, 2);
            window.Close();
        });

        private static readonly (int W, int H, double Aspect, string Mark)[] Mixed =
        [
            (280, 200, 0.73, "SNES"), (140, 200, 1.43, "NES"), (180, 200, 1.0, "GB"), (280, 200, 0.7, "N64"), (140, 200, 1.41, "PS2"),
            (143, 200, 1.4, "SMS"), (200, 200, 1.0, "PS"), (140, 200, 0.7, "MD"), (180, 200, 1.0, "GBC"), (280, 200, 0.73, "SNES"),
        ];

        private static List<CoverItem> MixedShelf() => Mixed.Select((m, i) => new CoverItem($"m{i}", $"Game {i}") { Picture = Banded(m.W, m.H), Aspect = m.Aspect, Mark = m.Mark }).ToList();

        // Every drawn cover has its picture's shape, stands on its cell's foot and shows all four edges.
        private static List<string> Covers(Window window, Control root, double u, string where)
        {
            var faults = new List<string>();
            RenderedFrame frame = UiTest.Redraw(window);
            foreach (CoverCard card in root.GetVisualDescendants().OfType<CoverCard>())
            {
                IImage picture = card.Item!.Picture!;
                double want = picture.Size.Width / picture.Size.Height, got = card.CoverBounds.Width / card.CoverBounds.Height;
                if (Math.Abs(want - got) > 0.02) faults.Add($"{where} {card.Name}: drawn at {got:0.000}, its picture is {want:0.000}");
                if (Math.Abs(card.CoverBounds.Bottom - card.CellHeight) > 0.5) faults.Add($"{where} {card.Name}: stands at {card.CoverBounds.Bottom:0.0}, not the foot {card.CellHeight:0.0}");
                if (card.ShowsFocus) continue;
                Point at = card.TranslatePoint(default, window)!.Value;
                Rect drawn = new(at.X + card.CoverBounds.X * u, at.Y + card.CoverBounds.Y * u, card.CoverBounds.Width * u, card.CoverBounds.Height * u);
                faults.AddRange(Whole(frame, drawn, $"{where} {card.Name}"));
            }
            return faults;
        }

        public static TheoryData<int, int> Screens() => new() { { 1280, 800 }, { 1920, 1080 }, { 3840, 2160 } };

        [Theory]
        [MemberData(nameof(Screens))]
        public Task A_mixed_shelf_and_grid_draw_every_cover_whole_and_fit(int width, int height) => UiTest.Run(() =>
        {
            var hub = new HubLayout { Form = AdaptiveForm.BigScreen, Status = "2 controllers · 21:42", Title = "Home" };
            hub.Continue = new HubContinue("Granite Choir", "Super Nintendo · Paused 12 minutes ago", Banded(320, 224));
            hub.SetShelf(MixedShelf());
            hub.SetPlaces([new HubPlace("games", "Games") { Status = "1,284 games" }, new HubPlace("power", "Power") { Status = "Exit" }]);
            hub.FocusStart();
            var window = new Window { Width = width, Height = height, Content = hub };
            window.Show();
            MenuPanel.SetScale(hub, height / HubLayout.DesignHeight);
            UiTest.Settle(hub);
            double u = height / HubLayout.DesignHeight;
            Dump(window, $"cover-shelf-{width}x{height}");
            List<string> faults = Covers(window, hub, u, $"shelf {width}x{height}");
            faults.AddRange(FitAudit.Check(hub, [new FitAudit.Allowance("a place's live line", c => c.Name == HubLayout.StatusName)]));
            Assert.Equal(width * 800 / height >= 1400 ? 9 : 8, hub.ShelfShown.Count);
            window.Close();

            var gallery = new GalleryLayout { Count = "1,284 games" };
            gallery.SetSections([new AdaptiveSection("all", "All"), new AdaptiveSection("nes", "NES")]);
            gallery.SetItems(MixedShelf());
            var grid = new Window { Width = width, Height = height, Content = gallery };
            grid.Show();
            MenuPanel.SetScale(gallery, height / GalleryLayout.DesignHeight);
            UiTest.Settle(gallery);
            Dump(grid, $"cover-grid-{width}x{height}");
            faults.AddRange(Covers(grid, gallery, u, $"grid {width}x{height}"));
            faults.AddRange(FitAudit.Check(gallery));
            grid.Close();
            foreach (string f in faults) output.WriteLine(f);
            Assert.Empty(faults);
        });
    }
}
