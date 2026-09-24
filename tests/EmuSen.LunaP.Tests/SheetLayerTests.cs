using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // A window's content on a sheet inside its owner, for a session that shows one window at a time - see docs/LunaP.md §90.
    public class SheetLayerTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SheetLayerTests).GetTypeInfo().Assembly);

        private static (ToolWindow Host, SheetLayer Layer, Button Underneath) Host(bool presents = true)
        {
            var underneath = new Button { Name = "Underneath", Content = "Library" };
            var layer = new SheetLayer { PresentsWindows = presents };
            var host = new ToolWindow { Width = 800, Height = 600, Content = new Grid { Children = { underneath, layer } } };
            host.Show();
            underneath.Focus();
            return (host, layer, underneath);
        }

        private static ToolWindow Child(string title, out Button first, out Button second)
        {
            first = new Button { Name = "First", Content = "First" };
            second = new Button { Name = "Second", Content = "Second" };
            return new ToolWindow { Title = title, Width = 400, ClosesOnEscape = true, Content = new StackPanel { Children = { first, second } } };
        }

        private static void Key(InputElement target, Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = target });
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key, KeyModifiers = modifiers, Source = target });
        }

        [Fact]
        public Task A_presented_window_is_drawn_in_its_owner_and_closing_it_gives_everything_back() => Session.Dispatch(() =>
        {
            var (host, layer, underneath) = Host();
            ToolWindow child = Child("Settings", out Button first, out _);

            Task closed = SheetLayer.Show(child, host);

            Assert.False(child.IsVisible);
            Assert.Empty(host.OwnedWindows);
            Assert.True(layer.IsVisible);
            Assert.Same(child, layer.Current);
            Control sheet = layer.SheetOf(child)!;
            Assert.Contains(first, sheet.GetVisualDescendants());
            Assert.Contains(sheet.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Settings");
            Assert.Same(first, host.FocusManager!.GetFocusedElement());

            child.Close();

            Assert.True(closed.IsCompleted);
            Assert.False(layer.IsVisible);
            Assert.Null(layer.Current);
            Assert.Empty(layer.Children);
            Assert.Same(underneath, host.FocusManager!.GetFocusedElement());
            host.Close();
        }, default);

        [Fact]
        public Task A_layer_that_does_not_present_leaves_the_window_a_window() => Session.Dispatch(() =>
        {
            var (host, layer, _) = Host(presents: false);
            ToolWindow child = Child("Settings", out _, out _);

            SheetLayer.Show(child, host);

            Assert.True(child.IsVisible);
            Assert.Contains(child, host.OwnedWindows);
            Assert.False(layer.IsPresenting);
            child.Close();
            host.Close();
        }, default);

        [Fact]
        public Task A_window_owned_by_a_presented_window_goes_on_top_and_the_one_beneath_comes_back_after() => Session.Dispatch(() =>
        {
            var (host, layer, _) = Host();
            ToolWindow settings = Child("Settings", out Button settingsFirst, out _);
            ToolWindow picker = Child("Picker", out Button pickerFirst, out _);

            SheetLayer.Show(settings, host);
            SheetLayer.Show(picker, settings);

            Assert.Same(layer, SheetLayer.PresenterOf(picker));
            Assert.Same(picker, layer.Current);
            Assert.False(layer.SheetOf(settings)!.IsVisible);
            Assert.Same(pickerFirst, host.FocusManager!.GetFocusedElement());

            picker.Close();

            Assert.Same(settings, layer.Current);
            Assert.True(layer.SheetOf(settings)!.IsVisible);
            Assert.Same(settingsFirst, host.FocusManager!.GetFocusedElement());
            settings.Close();
            host.Close();
        }, default);

        [Fact]
        public Task Tab_stays_on_the_sheet_and_Escape_closes_a_window_that_closes_on_Escape() => Session.Dispatch(() =>
        {
            var (host, layer, underneath) = Host();
            ToolWindow child = Child("Settings", out Button first, out Button second);
            SheetLayer.Show(child, host);

            for (int i = 0; i < 5; i++)
            {
                Key((InputElement)host.FocusManager!.GetFocusedElement()!, Avalonia.Input.Key.Tab);
                Assert.Contains(host.FocusManager!.GetFocusedElement(), new object[] { first, second });
            }

            Key(first, Avalonia.Input.Key.Escape);
            Assert.False(layer.IsPresenting);
            host.Close();
        }, default);

        [Fact]
        public Task A_dialog_on_a_sheet_answers_what_it_closed_with() => Session.Dispatch(() =>
        {
            var (host, layer, _) = Host();

            Task<bool> answer = Dialogs.ConfirmAsync(host, "Delete", "Delete it?", "Delete", "Cancel");
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(host.OwnedWindows);
            Control sheet = layer.SheetOf(layer.Current!)!;
            Button accept = sheet.GetVisualDescendants().OfType<Button>().Single(b => (b.Content as string) == "Delete");
            Assert.Same(accept, host.FocusManager!.GetFocusedElement());
            accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.True(answer.IsCompleted);
            Assert.True(answer.Result);
            Assert.False(layer.IsPresenting);
            host.Close();
        }, default);

        [Fact]
        public Task A_dialog_on_a_sheet_dismissed_with_Escape_answers_the_default() => Session.Dispatch(() =>
        {
            var (host, layer, _) = Host();

            Task<string?> answer = Dialogs.PromptAsync(host, "Name", "Name it", "Old");
            Dispatcher.UIThread.RunJobs();
            Key((InputElement)host.FocusManager!.GetFocusedElement()!, Avalonia.Input.Key.Escape);
            Dispatcher.UIThread.RunJobs();

            Assert.True(answer.IsCompleted);
            Assert.Null(answer.Result);
            Assert.False(layer.IsPresenting);
            host.Close();
        }, default);

        [Fact]
        public Task A_window_slot_presents_once_and_brings_the_sheet_forward_the_second_time() => Session.Dispatch(() =>
        {
            var (host, layer, _) = Host();
            var slot = new WindowSlot<ToolWindow>();
            int built = 0;
            ToolWindow Make() { built++; return Child("Cheats", out _, out _); }

            slot.Show(host, Make);
            ToolWindow other = Child("Other", out _, out _);
            SheetLayer.Show(other, host);
            Assert.Same(other, layer.Current);

            slot.Show(host, Make);

            Assert.Equal(1, built);
            Assert.Same(slot.Current, layer.Current);
            slot.Close();
            other.Close();
            Assert.False(layer.IsPresenting);
            host.Close();
        }, default);

        [Fact]
        public Task A_shown_window_cannot_also_be_presented() => Session.Dispatch(() =>
        {
            var (host, layer, _) = Host();
            ToolWindow child = Child("Settings", out _, out _);
            child.Show();

            Assert.Throws<System.InvalidOperationException>(() => { _ = layer.Present(child); });
            child.Close();
            host.Close();
        }, default);

        // A window that docks its buttons at the bottom declares them first; the sheet starts at the top all the same.
        [Fact]
        public Task Focus_starts_at_the_top_control_not_the_first_declared_nor_a_tab_header() => Session.Dispatch(() =>
        {
            var (host, _, _) = Host();
            var close = new Button { Content = "Close" };
            var top = new Button { Content = "Top" };
            DockPanel.SetDock(close, Dock.Bottom);
            var tabs = new TabControl { Items = { new TabItem { Header = "One", Content = top }, new TabItem { Header = "Two", Content = new Button { Content = "Other" } } } };
            var child = new ToolWindow { Title = "Docked", Width = 400, Content = new DockPanel { Children = { close, tabs } } };

            SheetLayer.Show(child, host);

            Assert.Same(top, host.FocusManager!.GetFocusedElement());
            Assert.Equal(0, tabs.SelectedIndex);
            child.Close();
            host.Close();
        }, default);
    }
}
