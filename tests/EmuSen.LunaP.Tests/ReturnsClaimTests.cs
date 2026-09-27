using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Fluent;
using EmuSen.LunaP.Settings;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // THE 105 <returns> TAGS, AND THE 52 OF THEM THAT MAKE A CHECKABLE CLAIM - see §82.2.
    //
    // The largest single promise in this package is made 29 times in near-identical words: "The same
    // control, so calls can be chained. Nothing is copied." A fluent helper that started returning a
    // clone would break every chained call silently - the first call in the chain would configure an
    // object nobody kept - and nothing else in the suite would notice.
    //
    // The one wrong tag here is a reminder that a doc sweep is a change like any other: the claim
    // that Commands() leaves submenu owners out arrived a day and a half AFTER the walk and the test
    // that both say it does not.
    public class ReturnsClaimTests
    {
        private sealed record Row(int N);

        [Fact]
        public void Commands_leaves_out_separators_and_submenu_owners()
        {
            var open = new LunaAction("Open");
            var a = new LunaAction("a.rom");
            var b = new LunaAction("b.rom");
            var recent = new LunaAction("Recent") { Submenu = new LunaMenu("Recent", new[] { a, b }) };
            var menu = new LunaMenu("File", new[] { open, LunaAction.Separator(), recent });

            List<LunaAction> got = menu.Commands().ToList();

            Assert.DoesNotContain(got, x => x.IsSeparator);
            Assert.Contains(open, got);
            Assert.Contains(a, got);
            Assert.Contains(b, got);

            // The owner IS returned, and before its children. The <returns> tag said owners were
            // left out; the walk and ActionTests both say otherwise and both predate it (§82.2).
            Assert.Contains(recent, got);
            Assert.True(got.IndexOf(recent) < got.IndexOf(a));
        }

        [Fact]
        public void Vocabulary_lookups_are_empty_rather_than_null()
        {
            Assert.Empty(CssTheme.StatesOf("not-an-element-at-all"));
            Assert.Empty(CssTheme.PartsOf("not-an-element-at-all"));
            Assert.NotNull(CssTheme.StatesOf("not-an-element-at-all"));
        }

        [Fact]
        public void Stores_answer_null_for_a_key_never_saved()
        {
            string key = "never-saved-" + Guid.NewGuid().ToString("N");
            Assert.Null(TableLayoutStore.Load(key));
            Assert.Null(PaneLayoutStore.Load(key));
            Assert.Null(WindowPlacementStore.Load(key));
        }

        [Fact]
        public void A_json_store_answers_null_for_missing_empty_and_malformed()
        {
            string root = Path.Combine(Path.GetTempPath(), "luna-returns-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var store = new JsonSettingsStore(root);

            Assert.Null(store.Load<Row>(null, "absent.json"));

            File.WriteAllText(Path.Combine(root, "empty.json"), "");
            Assert.Null(store.Load<Row>(null, "empty.json"));

            File.WriteAllText(Path.Combine(root, "broken.json"), "{ this is not json");
            Assert.Null(store.Load<Row>(null, "broken.json"));

            // "True if it was written."
            Assert.True(store.Save(null, "written.json", new Row(1)));
            Assert.Equal(1, store.Load<Row>(null, "written.json")!.N);

            Directory.Delete(root, recursive: true);
        }

        [Fact]
        public Task Chaining_hands_back_the_same_object_and_copies_nothing() => UiTest.Run(() =>
        {
            var control = new Button();

            // LayoutExtensions - "The same control, so calls can be chained. Nothing is copied."
            Assert.Same(control, control.Margin(4));
            Assert.Same(control, control.Margin(4, 8));
            Assert.Same(control, control.Margin(1, 2, 3, 4));
            Assert.Same(control, control.Width(10));
            Assert.Same(control, control.Height(10));
            Assert.Same(control, control.MaxHeight(10));
            Assert.Same(control, control.MinSize(1, 2));
            Assert.Same(control, control.Grow());
            Assert.Same(control, control.Left());
            Assert.Same(control, control.Right());
            Assert.Same(control, control.Center());
            Assert.Same(control, control.Dock(Avalonia.Controls.Dock.Top));
            Assert.Same(control, control.AtColumn(1));
            Assert.Same(control, control.AtRow(1));
            Assert.Same(control, control.Name("n"));
            Assert.Same(control, control.Visible(true));

            var panel = new StackPanel();
            Assert.Same(panel, panel.Spacing(4));

            var text = new TextBlock();
            Assert.Same(text, text.Bold());
            Assert.Same(text, text.FontSize(12));
            Assert.Same(text, text.Wrap());

            // AccessibilityExtensions - the same promise.
            Assert.Same(control, control.AccessibleName("n"));
            Assert.Same(control, control.HelpText("h"));
            Assert.Same(control, control.LabeledBy(new TextBlock()));
            Assert.Same(control, control.LiveRegion());
            Assert.Same(control, control.Decorative());

            // And the three non-extension chaining promises.
            var action = new LunaAction("x");
            var group = new ActionGroup();
            Assert.Same(action, group.Add(action));

            var table = new LunaTable<Row>();
            Assert.Same(table, table.Column("n", r => r.N.ToString()));
            Assert.Same(table, table.Column(new LunaColumn<Row>("m", r => "")));

            var window = new AppWindow();
            var side = new SidePanel();
            Assert.Same(side, window.AddPanel(side));
        });

        [Fact]
        public Task Panel_toggles_are_the_panels_own_actions_in_order() => UiTest.Run(() =>
        {
            // DIFFERENT SIDES. One panel per side is documented ("replacing whatever was already on
            // that edge"), so two panels defaulting to the same Side is a test that measures the
            // replacement rule rather than the ordering claim.
            var window = new AppWindow();
            var first = new SidePanel { Title = "First", Side = PanelSide.Left };
            var second = new SidePanel { Title = "Second", Side = PanelSide.Right };
            window.AddPanel(first);
            window.AddPanel(second);

            IReadOnlyList<LunaAction> toggles = window.PanelToggles();

            Assert.Equal(2, toggles.Count);
            // "in the order the panels were added" and "not copies"
            Assert.Same(first.ToggleAction, toggles[0]);
            Assert.Same(second.ToggleAction, toggles[1]);
        });

        [Fact]
        public Task Count_parts_is_zero_for_a_control_that_was_never_shown() => UiTest.Run(() =>
        {
            var never = new LunaTable<Row>();
            Assert.Equal(0, never.CountParts<Button>());
            Assert.Null(never.FindPart<Button>());
        });

        [Fact]
        public void Available_lists_builtin_first_and_a_name_once()
        {
            string root = Path.Combine(Path.GetTempPath(), "luna-themes-" + Guid.NewGuid().ToString("N"));
            ISettingsStore previous = LunaSettings.Store;
            try
            {
                LunaSettings.Store = new JsonSettingsStore(root);
                Directory.CreateDirectory(LunaTheme.Directory);
                File.WriteAllText(Path.Combine(LunaTheme.Directory, "dusk.axaml"), "<ResourceDictionary/>");
                File.WriteAllText(Path.Combine(LunaTheme.Directory, "dusk.css"), ":root { }");
                File.WriteAllText(Path.Combine(LunaTheme.Directory, "amber.css"), ":root { }");

                IReadOnlyList<string> names = LunaTheme.Available();

                Assert.Equal(LunaTheme.BuiltIn, names[0]);
                Assert.Equal(1, names.Count(n => string.Equals(n, "dusk", StringComparison.OrdinalIgnoreCase)));
                Assert.Contains("amber", names);
            }
            finally
            {
                LunaSettings.Store = previous;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }
    }
}
