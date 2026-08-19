using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace EmuSen.LunaP.Testing
{
    // Finding the one headless session a suite dispatches onto, and refusing to hand it over when
    // the suite is configured in a way that is known to corrupt it - see docs/LunaP.md §22.8.
    /// <summary>The one headless Avalonia session a test assembly dispatches onto.</summary>
    public static class UiSession
    {
        private static HeadlessUnitTestSession? _session;
        private static Assembly? _assembly;

        // Set this to true only if the suite serialises its test classes some other way than the
        // assembly attribute - a single shared xunit collection does the same job. It is a
        // statement that the hazard below has been handled, not a way to silence the message.
        /// <summary>Set to true to state that the suite serialises its test classes some other way than the assembly attribute, such as a single shared xunit collection.</summary>
        public static bool ParallelismIsHandled { get; set; }

        // The consumer's test assembly, found rather than configured.
        //
        // This CANNOT be `typeof(UiSession).Assembly`, which is what the harness used before it was
        // packaged. HeadlessUnitTestSession reads [AvaloniaTestApplication] off the assembly it is
        // given, and once this code ships in a package that attribute is on the consumer's test
        // assembly, never on this one. Getting it wrong is not subtle - there would be no
        // application at all - but it is the kind of thing that only shows up in the first
        // consumer, which is exactly who should not be finding it.
        //
        // §19.1 refused to give the toolkit a required setup step, and the same applies here: a
        // suite running under Avalonia.Headless must already carry [AvaloniaTestApplication], so
        // there is nothing to configure that is not configured already.
        /// <summary>The consumer's test assembly, found by looking for the one carrying <c>[AvaloniaTestApplication]</c>.</summary>
        /// <exception cref="System.InvalidOperationException">No loaded assembly carries <c>[AvaloniaTestApplication]</c>, or more than one does. Call <see cref="Use"/> to name it in the second case.</exception>
        public static Assembly TestAssembly => _assembly ??= Find();

        /// <summary>The one headless session for this test assembly, started on first use.</summary>
        public static HeadlessUnitTestSession Current
        {
            get
            {
                if (_session is not null) return _session;

                Assembly assembly = TestAssembly;
                RequireSerialTests(assembly);
                return _session = HeadlessUnitTestSession.GetOrStartForAssembly(assembly);
            }
        }

        // For a suite whose layout defeats the search - several test assemblies in one process, say.
        /// <summary>Names the test assembly explicitly, for a layout where the search cannot pick one - several test assemblies loaded into one process, say.</summary>
        /// <param name="assembly">The assembly carrying <c>[AvaloniaTestApplication]</c>. Setting this discards any session already started.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="assembly"/> is null.</exception>
        public static void Use(Assembly assembly)
        {
            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            _session = null;
        }

        private static Assembly Find()
        {
            Assembly[] candidates = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetCustomAttributes()
                    .Any(x => x.GetType().Name == "AvaloniaTestApplicationAttribute"))
                .ToArray();

            if (candidates.Length == 1) return candidates[0];

            throw new InvalidOperationException(candidates.Length == 0
                ? "No loaded assembly carries [assembly: AvaloniaTestApplication(...)]. A headless "
                  + "Avalonia suite needs one; it names the AppBuilder every test dispatches onto. "
                  + "LunaHeadless.BuildApp() is a ready-made one that loads LunaP's theme."
                : $"{candidates.Length} loaded assemblies carry [AvaloniaTestApplication] "
                  + $"({string.Join(", ", candidates.Select(a => a.GetName().Name))}). "
                  + "Call UiSession.Use(typeof(SomeTestInYourProject).Assembly) to say which one.");
        }

        // THE HAZARD THIS PACKAGE WOULD OTHERWISE SHIP IN SILENCE.
        //
        // Every test dispatches onto ONE headless application, and several things around it are
        // process-global: LunaSettings.Store, LunaSettings.Diagnostics, and the applied theme's
        // resource dictionary. xunit parallelises across test classes, so one class's constructor
        // can replace another class's diagnostics hook while that class is mid-assertion.
        //
        // That is not hypothetical and it is not this suite's private problem. It cost a real
        // failure (§20.2): a test green on a developer machine every time, red on the first run
        // against a two-core CI runner. And it has been discovered independently twice - Pegasus's
        // hand-rolled harness declares its own xunit collection for the same reason, in F#, before
        // this package existed.
        //
        // A consumer inherits the hazard the moment it uses this harness, in its worst form: green
        // locally, red on CI, and non-deterministic in between. So the harness refuses to start
        // rather than let that be discovered later.
        private static void RequireSerialTests(Assembly assembly)
        {
            if (ParallelismIsHandled || DisablesParallelization(assembly)) return;

            throw new InvalidOperationException(
                $"{assembly.GetName().Name} does not disable xunit test parallelisation, and this "
                + "harness cannot be used safely without it.\n\n"
                + "Every test shares one headless Avalonia application and several process-global "
                + "statics around it, so running test CLASSES concurrently lets one class's "
                + "constructor overwrite another's state mid-assertion. It presents as a test that "
                + "passes locally and fails on a CI runner with more cores.\n\n"
                + "Add this to your test project:\n\n"
                + "    [assembly: CollectionBehavior(DisableTestParallelization = true)]\n\n"
                + "Or, if the suite serialises its classes another way (one shared collection), set "
                + "UiSession.ParallelismIsHandled = true.");
        }

        // Public so a suite can assert its own configuration, and so the guard itself can be
        // shown to work: pointed at an assembly WITHOUT the attribute it must return false, which
        // is the only way to demonstrate a check whose sabotage would otherwise be a build-file
        // edit nobody would leave in. By name, so this package needs xunit.assert and not xunit.core.
        /// <summary>Whether an assembly carries <c>[CollectionBehavior(DisableTestParallelization = true)]</c>.</summary>
        /// <param name="assembly">The assembly to inspect.</param>
        /// <returns>True if the attribute is present and set. Public so a suite can assert its own configuration, and so this check can be shown to work by pointing it at an assembly without the attribute.</returns>
        public static bool DisablesParallelization(Assembly assembly) =>
            assembly.GetCustomAttributes()
                .Where(a => a.GetType().Name == "CollectionBehaviorAttribute")
                .Any(a => a.GetType().GetProperty("DisableTestParallelization")?.GetValue(a) is true);
    }

    // The headless application a LunaP suite wants, so a consumer does not rebuild it and miss a
    // piece. §3.1 is why the theme include matters more than it looks: without the real theme,
    // templated controls have no template, render as nothing, and every assertion over them
    // silently passes. §17 records that shipping without it has happened once.
    /// <summary>The headless application a LunaP suite runs against, with the toolkit's theme already applied.</summary>
    public static class LunaHeadless
    {
        /// <summary>The headless application a LunaP suite runs against, with the toolkit's theme applied and real Skia rendering.</summary>
        /// <returns>A builder ready to hand to <c>[AvaloniaTestApplication]</c>.</returns>
        public static AppBuilder BuildApp() => BuildApp(_ => { });

        // `extra` runs after LunaP's own setup, for a consumer that has its own styles to add.
        /// <summary>The headless application, with a hook for a consumer that has its own styles or services to add.</summary>
        /// <param name="extra">Runs after LunaP's own setup, so it can override what LunaP configured.</param>
        /// <returns>A builder ready to hand to <c>[AvaloniaTestApplication]</c>.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="extra"/> is null.</exception>
        public static AppBuilder BuildApp(Action<AppBuilder> extra)
        {
            if (extra is null) throw new ArgumentNullException(nameof(extra));

            AppBuilder builder = AppBuilder.Configure<Application>()
                // UseSkia rather than the headless drawing stub, so a captured frame goes through
                // a real render pass and a colour count means something - see §10.
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .UseSkia()
                .AfterSetup(b =>
                {
                    b.Instance!.Styles.Add(new StyleInclude(null as Uri)
                    {
                        Source = new Uri("avares://EmuSen.LunaP/Theme/LunaTheme.axaml"),
                    });
                    b.Instance.RequestedThemeVariant = ThemeVariant.Dark;
                });

            extra(builder);
            return builder;
        }
    }
}
