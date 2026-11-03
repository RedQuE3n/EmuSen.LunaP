using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // The manager frame on AdaptiveLayout, held to a package manager's columns and a core manager's - see docs/LunaP.md §203.
    public class ManagerLayoutTests
    {
        public sealed record Row(string Name, string Group, string State, string Size, string Extra);

        private static readonly ManagerMark Install = new("install", "install");
        private static readonly ManagerMark Update = new("update", "update");
        private static readonly ManagerMark Remove = new("remove", "remove");

        internal static readonly Row[] CoreRows =
        {
            new("Sapphire", "ps2", "built in", "9 MB", "EmuSen"),
            new("LRPS2", "ps2", "available", "41 MB", "libretro"),
            new("Diamond", "ps1", "built in", "6 MB", "EmuSen"),
            new("Beetle PSX", "ps1", "update", "8 MB", "libretro"),
        };

        internal static readonly Row[] PackageRows =
        {
            new("Sonic CD (US)", "segacd", "linked", "156 MB", "ok"),
            new("Mickey Mania", "segacd", "linked", "410 MB", "ok"),
            new("Jak and Daxter", "ps2", "-", "4.1 GB", "-"),
        };

        private static IReadOnlyList<ManagerMark> CoreMarks(Row r) => r.State switch
        {
            "available" => new[] { Install },
            "update" => new[] { Update, Remove },
            _ => Array.Empty<ManagerMark>(),
        };

        // A core manager's columns and a package manager's, the two the frame was built against - see docs/LunaP.md §203.4.
        internal static ManagerLayout<Row> Cores(AdaptiveForm form = AdaptiveForm.Desktop)
        {
            var m = new ManagerLayout<Row> { Title = "Cores", HasSearch = true, Form = form, RowKey = r => r.Name };
            m.Column(new LunaColumn<Row>("Core", r => r.Name), bigScreen: true)
             .Column(new LunaColumn<Row>("Console", r => r.Group))
             .Column(new LunaColumn<Row>("Source", r => r.Extra))
             .Column(new LunaColumn<Row>("Version", r => "2026-10-31"))
             .Column(new LunaColumn<Row>("State", r => r.State), bigScreen: true)
             .Column(new LunaColumn<Row>("Default", r => r.State == "built in" ? "●" : ""))
             .Column(new LunaColumn<Row>("Size", r => r.Size) { Width = "Auto" });
            m.SetSections(new[] { new AdaptiveSection("all", "All", "Consoles"), new AdaptiveSection("ps2", "PlayStation 2", "Consoles"), new AdaptiveSection("ps1", "PlayStation", "Consoles") });
            m.InSection = (r, s) => s == "all" || r.Group == s;
            m.SearchKey = r => r.Name;
            m.MarksFor = CoreMarks;
            m.DetailFor = r => r is null ? null : new TextBlock { Text = r.Name + " - " + r.Extra };
            m.SetToolActions(new[]
            {
                new LunaAction("Install", () => { }) { PadButton = PadGlyphButton.North },
                new LunaAction("Update", () => { }),
                new LunaAction("Remove", () => { }),
                new LunaAction("Make Default", () => { }) { PadButton = PadGlyphButton.Select },
                new LunaAction("Options…", () => { }),
                new LunaAction("Check for Updates", () => { }),
            });
            m.SetRows(CoreRows);
            m.SetSpace("/Cores 312 MB · 410 GB free", 0.4);
            return m;
        }

        internal static ManagerLayout<Row> Packages(AdaptiveForm form = AdaptiveForm.Desktop)
        {
            var m = new ManagerLayout<Row> { Title = "Packages", HasSearch = true, Form = form, RowKey = r => r.Name };
            m.Column(new LunaColumn<Row>("Game", r => r.Name), bigScreen: true)
             .Column(new LunaColumn<Row>("Console", r => r.Group))
             .Column(new LunaColumn<Row>("Mode", r => r.State), bigScreen: true)
             .Column(new LunaColumn<Row>("State", r => r.Extra))
             .Column(new LunaColumn<Row>("Size", r => r.Size) { Width = "Auto" });
            m.SetSections(new[] { new AdaptiveSection("all", "All", "Consoles"), new AdaptiveSection("packaged", "Packaged", "State"), new AdaptiveSection("problems", "Problems", "State") });
            m.MarksFor = _ => new[] { new ManagerMark("make", "make linked"), new ManagerMark("unmake", "unmake"), new ManagerMark("check", "check") };
            m.SetToolActions(new[] { new LunaAction("Make", () => { }), new LunaAction("Unmake", () => { }), new LunaAction("Check", () => { }), new LunaAction("Zip", () => { }), new LunaAction("Convert to CHD", () => { }) });
            m.SetRows(PackageRows);
            m.SetSpace("8.2 GB · 1.1 TB free", 0.6);
            return m;
        }

        // The width a line of text takes unbounded, in the same face and size, to compare with the box it was given.
        private static double NaturalWidth(TextBlock text)
        {
            var probe = new TextBlock { Text = text.Text, FontFamily = text.FontFamily, FontSize = text.FontSize, FontWeight = text.FontWeight, FontStyle = text.FontStyle, FontStretch = text.FontStretch };
            probe.Measure(Size.Infinity);
            return probe.DesiredSize.Width;
        }

        private static Window Host(Control layout, int width = 1280, int height = 800)
        {
            var window = new Window { Width = width, Height = height, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            return window;
        }

        public static TheoryData<string, AdaptiveForm, int, int> Harness()
        {
            var data = new TheoryData<string, AdaptiveForm, int, int>();
            foreach (string tool in new[] { "Cores", "Packages" })
                foreach (AdaptiveForm form in new[] { AdaptiveForm.Desktop, AdaptiveForm.BigScreen })
                    foreach ((int w, int h) in new[] { (1280, 800), (1920, 1200), (1280, 720) })
                        data.Add(tool, form, w, h);
            return data;
        }

        [Theory]
        [MemberData(nameof(Harness))]
        public Task Both_tools_fit_with_every_role_whole_and_apart(string tool, AdaptiveForm form, int width, int height) => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = tool == "Cores" ? Cores(form) : Packages(form);
            Window window = Host(m, width, height);
            var screen = new Rect(0, 0, width, height);
            var boxes = new List<(AdaptiveRole, Rect)>();
            foreach (AdaptiveRole role in Enum.GetValues<AdaptiveRole>())
            {
                if (m.HostOf(role) is not { } host) continue;
                var box = new Rect(host.TranslatePoint(default, window)!.Value, host.Bounds.Size);
                Assert.True(box.Width > 0 && box.Height > 0 && screen.Contains(box), $"{tool} {form} {width}x{height}: {role} at {box}");
                boxes.Add((role, box));
            }
            for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                    Assert.False(boxes[i].Item2.Intersects(boxes[j].Item2), $"{tool} {form}: {boxes[i].Item1} overlaps {boxes[j].Item1}");
            Control status = m.HostOf(AdaptiveRole.Status)!;
            foreach (Control part in status.GetVisualDescendants().OfType<Control>().Where(c => c is Button or ProgressBar or TextBlock && c.IsEffectivelyVisible))
            {
                var box = new Rect(part.TranslatePoint(default, window)!.Value, part.Bounds.Size);
                Assert.True(screen.Contains(box) && box.Width > 0, $"{tool} {form} {width}x{height}: {part.GetType().Name} at {box} is cut off");
            }
            foreach (TextBlock text in status.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.TextTrimming == Avalonia.Media.TextTrimming.None && t.TextWrapping == Avalonia.Media.TextWrapping.NoWrap))
                Assert.True(NaturalWidth(text) <= text.Bounds.Width + 0.5, $"{tool} {form} {width}x{height}: '{text.Text}' runs past its box, {NaturalWidth(text):0} wide in {text.Bounds.Width:0}");
            window.Close();
        });

        [Fact]
        public Task The_big_screen_list_shows_only_its_columns_and_keeps_the_selection() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores();
            Window window = Host(m);
            Assert.Contains(m.Table.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Console");
            m.Table.Select(CoreRows[1]);
            m.Form = AdaptiveForm.BigScreen;
            UiTest.Settle(m);
            Assert.Same(CoreRows[1], m.Selected);
            Assert.Contains(m, m.BigScreenTable.GetVisualAncestors());
            Assert.DoesNotContain(m, m.Table.GetVisualAncestors());
            List<string?> headers = m.BigScreenTable.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("State", headers);
            Assert.DoesNotContain("Console", headers);
            window.Close();
        });

        [Fact]
        public Task Sections_and_search_filter_the_rows_the_frame_shows() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores();
            Window window = Host(m);
            Assert.Equal(4, m.Shown.Count);
            m.Press(PadGlyphButton.RightShoulder);
            Assert.Equal(new[] { "Sapphire", "LRPS2" }, m.Shown.Select(r => r.Name));
            m.SearchText = "lr";
            Assert.Equal(new[] { "LRPS2" }, m.Shown.Select(r => r.Name));
            m.SearchText = "";
            m.SelectSection("all");
            Assert.Equal(2, m.Shown.Count);
            window.Close();
        });

        [Fact]
        public Task A_row_is_marked_only_for_what_it_is_offered_and_the_mark_cycles_back_to_none() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores();
            Window window = Host(m);
            Assert.False(m.SetMark(CoreRows[0], "install"));
            Assert.False(m.SetMark(CoreRows[1], "remove"));
            Assert.Equal(Update, m.CycleMark(CoreRows[3]));
            Assert.Equal(Remove, m.CycleMark(CoreRows[3]));
            Assert.Null(m.CycleMark(CoreRows[3]));
            Assert.Null(m.CycleMark(CoreRows[0]));
            Assert.False(m.Apply.IsEnabled);
            m.Table.Select(CoreRows[1]);
            Assert.True(m.Press(PadGlyphButton.West));
            Assert.Equal(Install, m.MarkOf(CoreRows[1]));
            Assert.True(m.Apply.IsEnabled);
            Assert.Equal("1 marked: install 1", m.SummaryText);
            window.Close();
        });

        [Fact]
        public Task Nothing_runs_until_the_plan_is_confirmed_and_marks_wait_while_it_runs() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores();
            m.Plan = marked => "download " + string.Join(", ", marked.Select(x => x.Row.Name));
            var runs = new List<IReadOnlyList<ManagerMarked<Row>>>();
            m.Applying += runs.Add;
            Window window = Host(m);
            Assert.False(m.OpenPlan());
            m.SetMark(CoreRows[1], "install");
            m.SetMark(CoreRows[3], "update");
            Assert.True(m.Press(PadGlyphButton.Start));
            Assert.True(m.IsPlanOpen);
            Assert.Equal("download LRPS2, Beetle PSX", m.PlanText);
            Assert.Empty(runs);
            Assert.True(m.Press(PadGlyphButton.East));
            Assert.False(m.IsPlanOpen);
            Assert.Empty(runs);
            Assert.Equal(2, m.Marked.Count);

            m.Apply.Invoke();
            Assert.True(m.Press(PadGlyphButton.Start));
            Assert.Single(runs);
            Assert.Equal(new[] { "LRPS2", "Beetle PSX" }, runs[0].Select(x => x.Row.Name));
            Assert.True(m.IsRunning);
            Assert.False(m.Apply.IsEnabled);
            Assert.False(m.SetMark(CoreRows[1], null));
            Assert.False(m.OpenPlan());
            m.SetProgress(0.5, "LRPS2: checking");
            ProgressBar bar = m.GetVisualDescendants().OfType<ProgressBar>().Single(p => Avalonia.Automation.AutomationProperties.GetName(p) == "Progress");
            Assert.True(bar.IsEffectivelyVisible);
            Assert.Equal(0.5, bar.Value);
            m.Finish();
            Assert.False(m.IsRunning);
            Assert.Empty(m.Marked);
            Assert.False(bar.IsEffectivelyVisible);
            window.Close();
        });

        [Fact]
        public Task Marks_survive_a_refresh_of_their_rows_and_go_with_rows_that_left() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores();
            Window window = Host(m);
            m.SetMark(CoreRows[1], "install");
            m.SetMark(CoreRows[3], "update");
            m.SetRows(new[] { CoreRows[0], CoreRows[1] with { Size = "42 MB" }, CoreRows[2] });
            Assert.Equal(new[] { "LRPS2" }, m.Marked.Select(x => x.Row.Name));
            Assert.Equal("42 MB", m.Marked[0].Row.Size);
            m.SetRows(CoreRows);
            Assert.Null(m.MarkOf(CoreRows[3]));
            Assert.Equal("1 marked: install 1", m.SummaryText);
            window.Close();
        });

        [Fact]
        public Task A_notice_stands_in_the_summarys_place_only_while_nothing_is_marked() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores();
            Window window = Host(m);
            Assert.Equal("Nothing marked", m.SummaryText);
            m.SetNotice("No buildbot listing is known for this platform.");
            Assert.Equal("No buildbot listing is known for this platform.", m.SummaryText);
            m.SetMark(CoreRows[1], "install");
            Assert.Equal("1 marked: install 1", m.SummaryText);
            m.SetMark(CoreRows[1], null);
            Assert.Equal("No buildbot listing is known for this platform.", m.SummaryText);
            m.SetNotice(null);
            Assert.Equal("Nothing marked", m.SummaryText);
            window.Close();
        });

        [Fact]
        public Task The_space_meters_words_are_shown_whole_beside_its_bar() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Packages();
            m.SetSpace("263 KB in packages · 100 GB free", 0.25);
            Window window = Host(m);
            TextBlock words = m.HostOf(AdaptiveRole.Status)!.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == m.SpaceText);
            Assert.True(NaturalWidth(words) <= words.Bounds.Width + 0.5, $"{NaturalWidth(words)} in {words.Bounds.Width}");
            ProgressBar bar = m.GetVisualDescendants().OfType<ProgressBar>().Single(p => Avalonia.Automation.AutomationProperties.GetName(p) == "Space");
            Assert.Equal(0.25, bar.Value);
            Assert.Equal("263 KB in packages · 100 GB free", Avalonia.Automation.AutomationProperties.GetItemStatus(bar));
            window.Close();
        });

        [Fact]
        public Task The_detail_follows_the_chosen_row() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores();
            Window window = Host(m);
            m.Table.Select(CoreRows[2]);
            UiTest.Settle(m);
            Assert.Contains(m.HostOf(AdaptiveRole.Detail)!.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Diamond - EmuSen");
            window.Close();
        });

        [Fact]
        public Task Apply_is_drawn_in_the_apply_bar_not_the_toolbar_and_keeps_its_pad_button() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores();
            Window window = Host(m);
            Assert.DoesNotContain(m.HostOf(AdaptiveRole.Actions)!.GetVisualDescendants().OfType<Button>(), b => Equals(b.Content, "Apply…"));
            Assert.Contains(m.HostOf(AdaptiveRole.Status)!.GetVisualDescendants().OfType<Button>(), b => Equals(b.Content, "Apply…"));
            Assert.Contains(m.Hints, h => h.Label == "Apply…" && h.Button == PadGlyphButton.Start);
            // The main action is named last, after the face buttons' actions and List (docs/LunaP.md §211.2).
            Assert.Equal("Apply…", m.Hints.Last(h => h.Button is PadGlyphButton.Start or PadGlyphButton.North or PadGlyphButton.West or PadGlyphButton.Select).Label);
            window.Close();
        });

        // Marking a row by the pad keeps the focus on that row, for every tool on the frame (docs/LunaP.md §211.6).
        [Theory]
        [InlineData(AdaptiveForm.BigScreen)]
        [InlineData(AdaptiveForm.Desktop)]
        public Task Marking_a_row_keeps_the_focus_on_it(AdaptiveForm form) => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores(form);
            Window window = Host(m);
            LunaTable<Row> table = form == AdaptiveForm.BigScreen ? m.BigScreenTable : m.Table;
            table.Select(CoreRows[1]);
            UiTest.Settle(m);
            Assert.True(m.FocusRole(AdaptiveRole.Content));
            UiTest.Settle(m);
            object? Focused() => (TopLevel.GetTopLevel(m)?.FocusManager?.GetFocusedElement() as Control)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()?.DataContext;
            Assert.Same(CoreRows[1], Focused());
            if (form == AdaptiveForm.BigScreen) Assert.True(m.Press(PadGlyphButton.West));
            else m.Mark.Invoke();
            UiTest.Settle(m);
            Assert.Equal("install", m.MarkOf(CoreRows[1])?.Key);
            Assert.Same(CoreRows[1], Focused());
            window.Close();
        });

        // A plan switch says in a sentence what it does, under its name (docs/LunaP.md §211.7).
        [Theory]
        [InlineData(AdaptiveForm.BigScreen)]
        [InlineData(AdaptiveForm.Desktop)]
        public Task Every_plan_switch_says_what_it_does(AdaptiveForm form) => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores(form);
            m.SetPlanOptions([new LunaAction("Videos", _ => { }) { IsCheckable = true, HelpText = "Also fetches each game's video." }]);
            Window window = Host(m);
            Assert.True(m.SetMark(CoreRows[1], "install"));
            Assert.True(m.OpenPlan());
            UiTest.Settle(m);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains(ManagerLayout<Row>.PlanOptionSentenceClass) && t.Text == "Also fetches each game's video." && t.IsEffectivelyVisible);
            // Its name is drawn too: on the big screen a switch's template draws none of its own.
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Videos" && t.IsEffectivelyVisible);
            window.Close();
        });

        // The big screen's list and bar held to the design's mockups - see docs/LunaP.md §211.2.
        [Fact]
        public Task The_big_screen_list_has_no_header_box_full_width_rows_and_Apply_beside_the_meter() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores(AdaptiveForm.BigScreen);
            m.PageFor = r => new[] { new LunaAction("Details", () => { }) };
            Window window = Host(m);
            m.BigScreenTable.Select(CoreRows[1]);
            UiTest.Settle(m);
            Assert.DoesNotContain(m.BigScreenTable.GetVisualDescendants().OfType<Border>(), b => b.IsEffectivelyVisible && b.Child is Grid { Name: "PART_Header" });
            ListBoxItem item = m.BigScreenTable.GetVisualDescendants().OfType<ListBoxItem>().First(i => ReferenceEquals(i.DataContext, CoreRows[1]));
            Border row = item.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains(LunaTable<Row>.GroupRowClass));
            Assert.Equal(item.Bounds.Width, row.Bounds.Width, 1);
            Assert.Equal(new Avalonia.Thickness(4, 0, 0, 0), row.BorderThickness);
            Assert.Equal(new Avalonia.Thickness(0, 0, 0, 1), item.Padding);
            Assert.Contains(m.HostOf(AdaptiveRole.Status)!.GetVisualDescendants().OfType<Button>(), b => b.IsEffectivelyVisible && b.Command == m.Apply);
            List<string> said = m.Hints.Select(h => h.Label).ToList();
            Assert.Equal("Apply…", said[^1]);
            Assert.True(said.IndexOf("More") is > 2 and var more && more < said.Count - 1, string.Join(", ", said));
            window.Close();
        });

        [Fact]
        public Task A_frame_lists_its_rows_under_group_headings_in_both_forms() => UiTest.Run(() =>
        {
            ManagerLayout<Row> m = Cores(AdaptiveForm.BigScreen);
            m.GroupOf = r => r.Group == "ps2" ? "Sony 2" : "Sony 1";
            Window window = Host(m);
            Assert.Equal(new string?[] { "SONY 2", null, "SONY 1", null }, Enumerable.Range(0, 4).Select(m.BigScreenTable.HeadingAt));
            m.Form = AdaptiveForm.Desktop;
            UiTest.Settle(m);
            Assert.Equal("SONY 2", m.Table.HeadingAt(0));
            window.Close();
        });
    }
}
