using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Fluent;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Gallery
{
    // Every control in the kit, once, with sample data - the visual reference, and what one render test covers - see docs/LunaP.md §7.
    //
    // AN AppWindow RATHER THAN A Window SINCE §26, and that is the gallery doing its job rather
    // than a convenience. The shell's parts cannot be shown as a row of samples: a menu bar is not
    // a thing you look at next to a meter row, it is the top of a window, and a side panel only
    // means anything when there is something for it to be beside. So the gallery IS a shell, with
    // the control samples as its central content - which also makes it the one place the whole
    // arrangement is exercised in a render pass.
    /// <summary>Every control in the kit, shown once with sample data, as the visual reference.</summary>
    public class GalleryWindow : AppWindow
    {
        public GalleryWindow()
        {
            Title = "LunaP gallery";
            Width = 720;
            Height = 1120;

            var console = new ConsolePane { Prompt = "DianaOS #: ", HistorySource = () => new[] { "help", "coretop" } };
            console.Submitted += line => console.AppendLine("DianaOS #: " + line);

            var swatch = new RgbaImageView { Stretch = Stretch.None };
            swatch.SetFrame(Ramp(256, 32), 256, 32);

            var meters = new MeterList
            {
                Meters = new List<MeterEntry>
                {
                    new("S-CPU", 24, "24.0%"),
                    new("S-PPU", 68, "68.0%"),
                    new("SuperFX", 91, "91.0%"),
                    new("A name long enough to be trimmed by the label column", 5, "5.0%"),
                },
            };

            var filter = new FilterBar { ShowFacet = true, FacetLabel = "Console:", Placeholder = "Search titles" };
            filter.SetFacets(new[] { "All consoles", "NES", "SNES" }, "All consoles");

            // A real typed list, so the gallery shows the thing it actually is: rows built from a
            // model through a projection, with the model coming back on selection.
            var peers = new LunaList<string> { Height = 90 };
            peers.Refresh(new[] { "ami", "usagi", "rei", "makoto" });
            peers.SelectedIndex = 1;

            var tabs = new Tabs();
            tabs.Add("General", Ui.Hint("A tab's content is any control."));
            tabs.Add("NES", Ui.Hint("Appended by Tabs.Add, not declared in XAML."));
            tabs.Add("SNES", Ui.Hint("RemoveFrom(1) drops these again."));

            // Columns over a model, which is the shape the one piece of evidence for this control
            // actually has - a field list with a name, a type and a page number (§27). The other
            // three columns are here to show a feature rather than because the evidence has them,
            // and each says which below.
            //
            // TWO SORTABLE AND ONE NOT, on purpose. A gallery that made every column sortable would
            // show the feature and hide the choice; a heading with no comparison stays a plain
            // label, and seeing the two side by side is the only way to notice that "sortable" is
            // something a caller decides per column rather than something the table does.
            //
            // The page column sorts NUMERICALLY while displaying a string, which is the argument
            // for Sort taking a comparison over the model: sorting the text would put "10" before
            // "9" in a table whose whole job is to be read.
            //
            // A GUTTER, AND THE FIRST COLUMN PINNED - which between them are why this table is wider
            // than the space it is in, deliberately. Freezing is only a thing you can see when
            // something scrolls past the frozen part, and a table of star-width columns fits by
            // definition and never scrolls. So the sample gives up demonstrating star widths in
            // order to demonstrate the gutter, the seam and the pin; §65.2 is that trade, argued.
            //
            // FrozenColumns counts the caller's columns and not the grid's - the gutter is pinned on
            // its own account and takes none of the count (§63.2). One rather than two, because a
            // band has to leave room for the columns it is pinned in front of and the table refuses
            // one that does not (§64.1) - at which point the gallery would silently show nothing.
            //
            // AND IT SELECTS CELLS RATHER THAN ROWS, which the gallery can afford to show precisely
            // because it is not the only selectable thing on the page. A unit is exclusive - a table
            // cannot demonstrate both - so this would normally be the same trade as the star widths
            // above, giving up the default to show the new thing. It is not, because the LunaList of
            // peers further up is a row selection, is selected in the static render, and is the
            // shape almost every list in an application has. §67.6.
            //
            // Multiple, so Shift and Ctrl do something: single-cell selection is the half of this a
            // reader would assume, and a rectangle drawn with Shift+arrow is the half they would not.
            var fields = new LunaTable<Field>
            {
                Key = f => f.Name,
                RowHeader = (_, i) => (i + 1).ToString(),
                RowHeaderCaption = "#",
                FrozenColumns = 1,
                SelectionUnit = LunaSelectionUnit.Cell,
                SelectionMode = LunaSelectionMode.Multiple,
            };
            // THE NAME COLUMN IS EDITABLE AND THE OTHER TWO ARE NOT, which is the same choice the
            // sortable/unsortable pair above makes and for the same reason: a gallery where every
            // column did everything would show the features and hide the fact that each one is a
            // per-column decision. Double-click a name, or select a row and press F2.
            //
            // Validate returns the PROBLEM rather than false, so the message under the table is the
            // caller's sentence - the same shape FieldRow.Error uses, which is what makes an invalid
            // cell and an invalid field one idea instead of two (§50.1).
            //
            // AN ABSOLUTE WIDTH RATHER THAN THE "2*" THIS COLUMN CARRIED UNTIL §65. A frozen
            // column's width IS the band, so a caller who freezes one is declaring how much of the
            // viewport stops scrolling - and a star column in a table that overflows resolves to its
            // content anyway, which would make the band whatever the longest name happened to be.
            fields.Column(new LunaColumn<Field>("name", f => f.Name)
                  {
                      Width = "200",
                      Sort = (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture),
                      Commit = (f, text) => f.Name = text.Trim(),
                      Validate = (_, text) => string.IsNullOrWhiteSpace(text) ? "A field needs a name." : null,
                  })
                  .Column("type", f => f.Type, "120")

                  // The column that pushes the table past its own width, and it is a real one rather
                  // than filler: a field list that says what a field IS and not what it starts as is
                  // half a schema.
                  .Column("default", f => f.Default, "150")
                  // RIGHT-ALIGNED, which is the case per-column alignment exists for and the same
                  // argument the gutter already carries (§58.5): numbers read down a column by their
                  // last digit, and a left-aligned run of 9, 10, 11 puts the units under the tens.
                  // Beside four left-aligned columns it also shows that alignment is a per-column
                  // decision rather than something the table does.
                  .Column(new LunaColumn<Field>("pg", f => f.Page.ToString())
                  {
                      Width = "40",
                      Alignment = HorizontalAlignment.Right,
                      Sort = (a, b) => a.Page.CompareTo(b.Page),
                  })

                  // THE OTHER TWO CELL KINDS, because a table that can only be shown drawing text is
                  // a table whose §57 is a paragraph nobody can see. A check column is the commonest
                  // non-text column there is, and this one is live: tick it and the model changes.
                  .Column(new LunaColumn<Field>("req", f => f.Required, (f, on) => f.Required = on)
                  {
                      Width = "40",
                  })

                  // AND THE ESCAPE HATCH, DELIBERATELY SHOWN AS SOMETHING UNREADABLE. A coloured dot
                  // is exactly the cell a screen reader cannot describe, which is why the template
                  // form REQUIRES its third argument - the sentence a reader hears in place of the
                  // shape. Seeing the dot beside "kind: text" in the row's spoken name is the whole
                  // argument for that being required rather than optional (§24, §57.2).
                  .Column(new LunaColumn<Field>(
                      "kind",
                      // NO HorizontalAlignment SINCE §69.2, and its absence is the point. A template
                      // cell now starts at its column's left edge like every other kind of cell
                      // unless the caller says otherwise, so the line that used to be here is the
                      // line a consumer no longer has to know to write.
                      f => new Ellipse
                      {
                          Width = 8,
                          Height = 8,
                          Fill = f.Type == "checkbox" ? LunaPalette.Info : LunaPalette.Nominal,
                          VerticalAlignment = VerticalAlignment.Center,
                      },
                      f => f.Type)
                  {
                      Width = "30",
                  });

            var schema = new List<Field>
            {
                new Field("Site", "text", 1, required: true, @default: "(from job)"),
                new Field("Technician", "text", 1, required: true, @default: "(current user)"),
                new Field("Approved", "checkbox", 2, required: false, @default: "no"),
                new Field("Total aid retained", "text", 2, required: false, @default: "0.00"),
            };

            fields.Refresh(schema);

            // ROWS THE USER CAN REORDER, AND THE HANDLER A CONSUMER ACTUALLY WRITES. The table
            // reports where the drop landed and changes nothing itself (§71.1), so this is not
            // ceremony the gallery is adding on top - it is the whole of what the feature asks of a
            // caller, and showing it with the collection missing would show half an idea.
            //
            // A field list is the right sample for it: the order of fields on a form is a decision
            // somebody makes by looking at it, which is exactly when dragging beats retyping.
            fields.CanReorderRows = true;
            fields.RowDropped += drop =>
            {
                foreach (Field moved in drop.Rows) schema.Remove(moved);

                int at = drop.Target is null ? schema.Count : schema.IndexOf(drop.Target);
                if (drop.Position == LunaDropPosition.After) at++;

                schema.InsertRange(Math.Clamp(at, 0, schema.Count), drop.Rows);
                fields.Refresh(schema);
            };

            // THE ONLY SAMPLES HERE THIS TOOLKIT DID NOT WRITE, and that is the reason they are
            // here. §48 handed LunaP's colours to FluentTheme's own resource keys so that a stock
            // TextBox, CheckBox or Slider paints in this palette instead of Fluent's #0078D7 - and
            // until this section existed, the only evidence of that was a test.
            //
            // A gallery that shows nine LunaP controls and none of the controls an application is
            // actually mostly made of is a gallery that cannot answer the question §48 was built to
            // answer: does the join show? Put them on the same page as the rest and the answer is
            // one look rather than an argument.
            //
            // A RadioButton PAIR rather than one, because a single radio button shows the fill and
            // hides the thing the fill is for. Both states of the CheckBox and the ToggleSwitch for
            // the same reason: LunaAccent and LunaOnAccent are a pairing (§48.3), and a sample that
            // only ever shows the checked half never shows the pairing failing.
            var forms = Ui.Stack(10,
                new TextBox { Text = "smw.sfc", Width = 220, HorizontalAlignment = HorizontalAlignment.Left },
                // PlaceholderText and not Watermark: the latter is [Obsolete] in Avalonia 12.1.0 and
                // the build says so. The placeholder is here because it is the one piece of a
                // TextBox that reads through its own key - TextControlPlaceholderForeground, which
                // the bridge maps to LunaMuted - so an unstyled placeholder is a visible seam.
                new TextBox { PlaceholderText = "Search titles", Width = 220, HorizontalAlignment = HorizontalAlignment.Left },
                Ui.Row(16,
                    new CheckBox { Content = "Pause when unfocused", IsChecked = true },
                    new CheckBox { Content = "Confirm on exit" }),
                Ui.Row(16,
                    new RadioButton { Content = "Nearest", GroupName = "scale", IsChecked = true },
                    new RadioButton { Content = "Linear", GroupName = "scale" }),
                Ui.Row(16,
                    new ToggleSwitch { IsChecked = true },
                    new ToggleSwitch()),
                new Slider { Minimum = 0, Maximum = 100, Value = 40, Width = 220, HorizontalAlignment = HorizontalAlignment.Left },
                new ProgressBar { Minimum = 0, Maximum = 100, Value = 60, Width = 220, HorizontalAlignment = HorizontalAlignment.Left },
                Ui.Row(16,
                    new NumericUpDown { Value = 3, Width = 120 },
                    new CalendarDatePicker { SelectedDate = new DateTime(2026, 8, 13) }));

            // A splitter with something on each side of it, sized so the divider is visibly not
            // in the middle - a proportional splitter would put it there and the fixed/elastic
            // arrangement §26.6 chose would be invisible in the picture.
            var split = new SplitPane
            {
                Height = 96,
                FixedSize = 150,
                MinFirst = 60,
                MinSecond = 60,
                First = Ui.Hint("Fixed: 150pt, and stays 150pt when the window is widened."),
                Second = Ui.Hint("Elastic: takes whatever is left."),
            };

            Central = Ui.Scroll(Ui.Stack(10,
                Ui.Section("Text", Ui.Stack(6,
                    Ui.Mono("PC=0x008123  A=0x0000  X=0x01FF"),
                    Ui.Hint("Grey, 11pt, wrapping - the explanatory line under a label or a checkbox."))),

                Ui.Section("Meters", meters),

                Ui.Section("Image view", swatch),

                Ui.Section("Settings fields", Ui.Stack(10,
                    new FieldRow
                    {
                        Label = "ROM Directory",
                        Hint = "Default folder for Open ROM... and the ROM list.",
                        Content = new PathPickerRow { Placeholder = "(not set)", BrowseTitle = "Choose ROM Directory" },
                    },
                    new FieldRow
                    {
                        Label = "Emulator Core",
                        Content = new ComboBox { ItemsSource = new[] { "SNES", "NES" }, SelectedIndex = 0 }.Grow(),
                    },

                    // AN INVALID FIELD, SHOWN INVALID, because §49's error state is the one thing in
                    // this kit whose whole job is to appear only when something is wrong - and a
                    // gallery that shows every control in its happy state never shows it at all.
                    //
                    // Hint AND Error together on purpose: the two are different sentences that both
                    // stay on screen, which is the argument for ItemStatus over HelpText written up
                    // beside FieldRow's peer. Seeing them stacked is the only way to notice that the
                    // advice survives the failure.
                    new FieldRow
                    {
                        Label = "Save State Folder",
                        Hint = "Where save states are written.",
                        Error = "That folder does not exist.",
                        Content = new TextBox { Text = "/mnt/roms/states" },
                    })),

                Ui.Section("Form controls", forms),

                Ui.Section("Widgets", Ui.Stack(8,
                    filter,
                    Ui.Row(16,
                        new LunaSwitch { Label = "Enable Logging", IsChecked = true },
                        new LunaSwitch { Label = "Concurrent GC" }),
                    tabs.Height(90))),

                Ui.Section("Lists and empty states", Ui.Stack(8,
                    peers,
                    new EmptyState
                    {
                        Message = "No ROMs in the library.",
                        Detail = "Add a folder in Preferences to see them here.",
                    })),

                Ui.Section("Console", console.Height(160)),

                Ui.Section("Cards", new Card
                {
                    Header = "Emulation",
                    Content = Ui.Stack(6,
                        Ui.Hint("A titled surface, on LunaP's own key rather than FluentTheme's."),
                        new LunaSwitch { Label = "Pause when unfocused", IsChecked = true }),
                }),

                Ui.Section("Table", fields.Height(150)),

                Ui.Section("Split pane", split)).Margin(12));

            // The shell's own status line, which is the arrangement five windows in one
            // application laid out by hand: a message on the left, a run of buttons on the right
            // (§21.2). It is one control and always has been - what was missing was a window that
            // put it where it goes.
            Status = "Ready.";
            StatusContent = Ui.Buttons(
                Ui.Button("Apply", () => { }),
                Ui.Button("Close", Close));

            BuildShell();

            console.AppendLine("DianaOS #: help");
            console.AppendLine("Type a command. This pane knows nothing about DianaOS.");
        }

        // The menu bar, the toolbar and a docked panel, all built from the same actions - which is
        // the point of §26 and cannot be shown by putting three controls next to each other.
        private void BuildShell()
        {
            // One action, three surfaces: the File menu, the toolbar, and Ctrl+O. Changing its
            // enabled state changes all three, which is the thing four hand-written declarations
            // could never quite manage.
            var open = new LunaAction("Open ROM...", () => Status = "Open chosen.")
            {
                Shortcut = KeyGesture.Parse("Ctrl+O"),
                HelpText = "Chooses a ROM to load.",
            };

            var save = new LunaAction("Save State", () => Status = "State saved.")
            {
                Shortcut = KeyGesture.Parse("Ctrl+S"),
            };

            // Disabled from the start, to show that a greyed menu entry, a greyed toolbar button
            // and a shortcut that does nothing are one fact rather than three.
            var strip = new LunaAction("Remove Fields", () => Status = "Fields removed.")
            {
                IsEnabled = false,
                HelpText = "Nothing is loaded, so there is nothing to remove.",
            };

            var grid = new LunaAction("Grid", self => Status = self.IsChecked ? "Grid on." : "Grid off.")
            {
                IsCheckable = true,
                Shortcut = KeyGesture.Parse("Ctrl+G"),
            };

            // A radio set, which is what an ActionGroup is for: exactly one of these is ticked at
            // any moment and the group does the unticking.
            var variants = new ActionGroup();
            LunaAction dark = variants.Add("Dark");
            LunaAction light = variants.Add("Light");
            dark.IsChecked = true;

            var explorer = new SidePanel
            {
                Title = "Explorer",
                Side = PanelSide.Left,
                PanelSize = 180,
                Content = Ui.Stack(6,
                    Ui.Hint("Docked to an edge, closable, and remembered when it has a key."),
                    Ui.Mono("smw.sfc\nzelda.sfc\nmetroid.sfc")),
            };

            AddPanel(explorer);

            SetMenus(
                new LunaMenu("File", open, save, LunaAction.Separator(), strip),
                new LunaMenu("View", grid, LunaAction.Separator(), explorer.ToggleAction,
                    new LunaAction("Theme") { Submenu = new LunaMenu("Theme", dark, light) }),
                new LunaMenu("Help", new LunaAction("About LunaP", () => Status = "A small Avalonia toolkit.")));

            SetToolBar(open, save, LunaAction.Separator(), grid, strip);
        }

        // A model for the table to project, so the gallery shows the control doing the thing it is
        // for: rows built from a type through three projections, with the type coming back on
        // selection rather than a row index.
        // A CLASS AND NO LONGER A RECORD, because §50 gave the table editing and Commit writes back
        // into the model. A positional record's properties are init-only, so there is nothing for a
        // Commit to assign - which is a fact about editing worth meeting here, in the gallery, rather
        // than in a consumer's own code.
        private sealed class Field
        {
            public Field(string name, string type, int page, bool required, string @default)
            {
                Name = name;
                Type = type;
                Page = page;
                Required = required;
                Default = @default;
            }

            public string Name { get; set; }
            public string Type { get; set; }
            public int Page { get; set; }
            public string Default { get; set; }

            // Written by the check column's toggle, which is the point of it being here: the gallery
            // table is live, and a tick changes a model rather than a picture.
            public bool Required { get; set; }
        }

        // Real pixels, so the image view is not just showing a flat rectangle.
        private static byte[] Ramp(int width, int height)
        {
            var rgba = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = ((y * width) + x) * 4;
                    rgba[i] = (byte)x;
                    rgba[i + 1] = (byte)(y * 8);
                    rgba[i + 2] = (byte)(255 - x);
                    rgba[i + 3] = 255;
                }
            }

            return rgba;
        }
    }
}
