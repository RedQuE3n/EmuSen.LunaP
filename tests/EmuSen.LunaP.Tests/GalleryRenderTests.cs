using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Gallery;

namespace EmuSen.LunaP.Tests
{
    // One real Skia pass over every control at once - what catches a control that quietly renders as nothing - see docs/LunaP.md §7.
    public class GalleryRenderTests
    {
        [Fact]
        public Task Every_control_in_the_kit_is_realised() => UiTest.Run(() =>
        {
            var window = new GalleryWindow();
            window.Show();

            Assert.True(window.CountParts<SectionHeader>() >= 7);
            Assert.Equal(2, window.CountParts<MonoText>());
            Assert.Equal(4, window.CountParts<MeterRow>());
            Assert.Equal(1, window.CountParts<RgbaImageView>());
            // THREE since §49: two valid and one showing an error, because an error state that is
            // only ever exercised by its own unit test is a state nobody looks at.
            Assert.Equal(3, window.CountParts<FieldRow>());
            Assert.Equal(1, window.FindParts<ErrorText>().Count(t => t.IsVisible));
            Assert.Equal(1, window.CountParts<PathPickerRow>());
            Assert.Equal(1, window.CountParts<ConsolePane>());
            Assert.Equal(1, window.CountParts<StatusBar>());
            Assert.Equal(1, window.CountParts<ButtonBar>());
            Assert.Equal(1, window.CountParts<FilterBar>());
            Assert.Equal(3, window.CountParts<LunaSwitch>());
            Assert.Equal(1, window.CountParts<Tabs>());

            // The shell, which the gallery IS since §26 rather than showing as a row of samples.
            Assert.Equal(1, window.CountParts<MenuBar>());
            Assert.Equal(1, window.CountParts<ToolBar>());
            Assert.Equal(1, window.CountParts<Card>());
            Assert.Equal(1, window.CountParts<SidePanel>());

            // Counted through the non-generic base, which is the same reason it exists at all: a
            // generic type cannot be named in a style selector, and it cannot be named here
            // either without knowing the gallery's own model type.
            Assert.Equal(1, window.CountParts<LunaTable>());

            // The header proves the template applied - the whole risk with a generic control,
            // whose style selector has to be `:is(...)` or it silently matches nothing (§27.2).
            //
            // Rows are asserted as "at least one" rather than as four, and the reason is worth
            // keeping: the table sits near the bottom of a scrolled page, and its ListBox
            // virtualises, so exactly one container is realised here. A count of four passes only
            // when the table happens to be in view, which is a test that breaks when somebody
            // adds a section above it. TableTests asserts all three rows against a table that is
            // actually on screen.
            LunaTable table = window.FindPart<LunaTable>()!;
            Assert.Equal(3, table.FindNamed<Grid>("PART_Header").ColumnDefinitions.Count);
            Assert.True(table.CountParts<ListBoxItem>() >= 1);

            // FOUR, and three of them are the shell's own. AppWindow builds one divider per edge
            // and leaves it in the tree with a null pane where no panel is docked, so toggling a
            // panel never re-parents the central content. The fourth is the one the gallery shows
            // on purpose. A number that changes here means the shell's layout changed shape.
            Assert.Equal(4, window.CountParts<SplitPane>());

            // Templated, not merely present: a wrapper that lost its base style key renders as nothing - see docs/LunaP.md §14.1.
            Assert.NotNull(window.FindPart<Tabs>()!.FindPart<TabItem>());
            Assert.True(window.FindPart<LunaSwitch>()!.GetVisualChildren().Any());
            Assert.NotNull(window.FindPart<MenuBar>()!.FindPart<MenuItem>());
            Assert.NotNull(window.FindPart<ToolBar>()!.FindPart<ActionButton>());
        });

        [Fact]
        public Task The_gallery_renders_more_than_a_flat_image() => UiTest.Run(() =>
        {
            var window = new GalleryWindow();
            window.Show();

            // The image-view ramp alone contributes hundreds; a templating failure collapses this to a handful.
            UiTest.AssertLaidOut(window, "gallery", minColours: 64);
        });

        // The gallery is the kit's own baseline target, so it has to be reproducible in the first place.
        [Fact]
        public Task The_gallery_renders_the_same_way_twice() =>
            UiTest.Run(() => UiTest.AssertStable("gallery", () => new GalleryWindow()));

        // A GUARD THAT FINDS ITS OWN SUBJECTS, in the shape §28 prefers over a paragraph asking the
        // next author to remember.
        //
        // §48 re-skinned nine stock Avalonia controls and FormControlTests swept them, and for the
        // length of that arc the gallery showed exactly one of the nine - a ComboBox, which was
        // already there for another reason. The sweep passed the whole time. That is the §24 shape
        // repeating: a control can be correct and still be invisible, and the thing that noticed
        // last time was a person looking rather than a test.
        //
        // So the two are tied together at the source. FormControlTests.Kinds() is the list of
        // controls this toolkit has decided to support, and every one of them now has to appear in
        // the gallery. Adding a control to the sweep and forgetting the gallery fails here, which is
        // the only moment anybody would find out.
        [Fact]
        public Task Every_control_the_form_sweep_covers_is_in_the_gallery() => UiTest.Run(() =>
        {
            var window = new GalleryWindow();
            window.Show();

            Type[] shown = window.GetSelfAndVisualDescendants().Select(v => v.GetType()).Distinct().ToArray();

            string[] missing = FormControlTests.KindNames
                .Where(name => !shown.Any(t => t.Name == name))
                .ToArray();

            Assert.True(missing.Length == 0,
                "FormControlTests sweeps these controls, but the gallery never shows them, so nobody "
                + "can see whether the seam §48 closed is actually closed: "
                + string.Join(", ", missing)
                + ". Add them to the \"Form controls\" section of GalleryWindow. See docs/LunaP.md §48.6.");
        });
    }
}
