using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Windowing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // DOES THE PALETTE REACH EVERY CONTROL, OR ONLY THE ONES SOMEBODY REMEMBERED? - docs/LunaP.md §85.
    //
    // §48 handed LunaP's colours to 51 of FluentTheme's resource keys so that stock controls paint
    // in this toolkit's palette rather than Fluent's, and FormControlTests pinned the result. But
    // its subject list is nine names, chosen as "what an office application is mostly made of" and
    // deliberately hand-kept - so the guard could only ever see the nine controls that had already
    // been thought of. That is the shape the man page keeps warning about: probing a remembered
    // list looks like measurement and is not, because its silence means nothing.
    //
    // THIS FILE FINDS ITS OWN SUBJECTS. It reflects over every control Avalonia SHIPS - not every
    // control FluentTheme keys a theme for, which is a smaller and lazier list - constructs each
    // one, shows it in a real headless window and requires the colours it actually resolves to come
    // from LunaPalette. The first run found 28 stock controls painting Fluent's colours, against
    // the nine the old guard covered, and three of LunaP's OWN controls doing it too.
    //
    // WHY IT SWEEPS THE KIT AS WELL. The four were LunaList, ActionToggle, ActionMenuItem and Tabs,
    // and they are the StyleKeyOverride family LunaTheme.axaml calls "deliberately absent - they
    // borrow FluentTheme's templates wholesale, so they have no styles of their own to list".
    // Borrowing a template turns out to mean borrowing its colours, which nothing said and nothing
    // checked. A control this toolkit ships and names in its own README is held to the same standard
    // as one it merely inherits, so both sweeps run the same assertion.
    //
    // THE EXEMPTION TABLE WAS THE WORKLIST, NOT AN APOLOGY. Every entry named a control whose
    // colours were still Fluent's, with the count measured and what shows through; deleting an entry
    // is how a fix is finished, and No_exemption_has_been_outgrown fails if a control on the list has
    // started passing, so the table could not quietly outlive the problem it described. It is empty
    // now. All 100 stock controls and all 27 of this kit's paint only in LunaPalette.
    public class PaletteReachTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PaletteReachTests).GetTypeInfo().Assembly);

        // Namespaces whose types are never built by a consumer: presenters and primitives are parts
        // a template puts inside a control, and are swept anyway as descendants of the control that
        // owns them. Sweeping them standalone would report a Thumb with no Track around it, which is
        // a colour nobody can see.
        private static readonly string[] PartNamespaces =
        {
            "Avalonia.Controls.Primitives",
            "Avalonia.Controls.Presenters",
            "Avalonia.Controls.Chrome",
            "Avalonia.Controls.Embedding",
            "Avalonia.Dialogs",
        };

        // CONTROLS THAT STILL PAINT FLUENT'S COLOURS. It is empty, and keeping it is the point.
        //
        // It held 33 entries when §85 was written - 29 stock controls and 4 of this kit's own - and
        // passes 2 to 5 emptied it by adding 51 keys to Theme/FluentBridge.axaml, which took that
        // file from 51 overrides to 102 - it doubled. An entry is a
        // claim about a control and has to be as checkable as an assertion, so any that comes back
        // carries what shows through and how many distinct colours it is, measured rather than
        // described.
        //
        // WHAT PUTS ONE BACK. An Avalonia upgrade that adds a control, or that renames a resource
        // key one of those 102 overrides names - a key that stops existing is a silent no-op, which
        // is exactly why this sweep asserts the OUTCOME a control paints rather than asserting the
        // overrides exist (§48). The failure will name the control and the colours; the fix is
        // another key, or an entry here saying why not.
        private static readonly Dictionary<string, string> Exempt = new();

        // Every control Avalonia ships that a consumer can construct. Reflected rather than listed,
        // so an Avalonia upgrade that adds a control adds a subject here without anybody
        // remembering to - which is the half of §48's claim ("a control added to Avalonia next year
        // inherits it") that nothing was checking.
        private static IEnumerable<Type> StockControls() =>
            typeof(Control).Assembly.GetExportedTypes()
                .Where(t => typeof(Control).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericTypeDefinition)
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .Where(t => !PartNamespaces.Contains(t.Namespace))
                // A window is its own root and cannot be shown inside another one; ToolWindow and
                // AppWindow are covered by WindowingTests and ShellTests.
                .Where(t => !typeof(WindowBase).IsAssignableFrom(t))
                .OrderBy(t => t.Name);

        // This kit's own controls, found the same way and for the same reason.
        private static IEnumerable<Type> KitControls() =>
            typeof(Card).Assembly.GetExportedTypes()
                .Where(t => typeof(Control).IsAssignableFrom(t) && !t.IsAbstract)
                .Where(t => !typeof(WindowBase).IsAssignableFrom(t))
                .OrderBy(t => t.Name);

        public static TheoryData<string> Stock() => Names(StockControls());

        public static TheoryData<string> Kit() => Names(KitControls());

        private static TheoryData<string> Names(IEnumerable<Type> types)
        {
            var data = new TheoryData<string>();
            foreach (Type type in types)
            {
                string name = PaletteSweep.Readable(type);
                if (!Exempt.ContainsKey(name)) data.Add(name);
            }

            return data;
        }

        // The awkward few. Three of this kit's controls take a LunaAction because a control built
        // from one follows it rather than copying it (§26.3), and two are generic and have to be
        // closed over something before they exist at all. Everything else is a parameterless
        // constructor, which is why this is a switch with a default rather than a table.
        private static Control Build(string name) => name switch
        {
            nameof(ActionButton) => new ActionButton(new LunaAction("Open", () => { })),
            nameof(ActionToggle) => new ActionToggle(new LunaAction("Wrap", () => { })),
            nameof(ActionMenuItem) => new ActionMenuItem(new LunaAction("Quit", () => { })),
            "LunaList<T>" => new LunaList<string> { ItemsSource = new[] { "alpha", "beta" } },
            "LunaTable<T>" => BuildTable(),
            "TileGrid<T>" => BuildTiles(),
            nameof(OnScreenKeyboard) => new OnScreenKeyboard(new TextBox(), new[] { KeyboardLayout.Code }),
            _ => (Control)Activator.CreateInstance(
                     StockControls().Concat(KitControls()).First(t => PaletteSweep.Readable(t) == name))!,
        };

        // Populated, because an empty table has no rows and no cells to paint and would pass by
        // having nothing to say.
        private static Control BuildTable()
        {
            var table = new LunaTable<string>();
            table.Column("Name", x => x);
            table.Column("Length", x => x.Length.ToString());
            table.Refresh(new[] { "alpha", "beta", "gamma" });
            return table;
        }

        // Populated and with a tile selected, so the selection ring is on screen to be swept.
        private static Control BuildTiles()
        {
            var grid = new TileGrid<string>();
            grid.Refresh(new[] { "alpha", "beta", "gamma" });
            grid.Select("beta");
            return grid;
        }

        // Content, so that a container is not clean merely by being empty. Tabs is the case that
        // proves this matters: empty it paints nothing and passes, populated its items paint Fluent
        // blue.
        private static Control Fill(Control control)
        {
            switch (control)
            {
                case TabControl tabs when tabs.ItemCount == 0:
                    tabs.ItemsSource = new[]
                    {
                        new TabItem { Header = "One", Content = "first" },
                        new TabItem { Header = "Two", Content = "second" },
                    };
                    break;
                case ItemsControl items when items.ItemCount == 0:
                    items.ItemsSource = new[] { "alpha", "beta" };
                    break;
                case HeaderedContentControl headed:
                    headed.Header ??= "Header";
                    headed.Content ??= "body";
                    break;
                case ContentControl content:
                    content.Content ??= "body";
                    break;
                case TextBlock text when string.IsNullOrEmpty(text.Text):
                    text.Text = "text";
                    break;
            }

            return control;
        }

        private static string[] Sweep(string name)
        {
            Control control = Fill(Build(name));
            var window = new ToolWindow { Width = 460, Height = 300, Content = control };

            try
            {
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                EmuSen.LunaP.Testing.UiTest.Capture(window);

                string[] strays = PaletteSweep.Strays(control);
                window.Close();
                return strays;
            }
            catch (Exception e)
            {
                // A CONTROL THAT THROWS WHILE RENDERING IS ALMOST ALWAYS A TYPE-MISMATCHED
                // OVERRIDE, and saying so here is the difference between a five-minute fix and an
                // afternoon. §85.10 is the case: SystemAccentColor was overridden with a
                // SolidColorBrush like every other entry in the bridge, and five controls that had
                // been green died with InvalidCastException from somewhere deep inside Avalonia's
                // property store - a stack trace naming nothing this repository owns.
                //
                // Rethrown rather than swallowed. The test still fails; it just says what to look
                // at first.
                try { window.Close(); } catch { /* already coming apart; the original throw is the news */ }

                throw new InvalidOperationException(
                    $"{name} threw while rendering under LunaTheme: {e.GetType().Name}: {e.Message}"
                    + Environment.NewLine
                    + "A resource override of the wrong TYPE does this. Fluent reads some keys as a "
                    + "Color rather than a Brush, and handing those a SolidColorBrush does not degrade "
                    + "quietly - it throws and takes the control's whole render down. Look at the keys "
                    + "most recently added to Theme/FluentBridge.axaml; if one of them is Color-typed, "
                    + "it needs a per-variant StaticResource alias instead. See docs/LunaP.md §85.10.",
                    e);
            }
        }

        // THE SWEEP, over everything Avalonia ships.
        [Theory]
        [MemberData(nameof(Stock))]
        public Task A_stock_control_paints_only_in_the_palette(string name) => Session.Dispatch(() =>
        {
            string[] strays = Sweep(name);

            Assert.True(strays.Length == 0,
                $"{name} paints {strays.Length} colour(s) that are not in LunaPalette, so it renders in "
                + "FluentTheme's palette rather than this toolkit's:"
                + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", strays)
                + Environment.NewLine
                + "Hand LunaP's colours to the Fluent resource keys it resolves, in "
                + "Theme/FluentBridge.axaml, or style it in Theme/Controls/ and list that file in "
                + "LunaTheme.axaml. See docs/LunaP.md §48 and §85.");
        }, default);

        // THE SAME SWEEP, over this kit's own controls, where the standard is not negotiable.
        [Theory]
        [MemberData(nameof(Kit))]
        public Task A_kit_control_paints_only_in_the_palette(string name) => Session.Dispatch(() =>
        {
            string[] strays = Sweep(name);

            Assert.True(strays.Length == 0,
                $"{name} is a control this toolkit ships, and it paints {strays.Length} colour(s) from "
                + "FluentTheme rather than LunaPalette:"
                + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", strays)
                + Environment.NewLine
                + "See docs/LunaP.md §85.");
        }, default);

        // AN EXEMPTION THAT HAS STOPPED BEING TRUE IS A LIE THE SUITE IS TELLING ITSELF. Without
        // this, fixing a control would leave its entry in the table, the theory would keep skipping
        // it, and the next person to read the list would believe a defect that is no longer there -
        // and nothing would notice if it came back.
        [Fact]
        public Task No_exemption_has_been_outgrown() => Session.Dispatch(() =>
        {
            var fixedAlready = new List<string>();

            foreach (string name in Exempt.Keys)
            {
                if (Sweep(name).Length == 0) fixedAlready.Add(name);
            }

            Assert.True(fixedAlready.Count == 0,
                $"{string.Join(", ", fixedAlready)} now paint(s) only in LunaPalette, so the exemption in "
                + "PaletteReachTests.Exempt is out of date. Delete the entry - that is how the fix is "
                + "finished, and until it is deleted the control is not actually guarded.");
        }, default);

        // A TheoryData that quietly emptied would report a pass for every control at once. §26.11
        // and §48 both caught this shape of hollow guard, which is why it is asserted rather than
        // assumed.
        [Fact]
        public void The_sweep_has_subjects()
        {
            int stock = StockControls().Count();
            int kit = KitControls().Count();

            Assert.True(stock >= 100, $"Only {stock} stock controls are swept; there were 100 when §85 was written.");
            Assert.True(kit >= 32, $"Only {kit} kit controls are swept; the README says thirty-two (twenty-seven before §88).");
        }
    }
}
