using Avalonia;

namespace EmuSen.LunaP.Platform
{
    // The seam between LunaP and a windowing backend the toolkit may not name - see docs/LunaP.md §87.
    //
    // WHY IT HAD TO EXIST, and the measurement behind it. `LunaApp.Configure` installs Avalonia's
    // platform detection, which on Linux resolves to X11 and cannot resolve to anything else.
    // That is not a preference: `Avalonia.Desktop`, the package detection ships in, depends on
    // Avalonia.Native, Avalonia, Avalonia.X11, Avalonia.HarfBuzz, Avalonia.Skia and Avalonia.Win32,
    // and NOT on Avalonia.Wayland. The Wayland backend is opt-in rather than absent, and nothing
    // inside this toolkit can opt in on a host's behalf, because §1 says LunaP references Avalonia
    // and nothing else. A `PackageReference` to Avalonia.Wayland here would be a second dependency
    // that only means anything on one operating system, imposed on every consumer of a
    // cross-platform toolkit.
    //
    // So the host installs it, the way `ISettingsStore` (§19.1) has a host supply storage.
    //
    // WHAT AN IMPLEMENTATION OWES, AND THE TRAP IT MUST AVOID. Install the WINDOWING subsystem and
    // nothing else. LunaP still installs the renderer and the text shaper on this path, because
    // `UsePlatformDetect` quietly does three jobs rather than one - windowing, Skia AND HarfBuzz -
    // and a host that replaced it wholesale gets `Setup` throwing "No rendering system configured",
    // then "No text shaping system configured" once the first is fixed. That is measured, not
    // predicted: it is what happens on the first run of a bootstrap written the obvious way.
    /// <summary>The seam between LunaP and a windowing backend the toolkit cannot reference itself.</summary>
    public interface IWindowingBackend
    {
        /// <summary>Installs the windowing subsystem on a builder LunaP has already configured.</summary>
        /// <param name="builder">The builder under construction, with the renderer and text shaper already installed.</param>
        /// <returns>The builder. Install the windowing subsystem ONLY - LunaP has already installed Skia and HarfBuzz, and an implementation that also calls UsePlatformDetect would put X11 back on top of whatever it just chose.</returns>
        AppBuilder Install(AppBuilder builder);
    }
}
