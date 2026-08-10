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
`ButtonBar`, `StatusBar`, and the three text styles the theme knows about —
`SectionHeader`, `HintText`, `MonoText`.

**Windows**: `ToolWindow`, `PollingWindow` (a refresh on a cadence),
`MessageWindow`, dialogs, and `WindowSlot` for one-at-a-time windows.

**The gallery** — `GalleryWindow` shows every control in the kit against the
current theme, which is the fastest way to see what a theme you are writing
actually does.

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

`Diagnostics` is where "this file would not load, and why" goes. Loading is
best-effort and falls back to defaults either way; the hook only stops it
happening in silence.

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

132 tests, all headless — no window is ever put on a screen, including for the
render tests, which drive a real Avalonia control tree through a real Skia pass.

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

GPL-3.0-or-later.
