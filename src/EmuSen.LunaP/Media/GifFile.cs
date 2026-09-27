using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace EmuSen.LunaP.Media
{
    // A GIF read into frame records, composited into whole frames on demand by each frame's disposal method - see docs/LunaP.md §194.
    internal sealed class GifFile
    {
        private static readonly Dictionary<string, GifFile?> Files = new(StringComparer.Ordinal);

        // One image block as the file holds it: its rectangle, colours and still-compressed codes.
        private sealed record Record(int Left, int Top, int W, int H, bool Interlaced, uint[] Palette, int Transparent, int Disposal, int MinCode, byte[] Data);

        // The canvas before every sixteenth frame is drawn, kept once reached, so a jump back recomposes at most sixteen frames.
        private const int KeyEvery = 16;

        private readonly IReadOnlyList<Record> _records;
        private readonly Dictionary<int, uint[]> _keys = new();
        private (int Index, uint[] Before)? _next;

        private GifFile(int width, int height, IReadOnlyList<Record> records, IReadOnlyList<TimeSpan> delays)
        {
            Width = width;
            Height = height;
            _records = records;
            Delays = delays;
            _keys[0] = new uint[width * height];
        }

        internal int Width { get; }
        internal int Height { get; }
        internal IReadOnlyList<TimeSpan> Delays { get; }

        internal int Count => _records.Count;

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
            var records = new List<Record>();
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
                if (minCode is < 2 or > 11) throw new InvalidDataException("bad code size");
                records.Add(new Record(left, top, w, h, (flags & 0x40) != 0, palette, transparent, disposal, minCode, Data(b, ref pos)));
                delays.Add(delay);
                disposal = 0;
                transparent = -1;
                delay = TimeSpan.Zero;
            }

            if (records.Count == 0) throw new InvalidDataException("no frames");
            return new GifFile(width, height, records, delays);
        }

        // A frame as shown: straight 0xAARRGGBB over the whole logical screen. Stepping forward draws one frame; a jump back at most sixteen.
        internal uint[] Pixels(int index)
        {
            index = Math.Clamp(index, 0, Count - 1);
            int from;
            uint[] canvas;
            if (_next is { } n && n.Index <= index && index - n.Index < KeyEvery) (from, canvas) = (n.Index, (uint[])n.Before.Clone());
            else
            {
                from = index / KeyEvery * KeyEvery;
                while (!_keys.ContainsKey(from)) from -= KeyEvery;
                canvas = (uint[])_keys[from].Clone();
            }

            for (int j = from; ; j++)
            {
                if (j % KeyEvery == 0) _keys.TryAdd(j, (uint[])canvas.Clone());
                uint[] before = _records[j].Disposal == 3 ? (uint[])canvas.Clone() : canvas;
                Draw(_records[j], canvas);
                if (j == index)
                {
                    uint[] shown = (uint[])canvas.Clone();
                    if (j + 1 < Count) _next = (j + 1, Dispose(_records[j], canvas, before));
                    return shown;
                }

                canvas = Dispose(_records[j], canvas, before);
            }
        }

        private uint[] Dispose(Record r, uint[] canvas, uint[] before)
        {
            if (r.Disposal == 3) return before;
            if (r.Disposal == 2)
                for (int y = Math.Max(0, r.Top); y < Math.Min(Height, r.Top + r.H); y++)
                    for (int x = Math.Max(0, r.Left); x < Math.Min(Width, r.Left + r.W); x++) canvas[y * Width + x] = 0;
            return canvas;
        }

        private void Draw(Record r, uint[] canvas)
        {
            byte[] indices = Lzw(r.Data, r.MinCode, r.W * r.H);
            int[] rows = r.Interlaced ? Interlaced(r.H) : Sequential(r.H);
            for (int row = 0; row < r.H; row++)
            {
                int y = r.Top + rows[row];
                if (y < 0 || y >= Height) continue;
                for (int x = 0; x < r.W; x++)
                {
                    int cx = r.Left + x;
                    if (cx < 0 || cx >= Width) continue;
                    int i = indices[row * r.W + x];
                    if (i == r.Transparent || i >= r.Palette.Length) continue;
                    canvas[y * Width + cx] = r.Palette[i];
                }
            }
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
            uint[] src = Pixels(index);
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
