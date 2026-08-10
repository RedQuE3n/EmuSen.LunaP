using System;
using System.Linq;
using System.Reflection;

namespace EmuSen.LunaP.Tests
{
    // The rule this whole toolkit is built on, finally asserted in the repository that makes the
    // claim - see docs/LunaP.md §22.7.
    //
    // The guard existed before this and stayed behind: EmuSen.WiseMan's LeafAssemblyTests has run
    // `LunaP_references_nothing_of_EmuSen` since §10.4, and still does, against the package. That
    // is worth having and is not enough. It answers "did LunaP pick up something of EmuSen's",
    // which was the question during the split; the rule in EmuSen.LunaP.csproj is broader and
    // answers to nobody here - "AVALONIA AND NOTHING ELSE. Not a settings library, not a logging
    // library, not an application's own types."
    //
    // A toolkit whose headline claim is only checked by a different repository, which a consumer
    // may not have, is a toolkit making an unverified claim.
    public class LayeringTests
    {
        // Everything a .NET assembly is allowed to name here: Avalonia, and the base class library.
        private static bool IsAllowed(string name) =>
            name.StartsWith("Avalonia", StringComparison.Ordinal)
            || name.StartsWith("System", StringComparison.Ordinal)
            || name == "netstandard"
            || name == "mscorlib";

        // The rule, as a function, so it can be pointed at something other than the assembly it is
        // meant to protect - which is what makes it demonstrably able to fail.
        private static string[] Foreign(Assembly assembly) =>
            assembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? "")
                .Where(n => !IsAllowed(n))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

        [Fact]
        public void LunaP_references_Avalonia_and_the_base_library_and_nothing_else()
        {
            Assembly lunaP = typeof(EmuSen.LunaP.Controls.MeterRow).Assembly;

            Assert.Empty(Foreign(lunaP));
        }

        // The narrower claim the split was actually about, kept separate because it is the one a
        // reader of §19 and §20 comes looking for.
        [Fact]
        public void LunaP_names_nothing_of_EmuSen()
        {
            Assembly lunaP = typeof(EmuSen.LunaP.Controls.MeterRow).Assembly;

            string[] emuSen = lunaP.GetReferencedAssemblies()
                .Select(a => a.Name ?? "")
                .Where(n => n.StartsWith("EmuSen", StringComparison.Ordinal))
                .ToArray();

            Assert.Empty(emuSen);
        }

        // THE GUARD FAILING ON PURPOSE, PERMANENTLY.
        //
        // §10.4 records that this project makes new guards fail before trusting them, and the usual
        // way is to sabotage the thing under test and watch red. That does not work well here: the
        // sabotage would be adding a PackageReference, which is a build-file edit nobody would
        // leave in, so the demonstration would live only in a commit message.
        //
        // Instead the rule is pointed at THIS assembly, which references xunit and therefore must
        // fail it. If the day comes that this test breaks, the rule has stopped detecting anything
        // and the two above are worthless.
        [Fact]
        public void The_rule_rejects_an_assembly_that_breaks_it()
        {
            string[] foreign = Foreign(typeof(LayeringTests).Assembly);

            Assert.NotEmpty(foreign);
            Assert.Contains(foreign, n => n.StartsWith("xunit", StringComparison.OrdinalIgnoreCase));
        }

        // THE BLIND SPOT, CARRIED OVER RATHER THAN REDISCOVERED - §10.4 found it the hard way.
        //
        // The C# compiler elides a reference used ONLY for `const` values: the constant is baked
        // into the consuming assembly and no assembly reference survives to be seen here. A first
        // attempt to sabotage the original guard used a `const string` and produced a build naming
        // nothing at all - the guard was correct and the sabotage was not.
        //
        // So: a project can depend on another project's constants and these tests cannot see it. A
        // reader should not believe this file covers more than it does. There is no test for this
        // because there is nothing to observe; it is a documented limit, not a gap to fill.
        [Fact]
        public void The_reference_list_is_what_the_compiler_kept_not_what_the_source_named()
        {
            // Pinned so the limitation is visible in the suite rather than only in prose: this is
            // reading compiled metadata, which is the whole reason the const hole exists.
            Assembly lunaP = typeof(EmuSen.LunaP.Controls.MeterRow).Assembly;

            Assert.NotEmpty(lunaP.GetReferencedAssemblies());
            Assert.All(lunaP.GetReferencedAssemblies(), a => Assert.False(string.IsNullOrEmpty(a.Name)));
        }

        // The second package must not leak into the first. EmuSen.LunaP.Testing names xunit and
        // Avalonia.Headless, which the toolkit may not; the whole argument for it being a separate
        // package is that a consumer takes it from a TEST project only - docs/LunaP.md §22.8.
        [Fact]
        public void The_toolkit_does_not_reference_its_own_test_harness()
        {
            Assembly lunaP = typeof(EmuSen.LunaP.Controls.MeterRow).Assembly;

            Assert.DoesNotContain(lunaP.GetReferencedAssemblies(),
                a => (a.Name ?? "").Contains("Testing", StringComparison.Ordinal));
        }

        // The harness refuses to start against a suite that has not disabled xunit parallelisation,
        // because the statics it shares are process-global and the failure is otherwise green
        // locally and red on CI (§20.2). This asserts the check is real, from both sides.
        [Fact]
        public void The_parallelism_check_reads_the_assembly_it_is_given()
        {
            // This suite declares it, in AssemblyInfo.cs.
            Assert.True(EmuSen.LunaP.Testing.UiSession.DisablesParallelization(typeof(LayeringTests).Assembly));

            // The harness package does not, and must not be reported as if it did - otherwise the
            // check would pass for everybody and protect nobody.
            Assert.False(EmuSen.LunaP.Testing.UiSession.DisablesParallelization(
                typeof(EmuSen.LunaP.Testing.UiTest).Assembly));
        }

    }
}
