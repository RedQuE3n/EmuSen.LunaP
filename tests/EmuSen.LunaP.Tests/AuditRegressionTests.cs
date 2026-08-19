using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Settings;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // THE FINE-COMB PASS BEFORE 0.8.1 - see docs/LunaP.md §79.
    //
    // Every test here is a defect's own reproduction, kept rather than deleted once the defect was
    // fixed: §22.5's rule is that a guard is not trusted until it has failed, and each of these has,
    // against the code as it shipped in 0.8.0. Each names the § carrying the argument.
    public class AuditRegressionTests
    {
        private sealed record Row(int N);

        // ---- §79.3: a failed Save reported nothing at all ----

        [Fact]
        public void A_save_that_fails_says_so_through_diagnostics()
        {
            // A file where the store wants a directory, so CreateDirectory throws.
            string blocker = Path.Combine(Path.GetTempPath(), "luna-regress-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "not a directory");

            var reported = new List<string>();
            Action<string>? previous = LunaSettings.Diagnostics;
            LunaSettings.Diagnostics = reported.Add;

            try
            {
                var store = new JsonSettingsStore(Path.Combine(blocker, "sub"));
                bool ok = store.Save(null, "windows.json", new Dictionary<string, string> { ["a"] = "b" });

                Assert.False(ok);
                Assert.True(reported.Count > 0,
                    "Save returned false and reported nothing. Every caller in this toolkit discards "
                    + "the bool, so a failed write would be entirely silent. §79.3");
            }
            finally
            {
                LunaSettings.Diagnostics = previous;
                File.Delete(blocker);
            }
        }

        // ---- §79.2 and §79.5: the stale layout, and the file read once per column ----

        private sealed class CountingStore : ISettingsStore
        {
            public int Loads;
            public readonly Dictionary<string, TableLayout> Saved = new();

            public string Directory(string? category) => Path.GetTempPath();
            public bool Save<T>(string? category, string fileName, T value) where T : class => true;

            public T? Load<T>(string? category, string fileName) where T : class
            {
                Loads++;
                return Saved as T;
            }
        }

        private static IReadOnlyList<string> WidthsOf(LunaTable<Row> table)
        {
            FieldInfo field = typeof(LunaTable<Row>)
                .GetField("_columns", BindingFlags.Instance | BindingFlags.NonPublic)!;

            var widths = new List<string>();
            foreach (object spec in (IEnumerable)field.GetValue(table)!)
            {
                widths.Add(spec.GetType().GetProperty("Width")!.GetValue(spec)!.ToString()!);
            }

            return widths;
        }

        private static T WithStore<T>(CountingStore store, Func<T> body)
        {
            ISettingsStore previous = LunaSettings.Store;
            LunaSettings.Store = store;
            try
            {
                return body();
            }
            finally
            {
                LunaSettings.Store = previous;
            }
        }

        private static LunaTable<Row> TableOf(int columns)
        {
            var table = new LunaTable<Row> { TableKey = "fields" };
            for (int i = 0; i < columns; i++)
            {
                int captured = i;
                table.Column($"c{captured}", r => r.N.ToString(), "100");
            }

            return table;
        }

        [Fact]
        public Task A_layout_saved_for_fewer_columns_is_not_applied() => UiTest.Run(() =>
        {
            var store = new CountingStore();
            var old = new TableLayout();
            old.Widths.Add("500");
            old.Widths.Add("500");
            store.Saved["fields"] = old;

            IReadOnlyList<string> widths = WithStore(store, () => WidthsOf(TableOf(5)));

            Assert.True(widths.All(w => w == "100"),
                "A five-column table took the widths of a saved TWO-column layout: ["
                + string.Join(", ", widths)
                + "]. The table is a two-column table for one moment while it is being built, which "
                + "is when the stale layout matched. §79.2");
        });

        [Fact]
        public Task A_layout_that_still_matches_is_applied() => UiTest.Run(() =>
        {
            var store = new CountingStore();
            var saved = new TableLayout();
            for (int i = 0; i < 5; i++) saved.Widths.Add("250");
            store.Saved["fields"] = saved;

            IReadOnlyList<string> widths = WithStore(store, () => WidthsOf(TableOf(5)));

            // The other direction, so the fix above cannot be "never restore anything".
            Assert.True(widths.All(w => w == "250"),
                "A matching layout was not applied: [" + string.Join(", ", widths) + "]. §79.2");
        });

        [Fact]
        public Task The_layout_file_is_read_once_per_key_not_once_per_column() => UiTest.Run(() =>
        {
            var store = new CountingStore();
            var saved = new TableLayout();
            for (int i = 0; i < 30; i++) saved.Widths.Add("100");
            store.Saved["fields"] = saved;

            int loads = WithStore(store, () =>
            {
                TableOf(30);
                return store.Loads;
            });

            Assert.True(loads <= 2,
                $"Building a thirty-column table read tables.json {loads} times. Each read is a full "
                + "JSON parse of the file every table in the application shares. §79.5");
        });

        // ---- §79.4: a palette token nobody validated ----

        [Fact]
        public void A_misspelled_palette_token_is_reported()
        {
            CssThemeResult result = CssTheme.Parse(":root { --luna-surfce: #123456; }");

            Assert.True(result.Warnings.Count > 0,
                "A misspelled palette token produced no warning, so the real LunaSurface keeps its "
                + "default and the theme author is told nothing. §79.4");
        }

        [Fact]
        public void A_real_palette_token_is_not_reported()
        {
            // The negative control: the check above must not warn about every token.
            CssThemeResult result = CssTheme.Parse(":root { --luna-surface: #123456; }");

            Assert.True(result.Warnings.Count == 0,
                "A valid palette token warned: " + string.Join("; ", result.Warnings) + ". §79.4");
        }

        // ---- §79.1: init is not set ----

        [Fact]
        public void The_api_baseline_distinguishes_init_from_set()
        {
            var initOnly = typeof(LunaColumn<string>).GetProperties()
                .Where(p => p.SetMethod is { IsPublic: true })
                .Where(p => p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                    .Any(m => m.Name == "IsExternalInit"))
                .Select(p => p.Name)
                .ToList();

            Assert.NotEmpty(initOnly);

            // LunaColumn's own block only. WindowPlacement.Width is a genuine setter, and a search
            // over the whole file would read it as this one - the shape of hollow guard §22.6 is
            // about, arriving in the assertion rather than in the code.
            string[] lines = File.ReadAllLines(Path.Combine(
                RepoRoot(), "tests", "EmuSen.LunaP.Tests", "ApiSurface", "EmuSen.LunaP.txt"));

            var block = lines
                .SkipWhile(l => !l.StartsWith("public sealed class EmuSen.LunaP.Controls.LunaColumn", StringComparison.Ordinal))
                .Skip(1)
                .TakeWhile(l => l.StartsWith("    ", StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(block);

            var misdescribed = initOnly
                .Where(n => block.Any(l => l.EndsWith($" {n} {{ get; set; }}", StringComparison.Ordinal)))
                .ToList();

            Assert.True(misdescribed.Count == 0,
                "These init-only properties are written '{ get; set; }' in the baseline a consumer is "
                + "told their compiler sees: " + string.Join(", ", misdescribed)
                + ". Assigning one after construction is CS8852. §79.1");
        }


        // ---- §79.7: a count in the README that nothing checked ----

        // THE README SAID 207 FOR FOUR RELEASES, then 829 for under an hour.
        //
        // Both were measured when written and both rotted, which is what a hand-written count of
        // something the runner already knows does. §78.5 declined to propose testing prose against
        // behaviour and that still stands - but this is not prose. It is an integer this assembly can
        // produce, so it is asserted rather than trusted.
        //
        // COUNTED BY REFLECTION AND NOT BY ASKING THE RUNNER, because a test cannot ask its own run
        // how many tests there are without counting itself differently depending on the filter it was
        // invoked under. Facts and Theory CASES are both counted, because the README's sentence is
        // about what `dotnet test` prints and that is what it prints.
        [Fact]
        public void The_readme_states_the_number_of_tests_this_assembly_has()
        {
            int cases = 0;
            foreach (Type type in typeof(AuditRegressionTests).Assembly.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (method.GetCustomAttributes().Any(a => a.GetType().Name == "FactAttribute")) cases++;

                    foreach (Attribute data in method.GetCustomAttributes())
                    {
                        if (data is Xunit.Sdk.DataAttribute row) cases += row.GetData(method).Count();
                    }
                }
            }

            string readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));
            var stated = System.Text.RegularExpressions.Regex.Match(readme, @"\*\*(\d+) tests, all headless\*\*");

            Assert.True(stated.Success, "README.md no longer states a test count in the form '**N tests, all headless**'.");

            Assert.True(int.Parse(stated.Groups[1].Value) == cases,
                $"README.md says {stated.Groups[1].Value} tests; this assembly has {cases}. Update the "
                + "README - the number is the front door's only measured claim and it has gone stale "
                + "twice. §79.7");
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "LunaP.md"))) dir = dir.Parent;
            return dir!.FullName;
        }
    }
}
