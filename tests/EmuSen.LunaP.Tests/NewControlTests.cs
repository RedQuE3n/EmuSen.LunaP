using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Fluent;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Tests
{
    // EmptyState, LunaList and the fluent additions - see docs/LunaP.md §22.9.
    //
    // Every control here is asserted through a REAL TEMPLATE PART rather than a property. §5.5
    // records why: asserting on a property alone would have passed for a control whose style key
    // stopped matching, and the failure mode of that is not an exception but a control that renders
    // as nothing at all.
    public class NewControlTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NewControlTests).GetTypeInfo().Assembly);

        private static Task Realised<T>(Func<T> make, Action<T> assert) where T : Control => Session.Dispatch(() =>
        {
            T control = make();
            var window = new Window { Width = 400, Height = 300, Content = control };
            window.Show();
            assert(control);
        }, default);

        private sealed record Peer(string Handle, bool Online);

        [Fact]
        public Task An_empty_state_renders_its_message_through_a_real_part() =>
            Realised(() => new EmptyState { Message = "No ROM loaded." }, empty =>
            {
                TextBlock message = empty.FindNamed<TextBlock>("PART_Message");

                Assert.Equal("No ROM loaded.", message.Text);
                Assert.Equal(LunaPalette.Muted.Color, ((ISolidColorBrush)message.Foreground!).Color);
            });

        // Body-sized, not hint-sized. This is the distinction the control exists for: an empty
        // state is the window's whole content, not an aside under something else.
        [Fact]
        public Task An_empty_state_message_is_not_hint_sized() =>
            Realised(() => new EmptyState { Message = "Nothing here" }, empty =>
            {
                TextBlock message = empty.FindNamed<TextBlock>("PART_Message");

                Assert.NotEqual(LunaPalette.HintFontSize, message.FontSize);
            });

        [Fact]
        public Task An_empty_state_hides_its_detail_line_when_there_is_none() =>
            Realised(() => new EmptyState { Message = "No results" }, empty =>
            {
                Assert.False(empty.FindNamed<TextBlock>("PART_Detail").IsVisible);
            });

        [Fact]
        public Task An_empty_state_shows_a_detail_line_when_given_one() =>
            Realised(() => new EmptyState { Message = "No ROMs", Detail = "Add a folder in Preferences." }, empty =>
            {
                TextBlock detail = empty.FindNamed<TextBlock>("PART_Detail");

                Assert.True(detail.IsVisible);
                Assert.Equal("Add a folder in Preferences.", detail.Text);
            });

        [Fact]
        public Task A_luna_list_hands_back_the_model_not_the_row() =>
            Realised(() => new LunaList<Peer> { Label = p => p.Handle }, list =>
            {
                list.Refresh(new[] { new Peer("ami", true), new Peer("usagi", false) });
                list.SelectedIndex = 1;

                // No shadow array, no index arithmetic, and nothing to parse out of a label.
                Assert.Equal("usagi", list.Selected!.Handle);
                Assert.False(list.Selected.Online);
            });

        // The dance three sites wrote separately: rebuild the list, then go and find the selection
        // again. Here the rows are NEW OBJECTS each refresh, which is what a list rebuilt from a
        // database on every poll actually does - reference identity would lose the selection.
        [Fact]
        public Task A_luna_list_keeps_the_selection_across_a_refresh() =>
            Realised(() => new LunaList<Peer> { Label = p => p.Handle, Key = p => p.Handle }, list =>
            {
                list.Refresh(new[] { new Peer("ami", true), new Peer("usagi", false) });
                list.SelectedIndex = 1;

                list.Refresh(new[] { new Peer("ami", true), new Peer("usagi", true), new Peer("rei", true) });

                Assert.Equal("usagi", list.Selected!.Handle);
                Assert.True(list.Selected.Online);
            });

        // -1 is a real answer, not a failure to restore: the row that was selected is gone, and
        // picking its neighbour would be a guess about what the user meant.
        [Fact]
        public Task A_luna_list_clears_the_selection_when_the_selected_item_disappears() =>
            Realised(() => new LunaList<Peer> { Label = p => p.Handle, Key = p => p.Handle }, list =>
            {
                list.Refresh(new[] { new Peer("ami", true), new Peer("usagi", false) });
                list.SelectedIndex = 1;

                list.Refresh(new[] { new Peer("ami", true) });

                Assert.Null(list.Selected);
                Assert.Equal(-1, list.SelectedIndex);
            });

        // The same distinction Dropdown.Chose draws: a refresh restoring what was already selected
        // is not a user choosing something.
        [Fact]
        public Task A_luna_list_does_not_raise_chose_for_a_restored_selection() =>
            Realised(() => new LunaList<Peer> { Label = p => p.Handle, Key = p => p.Handle }, list =>
            {
                list.Refresh(new[] { new Peer("ami", true), new Peer("usagi", false) });
                list.SelectedIndex = 1;

                int chose = 0;
                list.Chose += _ => chose++;

                list.Refresh(new[] { new Peer("ami", true), new Peer("usagi", true) });

                Assert.Equal(0, chose);
                Assert.Equal("usagi", list.Selected!.Handle);
            });

        // A generic control cannot carry a XAML style selector, so it borrows ListBox's theme by
        // pinning its style key. Without that it has no template and no items - the §5.5 trap.
        [Fact]
        public Task A_luna_list_is_templated_by_borrowing_the_list_box_theme() =>
            Realised(() => new LunaList<Peer> { Label = p => p.Handle }, list =>
            {
                list.Refresh(new[] { new Peer("ami", true), new Peer("usagi", false) });
                list.ApplyTemplate();

                Assert.NotNull(list.FindPart<ItemsPresenter>());
            });

        [Fact]
        public void Rows_assigns_by_position_and_an_explicit_row_still_wins()
        {
            var first = new TextBlock();
            var second = new TextBlock();
            var pinned = new TextBlock();
            Grid.SetRow(pinned, 0);

            Grid grid = Ui.Rows("Auto,Auto,*", first, second, pinned);

            Assert.Equal(0, Grid.GetRow(first));
            Assert.Equal(1, Grid.GetRow(second));

            // Positional assignment is a convenience and never a rule it imposes - §9 settled that
            // for Cols and Rows follows it.
            Assert.Equal(0, Grid.GetRow(pinned));
            Assert.Equal(3, grid.RowDefinitions.Count);
        }

        [Fact]
        public void A_section_can_take_more_than_one_child()
        {
            StackPanel section = Ui.Section("Audio", new TextBlock(), new TextBlock(), new TextBlock());

            // The header plus all three, rather than the header plus one.
            Assert.Equal(4, section.Children.Count);
            Assert.IsType<SectionHeader>(section.Children[0]);
        }

        [Fact]
        public void A_single_child_section_still_takes_its_spacing_overload()
        {
            StackPanel section = Ui.Section("Audio", new TextBlock(), spacing: 4);

            // The params overload must not have stolen the existing signature from callers.
            Assert.Equal(4, section.Spacing);
            Assert.Equal(2, section.Children.Count);
        }
    }
}
