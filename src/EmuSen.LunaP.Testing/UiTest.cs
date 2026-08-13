using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
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

        // Cheap and literal: whatever has already been drawn. See Redraw for why that is not always
        // the frame you want.
        public static RenderedFrame Capture(Window window)
        {
            using WriteableBitmap bitmap = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException($"{window.GetType().Name} captured no frame - it was probably never shown.");

            return Capture(bitmap);
        }

        // A frame that is NOT the window's first draw, which on macOS is a different picture - see
        // docs/LunaP.md §38.
        //
        // WHY THIS CANNOT JUST CAPTURE TWICE, which is what it looked like it should be. Measured on
        // Avalonia 12.1.0 with a custom draw operation counting actual draw passes (§38.1):
        //
        //   capture an unchanged window again   -> NO draw happens. CaptureRenderedFrame runs the
        //                                          dispatcher, then GetLastRenderedFrame COPIES the
        //                                          frame already held by the window impl. Two
        //                                          captures of a clean window are the same picture
        //                                          by construction, not by agreement.
        //   InvalidateVisual on an ancestor     -> still no draw of the descendant. Every visual
        //                                          owns its own draw list, and dirtying a parent
        //                                          does not dirty what it contains.
        //   InvalidateVisual on every visual    -> a real second draw pass.
        //
        // So the loop is the whole method, and dropping it turns this back into a copy. That is not
        // a subtle failure to have shipped: the fix for §37.3 was very nearly "capture it twice",
        // which would have looked right, changed nothing, and been written down as a fix.
        // RenderPassTests pins all three of those results so the next Avalonia bump has to agree.
        //
        // The leading capture is there so the frame handed back is never the first draw even when
        // the caller has not captured yet - if Show() has not drawn, that call is draw one.
        /// <summary>Forces a genuine second render pass and captures that, rather than the window's first draw.</summary>
        public static RenderedFrame Redraw(Window window)
        {
            using WriteableBitmap bitmap = Rasterise(window);
            return Capture(bitmap);
        }

        private static WriteableBitmap Rasterise(Window window)
        {
            // Disposed rather than discarded: GetLastRenderedFrame allocates a fresh bitmap on every
            // call, so a dropped one is a full frame of unmanaged memory held until a collection.
            window.CaptureRenderedFrame()?.Dispose();
            foreach (Visual visual in window.GetSelfAndVisualDescendants()) visual.InvalidateVisual();

            return window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException($"{window.GetType().Name} captured no frame - it was probably never shown.");
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
        //
        // It rasterises through Redraw's path rather than capturing directly, because it ends by
        // handing the frame to AssertMatchesBaseline and the window's FIRST draw is not a safe thing
        // to compare against a baseline (§38). The colour count does not care either way; the
        // baseline comparison is the reason.
        public static RenderedFrame AssertLaidOut(Window window, string name, int minColours = 8)
        {
            using WriteableBitmap bitmap = Rasterise(window);

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
            // THE FIRST FRAME IS THROWN AWAY, and §37 is the measurement that put it there. On
            // macOS the first render of a process differs from every render after it by 0.39% of
            // the buffer - 12,741 bytes of 3,225,600, along glyph edges - while renders two and
            // three are byte-identical. Linux and Windows show no such warm-up.
            //
            // Discarding it is not weakening the assertion, it is asking the right question. What
            // this method exists to answer is "can this window be compared against a baseline",
            // and the answer on macOS is yes from the second frame on. A comparison that included
            // the cold frame would report every macOS window as unstable and be wrong about all of
            // them. What it still catches is unchanged: a window whose steady state moves.
            //
            // It throws away a whole BUILD rather than calling Redraw, and the difference is the
            // point: comparing two separately constructed windows also catches construction that
            // is not deterministic, which a redraw of one window cannot see. §38.2 is why the
            // discarded build is a genuine draw pass and a second capture would not have been.
            _ = Once(build, name + ".warmup");

            RenderedFrame first = Once(build, name + ".render1");
            RenderedFrame second = Once(build, name + ".render2");
            if (first.Hash == second.Hash) return;

            RenderedFrame third = Once(build, name + ".render3");

            string verdict = second.Hash == third.Hash
                ? "the FIRST render differs from the steady state, which is a warm-up effect rather than live content"
                : "every render differs, so this is genuinely non-deterministic";

            Assert.Fail(
                $"{name} did not render the same way twice. {verdict}.\n"
                + $"  hashes : {first.Hash:X16} {second.Hash:X16} {third.Hash:X16}\n"
                + $"  size   : {first.Width}x{first.Height}, {second.Width}x{second.Height}, {third.Width}x{third.Height}\n"
                + $"  1 vs 2 : {Describe(first, second)}\n"
                + $"  2 vs 3 : {Describe(second, third)}\n"
                + $"Set {DumpVariable} to a directory to get {name}.render1/2/3.png out of this run.\n"
                + "It is not a valid baseline target until this is understood. See docs/LunaP.md §37.");
        }

        private static RenderedFrame Once(Func<Window> build, string? dumpAs = null)
        {
            Window window = build();
            window.Show();

            using (WriteableBitmap bitmap = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException($"{window.GetType().Name} captured no frame - it was probably never shown."))
            {
                if (dumpAs is not null) Dump(dumpAs, bitmap);
                RenderedFrame frame = Capture(bitmap);
                window.Close();
                return frame;
            }
        }

        // WHAT THE DIFFERENCE LOOKS LIKE, not just how big it is, because the two candidate causes
        // produce the same byte count and completely different shapes:
        //
        //   antialiasing        thousands of pixels, spread over most of the image, each off by a
        //                       little - a large box and a small peak delta.
        //   content that moved  a tight box somewhere, with pixels off by a lot.
        //
        // §37 had to be diagnosed from a byte count and an argument about what the gallery contains,
        // and §37.4 recorded "nobody has dumped the two frames and looked at them" as an open item.
        // This is that item closed in the message itself: a reader gets the shape without opening a
        // PNG, and Dump is still there when they want to.
        private static string Describe(RenderedFrame a, RenderedFrame b)
        {
            if (a.Rgba.Length != b.Rgba.Length) return $"incomparable ({a.Rgba.Length} vs {b.Rgba.Length} bytes)";

            int bytes = 0, pixels = 0, peak = 0;
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;

            for (int i = 0; i + 3 < a.Rgba.Length; i += 4)
            {
                bool differs = false;
                for (int c = 0; c < 4; c++)
                {
                    int delta = Math.Abs(a.Rgba[i + c] - b.Rgba[i + c]);
                    if (delta == 0) continue;

                    bytes++;
                    differs = true;
                    if (delta > peak) peak = delta;
                }

                if (!differs) continue;

                pixels++;
                int pixel = i / 4;
                int x = pixel % a.Width, y = pixel / a.Width;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }

            if (pixels == 0) return "identical";

            return $"{bytes} of {a.Rgba.Length} bytes ({100.0 * bytes / a.Rgba.Length:F2}%), "
                + $"{pixels} pixels, peak channel delta {peak}/255, "
                + $"within ({left},{top})-({right},{bottom}) of {a.Width}x{a.Height}";
        }

        // Avalonia's own encoder, so this project needs no imaging dependency of its own.
        public static void Dump(string name, WriteableBitmap bitmap)
        {
            if (Environment.GetEnvironmentVariable(DumpVariable) is not { Length: > 0 } directory) return;

            Directory.CreateDirectory(directory);
            using FileStream file = File.Create(Path.Combine(directory, name + ".png"));
            bitmap.Save(file, new PngBitmapEncoderOptions());
        }

        // The overload to reach for, and the reason the other one is easy to misuse: this one owns
        // the rasterisation, so the frame it compares is never the window's first draw (§38).
        /// <summary>Redraws the window and compares that frame against its stored baseline.</summary>
        public static void AssertMatchesBaseline(string name, Window window) =>
            AssertMatchesBaseline(name, Redraw(window));

        // No-op unless a baseline directory and mode are both set; the caller's own assertions are what run in CI.
        //
        // THE FRAME YOU PASS HERE MUST NOT BE THE WINDOW'S FIRST DRAW. This overload compares
        // whatever it is handed and cannot rasterise anything itself, so on macOS a baseline written
        // from a first draw and compared against any later one mismatches on twelve thousand bytes
        // of antialiasing with nothing wrong - measured, §37.1. Prefer the Window overload above,
        // which cannot get this wrong; if you already hold a frame, get it from Redraw rather than
        // from Capture. §38.2 is why "capture it twice" is not the workaround it appears to be.
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
