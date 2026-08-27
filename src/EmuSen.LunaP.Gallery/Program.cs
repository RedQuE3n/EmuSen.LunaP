using System;
using Avalonia;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Gallery
{
    // `dotnet run --project src/EmuSen.LunaP.Gallery`, which until §86.13 was not a thing anybody
    // could do - there was no OutputType in this repository at all.
    //
    // LunaApp.Configure owns the UsePlatformDetect/WithInterFont/LogToTrace/UseX11 sequence and is
    // the one place the Wayland/X11 correction lives (§3). Spelling the builder chain out here
    // would reproduce three quarters of it and drop that silently, which is the defect §3 records.
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args) =>
            LunaApp.Configure<GalleryApp>().StartWithClassicDesktopLifetime(args);
    }
}
