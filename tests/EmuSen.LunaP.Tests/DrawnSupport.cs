using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // Pictures and fonts the drawn-control tests write for themselves, and the pixel reads they assert on - see docs/LunaP.md §98.6.
    internal static class DrawnSupport
    {
        internal static readonly string Folder = Path.Combine(Path.GetTempPath(), "EmuSen.LunaP.Tests.Drawn", Environment.ProcessId.ToString());

        // A PNG of the given size whose pixels come from a function, written once per name.
        internal static string Png(string name, int width, int height, Func<int, int, Color> pixel)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, name + ".png");
            if (File.Exists(path)) return path;
            using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (ILockedFramebuffer fb = bitmap.Lock())
            {
                var row = new byte[width * 4];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color c = pixel(x, y);
                        row[x * 4] = (byte)(c.B * c.A / 255);
                        row[x * 4 + 1] = (byte)(c.G * c.A / 255);
                        row[x * 4 + 2] = (byte)(c.R * c.A / 255);
                        row[x * 4 + 3] = c.A;
                    }

                    Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, row.Length);
                }
            }

            bitmap.Save(path);
            return path;
        }

        internal static string Flat(string name, int width, int height, Color colour) => Png(name, width, height, (_, _) => colour);

        internal static string Svg(string name, string markup)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, name + ".svg");
            File.WriteAllText(path, markup);
            return path;
        }

        // One of Avalonia.Fonts.Inter's faces copied out to a file, which is what a font path names.
        internal static string Font(string face)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, face + ".ttf");
            if (File.Exists(path)) return path;
            using Stream source = AssetLoader.Open(new Uri($"avares://Avalonia.Fonts.Inter/Assets/{face}.ttf"));
            using FileStream target = File.Create(path);
            source.CopyTo(target);
            return path;
        }

        internal static ToolWindow Show(Control content, double width, double height)
        {
            var window = new ToolWindow { Width = width, Height = height, Content = content, Background = Brushes.Black };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
            Dispatcher.UIThread.RunJobs();
            return window;
        }

        internal static RenderedFrame Frame(ToolWindow window) => UiTest.Redraw(window);

        internal static Color At(RenderedFrame f, double x, double y)
        {
            int i = ((int)y * f.Width + (int)x) * 4;
            return Color.FromArgb(f.Rgba[i + 3], f.Rgba[i], f.Rgba[i + 1], f.Rgba[i + 2]);
        }

        // Straight RGBA of whatever a drawing puts on a transparent canvas of the given size.
        internal static RenderedFrame Draw(int width, int height, Action<DrawingContext> draw)
        {
            using var target = new RenderTargetBitmap(new PixelSize(width, height));
            using (DrawingContext dc = target.CreateDrawingContext()) draw(dc);
            var px = new byte[width * height * 4];
            GCHandle pin = GCHandle.Alloc(px, GCHandleType.Pinned);
            try { target.CopyPixels(new PixelRect(0, 0, width, height), pin.AddrOfPinnedObject(), px.Length, width * 4); }
            finally { pin.Free(); }
            bool bgra = target.Format is not { } f || f == PixelFormat.Bgra8888;
            for (int i = 0; i < px.Length; i += 4)
            {
                if (bgra) (px[i], px[i + 2]) = (px[i + 2], px[i]);
                byte a = px[i + 3];
                if (a is > 0 and < 255) for (int k = 0; k < 3; k++) px[i + k] = (byte)Math.Min(255, px[i + k] * 255 / a);
            }

            return new RenderedFrame(px, width, height);
        }

        internal static int Covered(RenderedFrame f, byte alphaAtLeast = 128)
        {
            int n = 0;
            for (int i = 3; i < f.Rgba.Length; i += 4) if (f.Rgba[i] >= alphaAtLeast) n++;
            return n;
        }

        internal static bool Near(Color a, Color b, int tolerance = 6) =>
            Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance && Math.Abs(a.A - b.A) <= tolerance;
    }
}
