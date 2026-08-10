using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

[assembly: AvaloniaTestApplication(typeof(EmuSen.LunaP.Tests.TestAppBuilder))]

namespace EmuSen.LunaP.Tests
{
    // Read by HeadlessUnitTestSession.GetOrStartForAssembly to build the one shared headless app every test dispatches onto.
    public class TestAppBuilder
    {
        // UseSkia rather than the headless drawing stub, so a captured frame goes through a real render pass - see docs/LunaP.md §10.
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<Application>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .UseSkia()
                .AfterSetup(builder =>
                {
                    // The real LunaTheme.axaml, not a hand-built lookalike: a headless pass that
                    // misses it asserts over untemplated controls and passes green - see docs/LunaP.md §3.1.
                    builder.Instance!.Styles.Add(new StyleInclude(null as System.Uri)
                    {
                        Source = new System.Uri("avares://EmuSen.LunaP/Theme/LunaTheme.axaml"),
                    });
                    builder.Instance.RequestedThemeVariant = ThemeVariant.Dark;
                });
    }
}
