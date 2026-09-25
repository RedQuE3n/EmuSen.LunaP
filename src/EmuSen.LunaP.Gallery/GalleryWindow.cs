using System;
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

            var console = new ConsolePane { Prompt = "photos> ", HistorySource = () => new[] { "help", "scan" } };
            console.Submitted += line => console.AppendLine("photos> " + line);

            var swatch = new RgbaImageView { Stretch = Stretch.None };
            swatch.SetFrame(Ramp(256, 32), 256, 32);

            var meters = new MeterList
            {
                Meters = new List<MeterEntry>
                {
                    new("Import", 24, "24.0%"),
                    new("Thumbnails", 68, "68.0%"),
                    new("Index", 91, "91.0%"),
                    new("A name long enough to be trimmed by the label column", 5, "5.0%"),
                },
            };

            var filter = new FilterBar { ShowFacet = true, FacetLabel = "Camera:", Placeholder = "Search photos" };
            filter.SetFacets(new[] { "All cameras", "Kodak", "Nikon" }, "All cameras");

            // A real typed list, so the gallery shows the thing it actually is: rows built from a
            // model through a projection, with the model coming back on selection.
            var albums = new LunaList<string> { Height = 90 };
            albums.Refresh(new[] { "Iceland", "Studio", "Family", "Scans" });
            albums.SelectedIndex = 1;

            var tabs = new Tabs();
            tabs.Add("General", Ui.Hint("A tab's content is any control."));
            tabs.Add("RAW", Ui.Hint("Appended by Tabs.Add, not declared in XAML."));
            tabs.Add("JPEG", Ui.Hint("RemoveFrom(1) drops these again."));

            // Columns over a model, which is the shape the one piece of evidence for this control
            // actually has - a name, a type and a number (§27). The other three columns are here to
            // show a feature rather than because the evidence has them, and each says which below.
            //
            // THE SHAPE IS THAT EVIDENCE'S AND THE CONTENT DELIBERATELY IS NOT. §27 found the one
            // columnar view in any repository, in a form editor, and until §86.14 this demo WAS
            // that view - a `Field` of name, type, page, required and default, carrying that
            // application's own sample rows. Naming the evidence in the RECORD is how a reader
            // learns why this control exists; reproducing it here taught a reader that the toolkit
            // is for editing forms, which is one consumer's business and not this kit's. The rows
            // are not quoted in this comment either, for the same reason the model no longer holds
            // them; §86.14 has them, because that is the record.
            //
            // A PHOTO AND NOT AN INGREDIENT, WHICH §86.15 IS THE SECOND HALF OF. §86.14 replaced
            // the form editor with a recipe and left the rest of this file describing an emulator,
            // which is the leak that pass actually declined to fix. Everything on this page is one
            // invented photo library now, so a reader is not asked to believe that a toolkit is for
            // forms, or for emulators, or for baking.
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
            // albums further up is a row selection, is selected in the static render, and is the
            // shape almost every list in an application has. §67.6.
            //
            // Multiple, so Shift and Ctrl do something: single-cell selection is the half of this a
            // reader would assume, and a rectangle drawn with Shift+arrow is the half they would not.
            var photos = new LunaTable<Photo>
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
            photos.Column(new LunaColumn<Photo>("name", f => f.Name)
                  {
                      Width = "200",
                      Sort = (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture),
                      Commit = (f, text) => f.Name = text.Trim(),
                      Validate = (_, text) => string.IsNullOrWhiteSpace(text) ? "A photo needs a name." : null,
                  })
                  .Column("kind", f => f.Kind, "120")

                  // The column that pushes the table past its own width, and it is a real one rather
                  // than filler: a list of photographs that says what each file IS and not what is
                  // wrong with it is half a cull.
                  .Column("note", f => f.Note, "150")
                  // RIGHT-ALIGNED, which is the case per-column alignment exists for and the same
                  // argument the gutter already carries (§58.5): numbers read down a column by their
                  // last digit, and a left-aligned run of 9, 10, 11 puts the units under the tens.
                  // Beside four left-aligned columns it also shows that alignment is a per-column
                  // decision rather than something the table does.
                  .Column(new LunaColumn<Photo>("rate", f => f.Rating.ToString())
                  {
                      Width = "40",
                      Alignment = HorizontalAlignment.Right,
                      Sort = (a, b) => a.Rating.CompareTo(b.Rating),
                  })

                  // THE OTHER TWO CELL KINDS, because a table that can only be shown drawing text is
                  // a table whose §57 is a paragraph nobody can see. A check column is the commonest
                  // non-text column there is, and this one is live: tick it and the model changes.
                  .Column(new LunaColumn<Photo>("keep", f => f.Keep, (f, on) => f.Keep = on)
                  {
                      Width = "40",
                  })

                  // AND THE ESCAPE HATCH, DELIBERATELY SHOWN AS SOMETHING UNREADABLE. A coloured dot
                  // is exactly the cell a screen reader cannot describe, which is why the template
                  // form REQUIRES its third argument - the sentence a reader hears in place of the
                  // shape. Seeing the dot beside "kind: text" in the row's spoken name is the whole
                  // argument for that being required rather than optional (§24, §57.2).
                  .Column(new LunaColumn<Photo>(
                      "fmt",
                      // NO HorizontalAlignment SINCE §69.2, and its absence is the point. A template
                      // cell now starts at its column's left edge like every other kind of cell
                      // unless the caller says otherwise, so the line that used to be here is the
                      // line a consumer no longer has to know to write.
                      f => new Ellipse
                      {
                          Width = 8,
                          Height = 8,
                          Fill = f.Kind == "raw" ? LunaPalette.Info : LunaPalette.Nominal,
                          VerticalAlignment = VerticalAlignment.Center,
                      },
                      f => f.Kind)
                  {
                      Width = "30",
                  });

            var album = new List<Photo>
            {
                new Photo("IMG_4021.CR2", "raw", 4, keep: true, note: "underexposed, recoverable"),
                new Photo("IMG_4022.JPG", "jpeg", 2, keep: false, note: "out of focus"),
                new Photo("IMG_4023.CR2", "raw", 5, keep: true, note: "cover candidate"),
                new Photo("IMG_4024.JPG", "jpeg", 3, keep: false, note: "dust on the sensor"),
            };

            photos.Refresh(album);

            // ROWS THE USER CAN REORDER, AND THE HANDLER A CONSUMER ACTUALLY WRITES. The table
            // reports where the drop landed and changes nothing itself (§71.1), so this is not
            // ceremony the gallery is adding on top - it is the whole of what the feature asks of a
            // caller, and showing it with the collection missing would show half an idea.
            //
            // An album is the right sample for it: the order photographs sit in one is a decision
            // somebody makes by LOOKING at them rather than by knowing anything, which is exactly
            // the case where dragging beats retyping a number in a column.
            photos.CanReorderRows = true;
            photos.RowDropped += drop =>
            {
                foreach (Photo moved in drop.Rows) album.Remove(moved);

                int at = drop.Target is null ? album.Count : album.IndexOf(drop.Target);
                if (drop.Position == LunaDropPosition.After) at++;

                album.InsertRange(Math.Clamp(at, 0, album.Count), drop.Rows);
                photos.Refresh(album);
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
                new TextBox { Text = "IMG_4021.CR2", Width = 220, HorizontalAlignment = HorizontalAlignment.Left },
                // PlaceholderText and not Watermark: the latter is [Obsolete] in Avalonia 12.1.0 and
                // the build says so. The placeholder is here because it is the one piece of a
                // TextBox that reads through its own key - TextControlPlaceholderForeground, which
                // the bridge maps to LunaMuted - so an unstyled placeholder is a visible seam.
                new TextBox { PlaceholderText = "Search photos", Width = 220, HorizontalAlignment = HorizontalAlignment.Left },
                Ui.Row(16,
                    new CheckBox { Content = "Auto-rotate on import", IsChecked = true },
                    new CheckBox { Content = "Confirm on delete" }),
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

            // A library window and the thing it opens, §88's four controls together because they
            // only make sense together: a sidebar choosing what the grid shows, and over a "viewer"
            // the bar that appears when the pointer moves and the notice a button puts up.
            //
            // THE GRID HAS A HEIGHT, and it has to. This page is one long vertical stack in a scroll
            // viewer, so anything in it is offered unbounded height - and a TileGrid offered that
            // realises every tile, which is the one configuration §88.2 records as defeating it.
            // A long list under headings, and one adjustable number with its default - §94.
            var lenses = new GroupedList<string[]> { Group = l => l[0], Label = l => l[1], Detail = l => l[2], Badge = l => l[1] == "35mm f/1.4" ? "Mounted" : null, Key = l => l[1] };
            lenses.Refresh(new[]
            {
                new[] { "Primes", "35mm f/1.4", "Wide normal, weather sealed" },
                new[] { "Primes", "85mm f/1.8", "Portrait" },
                new[] { "Zooms", "24-70mm f/2.8", "Standard zoom" },
                new[] { "Zooms", "70-200mm f/4", "Telephoto zoom with a long name that wraps onto a second line" },
            });
            lenses.Select(new[] { "Primes", "35mm f/1.4", "" });
            Avalonia.Automation.AutomationProperties.SetName(lenses, "Lenses");
            var exposure = new SliderRow { Label = "Exposure compensation", Minimum = -3, Maximum = 3, Step = 0.3, DefaultValue = 0, Value = 0.6 };
            // A long column of them under headings, only the rows in view built - §97.
            var grading = new SliderList
            {
                ItemsSource = new object[]
                {
                    "Tone",
                    new SliderItem("Highlights", -100, 100, 1, 0, -20),
                    new SliderItem("Shadows", -100, 100, 1, 0, 35),
                    "Colour",
                    new SliderItem("Temperature", 2000, 10000, 50, 5500, 5500),
                    new SliderItem("Saturation", -100, 100, 1, 0, 0),
                },
            };
            Avalonia.Automation.AutomationProperties.SetName(grading, "Grading");

            var albumList = new SourceList { Width = 180 };
            albumList.Fill(new[]
            {
                new SourceListGroup("Library", new[]
                {
                    new SourceListItem("all", "All Photos", "40"),
                    new SourceListItem("starred", "Starred", "6"),
                }),
                new SourceListGroup("Albums", new[]
                {
                    new SourceListItem("iceland", "Iceland", "24"),
                    new SourceListItem("studio", "Studio", "16"),
                }),
            }, "all");
            Avalonia.Automation.AutomationProperties.SetName(albumList, "Albums");

            var thumbnails = new TileGrid<string> { TileWidth = 96, TileHeight = 72, Spacing = 12 };
            var shots = new List<string>();
            for (int i = 0; i < 40; i++) shots.Add($"IMG_{4000 + i}.JPG");
            thumbnails.Refresh(shots);
            thumbnails.Select("IMG_4002.JPG");
            Avalonia.Automation.AutomationProperties.SetName(thumbnails, "Photos");
            thumbnails.Activated += shot => Status = $"Opened {shot}.";

            var library = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Height = 220 };
            library.Children.Add(albumList);
            Grid.SetColumn(thumbnails, 1);
            library.Children.Add(thumbnails);

            var viewer = new Border { Background = LunaPalette.Void, Height = 150 };
            var notice = new NoticeLayer();
            var hud = new OverlayBar
            {
                Watch = viewer,
                Content = Ui.Row(8,
                    Ui.Button("Previous", () => { }),
                    Ui.Button("Pause", () => notice.Show("Slideshow paused")),
                    Ui.Button("Next", () => { }),
                    new Slider { Minimum = 0, Maximum = 100, Value = 30, Width = 120 }),
            };
            // §95: the viewer's filmstrip, the same photos in one row, its height given for §88.2's reason turned on its side.
            var filmstrip = new TileStrip<string> { TileWidth = 96, TileHeight = 72, Spacing = 12, Height = 100 };
            filmstrip.Refresh(shots);
            filmstrip.Select("IMG_4002.JPG");
            Avalonia.Automation.AutomationProperties.SetName(filmstrip, "Filmstrip");
            filmstrip.Activated += shot => Status = $"Opened {shot}.";

            var stage = new Grid();
            stage.Children.Add(viewer);
            stage.Children.Add(hud);
            stage.Children.Add(notice);

            // SAID ON THE PAGE AND NOT ONLY IN A COMMENT, because the reader this line is for is
            // looking at the window rather than at this file. Until §86.15 every sample here was an
            // emulator's, and the leak was found by somebody RUNNING the gallery and recognising a
            // consumer's own config tabs in it - not by reading the source, and not by any of the
            // 1,043 tests. A demo that reads like one application teaches that the kit is for that
            // application; saying otherwise out loud is the cheapest guard available, and it is the
            // only one that reaches the person the gallery exists for.
            Central = Ui.Scroll(Ui.Stack(10,
                Ui.Hint("Every sample on this page is invented. This toolkit knows nothing about "
                        + "photographs, and nothing about whatever you are building either - §86.15."),

                Ui.Section("Text", Ui.Stack(6,
                    Ui.Mono("f/2.8   1/250s   ISO 400   35mm"),
                    Ui.Hint("Grey, 11pt, wrapping - the explanatory line under a label or a checkbox."))),

                Ui.Section("Meters", meters),

                Ui.Section("Image view", swatch),

                Ui.Section("Settings fields", Ui.Stack(10,
                    new FieldRow
                    {
                        Label = "Library Folder",
                        Hint = "Default folder for Import... and the photo list.",
                        Content = new PathPickerRow { Placeholder = "(not set)", BrowseTitle = "Choose Library Folder" },
                    },
                    new FieldRow
                    {
                        Label = "Raw Decoder",
                        Content = new ComboBox { ItemsSource = new[] { "LibRaw", "dcraw" }, SelectedIndex = 0 }.Grow(),
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
                        Label = "Export Folder",
                        Hint = "Where exported files are written.",
                        Error = "That folder does not exist.",
                        Content = new TextBox { Text = "/mnt/photos/export" },
                    })),

                Ui.Section("Form controls", forms),

                Ui.Section("Widgets", Ui.Stack(8,
                    filter,
                    Ui.Row(16,
                        new LunaSwitch { Label = "Enable Logging", IsChecked = true },
                        new LunaSwitch { Label = "Concurrent GC" }),
                    tabs.Height(90))),

                Ui.Section("Lists and empty states", Ui.Stack(8,
                    albums,
                    lenses.Height(180),
                    exposure,
                    grading.Height(180),
                    new EmptyState
                    {
                        Message = "No photos in the library.",
                        Detail = "Add a folder in Preferences to see them here.",
                    })),

                Ui.Section("Console", console.Height(160)),

                Ui.Section("Cards", new Card
                {
                    Header = "Library",
                    Content = Ui.Stack(6,
                        Ui.Hint("A titled surface, on LunaP's own key rather than FluentTheme's."),
                        new LunaSwitch { Label = "Watch the folder for new files", IsChecked = true }),
                }),

                Ui.Section("Table", photos.Height(150)),

                Ui.Section("Library and viewer", Ui.Stack(8,
                    library,
                    Ui.Hint("Move the pointer over the picture for its bar; Pause puts up a notice."),
                    stage,
                    filmstrip)),

                Ui.Section("Split pane", split),

                // Drawn controls on a positioned canvas: an image, a list, a carousel, text and the indicators - §98 to §101.
                Ui.Section("Themed surface", DrawnSamples.Build())).Margin(12));

            // The shell's own status line, which is the arrangement five windows in one
            // application laid out by hand: a message on the left, a run of buttons on the right
            // (§21.2). It is one control and always has been - what was missing was a window that
            // put it where it goes.
            Status = "Ready.";
            StatusContent = Ui.Buttons(
                Ui.Button("Apply", () => { }),
                Ui.Button("Close", Close));

            BuildShell();

            console.AppendLine("photos> help");
            console.AppendLine("Type a command. This pane knows nothing about what the command means.");
        }

        // The menu bar, the toolbar and a docked panel, all built from the same actions - which is
        // the point of §26 and cannot be shown by putting three controls next to each other.
        private void BuildShell()
        {
            // One action, three surfaces: the File menu, the toolbar, and Ctrl+O. Changing its
            // enabled state changes all three, which is the thing four hand-written declarations
            // could never quite manage.
            var open = new LunaAction("Import...", () => Status = "Import chosen.")
            {
                Shortcut = KeyGesture.Parse("Ctrl+O"),
                HelpText = "Chooses a folder to import from.",
            };

            var save = new LunaAction("Export", () => Status = "Exported.")
            {
                Shortcut = KeyGesture.Parse("Ctrl+S"),
            };

            // Disabled from the start, to show that a greyed menu entry, a greyed toolbar button
            // and a shortcut that does nothing are one fact rather than three.
            var revert = new LunaAction("Revert", () => Status = "Reverted.")
            {
                IsEnabled = false,
                HelpText = "Nothing has changed, so there is nothing to revert.",
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

            // FOLLOWS THE WINDOW RATHER THAN KEEPING ITS OWN ANSWER (§26.3, §75.2). A checkable
            // action flips its own tick when invoked, which would be right if this item were the
            // only way in - it is not, so the window's own event is what drives the tick and the
            // handler only asks the window to toggle.
            var full = new LunaAction("Full Screen", ToggleFullScreen)
            {
                IsCheckable = true,
                Shortcut = KeyGesture.Parse("F11"),
                HelpText = "Fills the screen, and comes back to the state it left.",
            };

            FullScreenChanged += on => full.IsChecked = on;

            var explorer = new SidePanel
            {
                Title = "Explorer",
                Side = PanelSide.Left,
                PanelSize = 180,
                Content = Ui.Stack(6,
                    Ui.Hint("Docked to an edge, closable, and remembered when it has a key."),
                    Ui.Mono("IMG_4021.CR2\nIMG_4022.JPG\nIMG_4023.CR2")),
            };

            AddPanel(explorer);

            SetMenus(
                new LunaMenu("File", open, save, LunaAction.Separator(), revert),
                new LunaMenu("View", grid, full, LunaAction.Separator(), explorer.ToggleAction,
                    new LunaAction("Theme") { Submenu = new LunaMenu("Theme", dark, light) }),
                new LunaMenu("Help", new LunaAction("About LunaP", () => Status = "A small Avalonia toolkit.")));

            SetToolBar(open, save, LunaAction.Separator(), grid, revert);
        }

        // A model for the table to project, so the gallery shows the control doing the thing it is
        // for: rows built from a type through three projections, with the type coming back on
        // selection rather than a row index.
        // A CLASS AND NO LONGER A RECORD, because §50 gave the table editing and Commit writes back
        // into the model. A positional record's properties are init-only, so there is nothing for a
        // Commit to assign - which is a fact about editing worth meeting here, in the gallery, rather
        // than in a consumer's own code.
        private sealed class Photo
        {
            public Photo(string name, string kind, int rating, bool keep, string note)
            {
                Name = name;
                Kind = kind;
                Rating = rating;
                Keep = keep;
                Note = note;
            }

            public string Name { get; set; }
            public string Kind { get; set; }
            public int Rating { get; set; }
            public string Note { get; set; }

            // Written by the check column's toggle, which is the point of it being here: the gallery
            // table is live, and a tick changes a model rather than a picture.
            public bool Keep { get; set; }
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
