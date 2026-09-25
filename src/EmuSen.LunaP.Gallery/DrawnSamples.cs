using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Gallery
{
    // The drawn controls of §98 to §101 on one positioned canvas, their pictures written as small SVG files at start.
    /// <summary>Builds the gallery's section of drawn controls: a positioned canvas holding an image, text, a list, a carousel and the indicators.</summary>
    internal static class DrawnSamples
    {
        private static string Svg(string name, string body)
        {
            string folder = Path.Combine(Path.GetTempPath(), "EmuSen.LunaP.Gallery");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name + ".svg");
            File.WriteAllText(path, $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'>{body}</svg>");
            return path;
        }

        private static T At<T>(NormalizedCanvas canvas, T child, double x, double y, double w, double h, double ox = 0, double oy = 0) where T : Control
        {
            NormalizedCanvas.SetPosition(child, new Point(x, y));
            NormalizedCanvas.SetSize(child, new Size(w, h));
            NormalizedCanvas.SetOrigin(child, new Point(ox, oy));
            canvas.Children.Add(child);
            return child;
        }

        internal static Control Build()
        {
            string disc = Svg("disc", "<circle cx='10' cy='10' r='9' fill='#fff'/>");
            string check = Svg("check", "<rect width='20' height='20' rx='4' fill='#fff'/><path d='M5 10l3 3 7-7' fill='none' stroke='#000' stroke-width='2'/>");
            var canvas = new NormalizedCanvas { Height = 260, Background = EmuSen.LunaP.Theme.LunaPalette.Void };

            At(canvas, new FittedImage { Source = disc, Fit = ImageFit.Tile, TileSize = new Size(24, 24), Tint = Color.FromArgb(40, 255, 255, 255) }, 0, 0, 1, 1);
            At(canvas, new TextRowList
            {
                Items = new[] { new TextRow("Alpha"), new TextRow("Beta"), new TextRow("Folder", true), new TextRow("Gamma") },
                SelectedIndex = 1, FontSize = 14, SelectedBackgroundColor = EmuSen.LunaP.Theme.LunaPalette.Accent.Color, SelectorColor = Colors.Transparent,
                SelectedBackgroundCornerRadius = 6, HorizontalMargin = 8,
            }, 0.02, 0.05, 0.3, 0.6);
            At(canvas, new ImageCarousel { Items = new[] { new CarouselItem(null, "NES"), new CarouselItem(null, "SNES"), new CarouselItem(null, "N64") }, SelectedIndex = 1, FontSize = 16 },
                0.35, 0.05, 0.63, 0.35);
            At(canvas, new FontText { Text = "A long description that wraps within its box and is cut short where it no longer fits.", FontSize = 13, LineSpacing = 1.3 },
                0.35, 0.45, 0.4, 0.3);
            At(canvas, new StarRating { Value = 0.7 }, 0.78, 0.47, 0, 0.1);
            At(canvas, new BadgeStrip { Icons = new[] { check, disc }, ItemsPerLine = 4, ItemMargin = new Size(4, 0) }, 0.78, 0.62, 0.2, 0.1);
            At(canvas, new HintBar { Entries = new[] { new HintEntry("Launch", Glyph: "A"), new HintEntry("Back", Glyph: "B") }, FontSize = 13, BackgroundColor = Color.FromArgb(160, 0, 0, 0), Padding = new Thickness(6), BackgroundCornerRadius = 6 },
                0.5, 0.97, 0, 0, 0.5, 1);
            At(canvas, new ClockLabel { Live = true, FontSize = 13 }, 0.02, 0.97, 0, 0, 0, 1);
            At(canvas, new DeviceStatusBar { Status = new DeviceStatus(Wifi: true, BatteryPercent: 64), IconHeight = 13 }, 0.98, 0.97, 0, 0, 1, 1);
            Avalonia.Automation.AutomationProperties.SetName(canvas, "Themed surface");
            return canvas;
        }
    }
}
