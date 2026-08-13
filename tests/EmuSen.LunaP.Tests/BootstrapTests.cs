using System;
using Avalonia;
using EmuSen.LunaP;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // LunaApp.Configure, which until §35 was the only public entry point with no test at all.
    //
    // WHY IT WAS MISSED, because the reason is structural rather than an oversight. The headless
    // suite builds its own AppBuilder through LunaHeadless.BuildApp (§3.1), precisely so the harness
    // and a real frontend share one theme. That is right, and it means the sequence a consumer's
    // Program.cs actually calls is never executed by any test - every consumer's first line of
    // Avalonia code was unguarded.
    //
    // These assertions stop at the builder and never call Setup or Start. That is the whole trick:
    // AppBuilder.Configure only constructs a description of an application, so it can be inspected
    // in a headless run without a second Application fighting the session's.
    //
    // WHAT IS DELIBERATELY NOT ASSERTED HERE IS THE X11 CORRECTION, and §35.1 is the measurement.
    // On Avalonia 12.1.0, `AppBuilder.Configure<T>().UsePlatformDetect()` and LunaApp's
    // `...UsePlatformDetect().UseX11()` produce THE SAME windowing initializer - `<UseX11>b__0_0`
    // either way. An assertion that the initializer comes from UseX11 therefore passes whether or
    // not LunaApp.Configure calls UseX11 at all, which is a test that cannot fail, and §22.5's rule
    // is that one of those is not a test. The line stays in LunaApp; what it is worth is recorded
    // in §35.1 as a hazard rather than pinned here as a behaviour.
    public class BootstrapTests
    {
        private sealed class SampleApp : Application
        {
        }

        // UsePlatformDetect is in the chain, proven through what it installs rather than through its
        // own name: it is what brings Skia and HarfBuzz, and a builder missing it would come up with
        // no renderer and no text shaping. Dropping the call turns this red.
        [Fact]
        public void The_bootstrap_selects_the_platform_renderer_and_text_shaper()
        {
            AppBuilder builder = LunaApp.Configure<SampleApp>();

            Assert.Equal("Skia", builder.RenderingSubsystemName);
            Assert.Equal("HarfBuzz", builder.TextShapingSubsystemName);
        }

        [Fact]
        public void The_bootstrap_keeps_the_application_type()
        {
            Assert.Equal(typeof(SampleApp), LunaApp.Configure<SampleApp>().ApplicationType);
        }

        // The overload EmuSen.Hotaru needs, because its Main resolves a ROM and builds a core before
        // any Avalonia type is touched. It is a separate code path into the same Finish(), and an
        // overload that silently skipped the shared setup would give that application a window with
        // no theme and nothing would say so.
        [Fact]
        public void The_factory_overload_configures_the_same_way()
        {
            var made = new SampleApp();
            AppBuilder builder = LunaApp.Configure(() => made);

            Assert.Equal(typeof(SampleApp), builder.ApplicationType);
            Assert.Equal("Skia", builder.RenderingSubsystemName);
            Assert.Equal("HarfBuzz", builder.TextShapingSubsystemName);
        }
    }
}
