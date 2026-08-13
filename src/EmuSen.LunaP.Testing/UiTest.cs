using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Xunit;

namespace EmuSen.LunaP.Testing
{
    // A captured window, as plain RGBA8888.
    /// <summary>One captured frame: its raw RGBA pixels and the dimensions they came at.</summary>
    public readonly record struct RenderedFrame(byte[] Rgba, int Width, int Height)
    {
        // FNV-1a, which is enough to say "these two renders differ" and is not asked to do anything else.
        public ulong Hash
        {
            get
            {
                ulong hash = 14695981039346656037;
                foreach (byte b in Rgba) hash = (hash ^ b) * 1099511628211;
                return hash;
            }
        }

        // Stops early once the caller has seen enough; a flat-image check does not need the true total.
        public int DistinctColours(int stopAt = int.MaxValue)
        {
            var seen = new HashSet<uint>();
            for (int i = 0; i + 3 < Rgba.Length; i += 4)
            {
                seen.Add((uint)(Rgba[i] | (Rgba[i + 1] << 8) | (Rgba[i + 2] << 16)));
                if (seen.Count >= stopAt) break;
            }

            return seen.Count;
        }
    }

    // The one place a UI test dispatches, captures and asserts - see docs/LunaP.md §10.
    /// <summary>The one place a UI test dispatches onto the session, captures a frame and asserts about layout.</summary>
    public static class UiTest
    {
        // EMUSEN_UI_DUMP names a directory; every capture in the run lands in it as <name>.png.
        private const string DumpVariable = "EMUSEN_UI_DUMP";

        // Opt-in pixel-exact comparison, off unless both are set - see docs/LunaP.md §10.2.
        private const string BaselineVariable = "EMUSEN_UI_BASELINE";
        private const string BaselineModeVariable = "EMUSEN_UI_BASELINE_MODE";

        // Resolved from the CONSUMER's test assembly, not this one - UiSession explains why that
        // distinction only appears once the harness ships as a package.
        public static HeadlessUnitTestSession Session => UiSession.Current;

        // Window and control construction is only valid on the one dispatcher this session owns.
        public static Task Run(Action body) => Session.Dispatch(body, default);

        public static RenderedFrame Capture(Window window)
        {
            using WriteableBitmap bitmap = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException($"{window.GetType().Name} captured no frame - it was probably never shown.");

            return Capture(bitmap);
        }

        public static RenderedFrame Capture(WriteableBitmap bitmap)
        {
            int width = bitmap.PixelSize.Width;
            int height = bitmap.PixelSize.Height;
            var rgba = new byte[width * height * 4];
            using (ILockedFramebuffer fb = bitmap.Lock()) Marshal.Copy(fb.Address, rgba, 0, rgba.Length);
            return new RenderedFrame(rgba, width, height);
        }

        // The always-on assertion: a window that failed to lay out, or whose controls have no template, renders as one flat colour.
        public static RenderedFrame AssertLaidOut(Window window, string name, int minColours = 8)
        {
            using WriteableBitmap bitmap = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException($"{window.GetType().Name} captured no frame - it was probably never shown.");

            RenderedFrame frame = Capture(bitmap);
            Dump(name, bitmap);

            int distinct = frame.DistinctColours(minColours + 1);
            Assert.True(distinct > minColours,
                $"{name} rendered {distinct} distinct colours - layout or templating probably failed. Set {DumpVariable} to a directory and look at it.");

            AssertMatchesBaseline(name, frame);
            return frame;
        }

        // Builds and renders THREE times. A window that fails this can never be compared against a
        // baseline - see docs/LunaP.md §10.2 and §37.
        //
        // WHY THREE, AND WHY THE MESSAGE CARRIES NUMBERS. This assertion used to render twice and,
        // on failure, state a cause: "it shows something live (a clock, a pid, a counter)". That is
        // a hypothesis, and the first time it fired in anger - macOS on the §35 matrix, with Linux
        // and Windows green - the hypothesis was wrong. The gallery has no clock and no counter, and
        // the same binary is stable on the other two runners.
        //
        // An assertion that names a cause it has not measured sends the reader to look for something
        // that is not there, which is the §29.3.1 failure in a different costume. So it reports what
        // it saw and lets the reader conclude:
        //
        //   first != second, second == third  -> the FIRST render differs from the steady state,
        //                                        which is a warm-up effect (glyph caches, lazily
        //                                        realised platform resources), not live content.
        //   all three differ                  -> genuinely non-deterministic rendering.
        //
        // The pixel count matters as much as the verdict: two frames differing in 40 bytes along one
        // glyph edge is antialiasing, and two differing in half the buffer is a different window.
        public static void AssertStable(string name, Func<Window> build)
        {
            RenderedFrame first = Once(build);
            RenderedFrame second = Once(build);
            if (first.Hash == second.Hash) return;

            RenderedFrame third = Once(build);

            string verdict = second.Hash == third.Hash
                ? "the FIRST render differs from the steady state, which is a warm-up effect rather than live content"
                : "every render differs, so this is genuinely non-deterministic";

            Assert.Fail(
                $"{name} did not render the same way twice. {verdict}.\n"
                + $"  hashes : {first.Hash:X16} {second.Hash:X16} {third.Hash:X16}\n"
                + $"  size   : {first.Width}x{first.Height}, {second.Width}x{second.Height}, {third.Width}x{third.Height}\n"
                + $"  bytes differing 1v2: {Differing(first, second)}, 2v3: {Differing(second, third)}\n"
                + "It is not a valid baseline target until this is understood. See docs/LunaP.md §37.");
        }

        private static RenderedFrame Once(Func<Window> build)
        {
            Window window = build();
            window.Show();
            RenderedFrame frame = Capture(window);
            window.Close();
            return frame;
        }

        // "How different" rather than "different", because the two answers point at different causes.
        private static string Differing(RenderedFrame a, RenderedFrame b)
        {
            if (a.Rgba.Length != b.Rgba.Length) return $"incomparable ({a.Rgba.Length} vs {b.Rgba.Length} bytes)";

            int count = 0;
            for (int i = 0; i < a.Rgba.Length; i++)
            {
                if (a.Rgba[i] != b.Rgba[i]) count++;
            }

            return $"{count} of {a.Rgba.Length} ({100.0 * count / a.Rgba.Length:F2}%)";
        }

        // Avalonia's own encoder, so this project needs no imaging dependency of its own.
        public static void Dump(string name, WriteableBitmap bitmap)
        {
            if (Environment.GetEnvironmentVariable(DumpVariable) is not { Length: > 0 } directory) return;

            Directory.CreateDirectory(directory);
            using FileStream file = File.Create(Path.Combine(directory, name + ".png"));
            bitmap.Save(file, new PngBitmapEncoderOptions());
        }

        // No-op unless a baseline directory and mode are both set; the caller's own assertions are what run in CI.
        public static void AssertMatchesBaseline(string name, RenderedFrame frame)
        {
            if (Environment.GetEnvironmentVariable(BaselineVariable) is not { Length: > 0 } directory) return;

            string mode = Environment.GetEnvironmentVariable(BaselineModeVariable) ?? "compare";
            string path = Path.Combine(directory, name + ".frame");

            if (string.Equals(mode, "write", StringComparison.OrdinalIgnoreCase))
            {
                WriteBaseline(path, frame);
                return;
            }

            Assert.True(File.Exists(path), $"No baseline for {name} at {path}. Run once with {BaselineModeVariable}=write first.");

            RenderedFrame baseline = ReadBaseline(path);
            Assert.True(baseline.Width == frame.Width && baseline.Height == frame.Height,
                $"{name} changed size: baseline {baseline.Width}x{baseline.Height}, now {frame.Width}x{frame.Height}.");

            Assert.True(baseline.Hash == frame.Hash, $"{name} rendered {DifferingPixels(baseline, frame)} pixels differently from its baseline.");
        }

        private static int DifferingPixels(RenderedFrame a, RenderedFrame b)
        {
            int differing = 0;
            for (int i = 0; i + 3 < a.Rgba.Length; i += 4)
            {
                if (a.Rgba[i] != b.Rgba[i] || a.Rgba[i + 1] != b.Rgba[i + 1] || a.Rgba[i + 2] != b.Rgba[i + 2]) differing++;
            }

            return differing;
        }

        // Dimensions then raw pixels, one file, so a half-written pair can never be mistaken for a match.
        private static void WriteBaseline(string path, RenderedFrame frame)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var stream = new BinaryWriter(File.Create(path));
            stream.Write(frame.Width);
            stream.Write(frame.Height);
            stream.Write(frame.Rgba);
        }

        private static RenderedFrame ReadBaseline(string path)
        {
            using var stream = new BinaryReader(File.OpenRead(path));
            int width = stream.ReadInt32();
            int height = stream.ReadInt32();
            return new RenderedFrame(stream.ReadBytes(width * height * 4), width, height);
        }
    }
}
