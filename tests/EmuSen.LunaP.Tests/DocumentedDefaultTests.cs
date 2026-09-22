using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Headless;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Windowing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // EVERY SUMMARY THAT NAMES A DEFAULT, CHECKED AGAINST THE DEFAULT - see docs/LunaP.md §84.4.
    //
    // This file exists because of one property and one sentence. `ToolWindow.ClosesOnEscape` was
    // registered with no `defaultValue`, so it is false; its `///` said "True by default"; and the
    // `//` comment TWO LINES ABOVE the summary said "Off by default" and was right. A consumer
    // found it in the first hour of using the type.
    //
    // WHY §80 WALKED PAST IT, which is the part worth keeping. That pass took all 544 published
    // summaries as literal claims and probed them BY CALLING THINGS. A default value is not reached
    // by calling anything - it is what is already true before any call - so a method that probes by
    // invocation cannot see this class of claim at all. §81 added the claims a test could settle
    // and got fourteen of the fifteen below; the fifteenth is the one that was wrong, and it was
    // missed because its property is not on a control anyone was constructing.
    //
    // Nor was the BEHAVIOUR unguarded, which the first draft of this note got wrong and is corrected
    // here: `WindowingTests.Escape_closes_only_when_a_window_opts_in` shows a ToolWindow with the
    // property untouched and asserts Escape does not close it, and has since it was written. So the
    // code was right, its test was right, and only the sentence describing them was wrong - which is
    // exactly why nothing failed. **A test that agrees with the code cannot notice that the
    // documentation does not.** That is the gap this file fills, and it is the general form: every
    // one of the toolkit's summary-claim suites asserts the code against itself.
    //
    // THE TABLE IS TWO-SIDED ON PURPOSE. Each entry carries the words the summary uses AND the value
    // they mean, and both are asserted - the phrase against the published XML, the value against a
    // live instance. Editing a summary without editing the table fails; changing a default without
    // editing the table fails. A table holding only values would drift from the prose it is about,
    // which is the defect being guarded reintroduced as the guard.
    public class DocumentedDefaultTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(DocumentedDefaultTests).GetTypeInfo().Assembly);

        private sealed record Row(int N);

        // Member id as the compiler spells it, the phrase its summary uses, and how to read the real
        // thing. The reader runs on the UI thread, because most of these are on Avalonia controls.
        private static readonly (string Member, string Claim, Func<object?> Read)[] Claims =
        {
            ("P:EmuSen.LunaP.Windowing.ToolWindow.ClosesOnEscape",
                "False by default", () => new ToolWindow().ClosesOnEscape),

            ("P:EmuSen.LunaP.Controls.RgbaImageView.Stretch",
                "Defaults to Stretch.None", () => new RgbaImageView().Stretch),
            ("P:EmuSen.LunaP.Controls.RgbaImageView.IntegerScale",
                "Off by default", () => new RgbaImageView().IntegerScale),

            ("P:EmuSen.LunaP.Controls.LunaTable`1.SelectionUnit",
                "Row by default", () => new LunaTable<Row>().SelectionUnit),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.SelectionMode",
                "Single by default", () => new LunaTable<Row>().SelectionMode),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.VirtualizeColumns",
                "Off by default", () => new LunaTable<Row>().VirtualizeColumns),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.CanReorderRows",
                "Off by default", () => new LunaTable<Row>().CanReorderRows),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.RowHeaderWidth",
                "\"Auto\" by default", () => new LunaTable<Row>().RowHeaderWidth),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.RowHeaderCaption",
                "Empty by default", () => new LunaTable<Row>().RowHeaderCaption),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.EditGestures",
                "Double-click and F2 by default", () => new LunaTable<Row>().EditGestures),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.FrozenColumns",
                "Zero by default", () => new LunaTable<Row>().FrozenColumns),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.GridLines",
                "None by default", () => new LunaTable<Row>().GridLines),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.IndentSize",
                "16 by default", () => new LunaTable<Row>().IndentSize),
            ("P:EmuSen.LunaP.Controls.LunaTable`1.ExpanderColumn",
                "The first column by default", () => new LunaTable<Row>().ExpanderColumn),

            ("P:EmuSen.LunaP.Controls.TileGrid.TileWidth", "160 by default", () => new TileGrid<Row>().TileWidth),
            ("P:EmuSen.LunaP.Controls.TileGrid.TileHeight", "200 by default", () => new TileGrid<Row>().TileHeight),
            ("P:EmuSen.LunaP.Controls.TileGrid.Spacing", "20 by default", () => new TileGrid<Row>().Spacing),
            ("P:EmuSen.LunaP.Controls.OverlayBar.HideAfter", "1.5 seconds by default", () => new OverlayBar().HideAfter),
            ("P:EmuSen.LunaP.Controls.NoticeLayer.Duration", "1.75 seconds by default", () => new NoticeLayer().Duration),
        };

        // What each claim's words mean, kept apart from the phrase so that neither can be quietly
        // edited into agreement with the other.
        private static readonly Dictionary<string, object?> Values = new()
        {
            ["P:EmuSen.LunaP.Windowing.ToolWindow.ClosesOnEscape"] = false,
            ["P:EmuSen.LunaP.Controls.RgbaImageView.Stretch"] = Stretch.None,
            ["P:EmuSen.LunaP.Controls.RgbaImageView.IntegerScale"] = false,
            ["P:EmuSen.LunaP.Controls.LunaTable`1.SelectionUnit"] = LunaSelectionUnit.Row,
            ["P:EmuSen.LunaP.Controls.LunaTable`1.SelectionMode"] = LunaSelectionMode.Single,
            ["P:EmuSen.LunaP.Controls.LunaTable`1.VirtualizeColumns"] = false,
            ["P:EmuSen.LunaP.Controls.LunaTable`1.CanReorderRows"] = false,
            ["P:EmuSen.LunaP.Controls.LunaTable`1.RowHeaderWidth"] = "Auto",
            ["P:EmuSen.LunaP.Controls.LunaTable`1.RowHeaderCaption"] = "",
            ["P:EmuSen.LunaP.Controls.LunaTable`1.EditGestures"] = LunaEditGestures.Default,
            ["P:EmuSen.LunaP.Controls.LunaTable`1.FrozenColumns"] = 0,
            ["P:EmuSen.LunaP.Controls.LunaTable`1.GridLines"] = LunaGridLines.None,
            ["P:EmuSen.LunaP.Controls.LunaTable`1.IndentSize"] = 16.0,
            ["P:EmuSen.LunaP.Controls.LunaTable`1.ExpanderColumn"] = 0,
            ["P:EmuSen.LunaP.Controls.TileGrid.TileWidth"] = 160.0,
            ["P:EmuSen.LunaP.Controls.TileGrid.TileHeight"] = 200.0,
            ["P:EmuSen.LunaP.Controls.TileGrid.Spacing"] = 20.0,
            ["P:EmuSen.LunaP.Controls.OverlayBar.HideAfter"] = TimeSpan.FromSeconds(1.5),
            ["P:EmuSen.LunaP.Controls.NoticeLayer.Duration"] = TimeSpan.FromSeconds(1.75),
        };

        // A claim the sweep finds and this file does not check, with the reason it cannot be.
        private static readonly Dictionary<string, string> Exempt = new()
        {
            ["P:EmuSen.LunaP.Settings.LunaSettings.Diagnostics"] =
                "a process-global static that the suite itself assigns; reading it back at any point in a "
                + "run measures whichever test ran last, not the default. Its initial null is a field "
                + "initialiser with nothing between it and the declaration.",

            ["P:EmuSen.LunaP.Theme.LunaTheme.Source"] =
                "the same shape as LunaSettings.Diagnostics above, and exempt for the same reason: a "
                + "process-global that latches on first read, and ThemeTests, CssThemeTests and "
                + "ReturnsClaimTests all assign it so that each has its own themes folder (§86.11). "
                + "Reading it here measures whichever of them ran last. The default is one expression "
                + "with nothing between it and the property, and what it produces - a FolderThemeSource "
                + "over the application's themes folder - is only observable in a process that has not "
                + "touched it, which this is not.",
        };

        [Fact]
        public Task Every_documented_default_is_the_real_one() => Session.Dispatch(() =>
        {
            var wrong = new List<string>();

            foreach ((string member, _, Func<object?> read) in Claims)
            {
                if (!Values.TryGetValue(member, out object? expected)) continue;

                object? actual = read();
                if (!Equals(expected, actual)) wrong.Add($"{member}: documented {expected}, actually {actual}");
            }

            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        }, default);

        // The other side. A summary edited to claim something else, with the table left alone, has
        // to fail here - otherwise the table is a second copy of the documentation rather than a
        // check on it.
        [Fact]
        public void Every_checked_default_still_says_what_the_table_says_it_says()
        {
            XDocument doc = Published();
            var wrong = new List<string>();

            foreach ((string member, string claim, _) in Claims)
            {
                if (!Values.ContainsKey(member)) continue;

                string? summary = Summary(doc, member);
                if (summary is null)
                {
                    wrong.Add($"{member}: no published summary at all.");
                    continue;
                }

                if (!summary.Contains(claim, StringComparison.Ordinal))
                {
                    wrong.Add($"{member}: the table expects \"{claim}\" and the summary reads \"{summary}\"");
                }
            }

            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        }

        // THE HALF THAT MAKES THE TABLE IMPOSSIBLE TO FORGET, which is the same shape as
        // TemplateOrderTests' sweep (§28.2). A summary that starts claiming a default tomorrow fails
        // here until somebody has decided whether it is checkable.
        [Fact]
        public void Every_summary_claiming_a_default_is_checked_or_excused()
        {
            var pattern = new Regex(@"by default|[Dd]efaults to", RegexOptions.Compiled);
            var unaccounted = new List<string>();

            foreach (XElement member in Published().Root!.Element("members")!.Elements("member"))
            {
                string id = member.Attribute("name")!.Value;
                if (member.Element("summary") is not { } summary) continue;

                string text = string.Concat(summary.Nodes().Select(n => n.ToString())).Trim();
                if (!pattern.IsMatch(text)) continue;
                if (Values.ContainsKey(id) || Exempt.ContainsKey(id)) continue;

                unaccounted.Add($"{id}\n    {text}");
            }

            Assert.True(unaccounted.Count == 0,
                "These summaries name a default that nothing checks. Give each one a row in "
                + "DocumentedDefaultTests.Claims and Values, or an entry in Exempt with the reason:\n\n"
                + string.Join("\n", unaccounted));
        }

        private static string? Summary(XDocument doc, string member) =>
            doc.Root!.Element("members")!.Elements("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == member)
                ?.Element("summary") is { } s
                ? string.Concat(s.Nodes().Select(n => n.ToString())).Trim()
                : null;

        // The .xml the compiler writes beside the .dll, which is exactly what a consumer gets.
        private static XDocument Published()
        {
            string path = Path.ChangeExtension(typeof(ToolWindow).Assembly.Location, ".xml");
            Assert.True(File.Exists(path), $"No documentation file at {path}.");
            return XDocument.Load(path);
        }
    }
}
