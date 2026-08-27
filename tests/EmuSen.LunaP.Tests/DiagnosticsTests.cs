using System;
using System.IO;
using EmuSen.LunaP.Settings;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // WHERE A DIAGNOSTIC GOES WHEN THE HOST HAS NOT SAID - see docs/LunaP.md §86.4.
    //
    // Until §86 this channel defaulted to discarding, which made the toolkit quiet about exactly
    // the things it should be loud about. The measurement that forced the change came from a
    // consumer rather than from here: deleting the sink installation from BIMA-CSharp's Program.cs
    // left all twenty of its tests green, because nothing in that suite runs Main. A whole
    // diagnostic channel was disconnected and nothing anywhere had an opinion.
    //
    // These tests exist because that is the shape of defect no ordinary test catches: the feature
    // still works, the failure is still handled, and the only thing missing is the sentence saying
    // it happened.
    //
    // THE SUITE IS SERIAL, which is what makes capturing a process-global stream safe here. The
    // assembly carries [CollectionBehavior(DisableTestParallelization = true)] and UiSession
    // refuses to start without it (§20.2), so no other class is mid-assertion while Console.Error
    // is swapped out from under it.
    public class DiagnosticsTests
    {
        // Swap both process-globals, run, and put them back whatever happens. Written once rather
        // than four times because a leaked Console.Error would not fail this class - it would fail
        // some later, unrelated one, which is the worst way to find a test that does not clean up.
        private static string CapturedStandardError(Action<string>? sink, Action body)
        {
            Action<string>? previousSink = LunaSettings.Diagnostics;
            TextWriter previousError = Console.Error;
            var captured = new StringWriter();
            try
            {
                LunaSettings.Diagnostics = sink;
                Console.SetError(captured);
                body();
            }
            finally
            {
                Console.SetError(previousError);
                LunaSettings.Diagnostics = previousSink;
            }

            return captured.ToString();
        }

        [Fact]
        public void A_report_with_no_sink_installed_goes_to_standard_error()
        {
            // The whole change. Sabotage: restore `Diagnostics?.Invoke(message)` and this is red,
            // because nothing is written anywhere at all.
            string written = CapturedStandardError(null, () => LunaSettings.Report("the theme would not parse"));

            Assert.Contains("the theme would not parse", written, StringComparison.Ordinal);
        }

        [Fact]
        public void A_report_with_no_sink_says_which_toolkit_it_came_from()
        {
            // It lands in a stream the toolkit does not own, interleaved with whatever else the
            // host writes. Without attribution it is a line somebody has to go looking for the
            // source of, and the search starts in their own code.
            string written = CapturedStandardError(null, () => LunaSettings.Report("something"));

            Assert.StartsWith("LunaP: ", written, StringComparison.Ordinal);
        }

        [Fact]
        public void Standard_error_and_never_standard_output()
        {
            // A diagnostic is not the program's output. On stdout it lands in whatever pipe the
            // caller arranged and corrupts it, which is the failure this whole audit is about
            // (docs/Unix conventions, §86.1). Asserting the negative because the positive above
            // cannot tell the two streams apart.
            TextWriter previousOut = Console.Out;
            var captured = new StringWriter();
            try
            {
                Console.SetOut(captured);
                CapturedStandardError(null, () => LunaSettings.Report("not for stdout"));
            }
            finally
            {
                Console.SetOut(previousOut);
            }

            Assert.Equal(string.Empty, captured.ToString());
        }

        [Fact]
        public void An_installed_sink_gets_the_message_without_the_prefix()
        {
            // The prefix belongs to the fallback path only. A host with its own log has its own
            // formatting, and "LunaP: " baked into the string would appear inside whatever it
            // wraps around it.
            string? heard = null;
            CapturedStandardError(m => heard = m, () => LunaSettings.Report("plain"));

            Assert.Equal("plain", heard);
        }

        [Fact]
        public void A_sink_that_does_nothing_is_how_a_host_asks_for_silence()
        {
            // The half of §86.4 that matters. Silence is still available; it is no longer the
            // default. A host that has said nothing gets the loud version, and a host that wants
            // quiet has to say so - which is the only arrangement where a silent channel means
            // somebody chose one.
            string written = CapturedStandardError(_ => { }, () => LunaSettings.Report("swallowed"));

            Assert.Equal(string.Empty, written);
        }
    }
}
