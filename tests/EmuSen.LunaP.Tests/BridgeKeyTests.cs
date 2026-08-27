using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // AN OVERRIDE THAT NAMES A KEY NOBODY READS IS A NO-OP THAT LOOKS LIKE A FIX - docs/LunaP.md §85.12.
    //
    // Theme/FluentBridge.axaml works by redefining keys FluentTheme's own templates resolve. The
    // whole mechanism depends on the key name being right, and a wrong name fails in the worst
    // available way: the file compiles, the dictionary merges, the key resolves - to the entry we
    // just added, which nothing reads - and the control keeps painting Fluent's colour. Nothing
    // errors. §5.5 is the same shape from the styles side, and §29.2 from the include side.
    //
    // PaletteReachTests CATCHES THE COMMON CASE ALREADY, and this file exists for what it cannot
    // see. That sweep asserts the OUTCOME - the colours a control actually paints - so a misspelled
    // key shows up as the control staying Fluent. But it only sees controls it sweeps, at rest, in
    // one variant. A key that is dead because a later Avalonia renamed it, while some OTHER
    // override still happens to cover the same control, is invisible to it. So is every key that
    // only applies in a state nothing drives (§85.11).
    //
    // THE INVARIANT IS THAT WE ONLY OVERRIDE THINGS THAT ALREADY EXIST. A bridge key must be a key
    // something outside the bridge declares - otherwise we are not overriding, we are inventing,
    // and inventing a key is exactly what a typo does.
    public class BridgeKeyTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(BridgeKeyTests).GetTypeInfo().Assembly);

        private const string BridgeUri = "avares://EmuSen.LunaP/Theme/FluentBridge.axaml";

        // KEYS THAT ARE REAL BUT THAT FluentTheme DOES NOT DECLARE, with how that was established.
        // An entry here is a claim that something outside both the bridge and FluentTheme supplies
        // the key, and it has to be as checkable as an assertion.
        private static readonly Dictionary<string, string> PlatformProvided = new()
        {
            ["SystemAccentColor"] =
                "Avalonia supplies it through the platform, not through any dictionary FluentTheme "
                + "declares. Measured by deleting the bridge's entry and asking the live application "
                + "for it anyway: it resolved, to #ff0078d7, as a Color rather than a Brush. That "
                + "absence is also why the §85.10 probe could not find it - the candidate list was "
                + "built by walking FluentTheme, and this key is not in there to be walked.",
        };

        // The bridge's own keys, loaded from the assembly by URI rather than parsed off disk: this
        // is the dictionary that actually ships in the package, and a test that reads the source
        // file would keep passing if the build stopped including it.
        private static SortedSet<string> BridgeKeys() =>
            PaletteSweep.KeysOf(AvaloniaXamlLoader.Load(new Uri(BridgeUri)));

        [Fact]
        public Task Every_override_names_a_key_something_else_declares() => Session.Dispatch(() =>
        {
            SortedSet<string> bridge = BridgeKeys();
            SortedSet<string> fluent = PaletteSweep.KeysOf(new FluentTheme());

            string[] invented = bridge
                .Where(k => !fluent.Contains(k) && !PlatformProvided.ContainsKey(k))
                .ToArray();

            Assert.True(invented.Length == 0,
                $"Theme/FluentBridge.axaml declares {invented.Length} key(s) that FluentTheme does not: "
                + string.Join(", ", invented)
                + Environment.NewLine
                + "An override only overrides something that already exists. A key nothing else declares "
                + "is a new resource nobody reads - the file compiles, the dictionary merges, and the "
                + "control keeps painting Fluent's colour. Check the spelling against the control's own "
                + "ControlTheme setters, or add it to PlatformProvided with how you established it is "
                + "real. See docs/LunaP.md §85.12.");
        }, default);

        // The other half, and it is a different question. The test above asks whether the key is
        // real; this asks whether OUR value is the one that wins. A key can be spelled correctly and
        // still be shadowed - by Application.Resources, by a dictionary merged later, by a
        // ControlTheme's own local resources - and the symptom is once again a control that quietly
        // stays Fluent.
        [Fact]
        public Task Every_override_resolves_to_a_palette_colour() => Session.Dispatch(() =>
        {
            var wrong = new List<string>();
            ThemeVariant restore = Application.Current!.RequestedThemeVariant ?? ThemeVariant.Dark;

            try
            {
            foreach (ThemeVariant variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                // THE ACTIVE VARIANT IS SWITCHED, not just passed as an argument, and that is
                // load-bearing. Every override in the bridge is a brush whose Color is a
                // DynamicResource, so one instance serves both columns and reports whichever is
                // currently active - asking it for the light column from dark mode hands back the
                // dark colour. PaletteVariantTests measured this and takes the same approach.
                Application.Current!.RequestedThemeVariant = variant;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                // Resolved per variant, because LunaPalette is the dark column only. Comparing the
                // light column against it reports the CORRECT light accent as a stray, which is
                // what the first run of this test did - §85.12.
                HashSet<Color> palette = PaletteSweep.PaletteFor(variant);

                foreach (string key in BridgeKeys())
                {
                    if (!Application.Current!.TryFindResource(key, variant, out object? value))
                    {
                        wrong.Add($"{key} ({variant}): does not resolve at all");
                        continue;
                    }

                    Color? colour = value switch
                    {
                        ISolidColorBrush brush => brush.Color,
                        Color raw => raw,
                        _ => null,
                    };

                    if (colour is null)
                    {
                        wrong.Add($"{key} ({variant}): resolves to {value?.GetType().Name}, not a colour");
                        continue;
                    }

                    if (!palette.Contains(colour.Value))
                        wrong.Add($"{key} ({variant}): resolves to {colour.Value}, which is not in that variant's palette");
                }
            }
            }
            finally
            {
                // The harness pins Dark (§3.1) and every other test assumes it.
                Application.Current!.RequestedThemeVariant = restore;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }

            Assert.True(wrong.Count == 0,
                $"{wrong.Count} bridge override(s) do not resolve to this palette's colours:"
                + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", wrong)
                + Environment.NewLine
                + "Either the value points at a Luna colour key that does not exist, or something is "
                + "shadowing the override. See docs/LunaP.md §85.12.");
        }, default);

        // Both tests above pass trivially against an empty set, and an AvaloniaXamlLoader that
        // silently returned an empty dictionary would produce exactly that. §26.11 and §48 both
        // caught this shape.
        [Fact]
        public Task The_check_has_subjects() => Session.Dispatch(() =>
        {
            int bridge = BridgeKeys().Count;
            int fluent = PaletteSweep.KeysOf(new FluentTheme()).Count;

            Assert.True(bridge >= 102, $"Only {bridge} bridge keys found; there were 102 when §85.11 was written.");
            Assert.True(fluent >= 1054, $"Only {fluent} FluentTheme keys found; there were 1054 when §85.12 was written.");
        }, default);
    }
}
