using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;
using Xunit.Abstractions;

namespace EmuSen.LunaP.Tests
{
    // The manager frame held to the framed fit audit in every state, form and size, with real-length words - see docs/LunaP.md §204.
    public class ManagerFitAuditTests
    {
        private readonly ITestOutputHelper _out;

        public ManagerFitAuditTests(ITestOutputHelper output) => _out = output;

        public sealed record Row(string Name, string Group, string Mode, string State, string Size, string Source, string Version, string Licence);

        private static readonly string[] Consoles = { "nes", "snes", "n64", "gb", "gba", "md", "segacd", "32x", "sms", "psx", "ps2", "pce" };

        private static readonly string[] ConsoleNames =
        {
            "Nintendo Entertainment System", "Super Nintendo", "Nintendo 64", "Game Boy", "Game Boy Advance", "Mega Drive",
            "Mega-CD", "32X", "Master System", "PlayStation", "PlayStation 2", "PC Engine",
        };

        private static List<Row> Rows() => Enumerable.Range(0, 24).Select(i => new Row(
            i % 3 == 0 ? $"Granite Choir - The Very Long Packaged Edition With Every Subtitle (USA) (Rev {i})" : $"Sony - PlayStation 2 (LRPS2) build {i}",
            Consoles[i % Consoles.Length], i % 2 == 0 ? "linked" : "zipped", i % 4 == 0 ? "update" : "available",
            $"{i + 1}.{i} GB", i % 2 == 0 ? "libretro" : "EmuSen", "2026-10-31 nightly", "GPLv2 or later, with the MAME licence for some files")).ToList();

        private static readonly ManagerMark Make = new("make", "make linked");
        private static readonly ManagerMark Remove = new("remove", "remove");

        // Eight columns and twelve sections, a package manager's and a core manager's together.
        private static ManagerLayout<Row> Build(AdaptiveForm form)
        {
            var m = new ManagerLayout<Row> { Title = "Packages", HasSearch = true, Form = form, RowKey = r => r.Name };
            m.Column(new LunaColumn<Row>("Game", r => r.Name), bigScreen: true)
             .Column(new LunaColumn<Row>("Console", r => r.Group))
             .Column(new LunaColumn<Row>("Mode", r => r.Mode), bigScreen: true)
             .Column(new LunaColumn<Row>("State", r => r.State), bigScreen: true)
             .Column(new LunaColumn<Row>("Source", r => r.Source))
             .Column(new LunaColumn<Row>("Version", r => r.Version))
             .Column(new LunaColumn<Row>("Licence", r => r.Licence))
             .Column(new LunaColumn<Row>("Size", r => r.Size) { Width = "Auto" });
            m.SetSections(new[] { new AdaptiveSection("all", "All games", "Consoles") }.Concat(Consoles.Select((c, i) => new AdaptiveSection(c, ConsoleNames[i], "Consoles"))));
            m.InSection = (r, s) => s == "all" || r.Group == s;
            m.SearchKey = r => r.Name;
            m.MarksFor = _ => new[] { Make, Remove };
            m.DetailFor = r => r is null ? null : new TextBlock { Text = string.Join(" · ", r.Name, r.Group, r.Mode, r.State, r.Source, r.Version, r.Licence, r.Size), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            m.Summarize = marked => $"{marked.Count} marked: make {marked.Count(x => x.Mark == Make)} linked, remove {marked.Count(x => x.Mark == Remove)}, 14.2 GB to write";
            m.Plan = marked => string.Join("\n", marked.Select(x => $"{x.Mark.Label}: {x.Row.Name}"));
            m.SetToolActions(new[]
            {
                new LunaAction("Make", () => { }) { PadButton = PadGlyphButton.North },
                new LunaAction("Unmake", () => { }),
                new LunaAction("Check", () => { }),
                new LunaAction("Zip", () => { }),
                new LunaAction("Convert to CHD", () => { }) { PadButton = PadGlyphButton.Select },
                new LunaAction("Approve…", () => { }),
                new LunaAction("Make Default", () => { }),
                new LunaAction("Options…", () => { }),
                new LunaAction("Verify", () => { }),
                new LunaAction("Check for Updates", () => { }),
            });
            m.SetRows(Rows());
            m.SetSpace("263 KB in packages · 100 GB free", 0.42);
            return m;
        }

        // The frame's table shares its width among its columns, and the detail shows the chosen row whole.
        private static readonly FitAudit.Allowance CellsWholeInTheDetail = new(
            "a cell cut in the table is whole in the detail while its row is chosen",
            c => c is TextBlock && c.FindAncestorOfType<LunaTable>() is not null);

        public static TheoryData<AdaptiveForm, int, int, string> Cases()
        {
            var data = new TheoryData<AdaptiveForm, int, int, string>();
            foreach (AdaptiveForm form in Enum.GetValues<AdaptiveForm>())
                foreach ((int w, int h) in new[] { (1280, 800), (1920, 1200), (1280, 720) })
                    foreach (string state in new[] { "listing", "detail", "plan", "running", "notice", "last section" })
                        data.Add(form, w, h, state);
            return data;
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public Task The_frame_passes_the_fit_audit_in_every_state(AdaptiveForm form, int width, int height, string state) => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Build(form);
            var window = new Window { Width = width, Height = height, Content = m };
            window.Show();
            UiTest.Settle(m);
            Row first = m.Rows[0];
            m.Table.Select(first);
            m.BigScreenTable.Select(first);
            m.FocusRole(AdaptiveRole.Content);
            switch (state)
            {
                case "detail":
                    if (form == AdaptiveForm.BigScreen) m.OpenDetail();
                    break;
                case "plan":
                    foreach (Row r in m.Rows.Take(15)) m.SetMark(r, "make");
                    m.SetMark(m.Rows[3], "remove");
                    m.OpenPlan();
                    break;
                case "running":
                    foreach (Row r in m.Rows.Take(4)) m.SetMark(r, "make");
                    m.OpenPlan();
                    m.Confirm();
                    m.SetProgress(0.4, "Making Granite Choir - The Very Long Packaged Edition linked: 2.1 of 4.0 GB");
                    break;
                case "notice":
                    m.SetNotice("No library folder is set, so no package can be made; choose one in Settings.");
                    break;
                case "last section":
                    m.SelectSection(Consoles[^1]);
                    break;
            }
            UiTest.Settle(m);
            if (state == "last section" && form == AdaptiveForm.BigScreen)
                Assert.Contains(m.HostOf(AdaptiveRole.Navigation)!.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>(), t => t.IsChecked == true && t.Bounds.Width > 0 && t.Focusable);
            List<string> faults = form == AdaptiveForm.BigScreen
                ? FitAudit.Check(m, new[] { FitAudit.TextBoxesScroll, CellsWholeInTheDetail })
                : FitAudit.Check(m, new[] { FitAudit.TextBoxesScroll, CellsWholeInTheDetail }, smallest: 0).Where(f => !f.StartsWith("cut at a scrolling edge", StringComparison.Ordinal)).ToList();
            foreach (string f in faults) _out.WriteLine($"{form} {width}x{height} {state}: {f}");
            window.Close();
            Assert.Empty(faults);
        });
    }
}
