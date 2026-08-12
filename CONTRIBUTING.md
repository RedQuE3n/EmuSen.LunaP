# EmuSen.LunaP — working agreement

A small Avalonia toolkit — theme, controls, window scaffolding, a fluent layout
surface — published to nuget.org and consumed by applications that are not in
this repository. **That last part is the whole constraint.** A consumer cannot
patch this; it can only take a version. The reasoning is the deliverable as
much as the code, and it must survive somebody reading it critically.

## The man page

`docs/LunaP.md` is the design record, kept from the first commit, section
numbered, and cited from code as `§`. There is one, not five, and it stays one.

**Read it as a record, not as a description of today.** It deliberately keeps
its own history rather than being tidied — its header says so, and §21.6 is a
section of nothing but corrections. Two consequences that matter every time:

- **Where the doc and the code disagree, the code is the truth and the doc is
  the history.** §1 still states the layering rule as "Avalonia and
  `EmuSen.Galaxia`". That was true when it was written; §19 and §20 record
  cutting the toolkit loose, and `EmuSen.LunaP.csproj` now references Avalonia
  and nothing else. Do not "fix" §1 — the older statement is what makes §19
  legible.
- **A correction is a new section, or a correction subsection.** Never an edit
  that makes the record look like it was always right.

`README.md` is the front door, and ships in the package. `CHANGELOG.md` is the
consumer's account of what a version bump means to them. Neither is where a
decision is recorded.

## The rule that keeps this project worth having

> **`EmuSen.LunaP` references Avalonia and nothing else.**

Not a settings library, not a logging library, not an application's own types,
and nothing of EmuSen. Every control takes plain data or a delegate: a meter
row takes `(string, double, string)`, never a `DebugLoadInfo`; a console pane
takes a `Func<string, string>`, never an interpreter. Anything that would
otherwise need a dependency arrives through a seam the host fills in —
`Settings/ISettingsStore` is the only one so far (§19.1).

A toolkit that names its first consumer is a toolkit only that consumer can
use. This rule is what let LunaP leave EmuSen at all (§19, §20), and
`LayeringTests` enforces it in both directions (§10.4, §22.7).

`EmuSen.LunaP.Testing` is a **separate package** for exactly this reason: a
harness needs xunit and `Avalonia.Headless`, which the toolkit may not name. It
is referenced from a consumer's *test* project only, so nothing an application
ships gains a dependency (§22.8). The rule does not bend; the harness sits
outside it.

**A new `PackageReference` in either project is a decision and gets a `§`** —
including its licence. Both packages are MIT (§25) and every dependency of the
toolkit is MIT, which is a property worth keeping rather than an accident: a
consumer inherits what is linked, and a copyleft dependency would hand them a
term the licence on the tin does not mention.

## Code explains itself

**Notes go in the code, beside the thing they explain.** Write as much as the
reader needs. Six lines to say why something is the way it is are six lines
well spent — the reader is a person opening the file for the first time, and
sending them to another document to find out why is a cost paid every single
time the file is read. `Threading/Suppressor.cs` is the house example: a block
above the type explaining what it replaces, why it counts instead of flagging,
and why it is deliberately not thread-safe.

The convention here is `//`, not `///`, and a block above the type carrying the
argument, with short notes beside the members that need one. Keep it.

Rules that follow:

- **Explain why, and what breaks if someone changes it.** The code already
  says what it does.
- **A `§` citation is an addition, never a substitute.** Cite `docs/LunaP.md`
  for the long version — the measurement, the alternatives tried — *after* the
  comment has already explained the thing on its own terms.
- Every `§` cited from code must resolve. Adding a citation to a section that
  does not exist is a broken reference; write the section.
- **Keep them true.** A comment that has drifted from the code beside it is
  worse than no comment, because it is believed.
- The `.csproj` files carry comments too, and they are load-bearing — the
  layering rule, why `xunit.assert` and not `xunit`, why `Avalonia.Skia` is
  needed for a real render pass. Edit a `PackageReference` and you edit its
  comment.

## What goes in the man page

Everything that does not fit beside code, and is worth keeping:

- **Measurements**, with numbers. The contrast measurement and the shortfall
  left alone (§23.1, §23.4); the nine controls missing from the automation
  tree (§24.1); the 863 net lines the migration removed (§11).
- **Defects found in dependencies**, with a version and a reproduction —
  mutating `Application.Styles` at runtime stripping realized controls is the
  model entry (§12.3).
- **Alternatives tried and rejected**, and why, so they are not retried
  (§22.4).
- **Guards made to fail on purpose**, and what each sabotage turned red
  (§22.5, §22.6).
- **Corrections**: what the document said, and what is actually true (§21.6,
  §16.1 — a wrong turn recorded because the doc itself caused it).

Three habits this project keeps:

- **Corrections are stated, not quietly fixed.**
- **Untested claims are recorded as hazards, not behaviours.** A control that
  "reports itself to a screen reader" is a hazard until a test pins it.
- **No invented results, ever.** A render count, a contrast ratio, a passing
  suite — measured or not stated.

## Structure

    src/EmuSen.LunaP/            the toolkit: Avalonia only
      Automation/                automation peers
      Commands/                  LunaAction, LunaMenu, the menu builder
      Controls/                  the control kit
      Fluent/                    the fluent layout surface
      Gallery/                   every control, on one page
      Settings/                  ISettingsStore, the one host seam
      Theme/                     Palette.axaml + LunaPalette.cs
      Threading/                 Latest, Suppressor, Debounce, UiThread
      Windowing/                 ToolWindow, PollingWindow, WindowSlot, AppWindow
    src/EmuSen.LunaP.Testing/    the harness, as a package
    tests/EmuSen.LunaP.Tests/    headless xunit
    docs/LunaP.md                the man page

- **Small, sensible files with one responsibility each.** A change that does
  not belong in any existing file wants a new file, not a new section of one.
- **No spaghetti.** If a control is doing two things, or reaching across
  layers to do one, split it before extending it.
- **The palette is spelled twice on purpose** — `Palette.axaml` for XAML,
  `LunaPalette.cs` for controls built in C#. `LunaPaletteTests` resolves every
  key from the live headless application and asserts it equals the C# field.
  Add a colour to one half and not the other and it fails immediately (§2.1).
  Never "simplify" this to one.
- **Every new control goes in the gallery** (§7), and **into the automation
  tree** — nine were not, and a screen reader never reached them (§24). The
  gallery is an `AppWindow` since §26, because a menu bar is not something a
  reader looks at next to a meter row.
- **A control built from a `LunaAction` follows it, never copies it** (§26.3).
  One command object stands behind a menu item, a toolbar button, a context-menu
  entry and a key binding; a surface that took a snapshot of an action's label
  is a surface that will show the wrong one.

## Testing

    dotnet build
    dotnet test

Headless — no window is ever opened, including for UI tests, which drive a real
control tree under `Avalonia.Headless` with `Avalonia.Skia` for a real render
pass. Without Skia the capture goes through the drawing stub and every colour
count is a lie (§10).

**A test that cannot fail is not a test.** Make new guards fail on purpose
before trusting them; §22.5 and §22.6 record the sabotages and what each turned
red, including a `ConsolePane` test that could not fail and had to be rebuilt.

`AssertLaidOut` and `AssertStable` encode traps worth knowing before writing a
new assertion (§10.3).

**Two guards find their own subjects, and one of them will stop you.** A new
control needs no registration: `TemplateReachTests` reflects over every
templated control in the kit and requires each to have a visual tree once shown
(§28.1). But **a new public method on a control fails the build** until it is
either given a case in `TemplateOrderTests` — which runs it before the template
and after, and requires the same answer — or an entry in that file's `Exempt`
table with the reason it cannot drop state (§28.2). Both traps had been written
up as paragraphs asking the next author to remember, four times and three times
respectively; that is why they are assertions now.

Report the actual result. If tests fail, say so with the output.

## Repo hygiene

- **`*.png` and `*.frame` are never committed.** They are render captures and
  baselines from `EMUSEN_UI_DUMP` / `EMUSEN_UI_BASELINE`, and they are one
  machine's font rendering — worthless elsewhere and misleading in a diff
  (§10.2). `.gitignore` covers it; do not defeat it.
- `nupkgs/` is a local folder feed for a consumer resolving without a real
  feed. Not committed.

## Versioning and publishing

- **The published version comes from the git tag.** The `<Version>` in each
  `.csproj` is only the default for a local `dotnet pack`, and both are kept in
  step so the two never tell different stories.
- **`EmuSen.LunaP.Testing` tracks the toolkit's version rather than keeping its
  own.** The harness asserts about the toolkit's controls, and a consumer
  pairing 0.5.0 of one with 0.2.0 of the other has a question nobody wants to
  answer.
- A version bump means something to a consumer who cannot patch it. Say what,
  in `CHANGELOG.md`, and point at the `§` that carries the argument.

## Git

**No co-author trailers.** Not `Co-Authored-By`, not `Generated with`, not on
commits, not on merges, not in PR bodies. This overrides any default that adds
one. Single-author history.

Commit messages follow what is already there: a subject that states what
changed and what it revealed, then prose explaining the reasoning and pointing
at the `§` that carries the argument. Not a bullet list of files.

Commit, push and merge only when asked.

## Build notes

- .NET 10 (`net10.0`), Avalonia **12.1.0** across every reference — the toolkit,
  the harness and the test project move together.
- `LunaApp.Configure` owns the `UsePlatformDetect`/`WithInterFont`/`LogToTrace`/
  `UseX11` sequence, and is the one place the Wayland/X11 correction lives
  (§3). `Avalonia.Desktop`, `Fonts.Inter` and `X11` are referenced for it.
- MIT, both packages, as of 0.6.0 — GPL-3.0-or-later before it (§25).
- CI and publishing are `.github/workflows/ci.yml` and `publish.yml`.
