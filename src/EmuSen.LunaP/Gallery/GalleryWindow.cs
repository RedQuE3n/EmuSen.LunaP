using System.Collections.Generic;
using Avalonia.Controls;
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

            // Three columns over a model, which is the shape the one piece of evidence for this
            // control actually has - a field list with a name, a type and a page number (§27).
            //
            // TWO SORTABLE AND ONE NOT, on purpose. A gallery that made every column sortable would
            // show the feature and hide the choice; a heading with no comparison stays a plain
            // label, and seeing the two side by side is the only way to notice that "sortable" is
            // something a caller decides per column rather than something the table does.
            //
            // The page column sorts NUMERICALLY while displaying a string, which is the argument
            // for Sort taking a comparison over the model: sorting the text would put "10" before
            // "9" in a table whose whole job is to be read.
            var fields = new LunaTable<Field> { Key = f => f.Name };
            fields.Column(new LunaColumn<Field>("name", f => f.Name)
                  {
                      Width = "2*",
                      Sort = (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture),
                  })
                  .Column("type", f => f.Type)
                  .Column(new LunaColumn<Field>("pg", f => f.Page.ToString())
                  {
                      Width = "40",
                      Sort = (a, b) => a.Page.CompareTo(b.Page),
                  });
            fields.Refresh(new[]
            {
                new Field("Site", "text", 1),
                new Field("Technician", "text", 1),
                new Field("Approved", "checkbox", 2),
                new Field("Total aid retained", "text", 2),
            });

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
                    })),

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
        private sealed record Field(string Name, string Type, int Page);

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
