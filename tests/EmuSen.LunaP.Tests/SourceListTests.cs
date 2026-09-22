using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // SourceList - see docs/LunaP.md §88.3. Rows keyed by string, headings that are never rows, and
    // Chose raised by a person and not by Fill.
    public class SourceListTests
    {
        private static SourceListGroup[] Sidebar() => new[]
        {
            new SourceListGroup("Library", new[]
            {
                new SourceListItem("all", "All Games", "412"),
                new SourceListItem("fav", "Favourites"),
            }),
            new SourceListGroup("Consoles", new[]
            {
                new SourceListItem("snes", "Super Nintendo", "38"),
                new SourceListItem("n64", "Nintendo 64", "21"),
            }),
        };

        private static ToolWindow Show(Control content)
        {
            var window = new ToolWindow { Width = 260, Height = 400, Content = content };
            window.Show();
            Settle(window);
            return window;
        }

        private static void Settle(Window window)
        {
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
        }

        private static List<Border> Rows(Control list) =>
            list.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("source-row")).ToList();

        private static Border Row(Control list, string text) =>
            Rows(list).Single(r => r.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text));

        private static void Press(Window window, Key key, PhysicalKey physical)
        {
            window.KeyPress(key, RawInputModifiers.None, physical, string.Empty);
            window.KeyRelease(key, RawInputModifiers.None, physical, string.Empty);
            Settle(window);
        }

        [Fact]
        public Task Groups_have_capital_headings_and_rows_are_24_pixels() => UiTest.Run(() =>
        {
            var list = new SourceList();
            list.Fill(Sidebar(), "snes");
            ToolWindow window = Show(list);

            string[] headings = list.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Classes.Contains("source-header")).Select(t => t.Text!).ToArray();
            Assert.Equal(new[] { "LIBRARY", "CONSOLES" }, headings);

            Assert.Equal(4, Rows(list).Count);
            Assert.All(Rows(list), r => Assert.Equal(24, r.Bounds.Height));

            // The badge is there when given and absent when not.
            Assert.Contains(Row(list, "All Games").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "412");
            Assert.DoesNotContain(Row(list, "Favourites").GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("source-badge"));

            window.Close();
        });

        [Fact]
        public Task Fill_selects_by_key_without_raising_chose() => UiTest.Run(() =>
        {
            var list = new SourceList();
            int chose = 0;
            list.Chose += _ => chose++;
            list.Fill(Sidebar(), "n64");
            ToolWindow window = Show(list);

            Assert.Equal("n64", list.SelectedKey);
            Assert.Contains("selected", Row(list, "Nintendo 64").Classes);

            // Refilled with different text under the same key: the selection is the key's.
            list.Fill(new[] { new SourceListGroup("Consoles", new[] { new SourceListItem("n64", "N64", "22") }) }, "n64");
            Settle(window);
            Assert.Contains("selected", Row(list, "N64").Classes);

            // A key nothing has is no selection, not a pending one.
            list.Fill(Sidebar(), "gone");
            Assert.Null(list.SelectedKey);
            Assert.Equal(0, chose);

            window.Close();
        });

        [Fact]
        public Task The_selected_row_is_painted_in_the_accent() => UiTest.Run(() =>
        {
            var list = new SourceList();
            list.Fill(Sidebar(), "fav");
            ToolWindow window = Show(list);

            Application.Current!.TryGetResource("LunaAccent", ThemeVariant.Dark, out object? accent);
            Assert.Equal(((ISolidColorBrush)accent!).Color, ((ISolidColorBrush)Row(list, "Favourites").Background!).Color);
            Assert.NotEqual(((ISolidColorBrush)accent).Color, (Row(list, "All Games").Background as ISolidColorBrush)?.Color);

            window.Close();
        });

        [Fact]
        public Task Arrow_keys_step_over_headings_and_raise_chose() => UiTest.Run(() =>
        {
            var list = new SourceList();
            var chosen = new List<string>();
            list.Chose += key => chosen.Add(key);
            list.Fill(Sidebar(), null);
            ToolWindow window = Show(list);
            list.Focus();

            Press(window, Key.Down, PhysicalKey.ArrowDown);   // nothing selected: the first row
            Press(window, Key.Down, PhysicalKey.ArrowDown);   // fav
            Press(window, Key.Down, PhysicalKey.ArrowDown);   // over CONSOLES to snes
            Press(window, Key.End, PhysicalKey.End);          // n64
            Press(window, Key.Down, PhysicalKey.ArrowDown);   // already last: nothing
            Press(window, Key.Up, PhysicalKey.ArrowUp);       // snes
            Press(window, Key.Home, PhysicalKey.Home);        // all

            Assert.Equal(new[] { "all", "fav", "snes", "n64", "snes", "all" }, chosen);
            window.Close();
        });

        [Fact]
        public Task A_click_chooses_the_row_and_a_heading_chooses_nothing() => UiTest.Run(() =>
        {
            var list = new SourceList();
            var chosen = new List<string>();
            list.Chose += key => chosen.Add(key);
            list.Fill(Sidebar(), "all");
            ToolWindow window = Show(list);

            Border row = Row(list, "Super Nintendo");
            window.MouseDown(row.TranslatePoint(new Point(10, 12), window)!.Value, MouseButton.Left);
            window.MouseUp(row.TranslatePoint(new Point(10, 12), window)!.Value, MouseButton.Left);
            Settle(window);

            TextBlock heading = list.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "CONSOLES");
            window.MouseDown(heading.TranslatePoint(new Point(4, 4), window)!.Value, MouseButton.Left);
            Settle(window);

            Assert.Equal(new[] { "snes" }, chosen);
            Assert.Equal("snes", list.SelectedKey);
            window.Close();
        });

        [Fact]
        public Task It_is_a_list_of_named_groups_of_named_items() => UiTest.Run(() =>
        {
            var list = new SourceList();
            AutomationProperties.SetName(list, "Sidebar");
            list.Fill(Sidebar(), "snes");
            int chose = 0;
            list.Chose += _ => chose++;
            ToolWindow window = Show(list);

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(list);
            Assert.Equal(AutomationControlType.List, peer.GetAutomationControlType());
            Assert.Equal("Sidebar", peer.GetName());

            List<AutomationPeer> all = Descendants(peer).ToList();
            List<AutomationPeer> groups = all.Where(p => p.GetAutomationControlType() == AutomationControlType.Group).ToList();
            Assert.Equal(new[] { "Library", "Consoles" }, groups.Select(g => g.GetName()));

            // The items sit inside their group, and the badge is the status, not part of the name.
            AutomationPeer snes = Descendants(groups[1]).Single(p => p.GetName() == "Super Nintendo" && p.GetAutomationControlType() == AutomationControlType.ListItem);
            Assert.Equal(AutomationControlType.ListItem, snes.GetAutomationControlType());
            Assert.Equal("38", snes.GetItemStatus());

            // The upper-cased heading is hidden, so the group's name is not said twice.
            Assert.DoesNotContain(all, p => p.GetName() == "CONSOLES" && p.IsControlElement());

            Assert.Equal("Super Nintendo", Assert.Single(peer.GetProvider<ISelectionProvider>()!.GetSelection()).GetName());

            Descendants(groups[1]).Single(p => p.GetName() == "Nintendo 64" && p.GetAutomationControlType() == AutomationControlType.ListItem).GetProvider<ISelectionItemProvider>()!.Select();
            Assert.Equal("n64", list.SelectedKey);
            Assert.Equal(1, chose);

            window.Close();
        });

        private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
        {
            foreach (AutomationPeer child in peer.GetChildren())
            {
                yield return child;
                foreach (AutomationPeer deeper in Descendants(child)) yield return deeper;
            }
        }
    }
}
