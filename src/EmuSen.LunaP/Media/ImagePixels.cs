using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace EmuSen.LunaP.Media
{
    // A picture read from a file: a decoded raster, or an SVG document drawn at whatever size is asked - see docs/LunaP.md §98.2.
    internal sealed class ImageFile
    {
        private static readonly Dictionary<string, ImageFile> Files = new(StringComparer.Ordinal);

        private ImageFile(Bitmap? raster, SvgDocument? svg, bool missing)
        {
            Raster = raster;
            Svg = svg;
            Missing = missing;
        }

        internal Bitmap? Raster { get; }
        internal SvgDocument? Svg { get; }
        internal bool Missing { get; }
        internal bool Refused => Svg is { IsRefused: true };

        internal Size Intrinsic => Raster is { } r ? new Size(r.PixelSize.Width, r.PixelSize.Height) : Svg is { IsRefused: false } s ? s.Size : default;

        // One read per full path, on the UI thread, which is the only thread controls are built on.
        internal static ImageFile Open(string path)
        {
            string full = Path.GetFullPath(path);
            if (Files.TryGetValue(full, out ImageFile? known)) return known;
            ImageFile file;
            if (!File.Exists(full)) file = new ImageFile(null, null, true);
            else if (full.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) file = new ImageFile(null, SvgDocument.Load(full), false);
            else
            {
                try { file = new ImageFile(new Bitmap(full), null, false); }
                catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { file = new ImageFile(null, null, true); }
            }

            Files[full] = file;
            return file;
        }

        internal static void Forget() => Files.Clear();
    }

    // A tint, an optional second tint for a gradient, and a saturation, applied to a picture's pixels once and cached - see docs/LunaP.md §98.2.
    internal readonly record struct ImageEffects(Color Tint, Color TintEnd, bool Vertical, double Saturation)
    {
        internal bool IsIdentity => Tint == Colors.White && TintEnd == Colors.White && Saturation >= 1;

        // Rec. 601 luma weights, as the consumer measured its reference desaturating (§104.4, correcting §98.2's Rec. 709); mixed on the stored values.
        internal const double Red = 0.299, Green = 0.587, Blue = 0.114;
    }

    internal static class ImagePixels
    {
        private static readonly Dictionary<(string, int, int, ImageEffects), Bitmap> Prepared = new();

        // The picture rasterised at a pixel size (its own, for a raster) with the effects applied; null when there is nothing to draw.
        internal static Bitmap? Prepare(string path, ImageFile file, PixelSize size, ImageEffects effects)
        {
            if (file.Missing || file.Refused || size.Width <= 0 || size.Height <= 0) return null;
            if (file.Raster is { } raster)
            {
                if (effects.IsIdentity) return raster;
                size = raster.PixelSize;
            }

            var key = (Path.GetFullPath(path), size.Width, size.Height, effects);
            if (Prepared.TryGetValue(key, out Bitmap? done)) return done;

            using var target = new RenderTargetBitmap(size);
            using (DrawingContext dc = target.CreateDrawingContext())
            {
                var box = new Rect(0, 0, size.Width, size.Height);
                if (file.Raster is { } r) dc.DrawImage(r, box);
                else file.Svg!.Draw(dc, box);
            }

            int stride = size.Width * 4;
            byte[] pixels = new byte[stride * size.Height];
            GCHandle pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try { target.CopyPixels(new PixelRect(size), pin.AddrOfPinnedObject(), pixels.Length, stride); }
            finally { pin.Free(); }

            bool bgra = target.Format is not { } f || f == PixelFormat.Bgra8888;
            if (!effects.IsIdentity) Apply(pixels, size.Width, size.Height, effects, bgra);

            var bitmap = new WriteableBitmap(size, new Vector(96, 96), bgra ? PixelFormat.Bgra8888 : PixelFormat.Rgba8888, AlphaFormat.Premul);
            using (ILockedFramebuffer fb = bitmap.Lock())
            {
                for (int y = 0; y < size.Height; y++) Marshal.Copy(pixels, y * stride, fb.Address + y * fb.RowBytes, stride);
            }

            Prepared[key] = bitmap;
            return bitmap;
        }

        // Premultiplied pixels: saturation first, then the tint multiplies every channel, alpha included.
        internal static void Apply(byte[] px, int width, int height, ImageEffects fx, bool bgra)
        {
            int ri = bgra ? 2 : 0, bi = bgra ? 0 : 2;
            double s = Math.Clamp(fx.Saturation, 0, 1);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double t = fx.Vertical ? (height <= 1 ? 0 : (double)y / (height - 1)) : (width <= 1 ? 0 : (double)x / (width - 1));
                    Color c = fx.Tint == fx.TintEnd ? fx.Tint : Lerp(fx.Tint, fx.TintEnd, t);
                    int i = (y * width + x) * 4;
                    double r = px[i + ri], g = px[i + 1], b = px[i + bi], a = px[i + 3];
                    if (s < 1)
                    {
                        double l = ImageEffects.Red * r + ImageEffects.Green * g + ImageEffects.Blue * b;
                        r = l + (r - l) * s;
                        g = l + (g - l) * s;
                        b = l + (b - l) * s;
                    }

                    double ta = c.A / 255.0;
                    px[i + ri] = Byte(r * c.R / 255.0 * ta);
                    px[i + 1] = Byte(g * c.G / 255.0 * ta);
                    px[i + bi] = Byte(b * c.B / 255.0 * ta);
                    px[i + 3] = Byte(a * ta);
                }
            }
        }

        private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

        private static byte Byte(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);

        internal static void Forget() => Prepared.Clear();
    }
}
