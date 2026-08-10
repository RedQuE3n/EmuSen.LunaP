using Avalonia;
using Avalonia.Headless;
using EmuSen.LunaP.Testing;

[assembly: AvaloniaTestApplication(typeof(EmuSen.LunaP.Tests.TestAppBuilder))]

namespace EmuSen.LunaP.Tests
{
    // Read by HeadlessUnitTestSession to build the one shared headless app every test dispatches
    // onto. The body of it ships in EmuSen.LunaP.Testing now, so a consumer gets the same
    // application this suite runs against rather than a lookalike that misses the theme include -
    // see docs/LunaP.md §3.1 for what missing it costs, and §22.8 for the move.
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => LunaHeadless.BuildApp();
    }
}
