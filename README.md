# LunaP

A small Avalonia toolkit: a theme, a control kit, window scaffolding that
remembers where it was, and a fluent layout surface. It is the chrome around
whatever your application actually does.

Named for Luna-P, Chibiusa's floating gadget ball, which becomes whichever tool
is needed.

## The rule it is built on

**LunaP references Avalonia and nothing else.**

That is not modesty, it is the thing that makes it usable. Every control takes
plain data or a delegate — a meter row takes `(string, double, string)`, a
console pane takes a `Func<string, string>` — so nothing here can drag your
domain model into a window, and nothing here needs to know what your program is
for. Anything that would otherwise need a dependency arrives through a seam you
fill in.

It was written inside an emulator project, where three applications consume it,
and it left once that sentence became true. `docs/LunaP.md` §19 records what had
to move for it to be true and §20 records the move.

## Installing

    dotnet add package EmuSen.LunaP

The package id keeps the `EmuSen.` prefix from where it was written. It carries
no dependency on anything of EmuSen's — a test asserts exactly that.

## Releasing

Tag it, and the workflow does the rest:

    git tag v0.2.0
    git push origin v0.2.0

**The published version comes from the tag, not from the `.csproj`.** A version
written in two places will eventually disagree with itself, and the failure mode
here is one this project has already been bitten by: NuGet caches by package id
*and* version, so a package published under a version somebody has already
restored is a package nobody receives. The `<Version>` in the csproj stays as the
default for a local `dotnet pack` and nothing more.

**There is no API key and no repository secret.** Publishing uses NuGet Trusted
Publishing: the job asks GitHub for a short-lived token proving which repository
and which workflow file is running, and nuget.org exchanges it for a key valid
for minutes. Nothing long-lived is stored, so there is nothing to leak or rotate.

The trust policy lives on nuget.org under **Account → Trusted Publishing** and
names four things that must match `publish.yml` exactly — publisher
(GitHub Actions), repository owner (`RedQuE3n`, the GitHub *login*), repository
(`EmuSen.LunaP`), and workflow file (`publish.yml`). Renaming that file breaks
publishing, which is the point: the file name is part of what is being trusted.

The workflow runs the suite before it packs. A package that was never tested is
a package whose first user is testing it for you.

## Using it

**The bootstrap.** `LunaApp.Configure<App>()` replaces the `AppBuilder` chain a
`Program.cs` usually spells out:

```csharp
[STAThread]
public static void Main(string[] args) =>
    LunaApp.Configure<App>().StartWithClassicDesktopLifetime(args);
```

It applies the saved theme and picks X11 on Linux. That last part is not
cosmetic: `UsePlatformDetect` does not choose X11 on a Wayland session, and a
hand-rolled bootstrap that reproduces three quarters of this one is how that gets
dropped silently. `docs/LunaP.md` §3.

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

**A window** that remembers its own geometry:

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

**Layout**, without a XAML file: `Ui.Stack`, `Ui.Row`, `Ui.Dock`, `Ui.Cols`,
`Ui.Section`, `Ui.Scroll`, `Ui.Button`, `Ui.Header`, `Ui.Hint`, `Ui.Mono`, plus
`.Wrap()`, `.Width()`, `.Left()`, `.Margin()` extensions.

**Controls**: `MeterRow` and `MeterList`, `ConsolePane`, `FieldRow`,
`PathPickerRow`, `FilterBar`, `RgbaImageView`, `LunaSwitch`, `Dropdown`, `Tabs`,
`ButtonBar`, `StatusBar`, `EmptyState`, `LunaList<T>`, `LunaTable<T>`, `Card`,
`SplitPane`, `SidePanel`, `MenuBar`, `ToolBar`, and the four text styles the
theme knows about — `SectionHeader`, `HintText`, `MonoText`, `ErrorText`.

**Stock Avalonia controls are themed too, as of 0.8.0.** A `TextBox`,
`CheckBox`, `RadioButton`, `Slider`, `NumericUpDown`, `ComboBox`, `ProgressBar`,
`ToggleSwitch` or `CalendarDatePicker` you create yourself paints in LunaP's
palette rather than FluentTheme's — you write `new TextBox()` and it fits. This
is done by handing LunaP's colours to FluentTheme's own resource keys, so the
templates, keyboard handling and accessibility behaviour are Avalonia's
untouched, and a control added to Avalonia next year inherits it. A test shows
every one of them in a live window and requires the colours it actually resolves
to come from `LunaPalette`. `docs/LunaP.md` §48.

**A field can be wrong and say so.** `FieldRow.Error` shows a message under the
field; empty means valid, and there is no separate `IsValid` to disagree with it:

```csharp
new FieldRow
{
    Label = "Save State Folder",
    Hint  = "Where save states are written.",
    Error = Directory.Exists(path) ? "" : "That folder does not exist.",
    Content = new TextBox { Text = path },
}
```

`LunaList<T>` keeps hold of the type you gave it — you get the model back on
selection, not a row index into a parallel array — and `Refresh` puts the
selection back afterwards:

```csharp
var peers = new LunaList<Peer> { Label = p => p.Handle, Key = p => p.Handle };
peers.Chose += peer => Open(peer);
peers.Refresh(await roster.All());   // selection survives the rebuild
```

`LunaTable<T>` is the same idea with columns — each one a header and a
projection, so your model needs no attributes and no base class:

```csharp
var fields = new LunaTable<Field> { Key = f => f.Name };
fields.Column("name", f => f.Name, "2*")
      .Column("type", f => f.Type)
      .Column("pg", f => f.Page.ToString(), "40");
fields.Refresh(detected);            // selection survives the rebuild
```

Columns sort, resize and remember where you left them, and cells can be edited —
each one opt-in per column, so a table you already had behaves exactly as it did:

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

`Sort` compares the **models**, not the projected text, because "10" sorts before
"9" otherwise. `Commit` null — the default — means the column is read-only.
`Validate` returns the problem rather than a bool, and the message appears under
the table; a rejected edit keeps the caret rather than throwing away what was
typed. Double-click or F2 opens an editor, Enter commits, Escape cancels.

Give it a `RowHeader` and it grows a gutter down the left — numbers, or whatever
the model calls the row:

```csharp
fields.RowHeader = (_, i) => (i + 1).ToString();   // or (row, _) => row.Address.ToString("X4")
fields.RowHeaderCaption = "#";
```

Columns wider than the table scroll sideways, and the header follows. **This is a
fix rather than a feature**: before 0.8.0 the columns past the right edge were
resolved, clipped and unreachable by scrollbar, wheel or keyboard. Star-width
columns — the default — fit by definition and never scroll.

And the first columns can be pinned while the rest scroll under them:

```csharp
fields.FrozenColumns = 1;            // the gutter is always pinned; this counts your columns
```

Counted in the columns you declared, in the order you declared them, so a hidden
column takes one of the places. A band that would not leave room for the columns
behind it pins **nothing** rather than making them unreachable, and comes back by
itself when the window is widened — so a table that suddenly stops pinning is a
table that is too narrow, not a bug. There is a line where the pinning stops; it
takes `LunaBorder`, and `Border.frozen-edge` restyles it.

`FrozenColumns` is deliberately **not** remembered with the widths and the sort
order: those are what your user did, and this is what you declared. If you offer
it as a "Freeze first column" menu item, remember it in your own settings.

**A table can select cells instead of rows.** Two properties, because *how many*
and *what kind* are different questions:

```csharp
fields.SelectionUnit = LunaSelectionUnit.Cell;      // Row is the default
fields.SelectionMode = LunaSelectionMode.Multiple;  // and this still means how many

fields.CellChosen += cell => Show(cell?.Row, cell?.Column);
```

Arrow keys walk the columns, Home and End go to the ends, Shift extends a
**rectangle** and Ctrl+click adds one cell at a time. `SelectedCell` is the
current one, `SelectedCells` is every one in display order, and `SelectedItems`
still answers with the rows those cells are in. F2 opens the cell you are on
rather than the first editable column.

A cell coordinate is `(your model, column index)` — not two positions — so it
survives a `Refresh` that rebuilds every object, exactly as the row selection
does. Changing the unit clears the selection: a row has no column to become.

**A cell does not have to be text.** A checkbox column takes a boolean projection
and, optionally, somewhere to write it back; a template column takes a control and
the sentence a screen reader hears in its place, which is required rather than
optional because a coloured dot describes itself to nobody:

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

A template cell you gave a width to starts at the column's left edge like every
other cell. One you did not still stretches to fill the column, so a progress bar
or a background band works as you would expect — and an alignment you write
yourself always wins.

**Give it a `Children` projection and it is a tree.** Null — the default — means
it is not one, and a table that never sets it runs the code it always did:

```csharp
files.Children = f => f.Entries;      // return empty for a leaf
files.ExpanderColumn = 0;             // which column carries the toggle
files.ExpandAll();
```

Rows sort within their level, expansion is keyed by `Key` so it survives a poll
that hands back new objects, and a `Children` that returns an ancestor is dropped
rather than overflowing the stack.

If you want a full data grid, `Avalonia.Controls.TreeDataGrid` is the one to reach
for — but check `docs/LunaP.md` §27.1 and §54.5 first, because it requires a paid
Avalonia Accelerate licence that fails the **build**, not the run, in *your*
project. LunaP therefore does not depend on it.

`RgbaImageView` shows a raw RGBA buffer and reuses its bitmap across frames. It
takes them from wherever you already have them, so a frame in native memory is
not copied into a managed array just to be copied straight back out:

```csharp
view.SetFrame(pixels, w, h);                       // byte[]
view.SetFrame(buffer.Slice(offset, w * h * 4), w, h);  // ReadOnlySpan<byte>
view.SetFrame(core.FrameBufferAddress, w, h);      // nint, unchecked - you promise the size
```

`Stretch` defaults to `Stretch.None`, which does not scale at all — one bitmap
pixel to one layout pixel, which is what a pixel-accurate view wants. Set
`IntegerScale = true` with a scaling `Stretch` to enlarge by whole numbers only
and centre the result; a fractional factor makes nearest-neighbour duplicate some
rows and not others, which shimmers when anything moves. `docs/LunaP.md` §53.

**Threading**: `UiThread` (marshal onto the UI thread), `Latest<T>` (a fast
producer, the newest value, one callback), `Suppressor` (stop a control's own
change handler answering back while you write to it) and `Debounce`. All four
were things applications kept writing by hand; `docs/LunaP.md` §22 has the
counts, and §22.1 has a bug that turned up while generalising one of them.

**Windows**: `ToolWindow`, `PollingWindow` (a refresh on a cadence),
`MessageWindow`, dialogs, and `WindowSlot` for one-at-a-time windows.

**Commands, menus and a shell.** A `LunaAction` is one command — a label, a
shortcut, an enabled state, a handler — and every surface you put it on follows
it. Disable the action and the menu entry, the toolbar button and the keystroke
all go with it:

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

A panel's `ToggleAction` is the View-menu entry for it, and it is the *same
object* as its close button — so the tick and the panel cannot drift apart.
`SplitPane` gives you a draggable divider that remembers where it was left, in
pixels, under an opt-in `PaneKey`. What this deliberately does not do — floating
docks, icons, MDI, a native macOS menu bar — is listed in `docs/LunaP.md` §26.12
rather than left to be discovered.

**The gallery** — `GalleryWindow` shows every control in the kit against the
current theme, which is the fastest way to see what a theme you are writing
actually does.

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

## Settings

LunaP remembers two things: window geometry in `windows.json`, and the chosen
theme in `luna.json`. Where those go is yours to decide:

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

LunaP is **dark by default**, and that is a decision rather than the only option:
the palette carries a light column too, keyed by theme variant.

```csharp
LunaTheme.Variant = ThemeVariant.Default;   // follow the desktop
LunaTheme.Variant = ThemeVariant.Light;     // always light
```

Set it before `LunaApp.Configure`, which applies it. The default is `Dark` and
stays there on purpose — every consumer of this toolkit has been dark since it
existed, and following the desktop by default would mean an application looking
different after a version bump its author took for something else.

It matters that the two agree. `LunaTheme.axaml` includes a bare `<FluentTheme/>`,
which follows the system variant whatever LunaP does; leaving the palette fixed
while Avalonia's own controls moved is what put dark text on a dark surface for
anybody on a light desktop. `docs/LunaP.md` §23 has the measurement.

Every light foreground is held to 4.5:1 against the light surface by a test.
`LunaMuted` on the **dark** surface measures 4.22:1, below that floor; it
predates the light column, it is recorded rather than quietly adjusted, and §23.4
says why.

## Accessibility

Every LunaP control reports itself to the automation layer, and names itself from
the property it already had — a `MeterRow` from `Label`, an `EmptyState` from
`Message`, a `StatusBar` from `Status`. `FieldRow` lends its label to whatever
you put inside it, so the `TextBox` in a settings field is announced by the
field's name without you doing anything.

Where the toolkit cannot know what a control is *about* — a `MeterList`, an
`RgbaImageView` — it says nothing rather than guessing, and that is where you
come in:

```csharp
using EmuSen.LunaP.Fluent;

new RgbaImageView().AccessibleName("Game screen")
new Dropdown().AccessibleName("Console")
new Button { Content = "Prune" }.HelpText("Deletes every cheat for the selected system")
new TextBox().LabeledBy(theLabelYouAlreadyDrew)
```

Anything you set wins over the control's own name, so a toolkit default never
overrides your decision. `StatusBar` is a polite live region by default — set
`AutomationProperties.LiveSetting` to `Off` if yours updates continuously.

`LunaTable<T>` goes further, because a table is where a reader most needs it. It
reports itself as a data grid with a selection and a scroll pattern behind the
claim, so a reader can ask what is selected and move a table bigger than the
window. Each row announces as a sentence built from its own cells — "name: Site,
type: text, pg: 1" — and each cell is named for its column, with its value coming
from the pattern it carries. A template column's spoken sentence is what its cell
says, which is why that argument is required rather than optional.

One gap, stated rather than left to be found: a tree row exposes no
`IExpandCollapseProvider`. Its expander is a real focusable button named "Expand
&lt;row&gt;", which is the capability without the pattern (§68.7).

Worth knowing what this is not: it is measured against Avalonia's automation
tree, not against a running screen reader. `docs/LunaP.md` §24 has the before
measurement — nine controls that were not in the tree at all — and §24.4 is
honest about what is still missing.

## Themes

A theme is a resource dictionary of palette keys, written as `.axaml` or as CSS,
dropped in the directory `LunaTheme.Directory` points at. `LunaTheme.Available()`
lists them, `LunaTheme.Apply(name)` applies one, and the built-in palette is the
fallback under everything.

The CSS form exists because a palette is a list of colours and XAML is a heavy
way to write one. `docs/LunaP.md` §12.2 is the format.

One behaviour worth knowing if you write a theme switcher: **mutating
`Application.Styles` at runtime strips every already-realized control of its
styling**, LunaP's own included. `LunaTheme.Restyle(root)` detaches and reattaches
the content, which is what re-runs the style pass. §12.3 is the finding.

## Building and testing

    dotnet build
    dotnet test

207 tests, all headless — no window is ever put on a screen, including for the
render tests, which drive a real Avalonia control tree through a real Skia pass.
The suite runs serially on purpose; `docs/LunaP.md` §20.2 is the race that
taught us why.

The assertion that earns its keep is `AssertLaidOut`: a window that failed to lay
out, or whose controls have no template, renders as one flat colour, and counting
distinct colours catches that where walking the logical tree does not. Set
`EMUSEN_UI_DUMP` to a directory to get a PNG of every capture in the run.

Pixel-exact baselines are opt-in behind `EMUSEN_UI_BASELINE`, because they are an
artefact of one machine's font rendering. `docs/LunaP.md` §10.2 explains what
`AssertStable` is for and the trap it encodes.

## Documentation

`docs/LunaP.md` is the design record: what each part is, what was tried and
rejected, and the findings that cost something to learn. It is kept from the
first commit and has not been tidied to look like the toolkit was always
general — §1's layering rule is stated three different ways as the question it
was answering changed, and that is the useful part.

## Licence

MIT, as of 0.6.0. Link it into anything, including a closed application.

Versions 0.2.0 through 0.5.0 were published GPL-3.0-or-later, and remain so —
a package already on nuget.org cannot have its metadata changed, and a grant
already made cannot be withdrawn. If you are on one of those, take 0.6.0: it
is the same code under a licence that asks less of you. §25 is the reasoning.
