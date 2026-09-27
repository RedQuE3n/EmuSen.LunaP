using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace EmuSen.LunaP.Media
{
    // A GIF read into whole frames, each composited over the last by its disposal method, with each frame's delay - see docs/LunaP.md §194.
    internal sealed class GifFile
    {
        private static readonly Dictionary<string, GifFile?> Files = new(StringComparer.Ordinal);

        private GifFile(int width, int height, IReadOnlyList<uint[]> frames, IReadOnlyList<TimeSpan> delays)
        {
            Width = width;
            Height = height;
            Pixels = frames;
            Delays = delays;
        }

        internal int Width { get; }
        internal int Height { get; }

        // Straight (not premultiplied) 0xAARRGGBB, the whole logical screen per frame.
        internal IReadOnlyList<uint[]> Pixels { get; }
        internal IReadOnlyList<TimeSpan> Delays { get; }

        internal int Count => Pixels.Count;

        // One read per full path; null when the file is missing or is not a GIF this decoder reads.
        internal static GifFile? Open(string path)
        {
            string full = Path.GetFullPath(path);
            if (Files.TryGetValue(full, out GifFile? known)) return known;
            GifFile? file = null;
            try { if (File.Exists(full)) file = Decode(File.ReadAllBytes(full)); }
            catch (Exception e) when (e is IOException or InvalidDataException or IndexOutOfRangeException or ArgumentException) { file = null; }
            Files[full] = file;
            return file;
        }

        internal static void Forget() => Files.Clear();

        internal static GifFile Decode(byte[] b)
        {
            if (b.Length < 13 || b[0] != 'G' || b[1] != 'I' || b[2] != 'F') throw new InvalidDataException("not a GIF");
            int width = b[6] | b[7] << 8, height = b[8] | b[9] << 8;
            if (width <= 0 || height <= 0) throw new InvalidDataException("empty screen");
            int pos = 13;
            uint[]? global = null;
            if ((b[10] & 0x80) != 0) global = Palette(b, ref pos, 2 << (b[10] & 7));
            var canvas = new uint[width * height];
            var frames = new List<uint[]>();
            var delays = new List<TimeSpan>();
            int disposal = 0, transparent = -1;
            TimeSpan delay = TimeSpan.Zero;
            while (pos < b.Length)
            {
                byte block = b[pos++];
                if (block == 0x3B) break;
                if (block == 0x21)
                {
                    byte label = b[pos++];
                    if (label == 0xF9 && b[pos] >= 4)
                    {
                        byte packed = b[pos + 1];
                        disposal = packed >> 2 & 7;
                        delay = TimeSpan.FromMilliseconds((b[pos + 2] | b[pos + 3] << 8) * 10);
                        transparent = (packed & 1) != 0 ? b[pos + 4] : -1;
                    }

                    SkipBlocks(b, ref pos);
                    continue;
                }

                if (block != 0x2C) throw new InvalidDataException("unknown block");
                int left = b[pos] | b[pos + 1] << 8, top = b[pos + 2] | b[pos + 3] << 8, w = b[pos + 4] | b[pos + 5] << 8, h = b[pos + 6] | b[pos + 7] << 8;
                byte flags = b[pos + 8];
                pos += 9;
                uint[] palette = (flags & 0x80) != 0 ? Palette(b, ref pos, 2 << (flags & 7)) : global ?? throw new InvalidDataException("no palette");
                int minCode = b[pos++];
                byte[] indices = Lzw(Data(b, ref pos), minCode, w * h);
                uint[]? previous = disposal == 3 ? (uint[])canvas.Clone() : null;
                int[] rows = (flags & 0x40) != 0 ? Interlaced(h) : Sequential(h);
                for (int r = 0; r < h; r++)
                {
                    int y = top + rows[r];
                    if (y < 0 || y >= height) continue;
                    for (int x = 0; x < w; x++)
                    {
                        int cx = left + x;
                        if (cx < 0 || cx >= width) continue;
                        int index = indices[r * w + x];
                        if (index == transparent || index >= palette.Length) continue;
                        canvas[y * width + cx] = palette[index];
                    }
                }

                frames.Add((uint[])canvas.Clone());
                delays.Add(delay);
                if (disposal == 2)
                {
                    for (int y = Math.Max(0, top); y < Math.Min(height, top + h); y++)
                        for (int x = Math.Max(0, left); x < Math.Min(width, left + w); x++) canvas[y * width + x] = 0;
                }
                else if (previous is not null) canvas = previous;
                disposal = 0;
                transparent = -1;
                delay = TimeSpan.Zero;
            }

            if (frames.Count == 0) throw new InvalidDataException("no frames");
            return new GifFile(width, height, frames, delays);
        }

        private static uint[] Palette(byte[] b, ref int pos, int count)
        {
            var p = new uint[count];
            for (int i = 0; i < count; i++, pos += 3) p[i] = 0xFF000000u | (uint)b[pos] << 16 | (uint)b[pos + 1] << 8 | b[pos + 2];
            return p;
        }

        private static void SkipBlocks(byte[] b, ref int pos)
        {
            while (pos < b.Length && b[pos] != 0) pos += b[pos] + 1;
            pos++;
        }

        private static byte[] Data(byte[] b, ref int pos)
        {
            var data = new List<byte>();
            while (pos < b.Length && b[pos] != 0)
            {
                int n = b[pos++];
                data.AddRange(new ArraySegment<byte>(b, pos, Math.Min(n, b.Length - pos)));
                pos += n;
            }

            pos++;
            return data.ToArray();
        }

        // The variable-length LZW of the GIF format: a clear code resets the table, codes grow to twelve bits.
        private static byte[] Lzw(byte[] data, int minCode, int count)
        {
            var output = new byte[count];
            int clear = 1 << minCode, end = clear + 1, size = minCode + 1, next = end + 1, written = 0;
            var prefix = new int[4096];
            var suffix = new byte[4096];
            for (int i = 0; i < clear; i++) suffix[i] = (byte)i;
            var stack = new byte[4097];
            int old = -1, oldFirst = 0, bits = 0, acc = 0, at = 0;
            while (written < count)
            {
                while (bits < size)
                {
                    if (at >= data.Length) return output;
                    acc |= data[at++] << bits;
                    bits += 8;
                }

                int code = acc & ((1 << size) - 1);
                acc >>= size;
                bits -= size;
                if (code == clear)
                {
                    size = minCode + 1;
                    next = end + 1;
                    old = -1;
                    continue;
                }

                if (code == end) break;
                if (old == -1)
                {
                    if (code >= clear) break;
                    output[written++] = (byte)code;
                    old = oldFirst = code;
                    continue;
                }

                int sp = 0, c = code;
                if (code >= next)
                {
                    stack[sp++] = (byte)oldFirst;
                    c = old;
                }

                while (c >= clear)
                {
                    stack[sp++] = suffix[c];
                    c = prefix[c];
                }

                stack[sp++] = (byte)c;
                if (next < 4096)
                {
                    prefix[next] = old;
                    suffix[next] = (byte)c;
                    next++;
                    if (next == 1 << size && size < 12) size++;
                }

                while (sp > 0 && written < count) output[written++] = stack[--sp];
                old = code;
                oldFirst = c;
            }

            return output;
        }

        private static int[] Sequential(int h)
        {
            var rows = new int[h];
            for (int i = 0; i < h; i++) rows[i] = i;
            return rows;
        }

        // Interlaced rows arrive every 8th from 0, every 8th from 4, every 4th from 2, then every 2nd from 1.
        private static int[] Interlaced(int h)
        {
            var rows = new int[h];
            int n = 0;
            foreach ((int start, int step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
                for (int y = start; y < h; y += step) rows[n++] = y;
            return rows;
        }

        // A frame as a premultiplied bitmap, with a tint and saturation applied as ImagePixels applies them.
        internal Bitmap Frame(int index, ImageEffects effects)
        {
            uint[] src = Pixels[index];
            int stride = Width * 4;
            var px = new byte[stride * Height];
            for (int i = 0; i < src.Length; i++)
            {
                uint c = src[i];
                int a = (int)(c >> 24);
                px[i * 4 + 0] = (byte)(((c & 0xFF) * a + 127) / 255);
                px[i * 4 + 1] = (byte)(((c >> 8 & 0xFF) * a + 127) / 255);
                px[i * 4 + 2] = (byte)(((c >> 16 & 0xFF) * a + 127) / 255);
                px[i * 4 + 3] = (byte)a;
            }

            if (!effects.IsIdentity) ImagePixels.Apply(px, Width, Height, effects, bgra: true);
            var bitmap = new WriteableBitmap(new PixelSize(Width, Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using ILockedFramebuffer fb = bitmap.Lock();
            for (int y = 0; y < Height; y++) Marshal.Copy(px, y * stride, fb.Address + y * fb.RowBytes, stride);
            return bitmap;
        }
    }
}
