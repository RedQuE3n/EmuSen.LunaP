# LunaP

A small Avalonia toolkit: a theme, a control kit, an application shell, window
scaffolding that remembers where it was, and a fluent layout surface. It is the
chrome around whatever your application actually does.

Named for Luna-P, Chibiusa's floating gadget ball, which becomes whichever tool
is needed.

## What it requires

| | |
|---|---|
| Target framework | `net10.0` |
| Avalonia | **12.1.0**, across the toolkit, the harness and the test project |
| Platforms | Linux, Windows and macOS — the suite builds and runs on all three in CI |
| Licence | MIT, as of 0.6.0 |
| Dependencies | Avalonia only (`Avalonia`, `.Desktop`, `.Themes.Fluent`, `.Fonts.Inter`, `.X11`, `.Markup.Xaml.Loader`) |

**`net10.0` only, and that is a decision rather than an oversight.** Avalonia
12.1.0 also ships `net8.0`, so LunaP is stricter than its own dependency. .NET 8
leaves support around 2026-11-10 and .NET 9 already has, so multi-targeting buys
a dying LTS. `docs/LunaP.md` §34.2.

## The rule it is built on

**LunaP references Avalonia and nothing else.**

That is not modesty, it is the thing that makes it usable. Every control takes
plain data or a delegate — a meter row takes `(string, double, string)`, a
console pane takes a `Func<string, string>` — so nothing here can drag your
domain model into a window, and nothing here needs to know what your program is
for. Anything that would otherwise need a dependency arrives through a seam you
fill in; `ISettingsStore` is the only one so far.

It was written inside an emulator project, where three applications consume it,
and it left once that sentence became true. `docs/LunaP.md` §19 records what had
to move for it to be true and §20 records the move. A test asserts it in both
directions.

## Installing

    dotnet add package EmuSen.LunaP

The package id keeps the `EmuSen.` prefix from where it was written. It carries
no dependency on anything of EmuSen's.

The test harness is a **second package**, referenced from your test project only,
so that the rule above does not have to bend to accommodate xunit:

    dotnet add package EmuSen.LunaP.Testing

Both ship from the same tag at the same version number, because the harness
asserts about the toolkit's own controls and pairing two versions of them is a
question nobody wants to answer.

## The bootstrap

`LunaApp.Configure<App>()` replaces the `AppBuilder` chain a `Program.cs`
usually spells out:

```csharp
[STAThread]
public static void Main(string[] args) =>
    LunaApp.Configure<App>().StartWithClassicDesktopLifetime(args);
```

It applies the saved theme and picks X11 on Linux. That last part is not
cosmetic: `UsePlatformDetect` does not choose X11 on a Wayland session, and a
hand-rolled bootstrap that reproduces three quarters of this one is how that
gets dropped silently. `docs/LunaP.md` §3.

**The theme**, if you are not using `LunaApp`:

```csharp
public override void Initialize()
{
    var theme = new StyleInclude(new Uri("avares://YourApp/"))
    {
        Source = new Uri("avares://EmuSen.LunaP/Theme/LunaTheme.axaml"),
    };
    Styles.Add(theme);
}
```

Load it in your tests too. A headless pass that misses it asserts over
untemplated controls and passes green, which is how a window that rendered
nothing once shipped — §11 of the design record is the incident.

## The control kit

Twenty-seven controls, every one of them in `GalleryWindow` and every one of
them in the automation tree.

| | |
|---|---|
| **Text** | `SectionHeader`, `HintText`, `MonoText`, `ErrorText` — the four idioms the theme knows about |
| **Fields** | `FieldRow` (label, hint, error), `PathPickerRow`, `LunaSwitch`, `Dropdown`, `Tabs` |
| **Data** | `LunaList<T>`, `LunaTable<T>` |
| **Readouts** | `MeterRow`, `MeterList`, `StatusBar`, `EmptyState`, `RgbaImageView` |
| **Surfaces** | `Card`, `SplitPane`, `SidePanel`, `ConsolePane`, `FilterBar` |
| **Commands** | `MenuBar`, `ToolBar`, `ButtonBar`, `ActionButton`, `ActionToggle`, `ActionMenuItem` |

**Stock Avalonia controls are themed too, as of 0.8.0.** A `TextBox`,
`CheckBox`, `RadioButton`, `Slider`, `NumericUpDown`, `ComboBox`, `ProgressBar`,
`ToggleSwitch` or `CalendarDatePicker` you create yourself paints in LunaP's
palette rather than FluentTheme's — you write `new TextBox()` and it fits. This
is done by handing LunaP's colours to 51 of FluentTheme's own resource keys, so
the templates, keyboard handling and accessibility behaviour are Avalonia's
untouched, and a control added to Avalonia next year inherits it. A test shows
every one of them in a live window and requires the colours it actually resolves
to come from `LunaPalette`. `docs/LunaP.md` §48.

If you had restyled these controls yourself, your own styles still win: this
changes resources, not templates.

## Layout without a XAML file

`Ui.Stack`, `Ui.Row`, `Ui.Dock`, `Ui.Cols`, `Ui.Rows`, `Ui.Scroll`,
`Ui.Section`, `Ui.Button`, `Ui.Buttons`, `Ui.Header`, `Ui.Hint`, `Ui.Mono`,
`Ui.Text`.

Fluent setters, each returning what you gave it: `.Wrap()`, `.Bold()`,
`.FontSize()`, `.Width()`, `.Height()`, `.MaxHeight()`, `.MinSize()`,
`.Margin()`, `.Spacing()`, `.Left()`, `.Right()`, `.Center()`, `.Grow()`,
`.Dock()`, `.AtRow()`, `.AtColumn()`, `.Name()`, `.Visible()`.

`Ui.Cols` and `Ui.Rows` place each child in the next cell by position unless it
already carries an explicit one, which is how a child spans while the rest fall
where they are written. `Ui.Dock` follows `DockPanel`: the **last** child takes
the remaining space, which is the usual source of surprise.

## A field can be wrong and say so

`FieldRow.Error` shows a message under the field; empty means valid, and there
is no separate `IsValid` to disagree with it:

```csharp
new FieldRow
{
    Label = "Save State Folder",
    Hint  = "Where save states are written.",
    Error = Directory.Exists(path) ? "" : "That folder does not exist.",
    Content = new TextBox { Text = path },
}
```

`FieldRow` lends its label to whatever you put inside it, so the `TextBox` in a
settings field is announced by the field's name without you doing anything.

## Lists

`LunaList<T>` keeps hold of the type you gave it — you get the model back on
selection, not a row index into a parallel array — and `Refresh` puts the
selection back afterwards:

```csharp
var peers = new LunaList<Peer> { Label = p => p.Handle, Key = p => p.Handle };
peers.Chose += peer => Open(peer);
peers.Refresh(await roster.All());   // selection survives the rebuild
```

`Key` defaults to reference identity, which is right for a cached model and
wrong for rows rebuilt on every poll. Give it a key when your models are
replaced rather than mutated.

### Setting a value is not the user doing something

Every control in the kit that raises a "the user chose this" event holds to one
rule: **writing the value from code does not raise it.** `Dropdown.Chose` is not
raised by `Fill`, `LunaList.Chose` and `LunaTable.Chose` are not raised by
`Refresh` or `Select`, `PathPickerRow.PathPicked` is not raised by setting
`Path`, and `FilterBar.Changed` is not raised by setting `SearchText`.

This is what lets you restore saved state without it looking like input — a
window that reopens with the last filter, sort and selection in place does not
re-run the query that produced them. The one deliberate exception is documented
where it lives: writing `LunaList.SelectedIndex` directly *does* raise `Chose`,
because a direct index write is not a restore.

`FilterBar.Changed` only started honouring this in 0.10.0; before that, assigning
`SearchText` raised it synchronously. If you have code that leant on the raise,
call your handler yourself after setting the value. `docs/LunaP.md` §80.1.

## Tables

`LunaTable<T>` is the same idea with columns — each one a header and a
projection, so your model needs no attributes and no base class:

```csharp
var fields = new LunaTable<Field> { Key = f => f.Name };
fields.Column("name", f => f.Name, "2*")
      .Column("type", f => f.Type)
      .Column("pg", f => f.Page.ToString(), "40");
fields.Refresh(detected);            // selection survives the rebuild
```

**Everything below is additive and off by default.** A table with no `Children`,
no `SelectionMode`, no `Commit`, no `FrozenColumns`, no `CanReorderRows` and no
`VirtualizeColumns` behaves exactly as it did in 0.7.0.

### Sorting, alignment and editing

```csharp
fields.Column(new LunaColumn<Field>("name", f => f.Name)
{
    Width    = "2*",
    Sort     = (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture),
    Commit   = (f, text) => f.Name = text.Trim(),
    Validate = (_, text) => string.IsNullOrWhiteSpace(text) ? "A field needs a name." : null,
});

fields.TableKey = "fields";          // remember widths and sort order
```

```csharp
fields.Column(new LunaColumn<Field>("pg", f => f.Page.ToString())
{
    Width             = "40",
    Alignment         = HorizontalAlignment.Right,  // null - the default - changes nothing
    VerticalAlignment = VerticalAlignment.Center,
    Sort              = (a, b) => a.Page.CompareTo(b.Page),
});

fields.SortBy(2, descending: true);  // as though the heading had been clicked
fields.ClearSort();                  // back to the order you gave
```

The heading follows the column, so a right-aligned column of numbers does not
sit under a left-aligned word. `SortBy` refuses a column with no `Sort` rather
than falling back to sorting the displayed text, and a remembered layout still
wins over a sort you set in code — what your user clicked last time outranks
what your application declared this time. `SortedColumn` and `SortedDescending`
read it back.

`Sort` compares the **models**, not the projected text, because "10" sorts
before "9" otherwise. `Commit` null — the default — means the column is
read-only. `Validate` returns the problem rather than a bool, and the message
appears under the table; a rejected edit keeps the caret rather than throwing
away what was typed. Double-click or F2 opens an editor, Enter commits, Escape
cancels. `Edit(item, column)` opens one from code, and `IsEditing` says whether
one is open.

A layout under `TableKey` is written on every change and flushed when the table
leaves the visual tree; `SaveNow()` forces it.

### Widths and visibility

```csharp
fields.Column(new LunaColumn<Field>("type", f => f.Type)
{
    MinWidth  = 60,          // null - the default - is the Grid's own 0 and infinity
    MaxWidth  = 200,
    IsVisible = showTypes,   // hides it WITHOUT moving any index
});
```

A hidden column keeps its place, so a remembered layout, a sort and
`Edit(item, 2)` all still mean what they meant.

### Rows, gestures and lifecycle

```csharp
fields.SelectionMode = LunaSelectionMode.Multiple;  // None, Single (default), Multiple
fields.GridLines = LunaGridLines.All;               // None (default), Horizontal, Vertical, All
fields.EditGestures = LunaEditGestures.F2 | LunaEditGestures.DoubleTap;

fields.RowPrepared += (row, container) => { };      // realised
fields.RowClearing += (row, container) => { };      // recycled
fields.CellValueChanged += (row, column) => { };    // a commit or a toggle wrote

fields.BringRowIntoView(row);
fields.TryGetRow(row, out Control? visual);
fields.TryGetCell(row, 2, out Control? cell);
```

`EditGestures` is a set rather than a mode because the gestures compose. The two
`TryGet` methods answer **false** for a row that is not currently realised
rather than forcing one into existence — which is why `BringRowIntoView` exists.
There is deliberately no `CellPrepared`/`CellClearing`: it would fire per cell
per row per realization, and the two things it is wanted for are already a
template column and a projection.

### A gutter down the left

```csharp
fields.RowHeader = (_, i) => (i + 1).ToString();   // or (row, _) => row.Address.ToString("X4")
fields.RowHeaderCaption = "#";
fields.RowHeaderWidth = "48";
```

`RowHeader` takes the row and its **displayed** index. The gutter stays put when
the table scrolls sideways, whatever `FrozenColumns` says, because a row label
that scrolls away leaves your user reading a line of values with nothing to say
which row it belongs to.

### Scrolling sideways, and frozen columns

Columns wider than the table scroll sideways, and the header follows. **This is
a fix rather than a feature**: before 0.8.0 the columns past the right edge were
resolved, clipped and unreachable by scrollbar, wheel or keyboard. Star-width
columns — the default — fit by definition and never scroll.

```csharp
fields.FrozenColumns = 1;            // the gutter is always pinned; this counts your columns
```

Counted in the columns you declared, in the order you declared them, so a hidden
column takes one of the places. A band that would not leave room for the columns
behind it pins **nothing** rather than making them unreachable, and comes back
by itself when the window is widened — so a table that suddenly stops pinning is
a table that is too narrow, not a bug. There is a line where the pinning stops;
it takes `LunaBorder`, and `Border.frozen-edge` restyles it.

`FrozenColumns` is deliberately **not** remembered with the widths and the sort
order: those are what your user did, and this is what you declared. If you offer
it as a "Freeze first column" menu item, remember it in your own settings.

### Building only the columns in view

```csharp
fields.VirtualizeColumns = true;     // off by default
```

Worth it when a table scrolls sideways past many columns, and worth leaving off
otherwise. Measured on 120 columns of 120 pixels in an 800-wide viewport, where
6.7 of them are visible: a refresh went from 42.7ms to 6.0ms, and one row held
eight cells instead of 120 (`§72.1`).

Two things to know before you turn it on. **Only fixed-width columns are ever
left out** — an `Auto` or star column takes its width from its content, so
dropping its cells would change how wide it is, and frozen columns are on screen
at every offset by definition. A table of star columns therefore gains nothing,
which is the same table that never scrolls sideways anyway. And **a column that
is not built has no cell**, so `TryGetCell` answers false for it and a screen
reader walking cells does not reach it — the same trade row virtualization has
always made for a row scrolled away. Editing and the arrow keys are unaffected:
both bring a column back before they go looking for it.

What does *not* change is the sentence a screen reader hears for the row, which
is built from your columns rather than from the cells that happen to exist.

### Dragging rows into a new order

The table changes nothing itself — it tells you where the drop landed and you
move your own rows:

```csharp
fields.CanReorderRows = true;
fields.RowDropped += drop =>
{
    foreach (Field moved in drop.Rows) schema.Remove(moved);

    int at = drop.Target is null ? schema.Count : schema.IndexOf(drop.Target);
    if (drop.Position == LunaDropPosition.After) at++;

    schema.InsertRange(at, drop.Rows);
    fields.Refresh(schema);
};
```

`Alt+Up`/`Alt+Down` moves the selected row without a pointer. `CanDrop` refuses
a drop before the indicator promises it. In a tree, dropping into the middle of
a row reports `Inside` — a reparent rather than a reorder. Dragging a row inside
a multi-selection takes the whole selection.

This is pointer capture rather than the platform's drag-and-drop, so a row can
be reordered inside its table but **cannot be dragged out of it** into another
control.

### Selecting cells instead of rows

Two properties, because *how many* and *what kind* are different questions:

```csharp
fields.SelectionUnit = LunaSelectionUnit.Cell;      // Row is the default
fields.SelectionMode = LunaSelectionMode.Multiple;  // and this still means how many

fields.CellChosen += cell => Show(cell?.Row, cell?.Column);
```

Arrow keys walk the columns, Home and End go to the ends, Shift extends a
**rectangle** and Ctrl+click adds one cell at a time. `SelectedCell` is the
current one, `SelectedCells` is every one in display order, and `SelectedItems`
still answers with the rows those cells are in. `IsCellSelected`, `SelectCell`
and `ClearCellSelection` drive it from code. F2 opens the cell you are on rather
than the first editable column.

A cell coordinate is `(your model, column index)` — not two positions — so it
survives a `Refresh` that rebuilds every object, exactly as the row selection
does. Changing the unit clears the selection: a row has no column to become.

### A cell does not have to be text

A checkbox column takes a boolean projection and, optionally, somewhere to write
it back; a template column takes a control and the sentence a screen reader
hears in its place, which is required rather than optional because a coloured
dot describes itself to nobody:

```csharp
fields.Column(new LunaColumn<Field>("req", f => f.Required, (f, on) => f.Required = on)
      {
          Width = "40",                // read-only if you leave the writer off
      })
      .Column(new LunaColumn<Field>(
          "kind",
          f => new Ellipse { Width = 8, Height = 8, Fill = ColourFor(f.Type) },
          f => f.Type));                // what a screen reader hears instead
```

Both are ordinary constructors, so `Width`, `Sort`, `MinWidth`, `IsVisible` and
the rest apply exactly as they do to a text column. A `Toggle` that declines to
write leaves the tick where it was — the table re-reads your model rather than
trusting the box. What it cannot do is say *why*, which a text column's
`Validate` can.

A template cell you gave a width to starts at the column's left edge like every
other cell. One you did not still stretches to fill the column, so a progress
bar or a background band works as you would expect — and an alignment you write
yourself always wins.

### A table can be a tree

One projection, and null — the default — is a flat table:

```csharp
files.Children = node => node.Kids;   // return empty for a leaf; null is a flat table
files.ExpanderColumn = 0;             // which column carries the toggle
files.IndentSize = 16;
files.ExpandAll();                    // Expand, Collapse, CollapseAll, IsExpanded
```

A projection rather than an interface, so your model needs no base class and no
knowledge that LunaP exists — one that keeps its children elsewhere writes
`n => index[n.Id]`, which an interface could not express. Sorting applies at
**every level**, so a tree stays a tree rather than becoming an alphabetical
list of everything; expansion is keyed by `Key`, so it survives a `Refresh` that
rebuilds every object; and a `Children` that returns an ancestor is dropped
rather than overflowing the stack.

### If you want a full data grid

`Avalonia.Controls.TreeDataGrid` is the one to reach for — but check
`docs/LunaP.md` §27.1 and §54.5 first, because it requires a paid Avalonia
Accelerate licence that fails the **build**, not the run, in *your* project.
LunaP therefore does not depend on it, and `LunaTable<T>` closed all thirteen
gaps that decision was measured against (§73).

## Images and frames

`RgbaImageView` shows a raw RGBA buffer and reuses its bitmap across frames. It
takes them from wherever you already have them, so a frame in native memory is
not copied into a managed array just to be copied straight back out:

```csharp
view.SetFrame(pixels, w, h);                           // byte[]
view.SetFrame(buffer.Slice(offset, w * h * 4), w, h);  // ReadOnlySpan<byte>
view.SetFrame(core.FrameBufferAddress, w, h);          // nint, unchecked - you promise the size
```

`Stretch` defaults to `Stretch.None`, which does not scale at all — one bitmap
pixel to one layout pixel, which is what a pixel-accurate view wants. Set
`IntegerScale = true` with a scaling `Stretch` to enlarge by whole numbers only
and centre the result; a fractional factor makes nearest-neighbour duplicate
some rows and not others, which shimmers when anything moves. `docs/LunaP.md`
§53.

## Commands, menus and a shell

A `LunaAction` is one command — a label, a shortcut, an enabled state, a handler
— and every surface you put it on follows it. Disable the action and the menu
entry, the toolbar button and the keystroke all go with it:

```csharp
var open = new LunaAction("Open ROM...", () => Load())
{
    Shortcut = KeyGesture.Parse("Ctrl+O"),
    HelpText = "Chooses a ROM to load.",
};
var grid = new LunaAction("Grid", self => ShowGrid(self.IsChecked)) { IsCheckable = true };

var window = new AppWindow { Title = "Studio", WindowKey = "main" };
window.SetMenus(new LunaMenu("File", open, LunaAction.Separator(), quit));
window.SetToolBar(open, grid);
window.AddPanel(new SidePanel { Title = "Explorer", Side = PanelSide.Left, PanelKey = "explorer" });
window.Central = editor;
window.Status = "Ready.";
```

`SetMenus` and `SetToolBar` also **bind the shortcuts**, which is a separate act
from showing them: `MenuItem.InputGesture` draws "Ctrl+O" in the menu and binds
nothing at all, so a hand-built menu can advertise a key that does nothing.
Claim one key twice and LunaP says so through `LunaSettings.Diagnostics` rather
than letting the second command quietly never fire.

**A group of actions can be exclusive.** `ActionGroup` is Qt's `QActionGroup`:
adding an action makes it checkable and ticking one unticks the rest. An action
already in another group is refused rather than silently moved.

```csharp
var speed = new ActionGroup();
var half  = speed.Add("50%",  _ => SetSpeed(0.5));
var full  = speed.Add("100%", _ => SetSpeed(1.0));

speed.Checked = full;    // checks that one, unchecks the rest, runs no handler
speed.Checked = null;    // unchecks everything
```

`Checked` both reads and writes, and **writing it runs no handler** — so a window
that shows the current selection cannot apply it just by displaying it. Assigning
an action that is not a member throws rather than joining it silently.

**A menu can nest.** `LunaAction.Submenu` takes a `LunaMenu`, so a "Recent
Files" entry is an action carrying its own menu rather than a second kind of
object.

**Menus without an `AppWindow`.** `Menus.Context(actions)` builds a
`ContextMenu`, `Menus.Items(...)` builds the controls, and
`Menus.BindShortcuts(target, ...)` / `Menus.Unbind(...)` bind and release the
keystrokes on any `InputElement`.

A panel's `ToggleAction` is the View-menu entry for it, and it is the *same
object* as its close button — so the tick and the panel cannot drift apart.
`AppWindow.PanelToggles()` hands you every one of them for a View menu.
`SplitPane` gives you a draggable divider that remembers where it was left, in
pixels, under an opt-in `PaneKey`; `Orientation`, `Fixed`, `FixedSize`,
`MinFirst`, `MinSecond`, `SplitterThickness` and `DividerLabel` shape it.

## Windows

`ToolWindow` is the base: a `WindowKey` to remember geometry under,
`ClosesOnEscape`, and the theme's restyle hook. `PollingWindow` refreshes on a
cadence and stops while hidden. `MessageWindow` and `Dialogs` cover the rest —
`ConfirmAsync`, `ErrorAsync`, `PickFileAsync`, `PickFolderAsync` and
`SaveFileAsync`, all returning paths rather than storage items. `WindowSlot<T>`
holds a one-at-a-time window, with `RefreshIfOpen` for the case where it is not.

**A window that remembers its own geometry:**

```csharp
public class SettingsWindow : ToolWindow
{
    public SettingsWindow()
    {
        Title = "Settings";
        WindowKey = "settings";   // opt in; without a key nothing is remembered
        Content = Ui.Stack(8, Ui.Header("Audio"), Ui.Row(6, volume, mute));
    }
}
```

**A defect fixed in 0.8.0, and it reaches anybody with a `WindowKey`.** A window
closed while maximized or full screen, with nothing stored from a previous run,
saved the **screen's** bounds as its own restored size — so it reopened the size
of the display with its title bar off the top. It now records the flag and no
geometry. The maximized half of that had been present since 0.2.0.

**Any window can go full screen**, and coming back out returns it to the state
it came from rather than always to a normal window:

```csharp
window.ToggleFullScreen();               // or: window.IsFullScreen = true
window.FullScreenChanged += on => full.IsChecked = on;
```

`IsFullScreen` is read from the window rather than stored beside it, so it stays
right when the platform's own full-screen affordance is what moved it. That is
also why `FullScreenChanged` exists: a checkable menu item that kept its own
tick would say the opposite of the window the first time somebody used a
window-manager shortcut. **F11 is yours to bind** — LunaP does not claim a
function key.

A window closed while full screen reopens as an ordinary window at the size it
had before, and **full screen is deliberately not remembered** while maximized
is: a window that reopens maximized still has its title bar and its close
button, and one that reopens full screen has neither, with the key that would
let it out bound to whatever you chose. Set `IsFullScreen` at startup if you
want it back.

**The pointer can get out of the way** when it has been still for a while, which
is what a full-screen anything wants:

```csharp
_idle = new IdleCursor(this);                            // the whole window, three seconds
_idle = new IdleCursor(screen, TimeSpan.FromSeconds(1)); // or just the framebuffer
```

It attaches to any control rather than being a flag on the window, because
"hidden over the video and visible over the toolbar" is the common case and a
window-level switch cannot say it. `Hide()` and `Show()` drive it directly — a
window entering full screen wants the pointer gone at once rather than in three
seconds — and `Show()` is also the seam for your own idea of activity, a gamepad
or a media player leaving playback. `IsHidden` and `HiddenChanged` read it back.

**Dispose it.** The cursor comes back on disposal, and one left hidden by an
object nobody unsubscribed is an application whose pointer is gone for good.

Only pointer *movement* counts as activity: keystrokes deliberately do not, or
an application somebody is holding four keys down in would never hide it at all.
A child that sets its own cursor keeps it, so the pointer reappears over a
sortable table heading.

**Files can be dropped onto any control**, arriving as local paths:

```csharp
_drop = new FileDrop(this, paths => Load(paths[0]));
_drop.Accept = paths => paths.Count == 1;      // refuses while the drag is still moving
```

Avalonia already extracts the files; what this removes is four lines of wiring
with two silent failures in them — forgetting `AllowDrop`, so no drag event is
raised at all, and forgetting to set an effect in `DragOver`, so the platform
refuses the drop before your handler is reached. Neither produces an error or a
mark on screen. **Dispose it**, and whatever `AllowDrop` you had is put back.

Paths rather than storage items, to match `Dialogs`. A file with no local path —
out of a remote share, or a virtual file from an archive viewer — is not
offered, and a drop carrying nothing else is refused rather than delivered
empty.

**What LunaP deliberately does not wrap**, because Avalonia already does it
well: `Window.Topmost`, `Window.Icon`, `TopLevel.Clipboard` (with `TryGetText`
and `TryGetFiles`), and `ExtendClientAreaToDecorationsHint` for a borderless
window. A toolkit may not charge a name for a property that already exists.

## Threading

`UiThread` marshals onto the UI thread — `Run` inline when it is already there,
`Post` otherwise, `IsCurrent` to ask. `Latest<T>` takes a fast producer and
presents the newest value through one callback. `Suppressor` stops a control's
own change handler answering back while you write to it, counting rather than
flagging so nesting works. `Debounce` collapses a burst — `Poke`, `Flush`,
`Cancel`, `IsPending`.

All four were things applications kept writing by hand; `docs/LunaP.md` §22 has
the counts, and §22.1 has a bug that turned up while generalising one of them.

## Settings

LunaP remembers four things, each opt-in: window geometry in `windows.json`,
split and panel sizes in `panes.json`, table widths and sort order in
`tables.json`, and the chosen theme in `luna.json`. Where those go is yours to
decide:

```csharp
LunaSettings.Store = new JsonSettingsStore("/path/to/your/config");
LunaSettings.Diagnostics = message => logger.Warn(message);
```

Set nothing and it writes indented JSON under `ApplicationData/<your entry
assembly>`. Implement `ISettingsStore` — three methods — if you keep settings
somewhere that is not a directory of JSON files.

**Under a test runner it does something different, on purpose.** The entry
assembly there is `testhost`, which is the same name for every project on the
machine — so settings would land in one shared folder that every other
repository's test suite also reads and writes. Instead the store roots itself
under your test project's own `bin` directory and reports where through
`Diagnostics`. A name you pass explicitly is always honoured. See `§43`.

`Diagnostics` is where "this file would not load, and why" goes. Loading is
best-effort and falls back to defaults either way; the hook only stops it
happening in silence.

## Light and dark

LunaP is **dark by default**, and that is a decision rather than the only
option: the palette carries a light column too, keyed by theme variant.

```csharp
LunaTheme.Variant = ThemeVariant.Default;   // follow the desktop
LunaTheme.Variant = ThemeVariant.Light;     // always light
```

Set it before `LunaApp.Configure`, which applies it. The default is `Dark` and
stays there on purpose — every consumer of this toolkit has been dark since it
existed, and following the desktop by default would mean an application looking
different after a version bump its author took for something else.

It matters that the two agree. `LunaTheme.axaml` includes a bare
`<FluentTheme/>`, which follows the system variant whatever LunaP does; leaving
the palette fixed while Avalonia's own controls moved is what put dark text on a
dark surface for anybody on a light desktop. `docs/LunaP.md` §23 has the
measurement.

Every light foreground is held to 4.5:1 against the light surface by a test.
`LunaMuted` on the **dark** surface measures 4.22:1, below that floor; it
predates the light column, it is recorded rather than quietly adjusted, and
§23.4 says why.

## Themes

A theme is a resource dictionary of palette keys, written as `.axaml` or as CSS,
dropped in the directory `LunaTheme.Directory` points at. `LunaTheme.Available()`
lists them, `LunaTheme.Apply(name)` applies one, `LunaTheme.Current` and
`LunaTheme.Saved` read the state back, and the built-in palette is the fallback
under everything.

**Seventeen colour tokens**, each spelled as a brush and a colour
(`LunaSurface` / `LunaSurfaceColor`, and so on):

| | |
|---|---|
| Surfaces | `LunaSurface`, `LunaInputSurface`, `LunaVoid`, `LunaBorder` |
| Text | `LunaText`, `LunaMuted`, `LunaSectionHeader`, `LunaMeterText` |
| Status | `LunaError`, `LunaWarning`, `LunaSuccess`, `LunaInfo` |
| Accent | `LunaAccent`, `LunaOnAccent` |
| Load ramp | `LunaNominal`, `LunaBusy`, `LunaHot` |

Plus `LunaMonoFont`, `LunaHeaderFontSize` and `LunaHintFontSize`. The load ramp
has a C# side too: `LunaPalette.ForLoad(percent)` and `LevelFor(percent)`, over
thresholds of 60% and 85%.

The palette is spelled twice on purpose — `Palette.axaml` for XAML,
`LunaPalette.cs` for controls built in C# — and a test resolves every key from
the live application and asserts it equals the C# field, so adding a colour to
one half and not the other fails immediately.

**The CSS form** exists because a palette is a list of colours and XAML is a
heavy way to write one:

```css
:root { --luna-surface: #1E1E1E; --luna-accent: #007ACC; }
section-header { color: var(--luna-section-header); font-size: 15px; }
console-pane .output { font-family: "JetBrains Mono"; }
```

The whole vocabulary is enumerable rather than something to guess at:
`CssTheme.ElementNames` gives the **22** element names a rule may target,
`PartsOf` and `StatesOf` their parts and states, `PropertyNames` the six property
names (`color`, `background`, `background-color`, `font-family`, `font-size`,
`font-weight`), and `CssTheme.TokenNames` the **20** `--luna-` tokens a `:root`
block may set. Anything outside it — an unknown element, an unknown property, or
a misspelled token — is reported through `CssThemeResult.Warnings` rather than
silently ignored. **Token names were not checked before 0.10.0**, so
`--luna-surfce` used to parse, do nothing, and say nothing. `docs/LunaP.md` §12.2
is the format and §79.4 is that fix.

One behaviour worth knowing if you write a theme switcher: **mutating
`Application.Styles` at runtime strips every already-realized control of its
styling**, LunaP's own included. `LunaTheme.Restyle(root)` detaches and
reattaches the content, which is what re-runs the style pass. §12.3 is the
finding.

## Accessibility

Every LunaP control reports itself to the automation layer, and names itself
from the property it already had — a `MeterRow` from `Label`, an `EmptyState`
from `Message`, a `StatusBar` from `Status`.

Where the toolkit cannot know what a control is *about* — a `MeterList`, an
`RgbaImageView` — it says nothing rather than guessing, and that is where you
come in:

```csharp
using EmuSen.LunaP.Fluent;

new RgbaImageView().AccessibleName("Game screen")
new Dropdown().AccessibleName("Console")
new Button { Content = "Prune" }.HelpText("Deletes every cheat for the selected system")
new TextBox().LabeledBy(theLabelYouAlreadyDrew)
new Border().Decorative()                       // out of the control view entirely
new TextBlock().LiveRegion()                    // announce when it changes
```

Anything you set wins over the control's own name, so a toolkit default never
overrides your decision. `StatusBar` is a polite live region by default — set
`AutomationProperties.LiveSetting` to `Off` if yours updates continuously.

`LunaTable<T>` goes further, because a table is where a reader most needs it. It
reports itself as a data grid with a selection and a scroll pattern behind the
claim, so a reader can ask what is selected and move a table bigger than the
window. Each row announces as a sentence built from its own cells — "name: Site,
type: text, pg: 1" — and each cell is named for its column, with its value
coming from the pattern it carries. Rows expose `ISelectionItemProvider` and
editable cells expose `IValueProvider`, so a reader can select a row and set a
cell — going through your `Validate` first, exactly as typing does. A template
column's spoken sentence is what its cell says, which is why that argument is
required rather than optional.

`LunaAutomationPeer` is public, so a control of your own can join the tree the
same way.

## Testing your own windows

`EmuSen.LunaP.Testing` is the harness this project tests itself with, as a
separate package so the toolkit itself keeps referencing Avalonia and nothing
else:

    dotnet add package EmuSen.LunaP.Testing

```csharp
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => LunaHeadless.BuildApp();
}

[Fact]
public Task The_settings_window_lays_out() => UiTest.Run(() =>
{
    var window = new SettingsWindow();
    window.Show();
    UiTest.AssertLaidOut(window, "settings");
});
```

`AssertLaidOut` is the one that earns its keep: a window that failed to lay out,
or whose controls have no template, renders as one flat colour, and counting
distinct colours catches that where walking the logical tree does not.

**`DisableTestParallelization` is required, and the harness refuses to start
without it.** Every test shares one headless application and several statics
around it are process-global, so running test classes concurrently lets one
class's constructor overwrite another's state mid-assertion — which presents as
a suite that is green on your machine and red on CI. `docs/LunaP.md` §20.2 is
the failure that taught us, §22.8 is why the refusal is loud rather than
documented.

The harness has its own README, shipped in its own package.

## Limitations

Collected here rather than left to be discovered. Each is a decision or a
measured gap, and each is argued where the work happened.

**The shell** (§26.12). No floating or re-dockable panels, no tabbed dock
groups, no MDI — one panel per side, and a second on the same side replaces the
first. No icons anywhere, so a toolbar is a row of words; this needs an icon
system rather than a property. No vertical toolbar, no split buttons, and no
overflow chevron when a toolbar is wider than its window — it clips. No tooltip
on a menu item, only on toolbar items. **No native macOS menu bar**: Avalonia
has `NativeMenu` and `MenuBar` does not use it, so a macOS application gets an
in-window menu strip where the platform expects one at the top of the screen.

**The table** (§73.2). No `CellPrepared`/`CellClearing`. A row cannot be dragged
*out* of its table. A tree row exposes no `IExpandCollapseProvider` — its
expander is a real focusable button named "Expand &lt;row&gt;", which is the
capability without the pattern. `FrozenColumns` is not remembered with the
widths and the sort.

**Accessibility** (§24.4). Everything above is measured against Avalonia's
automation tree, **not against a running screen reader** — no Orca, NVDA or
VoiceOver has been run against any of it. `ConsolePane` cannot announce line by
line: its output is one text block, so a live region there would re-read the
whole buffer on every append. `FieldRow.Error` reaches a reader that visits the
field, but nothing interrupts a reader who has moved on (§49.3).

**Themes.** The CSS vocabulary covers 22 elements, and `LunaTable<T>` and
`LunaList<T>` are **not** among them — a CSS theme reaches the rest of the kit
and not those two. A theme rule that parses cleanly but matches nothing at
runtime is silent: the parse cannot know what is on screen, and the sweep that
catches it runs at test time in this repository, not in a host loading a bad
theme (§30.5).

**Contrast.** `LunaMuted` on the dark surface measures 4.22:1, under the 4.5:1
the light column is held to. Recorded rather than quietly adjusted (§23.4).

**Trimming and AOT** (§36). Measured, not fixed. Trim-safe is reachable;
AOT-safe is not while the default settings store is reflection JSON and themes
are loaded from `.axaml` at runtime.

**Process-global statics.** `LunaSettings.Store`, `Diagnostics` and the applied
theme's resource dictionary are static, so every consumer test suite inherits
the parallelisation hazard the harness refuses to start under. That is
structural, not a bug in your suite (§21.3).

**Not built, with reasons** (§77.3): keeping the display awake, single-instance,
and a custom title-bar control. The first is the one with real value here and it
needs three untestable platform paths; the second is process coordination
wearing a window service's clothes, and the half of it that is easy is the half
nobody wants.

**Unexercised.** `RgbaImageView.Blit` reads `RowBytes` and copies row by row
when a framebuffer's stride is padded, but no backend measured here pads — the
loop's arithmetic is verified by forcing the branch; the padded case itself is
not. First place to look if an image comes out sheared (§53.2).

## Building and testing

    dotnet build
    dotnet test

**880 tests, all headless** — no window is ever put on a screen, including for
the render tests, which drive a real Avalonia control tree through a real Skia
pass. That figure is checked by the suite itself, because a hand-written count
of a thing the runner knows is a number that rots: this one said 207 for four
releases, and its replacement went stale within the hour (`§79.7`). The suite
runs serially on purpose; `docs/LunaP.md` §20.2 is the race that taught us why.
CI runs the same suite on Linux, Windows and macOS, and packs both packages on
every push so a missing README is found before a tag rather than after.

The assertion that earns its keep is `AssertLaidOut`: a window that failed to
lay out, or whose controls have no template, renders as one flat colour, and
counting distinct colours catches that where walking the logical tree does not.
Set `EMUSEN_UI_DUMP` to a directory to get a PNG of every capture in the run.

Pixel-exact baselines are opt-in behind `EMUSEN_UI_BASELINE`, because they are
an artefact of one machine's font rendering. `docs/LunaP.md` §10.2 explains what
`AssertStable` is for and the trap it encodes.

**The public API surface is written down** in
`tests/EmuSen.LunaP.Tests/ApiSurface/`, and a test fails the build when it
changes. Regenerate with `EMUSEN_API_APPROVE=1 dotnet test` and commit the
baseline — the diff on that file is the review. §32.

## Releasing

*Maintainers only; a consumer needs nothing from this section.*

Tag it, and the workflow does the rest:

    git tag v0.8.0
    git push origin v0.8.0

**The published version comes from the tag, not from the `.csproj`.** A version
written in two places will eventually disagree with itself, and the failure mode
here is one this project has already been bitten by: NuGet caches by package id
*and* version, so a package published under a version somebody has already
restored is a package nobody receives. The `<Version>` in each csproj stays as
the default for a local `dotnet pack` and nothing more — which also means it is
not evidence that anything shipped. 0.7.1 was prepared, written into both csproj
files and given a changelog entry, and never tagged; its fix has never reached a
consumer.

**There is no API key and no repository secret.** Publishing uses NuGet Trusted
Publishing: the job asks GitHub for a short-lived token proving which repository
and which workflow file is running, and nuget.org exchanges it for a key valid
for minutes. Nothing long-lived is stored, so there is nothing to leak or
rotate.

The trust policy lives on nuget.org under **Account → Trusted Publishing** and
names four things that must match `publish.yml` exactly — publisher
(GitHub Actions), repository owner (`RedQuE3n`, the GitHub *login*), repository
(`EmuSen.LunaP`), and workflow file (`publish.yml`). Renaming that file breaks
publishing, which is the point: the file name is part of what is being trusted.

The workflow runs the suite before it packs. A package that was never tested is
a package whose first user is testing it for you.

## Documentation

`CHANGELOG.md` is the consumer's account of what a version bump means to
somebody who cannot patch it.

`docs/LunaP.md` is the design record: what each part is, what was tried and
rejected, and the findings that cost something to learn. It is kept from the
first commit and has **not** been tidied to look like the toolkit was always
general — §1's layering rule is stated three different ways as the question it
was answering changed, and that is the useful part. Where it and the code
disagree, the code is the truth and the document is the history.

## Licence

MIT, as of 0.6.0. Link it into anything, including a closed application. Every
dependency of the toolkit is MIT too, so nothing here hands you a term the
licence on the tin does not mention.

Versions 0.2.0 through 0.5.0 were published GPL-3.0-or-later, and remain so —
a package already on nuget.org cannot have its metadata changed, and a grant
already made cannot be withdrawn. If you are on one of those, take 0.6.0 or
later: it is the same code under a licence that asks less of you. §25 is the
reasoning.

`EmuSen.LunaP.Testing` is MIT too, and links `xunit.assert`, which is
Apache-2.0. It is referenced from a test project, so nothing your application
ships carries it.
