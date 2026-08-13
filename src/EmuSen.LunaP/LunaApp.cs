using System;
using Avalonia;

namespace EmuSen.LunaP
{
    // The one Avalonia bootstrap sequence, previously spelled out in both frontends' Program.cs and a third time in WiseMan - see docs/LunaP.md §3.
    /// <summary>The Avalonia bootstrap sequence an application's Program.cs would otherwise spell out.</summary>
    public static class LunaApp
    {
        public static AppBuilder Configure<TApp>() where TApp : Application, new() =>
            Finish(AppBuilder.Configure<TApp>());

        // Hotaru hands Avalonia an already-constructed App (its Main resolves a ROM before any Avalonia type is touched).
        public static AppBuilder Configure<TApp>(Func<TApp> factory) where TApp : Application =>
            Finish(AppBuilder.Configure(factory));

        private static AppBuilder Finish(AppBuilder builder)
        {
            // AfterSetup, because the saved theme merges into Application.Current.Resources and needs the instance to exist.
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
