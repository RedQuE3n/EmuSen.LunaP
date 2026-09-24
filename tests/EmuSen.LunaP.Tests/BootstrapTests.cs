using System;
using Avalonia;
using EmuSen.LunaP;
using EmuSen.LunaP.Platform;
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
        // What With() would bind at Setup, read from its closure so the session's own locator is never touched - §92.
        private static X11PlatformOptions? BoundX11Options(AppBuilder builder)
        {
            var field = typeof(AppBuilder).GetField("_optionsInitializers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            if (field!.GetValue(builder) is not Delegate initializers) return null;
            foreach (Delegate d in initializers.GetInvocationList())
                foreach (var captured in d.Target?.GetType().GetFields() ?? [])
                    if (captured.GetValue(d.Target) is X11PlatformOptions options) return options;
            return null;
        }

        [Fact]
        public void Embedded_popups_bind_x11_options_that_draw_popups_inside_the_window()
        {
            if (!OperatingSystem.IsLinux()) return;
            Assert.Null(BoundX11Options(LunaApp.Configure<SampleApp>()));
            Assert.Null(BoundX11Options(LunaApp.Configure<SampleApp>().EmbedPopups(false)));
            Assert.True(BoundX11Options(LunaApp.Configure<SampleApp>().EmbedPopups())?.OverlayPopups);
        }

        [Fact]
        public void The_bootstrap_selects_the_platform_renderer_and_text_shaper()
        {
            AppBuilder builder = LunaApp.Configure<SampleApp>();

            Assert.Equal("Skia", builder.RenderingSubsystemName);
            Assert.Equal("HarfBuzz", builder.TextShapingSubsystemName);
        }

        // The §87 seam, and what it records at the moment it is handed the builder.
        //
        // The interesting assertion is not that Install ran - it is WHAT THE BUILDER ALREADY HAD
        // when it ran. UsePlatformDetect does three jobs, and the seam path replaces exactly one of
        // them; if LunaP ever installed Skia after handing over instead of before, a host would
        // receive a half-built builder and the failure would appear at Setup in somebody else's
        // application. So the backend records the two subsystem names it was given.
        private sealed class RecordingBackend : IWindowingBackend
        {
            public bool Installed { get; private set; }

            public string? RenderingWhenCalled { get; private set; }

            public string? TextShapingWhenCalled { get; private set; }

            public AppBuilder Install(AppBuilder builder)
            {
                Installed = true;
                RenderingWhenCalled = builder.RenderingSubsystemName;
                TextShapingWhenCalled = builder.TextShapingSubsystemName;

                // A real windowing subsystem, because a builder without one is not a fair sample of
                // what a host hands back. Never reaches Setup, so X11 is never contacted.
                return builder.UseX11();
            }
        }

        [Fact]
        public void The_seam_overload_installs_the_hosts_backend()
        {
            var backend = new RecordingBackend();

            LunaApp.Configure<SampleApp>(backend);

            Assert.True(backend.Installed);
        }

        // The contract the seam's own documentation states, pinned rather than trusted: the renderer
        // and the text shaper are LunaP's job on this path, and they are already there before the
        // host is asked for anything. Move either call after Install and this turns red.
        [Fact]
        public void The_seam_receives_a_builder_that_already_has_the_renderer_and_text_shaper()
        {
            var backend = new RecordingBackend();

            LunaApp.Configure<SampleApp>(backend);

            Assert.Equal("Skia", backend.RenderingWhenCalled);
            Assert.Equal("HarfBuzz", backend.TextShapingWhenCalled);
        }

        [Fact]
        public void The_seam_overload_keeps_the_application_type_and_subsystems()
        {
            AppBuilder builder = LunaApp.Configure<SampleApp>(new RecordingBackend());

            Assert.Equal(typeof(SampleApp), builder.ApplicationType);
            Assert.Equal("Skia", builder.RenderingSubsystemName);
            Assert.Equal("HarfBuzz", builder.TextShapingSubsystemName);
        }

        // A null backend is a programming error at startup, and startup is where it should be heard.
        // Falling through to platform detection would silently give a Wayland host an X11 session.
        [Fact]
        public void A_null_backend_is_refused_rather_than_ignored()
        {
            Assert.Throws<ArgumentNullException>(() => LunaApp.Configure<SampleApp>((IWindowingBackend)null!));
            Assert.Throws<ArgumentNullException>(() => LunaApp.Configure(() => new SampleApp(), null!));
        }

        [Fact]
        public void The_factory_seam_overload_configures_the_same_way()
        {
            var backend = new RecordingBackend();
            var made = new SampleApp();

            AppBuilder builder = LunaApp.Configure(() => made, backend);

            Assert.True(backend.Installed);
            Assert.Equal(typeof(SampleApp), builder.ApplicationType);
            Assert.Equal("Skia", backend.RenderingWhenCalled);
        }

        [Fact]
        public void The_bootstrap_keeps_the_application_type()
        {
            Assert.Equal(typeof(SampleApp), LunaApp.Configure<SampleApp>().ApplicationType);
        }

        // The overload a consumer needed, because its Main fully resolves its subject and builds before
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
