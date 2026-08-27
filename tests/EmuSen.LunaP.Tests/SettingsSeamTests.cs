using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Headless;
using Avalonia.Controls;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Settings;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // PASS 2's GATE: the whole seam, driven by something that is not a filesystem - see
    // docs/LunaP.md §86.11.
    //
    // THIS TEST COULD NOT HAVE BEEN WRITTEN BEFORE THAT PASS, and not in the sense of being
    // awkward. `ISettingsStore` had a third method, `string Directory(string? category)`, whose
    // return type is a path. An implementation over a dictionary had to answer it with something,
    // and whatever it answered would be a lie - which is exactly what a consumer keeping its
    // settings in SQLite ended up writing down about the seam.
    //
    // So this file is the proof rather than the argument. If the interface still demanded a
    // filesystem, `InMemorySettingsStore` below would not compile.
    public class SettingsSeamTests : IDisposable
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SettingsSeamTests).GetTypeInfo().Assembly);

        private readonly ISettingsStore _previousStore;
        private readonly IThemeSource _previousSource;
        private readonly InMemorySettingsStore _store = new();

        // Any row type will do; the tests below assert about which STORE a table resolves,
        // never about what it holds.
        private sealed record Row(int N);

        public SettingsSeamTests()
        {
            _previousStore = LunaSettings.Store;
            _previousSource = LunaTheme.Source;
            LunaSettings.Store = _store;
        }

        public void Dispose()
        {
            LunaSettings.Store = _previousStore;
            LunaTheme.Source = _previousSource;
        }

        // A settings store that is a Dictionary<string, string>. Nothing here knows what a path is.
        private sealed class InMemorySettingsStore : ISettingsStore
        {
            private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

            // The serializer is JsonSettingsStore's own, so this exercises the same round trip a
            // real store does rather than a simpler one that would hide a serialization problem.
            public T? Load<T>(string? category, string fileName) where T : class =>
                _files.TryGetValue(Key(category, fileName), out string? json)
                    ? System.Text.Json.JsonSerializer.Deserialize<T>(json, JsonSettingsStore.Options)
                    : null;

            public bool Save<T>(string? category, string fileName, T value) where T : class
            {
                _files[Key(category, fileName)] = System.Text.Json.JsonSerializer.Serialize(value, JsonSettingsStore.Options);
                return true;
            }

            public int Count => _files.Count;

            // A category is just part of a key here, which is the whole point: the seam never said
            // it had to be a directory.
            private static string Key(string? category, string fileName) =>
                category is null ? fileName : category + "/" + fileName;
        }

        // Themes out of a dictionary of strings. `Origin` answers in words rather than in a path,
        // which is the difference that let this interface exist where Directory could not.
        private sealed class DictionaryThemeSource : IThemeSource
        {
            private readonly Dictionary<string, string> _themes;

            public DictionaryThemeSource(Dictionary<string, string> themes) => _themes = themes;

            public string Origin => "(in memory)";

            public IReadOnlyList<string> Names() => _themes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

            public ThemeDocument? Open(string name) =>
                _themes.TryGetValue(name, out string? css)
                    ? new ThemeDocument(name, LunaTheme.CssExtension, css, $"(in memory) {name}")
                    : null;
        }

        [Fact]
        public void The_theme_catalog_can_be_served_from_something_that_is_not_a_folder()
        {
            LunaTheme.Source = new DictionaryThemeSource(new Dictionary<string, string>
            {
                ["Dusk"] = ":root { }",
                ["Amber"] = ":root { }",
            });

            IReadOnlyList<string> names = LunaTheme.Available();

            Assert.Equal(LunaTheme.BuiltIn, names[0]);
            Assert.Contains("Dusk", names);
            Assert.Contains("Amber", names);

            // And LunaTheme.Directory is honest about not being a folder, rather than inventing a
            // path for a source that has none.
            Assert.Equal(string.Empty, LunaTheme.Directory);
        }

        [Fact]
        public Task A_theme_applies_with_nothing_on_disk() => Session.Dispatch(() =>
        {
            LunaTheme.Source = new DictionaryThemeSource(new Dictionary<string, string>
            {
                ["Paper"] = ":root { --luna-surface: #123456; }",
            });

            try
            {
                Assert.True(LunaTheme.Apply("Paper"), "a theme served from memory did not apply");
                Assert.Equal("Paper", LunaTheme.Current);

                // The choice went through the in-memory store on the way out, and comes back
                // through it - so the persistence half is exercised too, not just the loading.
                Assert.Equal("Paper", LunaTheme.Saved);
            }
            finally
            {
                LunaTheme.Apply(LunaTheme.BuiltIn);
            }
        }, default);

        // ON THE DISPATCHER, and the first draft of this test was not - which cost a failure worth
        // recording. `Apply` reaches `Application.Current` before it reaches the source, so off the
        // UI thread it returns false without ever asking for the theme, and the assertion below
        // failed on a null report rather than on a wrong one. A test that exercises a diagnostic
        // has to get far enough into the call to produce it.
        // WRITTEN BECAUSE A SABOTAGE FOUND NOTHING (§86.11). Deleting the BuiltIn filter from
        // Available turned no test red at all: every source in the suite happened to offer names
        // that were not "Built-in", so the filter was unguarded behaviour rather than a guarded one.
        //
        // It matters more than it looks. BuiltIn is prepended unconditionally, so a source offering
        // the same name puts it in the list twice - and a theme menu with two identical entries,
        // one of which is the shipped palette and one of which is not, is a choice a user cannot
        // make correctly. A folder source could only hit this with a file called "Built-in.css";
        // a source that is not a folder has no such spelling constraint.
        [Fact]
        public void A_source_offering_the_built_in_name_does_not_get_it_listed_twice()
        {
            LunaTheme.Source = new DictionaryThemeSource(new Dictionary<string, string>
            {
                [LunaTheme.BuiltIn] = ":root { }",
                ["Dusk"] = ":root { }",
            });

            IReadOnlyList<string> names = LunaTheme.Available();

            Assert.Equal(1, names.Count(n => string.Equals(n, LunaTheme.BuiltIn, StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(LunaTheme.BuiltIn, names[0]);
            Assert.Contains("Dusk", names);
        }

        [Fact]
        public Task A_missing_theme_names_the_source_rather_than_a_path() => Session.Dispatch(() =>
        {
            LunaTheme.Source = new DictionaryThemeSource(new Dictionary<string, string>());
            string? reported = null;
            Action<string>? previous = LunaSettings.Diagnostics;
            try
            {
                LunaSettings.Diagnostics = m => reported = m;
                Assert.False(LunaTheme.Apply("nothing-by-that-name"));
            }
            finally
            {
                LunaSettings.Diagnostics = previous;
            }

            Assert.NotNull(reported);

            // The point: no path anywhere in the message. A source that is not a folder says so.
            Assert.Contains("(in memory)", reported!, StringComparison.Ordinal);
        }, default);

        // PASS 3's GATE: two windows, two stores, one process - see docs/LunaP.md §86.12.
        //
        // THE KEY IS DELIBERATELY THE SAME for both windows. That is what makes this a test of the
        // store and not of the key: with one process-global store, the second close overwrites the
        // first's entry and there is no arrangement of keys that separates them. Before §86.12 this
        // could not be expressed at all - every control reached for `LunaSettings.Store`, so "which
        // store" was not a question a caller was allowed to answer.
        [Fact]
        public Task Two_windows_with_two_stores_do_not_see_each_others_placement() => UiTest.Run(() =>
        {
            var left = new InMemorySettingsStore();
            var right = new InMemorySettingsStore();
            const string key = "two-stores-one-key";

            var a = new ToolWindow { WindowKey = key, Width = 400, Height = 300, Settings = left };
            a.Show();
            a.Close();

            var b = new ToolWindow { WindowKey = key, Width = 900, Height = 700, Settings = right };
            b.Show();
            b.Close();

            WindowPlacement? fromLeft = WindowPlacementStore.Load(key, left);
            WindowPlacement? fromRight = WindowPlacementStore.Load(key, right);

            Assert.NotNull(fromLeft);
            Assert.NotNull(fromRight);
            Assert.Equal(400, fromLeft!.Width);
            Assert.Equal(900, fromRight!.Width);
        });

        // WRITTEN BECAUSE A SABOTAGE FOUND NOTHING (§86.12). Pointing `RestorePlacement` back at
        // the process-wide store turned no test red: the gate above starts with two EMPTY stores,
        // so the restore reads nothing from either and only the save is observable.
        //
        // A window restoring from the wrong store is the worse half of the two, not the lesser. A
        // wrong save loses a preference; a wrong restore silently applies somebody else's - and on
        // a machine where the process-wide store holds a 111pt window from another profile, the
        // symptom is a window that opens at a size the user never chose and cannot account for.
        [Fact]
        public Task A_window_restores_from_its_own_store_and_not_the_process_wide_one() => UiTest.Run(() =>
        {
            var mine = new InMemorySettingsStore();
            const string key = "restore-from-mine";

            // Two different answers under one key. The process-wide store is this class's _store.
            WindowPlacementStore.Save(key, new WindowPlacement { Width = 111, Height = 111 });
            WindowPlacementStore.Save(key, new WindowPlacement { Width = 640, Height = 480 }, mine);

            var window = new ToolWindow { WindowKey = key, Width = 300, Height = 200, Settings = mine };
            window.Show();
            try
            {
                Assert.Equal(640, window.Width);
                Assert.Equal(480, window.Height);
            }
            finally
            {
                window.Close();
            }
        });

        // THE MECHANISM, ASSERTED DIRECTLY. The test above proves the outcome for a window, which is
        // the thing that has a store set on it; this proves the part that carries it to everything
        // else - a control three levels down that nobody plumbed anything into.
        //
        // `GetStore` and not just `For` on purpose: `For` would answer the window's store even if
        // inheritance were broken, because it falls back to the process-wide one and this class has
        // set that to _store. Asking what was INHERITED is the only version that can fail.
        [Fact]
        public Task A_control_nested_in_a_window_inherits_the_windows_store() => UiTest.Run(() =>
        {
            var store = new InMemorySettingsStore();
            var table = new LunaTable<Row>();
            var window = new ToolWindow
            {
                Content = new StackPanel { Children = { new Border { Child = table } } },
                Settings = store,
            };

            window.Show();
            try
            {
                Assert.Same(store, LunaSettings.GetStore(table));
                Assert.Same(store, LunaSettings.For(table));
            }
            finally
            {
                window.Close();
            }
        });

        // The other direction, and the reason the assertion above cannot simply be "always
        // inherits": a control in a window nobody set a store on must still work, and must get the
        // process-wide one. Without this, an implementation that returned some private default
        // would pass everything above and break every existing consumer.
        [Fact]
        public Task A_control_with_no_store_set_anywhere_falls_back_to_the_process_wide_one() => UiTest.Run(() =>
        {
            var table = new LunaTable<Row>();
            var window = new ToolWindow { Content = table };

            window.Show();
            try
            {
                Assert.Null(LunaSettings.GetStore(table));
                Assert.Same(LunaSettings.Store, LunaSettings.For(table));
            }
            finally
            {
                window.Close();
            }
        });

        [Fact]
        public void Window_placement_round_trips_through_a_store_with_no_files()
        {
            string key = "seam-" + Guid.NewGuid().ToString("N");

            WindowPlacementStore.Save(key, new WindowPlacement { X = 12, Y = 34, Width = 800, Height = 600 });
            WindowPlacement? back = WindowPlacementStore.Load(key);

            Assert.NotNull(back);
            Assert.Equal(12, back!.X);
            Assert.Equal(800, back.Width);
            Assert.True(_store.Count > 0, "nothing reached the store at all, so this proves nothing");
        }

        [Fact]
        public void Table_layout_round_trips_through_a_store_with_no_files()
        {
            string key = "seam-table-" + Guid.NewGuid().ToString("N");

            TableLayoutStore.Update(key, layout =>
            {
                layout.Widths = new List<string> { "*", "Auto" };
                layout.SortedBy = "Name";
                layout.Descending = true;
            });

            TableLayout? back = TableLayoutStore.Load(key);

            Assert.NotNull(back);
            Assert.Equal(new[] { "*", "Auto" }, back!.Widths);
            Assert.Equal("Name", back.SortedBy);
            Assert.True(back.Descending);
        }
    }
}
