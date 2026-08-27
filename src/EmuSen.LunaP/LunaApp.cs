using System;
using Avalonia;
using EmuSen.LunaP.Platform;

namespace EmuSen.LunaP
{
    // The one Avalonia bootstrap sequence, previously spelled out in both frontends' Program.cs and a third time in WiseMan - see docs/LunaP.md §3.
    /// <summary>The Avalonia bootstrap sequence an application's Program.cs would otherwise spell out.</summary>
    public static class LunaApp
    {
        /// <summary>The Avalonia bootstrap sequence, with LunaP theme applied after setup.</summary>
        /// <typeparam name="TApp">Your Application type, constructed by Avalonia.</typeparam>
        /// <returns>A builder to call StartWithClassicDesktopLifetime on.</returns>
        public static AppBuilder Configure<TApp>() where TApp : Application, new() =>
            Finish(AppBuilder.Configure<TApp>(), null);

        // The seam overloads, added at §87. A host that supplies a backend takes over ONE of the
        // three jobs UsePlatformDetect does; the other two stay here, where they cannot be
        // forgotten. See Platform/IWindowingBackend.cs for why the toolkit cannot install Wayland
        // itself.
        /// <summary>The bootstrap with a windowing backend the host supplies, rather than platform detection.</summary>
        /// <typeparam name="TApp">Your Application type, constructed by Avalonia.</typeparam>
        /// <param name="windowing">The backend to install. LunaP installs the renderer and text shaper itself.</param>
        /// <returns>A builder to call StartWithClassicDesktopLifetime on.</returns>
        public static AppBuilder Configure<TApp>(IWindowingBackend windowing) where TApp : Application, new() =>
            Finish(AppBuilder.Configure<TApp>(), windowing ?? throw new ArgumentNullException(nameof(windowing)));

        // For a caller whose Main must fully resolve something - a document, a device, a session -
        // before any Avalonia type is touched, and so hands Avalonia an App it built itself. One
        // consumer needed exactly that and is why this overload exists; docs/LunaP.md §3 names it.
        /// <summary>The same bootstrap for an application that must be constructed by the caller.</summary>
        /// <typeparam name="TApp">Your Application type.</typeparam>
        /// <param name="factory">Builds the application. For a Main that has to resolve something before any Avalonia type is touched.</param>
        /// <returns>A builder to call StartWithClassicDesktopLifetime on.</returns>
        public static AppBuilder Configure<TApp>(Func<TApp> factory) where TApp : Application =>
            Finish(AppBuilder.Configure(factory), null);

        /// <summary>The factory overload, with a windowing backend the host supplies.</summary>
        /// <typeparam name="TApp">Your Application type.</typeparam>
        /// <param name="factory">Builds the application, for a Main that must resolve something first.</param>
        /// <param name="windowing">The backend to install. LunaP installs the renderer and text shaper itself.</param>
        /// <returns>A builder to call StartWithClassicDesktopLifetime on.</returns>
        public static AppBuilder Configure<TApp>(Func<TApp> factory, IWindowingBackend windowing) where TApp : Application =>
            Finish(AppBuilder.Configure(factory), windowing ?? throw new ArgumentNullException(nameof(windowing)));

        private static AppBuilder Finish(AppBuilder builder, IWindowingBackend? windowing)
        {
            // AfterSetup, because the saved theme merges into Application.Current.Resources and needs the instance to exist.
            // The host-supplied path does not call UsePlatformDetect at all, because on Linux it
            // would select X11 and there would be no way to say otherwise. Skia and HarfBuzz are
            // installed here rather than left to the host: they are the two jobs UsePlatformDetect
            // does that have nothing to do with choosing a display server, and a host that had to
            // remember them would eventually not.
            if (windowing is not null)
            {
                builder = builder.UseSkia().UseHarfBuzz().WithInterFont().LogToTrace()
                    .AfterSetup(_ =>
                    {
                        Theme.LunaTheme.ApplyVariant();
                        Theme.LunaTheme.ApplySaved();
                    });

                return windowing.Install(builder);
            }

            builder = builder.UsePlatformDetect().WithInterFont().LogToTrace()
                .AfterSetup(_ =>
                {
                    // Before the saved theme, so a theme that overrides palette keys lands on top
                    // of the right variant rather than being re-resolved out from under itself.
                    Theme.LunaTheme.ApplyVariant();
                    Theme.LunaTheme.ApplySaved();
                });

            // Kept, and no longer claimed to do anything - see docs/LunaP.md §35.1.
            //
            // This line arrived with the note "UsePlatformDetect does not pick X11 on a Wayland
            // session", citing section 2a of EmuSen_Project_Overview_v2.md - a document that stayed
            // behind when LunaP left EmuSen (§19, §20), so the measurement behind it is unreachable
            // to anybody reading this repository, including us. (Spelled out rather than written as
            // a § so it cannot be mistaken for a citation into this project's own man page.)
            //
            // MEASURED ON AVALONIA 12.1.0, AND IT IS CURRENTLY A NO-OP: `UsePlatformDetect()` alone
            // and `UsePlatformDetect().UseX11()` leave the identical windowing initializer on the
            // builder. So this cannot be tested from here - an assertion that X11 was selected
            // passes with the call removed - and BootstrapTests says so rather than pinning it.
            //
            // It stays anyway. The original correction was made against a real symptom on a real
            // session, the failure it prevented would appear at Setup on somebody's Wayland desktop
            // rather than in any suite here, and removing a guard because its justification became
            // hard to read is how the symptom comes back. §35.1 records what it would take to
            // retire it honestly.
            return OperatingSystem.IsLinux() ? builder.UseX11() : builder;
        }
    }
}
