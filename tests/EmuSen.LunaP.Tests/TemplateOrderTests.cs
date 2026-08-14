using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // THE OTHER REPEATED DEFECT - see docs/LunaP.md §28.2.
    //
    // A window in this toolkit is built in its constructor: make the control, fill it, select
    // something, and only then show it. So every imperative method on a control is called BEFORE
    // its template exists at least as often as after, and a method that writes straight to a
    // template part does nothing at all on that path - no exception, no warning.
    //
    // It has been found three times, each time by a person rather than by the suite. §5.6:
    // ConsolePane's callers print a welcome banner from their constructor, so the pane buffers.
    // §14.2: FilterBar.SetFacets holds pending facets. §27.6: LunaTable.Select dropped a selection
    // made before the template, and TWELVE PASSING TESTS did not notice - it was found by looking
    // at a render dump and seeing no row highlighted.
    //
    // That last one is why this file exists rather than another paragraph. §27.6 concluded that a
    // render pass proves a window is not blank but not that it is right, and that looking is still
    // a separate act. That is true and it is not a control anybody can rely on. THIS IS THE SAME
    // FINDING TURNED INTO AN ASSERTION: run the identical script before the template and after it,
    // and require the same answer. There is no baseline to eyeball and no picture to interpret,
    // because the two runs are each other's expected value.
    //
    // WHAT IS NOT COVERED, and why that is not a gap. Styled properties are immune by
    // construction: Avalonia stores them on the control and the template binds to them, so order
    // cannot matter and there is nothing to guard. Only imperative methods can drop state, so only
    // imperative methods are in the registry - and the completeness test at the bottom is what
    // makes that a rule rather than a hope.
    public class TemplateOrderTests
    {
        private sealed record Case(string Name, Func<Control> Make, Action<Control> Configure, Func<Control, string> Read);

        private static readonly string[] Facets = { "All", "Audio", "Video" };

        // A tiny tree for the expansion case. Rebuilt per call so the two runs share no state -
        // which is also what makes "expand by key, across a rebuild" a real assertion here.
        private sealed class Node
        {
            public Node(string name, params Node[] kids)
            {
                Name = name;
                Kids = kids;
            }

            public string Name { get; }
            public Node[] Kids { get; }
        }

        private static Node[] Tree() => new[]
        {
            new Node("roms",
                new Node("snes", new Node("smw.sfc"), new Node("zelda.sfc")),
                new Node("nes", new Node("metroid.nes"))),
            new Node("saves"),
        };

        private static readonly Case[] Cases =
        {
            // §5.6's original: a banner printed from a constructor.
            new("ConsolePane.AppendLine/Clear",
                () => new ConsolePane(),
                c =>
                {
                    var pane = (ConsolePane)c;
                    pane.AppendLine("discarded");
                    pane.Clear();
                    pane.AppendLine("DianaOS 1.0");
                    pane.AppendLine("ready");
                },
                c => c.FindNamed<SelectableTextBlock>("PART_Output").Text ?? ""),

            // §14.2's: facets filled before the dropdown part exists.
            new("FilterBar.SetFacets",
                () => new FilterBar { ShowFacet = true, FacetLabel = "Console:" },
                c => ((FilterBar)c).SetFacets(Facets, "Video"),
                c =>
                {
                    Dropdown facet = c.FindNamed<Dropdown>("PART_Facet");
                    return $"{facet.ItemCount} items, showing {facet.SelectedItem}, bar reports {((FilterBar)c).Facet}";
                }),

            // §27.6's: the one a render found and twelve tests did not.
            //
            // The sortable column is here because a column declared before the template has to
            // produce a heading BUTTON afterwards, not just a label - the heading is built in
            // Rebuild, which runs from OnPartsAttached on that path, and a sort declared into a
            // control with no template yet would otherwise arrive as an inert TextBlock with no way
            // to notice. The read counts buttons for that reason.
            new("LunaTable.Column/Refresh/Select",
                () => new LunaTable<string>(),
                c =>
                {
                    var table = (LunaTable<string>)c;
                    table.Column("name", s => s)
                         .Column(new LunaColumn<string>("length", s => s.Length.ToString())
                         {
                             Width = "60",
                             Sort = (a, b) => a.Length.CompareTo(b.Length),
                         });
                    table.Refresh(new[] { "alpha", "beta", "gamma" });
                    table.Select("beta");
                },
                c => $"{c.FindNamed<Grid>("PART_Header").ColumnDefinitions.Count} columns, "
                    + $"{c.FindNamed<Grid>("PART_Header").Children.OfType<Button>().Count()} sortable, "
                    + $"{c.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ListBoxItem>().Count(i => i.IsSelected)} row selected, "
                    + $"model {((LunaTable<string>)c).Selected}"),

            // §67's, and it found something. A cell selection is held by KEY rather than by visual,
            // so it survives having no template with no work at all - but the ROW under the current
            // cell is put on the ListBox, and before the template there is no ListBox to put it on.
            // Configured first, the table came up with a boxed cell and Selected reading null, which
            // is exactly the disagreement this trap exists to catch. The read asks all three: the
            // model the table thinks is selected, the coordinate, and whether a box was actually
            // drawn - a selection nothing paints is §5.5's shape again.
            new("LunaTable.SelectCell/ClearCellSelection",
                () => new LunaTable<string> { SelectionUnit = LunaSelectionUnit.Cell },
                c =>
                {
                    var table = (LunaTable<string>)c;
                    table.Column("name", s => s).Column("length", s => s.Length.ToString(), "60");
                    table.Refresh(new[] { "alpha", "beta", "gamma" });
                    table.SelectCell("gamma", 1);
                    table.SelectCell("beta", 1);
                    table.ClearCellSelection();
                    table.SelectCell("beta", 0);
                },
                c =>
                {
                    var table = (LunaTable<string>)c;
                    return $"model {table.Selected}, "
                        + $"cell {table.SelectedCell?.Row}:{table.SelectedCell?.Column}, "
                        + $"selected {table.IsCellSelected("beta", 0)}/{table.IsCellSelected("beta", 1)}, "
                        + $"boxes {c.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("cell-selection"))}";
                }),

            // §70.3's. A sort set before the template has to be there when the table appears - the
            // fields are the control's own and Show applies them, which is the §27.6 shape one
            // property along. The read asks for the ROW ORDER as well as the reported state, because
            // a table that remembered the sort and never applied it would answer both questions
            // correctly and show the rows in arrival order.
            new("LunaTable.SortBy/ClearSort",
                () => new LunaTable<string>(),
                c =>
                {
                    var table = (LunaTable<string>)c;
                    table.Column("name", s => s)
                         .Column(new LunaColumn<string>("length", s => s.Length.ToString())
                         {
                             Sort = (a, b) => a.Length.CompareTo(b.Length),
                         });
                    table.Refresh(new[] { "gamma", "be", "alpha4" });
                    table.SortBy(0);
                    table.ClearSort();
                    table.SortBy(1, descending: true);
                },
                c =>
                {
                    var table = (LunaTable<string>)c;
                    return $"column {table.SortedColumn}, descending {table.SortedDescending}, "
                        + $"order {string.Join("/", table.Models)}";
                }),

            new("LunaList.Refresh/Select",
                () => new LunaList<string>(),
                c =>
                {
                    var list = (LunaList<string>)c;
                    list.Refresh(new[] { "one", "two", "three" });
                    list.Select("three");
                },
                c => $"{c.GetVisualDescendants().OfType<ListBoxItem>().Count(i => i.IsSelected)} selected, "
                    + $"model {((LunaList<string>)c).Selected}"),

            new("Dropdown.Fill",
                () => new Dropdown(),
                c => ((Dropdown)c).Fill(Facets, "Audio"),
                c => $"{((Dropdown)c).ItemCount} items, showing {((Dropdown)c).SelectedItem}"),

            new("Tabs.Add/RemoveFrom",
                () => new Tabs(),
                c =>
                {
                    var tabs = (Tabs)c;
                    tabs.Add("Console", new TextBlock { Text = "a" });
                    tabs.Add("Log", new TextBlock { Text = "b" });
                    tabs.Add("gone", new TextBlock { Text = "x" });

                    // RemoveFrom is "drop everything from here on", not "drop the one at this
                    // index" - the shape a caller rebuilding a variable tail of tabs wants. Read
                    // the other way it silently empties the control, which is what the first draft
                    // of this case did and what the hollow-read check below caught.
                    tabs.RemoveFrom(2);
                },
                c => string.Join(", ", c.GetVisualDescendants().OfType<TabItem>().Select(t => t.Header))),

            new("MenuBar.SetMenus",
                () => new MenuBar(),
                c => ((MenuBar)c).SetMenus(
                    new LunaMenu("File", new LunaAction("Open"), new LunaAction("Quit")),
                    new LunaMenu("View", new LunaAction("Zoom"))),
                c => string.Join(", ", c.GetVisualDescendants().OfType<MenuItem>().Select(m => m.Header))),

            new("ToolBar.SetActions",
                () => new ToolBar(),
                c => ((ToolBar)c).SetActions(
                    new LunaAction("Run"),
                    LunaAction.Separator(),
                    new LunaAction("Grid") { IsCheckable = true, IsChecked = true }),
                c => string.Join(", ", c.GetVisualDescendants().OfType<Control>()
                    .Where(x => x is ActionButton or ActionToggle or Separator)
                    .Select(x => x switch
                    {
                        ActionToggle toggle => $"[{(toggle.IsChecked == true ? "x" : " ")}] {toggle.Content}",
                        ActionButton button => $"{button.Content}",
                        _ => "|",
                    }))),

            // COVERED RATHER THAN EXEMPTED, and the distinction is the point. Edit and the
            // navigation three are exempt because they act on a realised row and there is nothing to
            // queue. Expansion is not like that: it is state the user owns, it lives in a set keyed
            // by model, and it is applied at the next flatten - so a tree expanded in a window's
            // constructor MUST come up expanded. That is a claim this file can test directly.
            new("LunaTable.Expand/Collapse/ExpandAll",
                () => new LunaTable<Node>(),
                c =>
                {
                    var table = (LunaTable<Node>)c;
                    table.Key = n => n.Name;
                    table.Column("name", n => n.Name);
                    table.Children = n => n.Kids;
                    table.Refresh(Tree());
                    table.ExpandAll();
                    table.Collapse(Tree()[0].Kids[0]);   // one branch shut again, by key
                },
                c =>
                {
                    var table = (LunaTable<Node>)c;
                    return string.Join(" | ", table.Models.Select(n =>
                        $"{n.Name}{(table.IsExpanded(n) ? "+" : "")}"));
                }),

            new("RgbaImageView.SetFrame/Clear",
                () => new RgbaImageView(),
                c =>
                {
                    var view = (RgbaImageView)c;
                    view.SetFrame(new byte[8 * 4 * 4], 8, 4);
                    view.Clear();
                    view.SetFrame(new byte[16 * 9 * 4], 16, 9);
                },
                c =>
                {
                    Image image = c.FindNamed<Image>("PART_Image");
                    return $"part shows {image.Source?.Size}, control reports {((RgbaImageView)c).Source?.Size}";
                }),
        };

        public static TheoryData<string> Names()
        {
            var data = new TheoryData<string>();
            foreach (Case single in Cases) data.Add(single.Name);
            return data;
        }

        // A render pass, not just a dispatcher drain, and the difference is measurable rather than
        // cautious: RunJobs applies templates, but a TabControl does not realise its TabItems into
        // the visual tree until something has laid the strip out, so the Tabs case read back an
        // empty string in BOTH orders and its own hollow-read check caught it. Capturing the frame
        // is the public way to force layout - the same one UiTest.AssertLaidOut uses.
        private static void Settle(Window window)
        {
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
        }

        private static string Observe(Case single, bool configureFirst)
        {
            Control control = single.Make();
            if (configureFirst) single.Configure(control);

            var window = new ToolWindow { Width = 500, Height = 340, Content = control };
            window.Show();
            Settle(window);

            if (!configureFirst)
            {
                single.Configure(control);
                Settle(window);
            }

            string read = single.Read(control);
            window.Close();
            return read;
        }

        // THE GUARD. The two orders are each other's expected value, which is what makes this
        // cheap to extend: a new case needs no separately-computed answer to be written down and
        // kept true.
        [Theory]
        [MemberData(nameof(Names))]
        public Task Configuring_before_the_template_reads_the_same_as_configuring_after(string name) => UiTest.Run(() =>
        {
            Case single = Cases.First(c => c.Name == name);

            string before = Observe(single, configureFirst: true);
            string after = Observe(single, configureFirst: false);

            // Checked first, and it is not a formality: a Read that returns "" for both orders
            // would pass the comparison below while asserting nothing at all. §26.11 caught
            // exactly that shape of hollow guard - every assertion passing against a control that
            // had been sabotaged into rendering nothing.
            Assert.True(after.Length > 0 && !after.StartsWith("0 ", StringComparison.Ordinal),
                $"{name}: reading after the template gave '{after}', which is empty or a zero count. "
                + "The comparison below would pass whatever the control did. Fix the Read, not the assertion.");

            Assert.True(before == after,
                $"{name} was dropped when it ran before the template existed."
                + Environment.NewLine + $"  configured, then shown: {before}"
                + Environment.NewLine + $"  shown, then configured: {after}"
                + Environment.NewLine
                + "A window here is built in its constructor, so the first line is the normal path. Hold the "
                + "state on the control and apply it in OnApplyTemplate, the way ConsolePane buffers its lines "
                + "(§5.6) and FilterBar holds its facets (§14.2). See docs/LunaP.md §28.2.");
        });

        // Which (type, method) pairs the cases above account for. Keyed by name rather than by
        // MethodInfo so that a params overload and the IEnumerable one it delegates to count as
        // the one method a reader thinks they are.
        private static readonly HashSet<(Type Type, string Method)> Covered = new()
        {
            (typeof(ConsolePane), nameof(ConsolePane.AppendLine)),
            (typeof(ConsolePane), nameof(ConsolePane.Clear)),
            (typeof(FilterBar), nameof(FilterBar.SetFacets)),
            (typeof(LunaTable<>), "Column"),
            (typeof(LunaTable<>), "Refresh"),
            (typeof(LunaTable<>), "Select"),

            // Expansion is state the user owns, kept in a set keyed by model and applied at the next
            // flatten - so a tree expanded in a window's constructor comes up expanded, and the
            // LunaTable.Expand/Collapse/ExpandAll case runs exactly that script in both orders.
            // IsExpanded is here because that case's read is what asks it.
            (typeof(LunaTable<>), "Expand"),
            (typeof(LunaTable<>), "Collapse"),
            (typeof(LunaTable<>), "ExpandAll"),
            (typeof(LunaTable<>), "CollapseAll"),
            (typeof(LunaTable<>), "IsExpanded"),

            // A cell selection is held by key and applied at the next mark, so the same script runs
            // in both orders - and the LunaTable.SelectCell/ClearCellSelection case runs it.
            // IsCellSelected is here because that case's read is what asks it.
            // A sort is two fields the control owns and Show applies, so it survives having no
            // template - the LunaTable.SortBy/ClearSort case runs exactly that script in both orders.
            (typeof(LunaTable<>), "SortBy"),
            (typeof(LunaTable<>), "ClearSort"),

            (typeof(LunaTable<>), "SelectCell"),
            (typeof(LunaTable<>), "ClearCellSelection"),
            (typeof(LunaTable<>), "IsCellSelected"),

            (typeof(LunaList<>), "Refresh"),
            (typeof(LunaList<>), "Select"),
            (typeof(Dropdown), nameof(Dropdown.Fill)),
            (typeof(Tabs), nameof(Tabs.Add)),
            (typeof(Tabs), nameof(Tabs.RemoveFrom)),
            (typeof(MenuBar), nameof(MenuBar.SetMenus)),
            (typeof(ToolBar), nameof(ToolBar.SetActions)),
            (typeof(RgbaImageView), nameof(RgbaImageView.SetFrame)),
            (typeof(RgbaImageView), nameof(RgbaImageView.Clear)),
        };

        // Methods that carry no state a template could show, each with the reason it is here
        // rather than in a case. An exemption is a claim about the method and has to be as
        // checkable as an assertion.
        private static readonly Dictionary<(Type Type, string Method), string> Exempt = new()
        {
            [(typeof(ConsolePane), nameof(ConsolePane.FocusInput))] =
                "moves focus, which is an act rather than state; there is nothing to be dropped and nothing to read back.",
            [(typeof(FilterBar), nameof(FilterBar.FocusSearch))] =
                "as ConsolePane.FocusInput.",
            [(typeof(ConsolePane), nameof(ConsolePane.ResetHistoryRecall))] =
                "sets one private field the template never sees; history recall is keyboard state, not display.",
            [(typeof(SplitPane), nameof(SplitPane.SaveNow))] =
                "writes the divider position to the settings store; nothing about the control's own appearance.",
            [(typeof(LunaTable<>), "SaveNow")] =
                "as SplitPane.SaveNow - writes column widths and the sort to the settings store. The other "
                + "half, restoring, is reachable before the template and is covered: TableKey and Column both "
                + "call Restore, and A_saved_layout_is_restored_whichever_order_it_is_set_in pins both orders.",

            // THE ONE EXEMPTION THAT REFUSES TO QUEUE, and the reason is worth reading before
            // somebody "fixes" it by giving it a pending field like Select's.
            //
            // Select before the template is a caller saying which row should be highlighted when the
            // window opens, which is a sensible thing to have asked for early and is why it queues.
            // Edit is not that. It puts a caret in a cell, and a caret belongs to a person who is
            // looking at the table - queuing one would mean a window that opens with an editor
            // already open on a row nobody has pointed at, triggered by a line that ran during
            // construction. So it no-ops, deliberately, and the claim is pinned rather than
            // asserted here: TableTests.Editing_before_there_is_a_row_does_nothing.
            // THE NAVIGATION THREE, and they are one claim rather than three. Each asks about a row
            // that is currently on screen, and before the template there is no screen - so "not
            // found" is the honest answer rather than a dropped call, and it is the SAME answer they
            // give afterwards for a row scrolled out of view. Nothing is queued and nothing is lost.
            [(typeof(LunaTable<>), "BringRowIntoView")] =
                "scrolls to a realised row; before the template there is nothing to scroll and nothing "
                + "worth queueing, since a caller who wants a row visible at startup sets Select instead. "
                + "Pinned by TableParityTests.Navigating_before_there_are_rows_is_answered_not_queued.",
            [(typeof(LunaTable<>), "TryGetRow")] =
                "returns false when the row is not realised, which is what it also does before the "
                + "template - a query, not state. Same guard.",
            [(typeof(LunaTable<>), "TryGetCell")] =
                "as TryGetRow.",

            [(typeof(LunaTable<>), "Edit")] =
                "opens a caret on a realised cell, so there is nothing to queue - a table with no rows on "
                + "screen has no cell to put one in, and a queued caret would open an editor nobody asked "
                + "for when the window appeared. Pinned by TableTests.Editing_before_there_is_a_row_does_nothing.",
        };

        // The half that makes the registry above impossible to forget. Every public imperative
        // method on a kit control is either exercised in both orders or explicitly excused, and a
        // method added tomorrow fails here until somebody has decided which it is.
        //
        // Instance methods only: a static method has no control and therefore no template part to
        // miss, which is why ConsolePane.Follows and FilterBar.Matches never reach this list.
        [Fact]
        public void Every_imperative_method_on_a_control_is_covered_or_excused()
        {
            var unaccounted = new List<string>();

            foreach (Type type in typeof(SectionHeader).Assembly.GetTypes()
                         .Where(t => t.Namespace == "EmuSen.LunaP.Controls" && t.IsPublic && typeof(Control).IsAssignableFrom(t)))
            {
                foreach (MethodInfo method in type.GetMethods(
                             BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    // Property and event accessors are compiler-generated pairs, and an override
                    // is Avalonia's method rather than one this kit added.
                    if (method.IsSpecialName || method.GetBaseDefinition() != method) continue;

                    var key = (type, method.Name);
                    if (Covered.Contains(key) || Exempt.ContainsKey(key)) continue;

                    unaccounted.Add($"{Readable(type)}.{method.Name}");
                }
            }

            Assert.True(unaccounted.Count == 0,
                $"{string.Join(", ", unaccounted.Distinct())} can be called before the template exists and "
                + "nothing says what happens then. Add a case to TemplateOrderTests.Cases, or an entry to Exempt "
                + "with the reason it cannot drop state.");
        }

        private static string Readable(Type type) =>
            type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] + "<T>" : type.Name;
    }
}
