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
`SplitPane`, `SidePanel`, `MenuBar`, `ToolBar`, and the three text styles the
theme knows about — `SectionHeader`, `HintText`, `MonoText`.

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

It is flat and stays flat: no tree, no sorting, no cell editing. If you want a
real data grid, `Avalonia.Controls.TreeDataGrid` is the one to reach for — but
check `docs/LunaP.md` §27.1 first, because it requires a paid Avalonia
Accelerate licence and LunaP therefore does not depend on it.

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
