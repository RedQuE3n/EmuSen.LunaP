using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // One description, two forms: placement, fit, focus from roles and the generated help bar - see docs/LunaP.md §202.
    public class AdaptiveLayoutTests
    {
        public static TheoryData<AdaptiveForm, int, int> FormsAndScreens()
        {
            var data = new TheoryData<AdaptiveForm, int, int>();
            foreach (AdaptiveForm form in Enum.GetValues<AdaptiveForm>())
            {
                data.Add(form, 1280, 800);
                data.Add(form, 1920, 1200);
                data.Add(form, 1280, 720);
            }
            return data;
        }

        internal static IReadOnlyList<LunaAction> SevenActions(List<string> invoked) => new[]
        {
            new LunaAction("Install", () => invoked.Add("Install")) { PadButton = PadGlyphButton.North },
            new LunaAction("Update", () => invoked.Add("Update")),
            new LunaAction("Remove", () => invoked.Add("Remove")),
            new LunaAction("Make Default", () => invoked.Add("Make Default")) { PadButton = PadGlyphButton.Select },
            new LunaAction("Options…", () => invoked.Add("Options…")),
            LunaAction.Separator(),
            new LunaAction("Check for Updates", () => invoked.Add("Check for Updates")) { IsMain = true, PadButton = PadGlyphButton.Start },
        };

        private static (Window Window, AdaptiveLayout Layout, List<string> Chosen, List<string> Invoked) Build(AdaptiveForm form, int width, int height, bool detail = true)
        {
            var chosen = new List<string>();
            var invoked = new List<string>();
            var layout = new AdaptiveLayout { Title = "Cores", HasSearch = true, SearchHint = "Search", Form = form };
            layout.SetSections(new[]
            {
                new AdaptiveSection("all", "All", "Consoles"),
                new AdaptiveSection("ps2", "PlayStation 2", "Consoles"),
                new AdaptiveSection("installed", "Installed", "Show"),
            });
            layout.SetActions(SevenActions(invoked));
            layout.Content = new ListBox { ItemsSource = new[] { "Sapphire", "LRPS2", "Diamond" } };
            if (detail) layout.Detail = new StackPanel { Children = { new TextBlock { Text = "LRPS2 - GPLv3" }, new Button { Content = "Licence…" } } };
            layout.Status = new TextBlock { Text = "2 marked" };
            layout.SectionChosen += chosen.Add;
            var window = new Window { Width = width, Height = height, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            return (window, layout, chosen, invoked);
        }

        private static Rect InWindow(Control control, Window window) =>
            new(control.TranslatePoint(default, window) ?? throw new InvalidOperationException(control.Name), control.Bounds.Size);

        [Theory]
        [MemberData(nameof(FormsAndScreens))]
        public Task Every_role_is_inside_the_window_and_none_overlaps_another(AdaptiveForm form, int width, int height) => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(form, width, height);
            var screen = new Rect(0, 0, width, height);
            var shown = new List<(AdaptiveRole Role, Rect Box)>();
            foreach (AdaptiveRole role in Enum.GetValues<AdaptiveRole>())
            {
                if (layout.HostOf(role) is not { } host) continue;
                Rect box = InWindow(host, window);
                Assert.True(box.Width > 0 && box.Height > 0, $"{form} {width}x{height}: {role} has no room ({box})");
                Assert.True(screen.Contains(box), $"{form} {width}x{height}: {role} at {box} is cut off");
                shown.Add((role, box));
            }
            foreach (AdaptiveRole needed in new[] { AdaptiveRole.Navigation, AdaptiveRole.Content, AdaptiveRole.Actions, AdaptiveRole.Search, AdaptiveRole.Status })
                Assert.Contains(shown, s => s.Role == needed);
            Assert.Equal(form == AdaptiveForm.Desktop, shown.Any(s => s.Role == AdaptiveRole.Detail));
            for (int i = 0; i < shown.Count; i++)
                for (int j = i + 1; j < shown.Count; j++)
                    Assert.False(shown[i].Box.Intersects(shown[j].Box), $"{form} {width}x{height}: {shown[i].Role} overlaps {shown[j].Role}");
            window.Close();
        });

        [Theory]
        [MemberData(nameof(FormsAndScreens))]
        public Task Every_drawn_action_is_whole_on_the_screen(AdaptiveForm form, int width, int height) => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(form, width, height);
            var screen = new Rect(0, 0, width, height);
            List<Button> buttons = layout.HostOf(AdaptiveRole.Actions)!.GetVisualDescendants().OfType<Button>().ToList();
            Assert.Equal(6, buttons.Count);
            foreach (Button button in buttons)
            {
                Rect box = InWindow(button, window);
                Assert.True(screen.Contains(box) && box.Width > 0, $"{form} {width}x{height}: {button.Content} at {box} is cut off");
            }
            window.Close();
        });

        [Fact]
        public Task The_big_screen_is_in_the_menus_type_and_the_desktop_is_not() => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(AdaptiveForm.Desktop, 1280, 800);
            Assert.False(MenuLook.GetIsOn(layout));
            layout.Form = AdaptiveForm.BigScreen;
            UiTest.Settle(layout);
            Assert.True(MenuLook.GetIsOn(layout));
            Assert.NotNull(layout.GetVisualDescendants().OfType<HintBar>().FirstOrDefault(h => h.IsEffectivelyVisible));
            layout.Form = AdaptiveForm.Desktop;
            UiTest.Settle(layout);
            Assert.Null(layout.GetVisualDescendants().OfType<HintBar>().FirstOrDefault(h => h.IsEffectivelyVisible));
            Assert.NotNull(layout.GetVisualDescendants().OfType<SourceList>().FirstOrDefault(h => h.IsEffectivelyVisible));
            window.Close();
        });

        [Fact]
        public Task The_help_bar_is_generated_from_the_roles_and_the_actions_pad_buttons() => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(AdaptiveForm.BigScreen, 1280, 800);
            List<(string Label, PadGlyphButton? Button)> hints = layout.Hints.Select(h => (h.Label, h.Button)).ToList();
            Assert.Equal(new (string, PadGlyphButton?)[]
            {
                ("Open", PadGlyphButton.South), ("Back", PadGlyphButton.East), ("Section", PadGlyphButton.Shoulders),
                ("Check for Updates", PadGlyphButton.Start), ("Install", PadGlyphButton.North), ("Make Default", PadGlyphButton.Select),
            }, hints);
            HintBar bar = layout.GetVisualDescendants().OfType<HintBar>().Single();
            Assert.Equal(layout.Hints.Select(h => h.Label), bar.Entries!.Select(h => h.Label));
            Button first = layout.HostOf(AdaptiveRole.Actions)!.GetVisualDescendants().OfType<Button>().First();
            Assert.Equal("Check for Updates", first.Content);
            Assert.Contains(AdaptiveLayout.MainClass, first.Classes);
            window.Close();
        });

        [Theory]
        [InlineData(AdaptiveForm.Desktop)]
        [InlineData(AdaptiveForm.BigScreen)]
        public Task The_pad_walks_every_role_in_order_and_back(AdaptiveForm form) => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(form, 1280, 800);
            Assert.True(layout.FocusRole(AdaptiveRole.Navigation));
            var walked = new List<AdaptiveRole?> { layout.FocusedRole };
            for (int i = 0; i < 4; i++)
            {
                layout.MoveFocus(1);
                walked.Add(layout.FocusedRole);
            }
            AdaptiveRole?[] expected = form == AdaptiveForm.Desktop
                ? new AdaptiveRole?[] { AdaptiveRole.Navigation, AdaptiveRole.Content, AdaptiveRole.Detail, AdaptiveRole.Actions, AdaptiveRole.Navigation }
                : new AdaptiveRole?[] { AdaptiveRole.Navigation, AdaptiveRole.Content, AdaptiveRole.Actions, AdaptiveRole.Navigation, AdaptiveRole.Content };
            Assert.Equal(expected, walked);
            layout.MoveFocus(-1);
            Assert.Equal(form == AdaptiveForm.Desktop ? AdaptiveRole.Actions : AdaptiveRole.Navigation, layout.FocusedRole);
            window.Close();
        });

        [Fact]
        public Task F6_moves_between_roles_on_the_desktop() => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(AdaptiveForm.Desktop, 1280, 800);
            layout.FocusRole(AdaptiveRole.Content);
            var focused = (InputElement)TopLevel.GetTopLevel(layout)!.FocusManager!.GetFocusedElement()!;
            focused.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F6, Source = focused });
            Assert.Equal(AdaptiveRole.Detail, layout.FocusedRole);
            focused = (InputElement)TopLevel.GetTopLevel(layout)!.FocusManager!.GetFocusedElement()!;
            focused.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F6, KeyModifiers = KeyModifiers.Shift, Source = focused });
            Assert.Equal(AdaptiveRole.Content, layout.FocusedRole);
            window.Close();
        });

        [Theory]
        [InlineData(1280, 800)]
        [InlineData(1280, 720)]
        public Task Twelve_sections_on_the_big_screen_show_the_chosen_one_whole_and_cut_none(int width, int height) => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(AdaptiveForm.BigScreen, width, height);
            string[] names = { "All games", "Nintendo Entertainment System", "Super Nintendo", "Nintendo 64", "Game Boy Advance", "Mega Drive", "Mega-CD", "32X", "Master System", "PlayStation", "PlayStation 2", "PC Engine" };
            layout.SetSections(names.Select((n, i) => new AdaptiveSection("s" + i, n, "Consoles")));
            UiTest.Settle(layout);
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0) Assert.True(layout.Press(PadGlyphButton.RightShoulder));
                UiTest.Settle(layout);
                Control nav = layout.HostOf(AdaptiveRole.Navigation)!;
                ToggleButton on = nav.GetVisualDescendants().OfType<ToggleButton>().Single(t => t.IsChecked == true);
                Assert.Equal(names[i], on.Content);
                Assert.True(on.Bounds.Width > 0 && new Rect(nav.Bounds.Size).Contains(new Rect(on.TranslatePoint(default, nav)!.Value, on.Bounds.Size)), $"{names[i]} is not drawn whole");
                Assert.Empty(FitAudit.Check(nav, null, smallest: 0));
            }
            window.Close();
        });

        [Fact]
        public Task The_shoulders_step_the_sections_and_raise_the_choice_wrapping_at_the_ends() => UiTest.Run(() =>
        {
            var (window, layout, chosen, _) = Build(AdaptiveForm.BigScreen, 1280, 800);
            Assert.Equal("all", layout.SelectedSection);
            Assert.True(layout.Press(PadGlyphButton.RightShoulder));
            Assert.True(layout.Press(PadGlyphButton.RightShoulder));
            Assert.True(layout.Press(PadGlyphButton.RightShoulder));
            Assert.True(layout.Press(PadGlyphButton.LeftShoulder));
            Assert.Equal(new[] { "ps2", "installed", "all", "installed" }, chosen);
            Assert.True(layout.SelectSection("ps2"));
            Assert.Equal(4, chosen.Count);
            ToggleButton on = layout.HostOf(AdaptiveRole.Navigation)!.GetVisualDescendants().OfType<ToggleButton>().Single(t => t.IsChecked == true);
            Assert.Equal("PlayStation 2", on.Content);
            window.Close();
        });

        [Fact]
        public Task South_opens_the_detail_page_on_the_big_screen_and_east_goes_back_a_level_each_time() => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(AdaptiveForm.BigScreen, 1280, 800);
            layout.FocusRole(AdaptiveRole.Content);
            Assert.True(layout.Press(PadGlyphButton.South));
            Assert.True(layout.IsDetailOpen);
            UiTest.Settle(layout);
            Assert.NotNull(layout.HostOf(AdaptiveRole.Detail));
            Assert.Null(layout.HostOf(AdaptiveRole.Content));
            Assert.Equal(AdaptiveRole.Detail, layout.FocusedRole);
            Assert.True(layout.Press(PadGlyphButton.East));
            Assert.False(layout.IsDetailOpen);
            Assert.Equal(AdaptiveRole.Content, layout.FocusedRole);
            Assert.False(layout.Press(PadGlyphButton.East));
            window.Close();
        });

        [Fact]
        public Task East_on_the_desktop_climbs_detail_to_content_to_navigation_then_is_declined() => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(AdaptiveForm.Desktop, 1280, 800);
            layout.FocusRole(AdaptiveRole.Detail);
            Assert.True(layout.Press(PadGlyphButton.East));
            Assert.Equal(AdaptiveRole.Content, layout.FocusedRole);
            Assert.True(layout.Press(PadGlyphButton.East));
            Assert.Equal(AdaptiveRole.Navigation, layout.FocusedRole);
            Assert.False(layout.Press(PadGlyphButton.East));
            Assert.False(layout.Press(PadGlyphButton.South));
            window.Close();
        });

        [Fact]
        public Task An_actions_own_pad_button_invokes_it_and_a_disabled_one_is_declined() => UiTest.Run(() =>
        {
            var (window, layout, _, invoked) = Build(AdaptiveForm.BigScreen, 1280, 800);
            Assert.True(layout.Press(PadGlyphButton.North));
            Assert.True(layout.Press(PadGlyphButton.Start));
            layout.Actions.First(a => a.Text == "Make Default").IsEnabled = false;
            Assert.False(layout.Press(PadGlyphButton.Select));
            Assert.False(layout.Press(PadGlyphButton.West));
            Assert.Equal(new[] { "Install", "Check for Updates" }, invoked);
            window.Close();
        });

        [Fact]
        public Task Changing_form_keeps_the_section_and_the_focused_role() => UiTest.Run(() =>
        {
            var (window, layout, _, _) = Build(AdaptiveForm.Desktop, 1280, 800);
            layout.SelectSection("installed");
            layout.FocusRole(AdaptiveRole.Content);
            layout.Form = AdaptiveForm.BigScreen;
            UiTest.Settle(layout);
            Assert.Equal("installed", layout.SelectedSection);
            Assert.Equal(AdaptiveRole.Content, layout.FocusedRole);
            layout.Form = AdaptiveForm.Desktop;
            UiTest.Settle(layout);
            Assert.Equal(AdaptiveRole.Content, layout.FocusedRole);
            Assert.NotNull(layout.HostOf(AdaptiveRole.Detail));
            window.Close();
        });

        [Fact]
        public Task A_layout_without_sections_or_detail_shows_neither_and_declines_the_shoulders() => UiTest.Run(() =>
        {
            var layout = new AdaptiveLayout { Content = new ListBox() };
            var window = new Window { Width = 1280, Height = 800, Content = layout };
            window.Show();
            UiTest.Settle(layout);
            Assert.Null(layout.HostOf(AdaptiveRole.Navigation));
            Assert.Null(layout.HostOf(AdaptiveRole.Detail));
            Assert.NotNull(layout.HostOf(AdaptiveRole.Content));
            Assert.False(layout.Press(PadGlyphButton.RightShoulder));
            Assert.Throws<ArgumentException>(() => layout.SetSections(new[] { new AdaptiveSection("a", "A"), new AdaptiveSection("a", "B") }));
            window.Close();
        });

        [Fact]
        public Task An_actions_pad_button_and_main_flag_raise_changed() => UiTest.Run(() =>
        {
            var action = new LunaAction("Install");
            int changed = 0;
            action.Changed += _ => changed++;
            action.PadButton = PadGlyphButton.North;
            action.IsMain = true;
            action.IsMain = true;
            Assert.Equal(2, changed);
        });
    }
}
