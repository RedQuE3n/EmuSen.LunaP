using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // The tool screen, and the frame's row action, row page, question form, filters and List card, held to the fit audit at three sizes - see docs/LunaP.md §208.
    public class ToolFrameTests
    {
        public sealed record Row(string Name, string Console, string State);

        private static readonly Row[] Rows =
        {
            new("PCEE2", "ps2", "approve"), new("Sapphire", "ps2", "builtin"), new("Diamond", "psx", "default"),
            new("Beetle PSX HW", "psx", "update"), new("VenusRT", "snes", "default"), new("Snes9x", "snes", "installed"),
            new("MoonRT", "nes", "default"), new("Nestopia UE", "nes", "available"), new("Mupen64Plus-Next", "n64", "available"),
        };

        private static readonly FitAudit.Allowance CellsWhole = new(
            "a cell cut in the table is whole in the detail while its row is chosen",
            c => c is TextBlock && c.FindAncestorOfType<LunaTable>() is not null);

        public static TheoryData<int, int> Sizes() => new() { { 1280, 800 }, { 1920, 1200 }, { 1280, 720 } };

        private sealed class Built
        {
            public ManagerLayout<Row> Frame = null!;
            public Window Window = null!;
            public readonly List<string> Invoked = new();
            public readonly List<string> Filters = new();
            public readonly LunaAction Theme = new("Theme", _ => { }) { IsCheckable = true, IsChecked = true };
            public readonly LunaAction Videos = new("Videos", _ => { }) { IsCheckable = true };
        }

        // A core manager's frame with a row action, a row page, four filters and one list action, the detail beside the list.
        private static Built Build(AdaptiveForm form, int width = 1280, int height = 800, bool show = true)
        {
            var b = new Built();
            var m = new ManagerLayout<Row> { Title = "Cores", HasSearch = true, Form = form, RowKey = r => r.Name, DetailPlacement = AdaptiveDetailPlacement.Beside, ContentShare = 0.6 };
            m.Column(new LunaColumn<Row>("Core", r => r.Name), bigScreen: true)
             .Column(new LunaColumn<Row>("Console", r => r.Console), bigScreen: true)
             .Column(new LunaColumn<Row>("Status", r => r.State), bigScreen: true);
            m.Header = new AdaptiveHeader("CORES", Facts: "17 cores · 6 installed · 1 to approve · catalogue read 31 Oct 2026");
            m.SetSections(new[] { new AdaptiveSection("all", "All"), new AdaptiveSection("ps2", "PlayStation 2"), new AdaptiveSection("psx", "PlayStation"), new AdaptiveSection("snes", "Super Nintendo"), new AdaptiveSection("nes", "NES"), new AdaptiveSection("n64", "Nintendo 64") });
            m.InSection = (r, s) => s == "all" || r.Console == s;
            m.SetFilters(new[] { new AdaptiveSection("all", "All"), new AdaptiveSection("installed", "Installed"), new AdaptiveSection("available", "Available"), new AdaptiveSection("approve", "To approve") });
            m.InFilter = (r, f) => f == "all" || r.State == f || (f == "installed" && r.State is "default" or "builtin" or "update");
            m.FilterChosen += b.Filters.Add;
            m.SearchKey = r => r.Name;
            m.MarksFor = r => r.State == "available" ? new[] { new ManagerMark("install", "install") } : Array.Empty<ManagerMark>();
            m.DetailFor = r => r is null ? null : new TextBlock { Text = $"{r.Name} runs {r.Console} games through the libretro host, in a sandbox of its own. Nightly build of 31 October 2026, 41 MB, downloaded today. Waiting for your approval: nothing downloaded runs until you approve it.", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            m.ActionFor = r => r.State switch
            {
                "approve" => new LunaAction("Approve…", () => b.Invoked.Add("approve " + r.Name)),
                "available" => new LunaAction("Install…", () => b.Invoked.Add("install " + r.Name)),
                "update" => new LunaAction("Update…", () => b.Invoked.Add("update " + r.Name)),
                "builtin" or "installed" => new LunaAction("Make Default", () => b.Invoked.Add("default " + r.Name)),
                _ => null,
            };
            m.PageFor = r => new[] { new LunaAction("Options", () => b.Invoked.Add("options " + r.Name)), new LunaAction("Verify", () => b.Invoked.Add("verify " + r.Name)), new LunaAction("Remove…", () => b.Invoked.Add("remove " + r.Name)) };
            m.PageTitle = r => r.Name;
            m.SetListActions(new[] { new LunaAction("Check for Updates", () => b.Invoked.Add("check")) });
            m.SetPlanOptions(new[] { b.Theme, b.Videos });
            m.SetToolActions(Array.Empty<LunaAction>());
            m.SetRows(Rows);
            m.SetSpace("/Cores 54 MB · 410 GB free", 0.6);
            b.Frame = m;
            if (!show) return b;
            b.Window = new Window { Width = width, Height = height, Content = m };
            b.Window.Show();
            UiTest.Settle(m);
            m.Table.Select(Rows[0]);
            m.BigScreenTable.Select(Rows[0]);
            m.FocusRole(AdaptiveRole.Content);
            UiTest.Settle(m);
            return b;
        }

        private static List<string> Faults(Control root, AdaptiveForm form) =>
            form == AdaptiveForm.BigScreen
                ? FitAudit.Check(root, new[] { FitAudit.TextBoxesScroll, CellsWhole })
                : FitAudit.Check(root, new[] { FitAudit.TextBoxesScroll, CellsWhole }, smallest: 0).Where(f => !f.StartsWith("cut at a scrolling edge", StringComparison.Ordinal)).ToList();

        private static Rect InWindow(Control control, Window window) => new(control.TranslatePoint(default, window) ?? default, control.Bounds.Size);

        private static T Named<T>(Control root, string name) where T : Control =>
            root.GetVisualDescendants().OfType<T>().First(c => Avalonia.Automation.AutomationProperties.GetName(c) == name || c.Name == name);

        [Theory]
        [MemberData(nameof(Sizes))]
        public Task Beside_the_content_the_detail_stands_beside_the_status_too_and_the_frame_fits(int width, int height) => UiTest.Run(() =>
        {
            Built b = Build(AdaptiveForm.BigScreen, width, height);
            Rect status = InWindow(b.Frame.HostOf(AdaptiveRole.Status)!, b.Window);
            Rect detail = InWindow(b.Frame.HostOf(AdaptiveRole.Detail)!, b.Window);
            Rect content = InWindow(b.Frame.HostOf(AdaptiveRole.Content)!, b.Window);
            Assert.True(status.Right <= detail.Left + 1, $"the status ends at {status.Right} but the detail starts at {detail.Left}");
            Assert.True(status.Top >= content.Bottom - 1, "the status is under the content");
            Assert.True(detail.Bottom >= status.Bottom - 1, "the detail does not reach the status's foot");
            Assert.Empty(Faults(b.Frame, AdaptiveForm.BigScreen));
            b.Window.Close();
        });

        [Fact]
        public Task A_does_the_chosen_rows_action_and_the_help_bar_names_it() => UiTest.Run(() =>
        {
            Built b = Build(AdaptiveForm.BigScreen);
            Assert.Equal("Approve…", b.Frame.Hints[0].Label);
            Assert.Equal(PadGlyphButton.South, b.Frame.Hints[0].Button);
            Assert.True(b.Frame.Press(PadGlyphButton.South));
            Assert.Equal(new[] { "approve PCEE2" }, b.Invoked);
            b.Frame.BigScreenTable.Select(Rows[1]);
            UiTest.Settle(b.Frame);
            Assert.Equal("Make Default", b.Frame.Hints[0].Label);
            b.Frame.BigScreenTable.Select(Rows[2]);
            UiTest.Settle(b.Frame);
            Assert.Equal("Back", b.Frame.Hints[0].Label);
            Assert.False(b.Frame.Press(PadGlyphButton.South));
            Assert.Single(b.Invoked);
            b.Window.Close();

            Built d = Build(AdaptiveForm.Desktop);
            Assert.False(d.Frame.Press(PadGlyphButton.South));
            Assert.Empty(d.Invoked);
            d.Window.Close();
        });

        [Fact]
        public Task X_opens_the_rows_page_whose_actions_close_it_and_B_closes_it() => UiTest.Run(() =>
        {
            Built b = Build(AdaptiveForm.BigScreen);
            Assert.Contains(b.Frame.Hints, h => h.Label == "More" && h.Button == PadGlyphButton.West);
            Assert.DoesNotContain(b.Frame.Hints, h => h.Label == "Mark");
            Assert.True(b.Frame.Press(PadGlyphButton.West));
            UiTest.Settle(b.Frame);
            Assert.True(b.Frame.IsPageOpen);
            Control page = b.Frame.HostOf(AdaptiveRole.Detail)!;
            Assert.Equal("AdaptivePage", page.Name);
            List<ActionButton> buttons = page.GetVisualDescendants().OfType<ActionButton>().ToList();
            Assert.Equal(new[] { "Options", "Verify", "Remove…" }, buttons.Select(x => x.Content?.ToString()));
            Assert.Contains("PCEE2", page.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.Empty(Faults(b.Frame, AdaptiveForm.BigScreen));
            Assert.Equal(AdaptiveRole.Detail, b.Frame.FocusedRole);
            buttons[1].Command?.Execute(null);
            buttons[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            UiTest.Settle(b.Frame);
            Assert.Equal(new[] { "verify PCEE2" }, b.Invoked);
            Assert.False(b.Frame.IsPageOpen);
            Assert.True(b.Frame.Press(PadGlyphButton.West));
            Assert.True(b.Frame.Press(PadGlyphButton.East));
            UiTest.Settle(b.Frame);
            Assert.False(b.Frame.IsPageOpen);
            Assert.Equal(AdaptiveRole.Content, b.Frame.FocusedRole);
            b.Window.Close();
        });

        [Fact]
        public Task The_question_is_No_then_Yes_on_the_big_screen_and_the_verbs_on_the_desktop() => UiTest.Run(() =>
        {
            Built b = Build(AdaptiveForm.BigScreen);
            Assert.True(b.Frame.SetMark(Rows[7], "install"));
            Assert.True(b.Frame.OpenPlan());
            UiTest.Settle(b.Frame);
            Assert.Equal("Apply these changes?", b.Frame.QuestionTitle);
            Control sheet = b.Frame.GetVisualDescendants().OfType<Border>().First(x => x.Classes.Contains(ManagerLayout<Row>.PlanClass));
            List<Button> answers = sheet.GetVisualDescendants().OfType<Button>().Where(x => x is not Avalonia.Controls.Primitives.ToggleButton).ToList();
            Assert.Equal(new[] { "No", "Yes" }, answers.Select(a => a.Content?.ToString()));
            Assert.True(answers[0].Bounds.Top < answers[1].Bounds.Top, "No is above Yes");
            Assert.Same(answers[0], TopLevel.GetTopLevel(b.Frame)?.FocusManager?.GetFocusedElement());
            Assert.Empty(Faults(b.Frame, AdaptiveForm.BigScreen));
            Assert.True(b.Frame.Press(PadGlyphButton.East));
            Assert.False(b.Frame.IsPlanOpen);

            Task<bool> asked = b.Frame.Ask("Approve PCEE2?", "libretro signs no build, so approving trusts the build the buildbot served that day.");
            UiTest.Settle(b.Frame);
            Assert.True(b.Frame.IsPlanOpen);
            Assert.Equal("Approve PCEE2?", b.Frame.QuestionTitle);
            Assert.False(b.Frame.Press(PadGlyphButton.Start));
            Assert.True(b.Frame.IsPlanOpen);
            Assert.Empty(Faults(b.Frame, AdaptiveForm.BigScreen));
            Assert.True(b.Frame.Confirm());
            Assert.True(asked.Result);
            Assert.False(b.Frame.IsPlanOpen);
            asked = b.Frame.Ask("Remove Snes9x?", "Your saves and settings are kept.");
            Assert.True(b.Frame.Press(PadGlyphButton.East));
            Assert.False(asked.Result);
            b.Window.Close();

            Built d = Build(AdaptiveForm.Desktop);
            _ = d.Frame.Ask("Approve PCEE2?", "The hash holds EmuSen to exactly this build.", "Approve", "Not Now");
            UiTest.Settle(d.Frame);
            Control dialog = d.Frame.GetVisualDescendants().OfType<Border>().First(x => x.Classes.Contains(ManagerLayout<Row>.PlanClass));
            Assert.Equal(new[] { "Not Now", "Approve" }, dialog.GetVisualDescendants().OfType<Button>().Where(x => x is not Avalonia.Controls.Primitives.ToggleButton).Select(a => a.Content?.ToString()));
            Assert.Empty(Faults(d.Frame, AdaptiveForm.Desktop));
            d.Frame.CancelPlan();
            d.Window.Close();
        });

        [Fact]
        public Task Plan_options_are_rows_of_the_plans_question_and_read_back_and_a_tools_question_has_none() => UiTest.Run(() =>
        {
            Built b = Build(AdaptiveForm.BigScreen);
            b.Frame.SetMark(Rows[7], "install");
            b.Frame.OpenPlan();
            UiTest.Settle(b.Frame);
            List<ToggleSwitch> rows = b.Frame.GetVisualDescendants().OfType<ToggleSwitch>().Where(t => t.IsEffectivelyVisible).ToList();
            Assert.Equal(new[] { "Theme", "Videos" }, rows.Select(MenuRows.GetLabel));
            Assert.Equal(new bool?[] { true, false }, rows.Select(r => r.IsChecked));
            rows[1].IsChecked = true;
            Assert.True(b.Videos.IsChecked);
            Assert.Empty(Faults(b.Frame, AdaptiveForm.BigScreen));
            b.Frame.CancelPlan();
            _ = b.Frame.Ask("Approve PCEE2?", "Trust on first download.");
            UiTest.Settle(b.Frame);
            Assert.DoesNotContain(b.Frame.GetVisualDescendants().OfType<ToggleSwitch>(), t => t.IsEffectivelyVisible);
            b.Frame.CancelPlan();
            b.Window.Close();

            Built d = Build(AdaptiveForm.Desktop);
            d.Frame.SetMark(Rows[7], "install");
            d.Frame.OpenPlan();
            UiTest.Settle(d.Frame);
            Assert.Equal(new[] { "Theme", "Videos" }, d.Frame.GetVisualDescendants().OfType<CheckBox>().Where(c => c.IsEffectivelyVisible).Select(c => c.Content?.ToString()));
            d.Frame.CancelPlan();
            d.Window.Close();
        });

        [Fact]
        public Task Select_opens_the_List_card_of_Search_Filter_and_the_list_actions_and_the_filter_narrows_the_rows() => UiTest.Run(() =>
        {
            Built b = Build(AdaptiveForm.BigScreen);
            Assert.Contains(b.Frame.Hints, h => h.Label == "List" && h.Button == PadGlyphButton.Select);
            Assert.True(b.Frame.Press(PadGlyphButton.Select));
            UiTest.Settle(b.Frame);
            Assert.True(b.Frame.IsListToolsOpen);
            MenuPanel card = b.Frame.ListCard;
            Assert.True(card.IsEffectivelyVisible);
            Assert.Equal("Search", Named<Button>(card, "Search").Content);
            Assert.Equal("Filter", MenuRows.GetLabel(Named<ComboBox>(card, "Filter")));
            Assert.Contains(card.GetVisualDescendants().OfType<ActionButton>(), a => a.Content?.ToString() == "Check for Updates");
            Assert.Empty(FitAudit.Check(b.Frame, new[] { FitAudit.TextBoxesScroll, CellsWhole }));
            Assert.False(b.Frame.Press(PadGlyphButton.LeftShoulder));
            Assert.True(b.Frame.Press(PadGlyphButton.East));
            Assert.False(b.Frame.IsListToolsOpen);
            Assert.Equal(AdaptiveRole.Content, b.Frame.FocusedRole);

            Assert.True(b.Frame.StepFilter(1));
            Assert.Equal(new[] { "installed" }, b.Filters);
            Assert.Equal(new[] { "Sapphire", "Diamond", "Beetle PSX HW", "VenusRT", "Snes9x", "MoonRT" }, b.Frame.Shown.Select(r => r.Name));
            Assert.True(b.Frame.SelectFilter("all"));
            Assert.Equal(9, b.Frame.Shown.Count);
            Assert.Single(b.Filters);
            b.Window.Close();

            Built d = Build(AdaptiveForm.Desktop);
            ComboBox show = Named<ComboBox>(d.Frame, "Show");
            Assert.True(show.IsEffectivelyVisible);
            Assert.Equal(0, show.SelectedIndex);
            show.SelectedIndex = 2;
            Assert.Equal(new[] { "available" }, d.Filters);
            Assert.Equal(new[] { "Nestopia UE", "Mupen64Plus-Next" }, d.Frame.Shown.Select(r => r.Name));
            Assert.False(d.Frame.Press(PadGlyphButton.Select));
            Assert.Empty(Faults(d.Frame, AdaptiveForm.Desktop));
            d.Window.Close();
        });

        private static (ToolWindow Host, SheetLayer Layer) Host(double width, double height)
        {
            var layer = new SheetLayer { PresentsWindows = true, MenuLook = true };
            var host = new ToolWindow { Width = width, Height = height, Content = new Grid { Children = { new Border(), layer } } };
            MenuPanel.SetScale(host, height / 800.0);
            host.Show();
            return (host, layer);
        }

        [Theory]
        [MemberData(nameof(Sizes))]
        public Task A_screen_presents_a_tool_full_screen_in_the_safe_area_with_no_frame_of_the_layers(int width, int height) => UiTest.Run(() =>
        {
            (ToolWindow host, SheetLayer layer) = Host(width, height);
            Built b = Build(AdaptiveForm.BigScreen, show: false);
            var window = new ToolWindow { Title = "Cores", Content = b.Frame, Width = 1120, Height = 720 };
            SheetLayer.SetScreen(window, true);
            _ = SheetLayer.Show(window, host);
            UiTest.Settle(layer);
            Assert.True(layer.DrawsMenu(window));
            Control root = layer.SheetOf(window)!;
            Assert.DoesNotContain(root.GetVisualDescendants().OfType<MenuPanel>(), p => p.Name == "SheetMenu");
            Rect sheet = InWindow(root, host);
            Assert.True(sheet.Width >= width - 1 && sheet.Height >= height - 1, $"the screen is {sheet.Width} x {sheet.Height} in {width} x {height}");
            double scale = height / 800.0;
            Rect header = InWindow(((Border)b.Frame.HostOf(AdaptiveRole.Header)!).Child!, host);
            Assert.True(header.Left >= 64 * scale - 1 && header.Top >= 40 * scale - 1, $"the header sits at {header.Left},{header.Top}");
            Assert.Empty(FitAudit.Check(root, new[] { FitAudit.TextBoxesScroll, CellsWhole }));
            window.Close();

            var framed = new ToolWindow { Title = "Cores", Content = new TextBlock { Text = "a sheet" } };
            _ = SheetLayer.Show(framed, host);
            UiTest.Settle(layer);
            Assert.Contains(layer.SheetOf(framed)!.GetVisualDescendants().OfType<MenuPanel>(), p => p.Name == "SheetMenu");
            framed.Close();
            host.Close();
        });
    }
}
