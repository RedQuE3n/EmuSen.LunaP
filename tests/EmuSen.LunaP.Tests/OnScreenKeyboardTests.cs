using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // A keyboard steered key by key over a window, typing into a text box - see docs/LunaP.md §91.
    public class OnScreenKeyboardTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(OnScreenKeyboardTests).GetTypeInfo().Assembly);

        private static (ToolWindow Window, TextBox Box) Window(string text = "")
        {
            var box = new TextBox { Text = text, CaretIndex = text.Length };
            var window = new ToolWindow { Width = 900, Height = 600, Content = new StackPanel { Children = { box, new Button { Content = "Other" } } } };
            window.Show();
            box.Focus();
            return (window, box);
        }

        private static void Key(InputElement target, Key key) =>
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

        [Fact]
        public Task It_opens_over_the_window_with_the_focus_and_is_found_there() => Session.Dispatch(() =>
        {
            var (window, box) = Window();
            OnScreenKeyboard keyboard = OnScreenKeyboard.Show(box, new[] { KeyboardLayout.Code }, "A  Type");

            Assert.True(keyboard.IsOpen);
            Assert.Same(keyboard, OnScreenKeyboard.OpenOver(window));
            Assert.Same(keyboard, OnScreenKeyboard.OpenOver(box));
            Assert.Same(keyboard, window.FocusManager!.GetFocusedElement());
            Assert.Equal("1", keyboard.CurrentKey);
            window.Close();
        }, default);

        [Fact]
        public Task A_code_is_typed_by_moving_and_pressing_and_a_row_change_lands_under_the_key_left() => Session.Dispatch(() =>
        {
            var (window, box) = Window();
            OnScreenKeyboard keyboard = OnScreenKeyboard.Show(box, new[] { KeyboardLayout.Code });

            keyboard.Move(6, 0);
            Assert.Equal("7", keyboard.CurrentKey);
            keyboard.Press();
            keyboard.Move(0, 1);
            Assert.Equal("E", keyboard.CurrentKey);
            keyboard.Press();
            Assert.Equal("7E", box.Text);
            Assert.Equal(2, box.CaretIndex);

            // E's middle is two and a half keys right of centre; of the third row's keys, Erase's is nearest.
            keyboard.Move(0, 1);
            Assert.Equal(KeyboardLayout.Erase, keyboard.CurrentKey);
            keyboard.Press();
            Assert.Equal("7", box.Text);

            // Between two keys equally near, the left one; across a row the highlight wraps.
            keyboard.Move(0, -2);
            Assert.Equal("7", keyboard.CurrentKey);
            keyboard.Move(-6, 0);
            Assert.Equal("1", keyboard.CurrentKey);
            keyboard.Move(-1, 0);
            Assert.Equal("8", keyboard.CurrentKey);
            window.Close();
        }, default);

        [Fact]
        public Task Typing_goes_in_at_the_caret_and_erase_at_the_start_does_nothing() => Session.Dispatch(() =>
        {
            var (window, box) = Window("ACDC");
            box.CaretIndex = 1;
            OnScreenKeyboard keyboard = OnScreenKeyboard.Show(box, new[] { KeyboardLayout.Code });

            keyboard.Type("B");
            Assert.Equal("ABCDC", box.Text);
            Assert.True(keyboard.Erase());
            Assert.True(keyboard.Erase());
            Assert.Equal("CDC", box.Text);
            Assert.False(keyboard.Erase());
            window.Close();
        }, default);

        [Fact]
        public Task Shift_capitalises_one_letter_and_Next_changes_the_layout_and_names_the_one_after() => Session.Dispatch(() =>
        {
            var (window, box) = Window();
            OnScreenKeyboard keyboard = OnScreenKeyboard.Show(box, new[] { KeyboardLayout.Letters, KeyboardLayout.Code });

            keyboard.Type(KeyboardLayout.Shift);
            Assert.True(keyboard.Shifted);
            keyboard.Type("m");
            keyboard.Type("a");
            keyboard.Type(KeyboardLayout.Space);
            Assert.Equal("Ma ", box.Text);
            Assert.False(keyboard.Shifted);

            Button next = keyboard.GetVisualDescendants().OfType<Button>().Single(b => (string?)b.Tag == KeyboardLayout.Next);
            Assert.Equal("Code", next.Content);
            keyboard.Type(KeyboardLayout.Next);
            Assert.Same(KeyboardLayout.Code, keyboard.Layout);
            keyboard.NextLayout(1);
            Assert.Same(KeyboardLayout.Letters, keyboard.Layout);
            window.Close();
        }, default);

        [Fact]
        public Task Done_keeps_the_text_and_gives_the_focus_back() => Session.Dispatch(() =>
        {
            var (window, box) = Window("OLD");
            OnScreenKeyboard keyboard = OnScreenKeyboard.Show(box, new[] { KeyboardLayout.Code });
            keyboard.Type("1");
            keyboard.Type(KeyboardLayout.Done);

            Assert.False(keyboard.IsOpen);
            Assert.Null(OnScreenKeyboard.OpenOver(window));
            Assert.True(keyboard.Closed.IsCompleted);
            Assert.True(keyboard.Closed.Result);
            Assert.Equal("OLD1", box.Text);
            Assert.Same(box, window.FocusManager!.GetFocusedElement());
            window.Close();
        }, default);

        [Fact]
        public Task The_arrow_keys_Enter_Backspace_and_Escape_steer_it_and_Escape_puts_the_text_back() => Session.Dispatch(() =>
        {
            var (window, box) = Window("OLD");
            OnScreenKeyboard keyboard = OnScreenKeyboard.Show(box, new[] { KeyboardLayout.Code });

            Key(keyboard, Avalonia.Input.Key.Right);
            Key(keyboard, Avalonia.Input.Key.Enter);
            Assert.Equal("OLD2", box.Text);
            Key(keyboard, Avalonia.Input.Key.Back);
            Key(keyboard, Avalonia.Input.Key.Back);
            Assert.Equal("OL", box.Text);
            Key(keyboard, Avalonia.Input.Key.Escape);

            Assert.False(keyboard.IsOpen);
            Assert.False(keyboard.Closed.Result);
            Assert.Equal("OLD", box.Text);
            window.Close();
        }, default);

        [Fact]
        public Task A_key_clicked_with_a_pointer_types_it() => Session.Dispatch(() =>
        {
            var (window, box) = Window();
            OnScreenKeyboard keyboard = OnScreenKeyboard.Show(box, new[] { KeyboardLayout.GameGenie });

            Button z = keyboard.GetVisualDescendants().OfType<Button>().Single(b => (string?)b.Tag == "Z");
            z.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("Z", box.Text);
            window.Close();
        }, default);

        [Fact]
        public void Every_shipped_layout_has_rows_of_one_width_and_a_way_to_finish()
        {
            foreach (KeyboardLayout layout in new[] { KeyboardLayout.Code, KeyboardLayout.GameGenie, KeyboardLayout.Letters })
            {
                Assert.Single(layout.Rows.Select(r => r.Sum(k => k.Width)).Distinct());
                Assert.Contains(layout.Rows.SelectMany(r => r), k => k.Key == KeyboardLayout.Done);
                Assert.Contains(layout.Rows.SelectMany(r => r), k => k.Key == KeyboardLayout.Erase);
            }

            Assert.Equal(new[] { ("1", 1), ("Space", 4), (":", 1) }, KeyboardLayout.Row("1 Space:4 :"));
            Assert.Throws<System.ArgumentException>(() => new KeyboardLayout("Empty", new[] { KeyboardLayout.Row("") }));
        }
    }
}

namespace EmuSen.LunaP.Tests
{
    // A path typed rather than picked, for a session whose platform picker is out of reach - see docs/LunaP.md §91.4.
    public class PathPickerTypingTests
    {
        private static readonly Avalonia.Headless.HeadlessUnitTestSession Session =
            Avalonia.Headless.HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PathPickerTypingTests).Assembly);

        private static (EmuSen.LunaP.Windowing.ToolWindow, EmuSen.LunaP.Controls.PathPickerRow, Avalonia.Controls.TextBox, Avalonia.Controls.Button) Row(bool editable)
        {
            var row = new EmuSen.LunaP.Controls.PathPickerRow { Path = "/old", IsEditable = editable };
            var other = new Avalonia.Controls.Button { Content = "Other" };
            var window = new EmuSen.LunaP.Windowing.ToolWindow { Width = 600, Height = 200, Content = new Avalonia.Controls.StackPanel { Children = { row, other } } };
            window.Show();
            var box = System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<Avalonia.Controls.TextBox>(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row)));
            return (window, row, box, other);
        }

        [Fact]
        public System.Threading.Tasks.Task A_typed_path_is_picked_on_leaving_the_box_and_on_Enter_and_blank_is_not() => Session.Dispatch(() =>
        {
            var (window, row, box, other) = Row(editable: true);
            var picked = new System.Collections.Generic.List<string>();
            row.PathPicked += picked.Add;
            Assert.False(box.IsReadOnly);

            box.Focus();
            box.Text = " /new ";
            other.Focus();
            Assert.Equal(new[] { "/new" }, picked);
            Assert.Equal("/new", row.Path);

            box.Focus();
            box.Text = "/newer";
            box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter, Source = box });
            Assert.Equal(new[] { "/new", "/newer" }, picked);

            box.Text = "  ";
            other.Focus();
            Assert.Equal(2, picked.Count);
            Assert.Equal("/newer", box.Text);
            window.Close();
        }, default);

        [Fact]
        public System.Threading.Tasks.Task A_row_that_is_not_editable_stays_read_only_and_picks_nothing_typed() => Session.Dispatch(() =>
        {
            var (window, row, box, other) = Row(editable: false);
            int picked = 0;
            row.PathPicked += _ => picked++;
            Assert.True(box.IsReadOnly);

            box.Focus();
            box.Text = "/typed";
            other.Focus();
            Assert.Equal(0, picked);
            Assert.Equal("/old", row.Path);
            window.Close();
        }, default);
    }
}
