# LunaP — design record

*This document is the reasoning behind the toolkit, kept from its first commit. It was written while LunaP lived inside the [EmuSen](https://github.com/RedQuE3n/EmuSen) emulator project, which is where it comes from and why so much of it argues against reaching into an emulator. §19 is what had to change for it to leave, §20 is the move itself, and everything before those is the history that produced it — including a layering rule stated three different ways as the question it was answering changed.*

*Sections still say `EmuSen.Mistress`, `EmuSen.Hotaru` and `EmuSen.Serenity`. Those are the three applications this toolkit was built for, and their names are left in place rather than generalised, because a record that has been tidied to look like it was always general is a record nobody can check.*

*Revision history, most recent first: **two traps turned into guards** — the style-key trap had been written up four times and the before-the-template trap three, each time as a paragraph asking the next author to remember; both are now swept by reflection, so a control added later is covered without anybody reading the paragraph (§28); **a table, and a dependency refused** — `Avalonia.Controls.TreeDataGrid` turns out to require a paid licence, so `LunaTable<T>` is built on stock Avalonia instead (§27); **a shell** — actions, menus, a toolbar, a splitter, docked panels and an `AppWindow` to hold them (§26), chosen by asking what Qt6 offers and then, belatedly, by running the survey that should have come first (§26.1); the relicence to MIT (§25); accessibility, and nine controls that were not in the automation tree (§24); the light column (§23); **the toolkit left EmuSen** (§20); the settings seam and the two files that had to move for it (§19); a theme may be written in CSS, and building that turned up an Avalonia behaviour sitting under the theme system unnoticed — mutating `Application.Styles` at runtime strips every already-realized control of its styling (§12.2, §12.3); the widget set and user themes from disk; the migration, which removed 863 net lines from two frontends.*

---

## 1. What it is, and the rule that keeps it useful

`EmuSen.LunaP` is the shared Avalonia toolkit for `EmuSen.Mistress`, `EmuSen.Hotaru` and the future launcher: theme, controls, window scaffolding, and a fluent layout surface. It is the chrome *around* the game picture; `EmuSen.Serenity` owns the picture itself and the two do not overlap.

**The layering rule is load-bearing and is the reason the project is worth having:**

> LunaP may reference **Avalonia** and **`EmuSen.Galaxia`** and nothing else. Not `EmuSen` (the core), not `EmuSen.DianaOS`, not `EmuSen.Cauldron`, not `EmuSen.Endymion`.

The launcher's entire value is browsing a library with no core loaded. One upward reference here hands it the whole emulator, permanently. `EmuSen.Serenity` already holds this line on the video side — it presents frames while taking `(byte[] rgba, int width, int height)` rather than an `ICore` — and **every LunaP control takes plain data or a delegate for the same reason.** A meter row takes `(string, double, string)`, never a `DebugLoadInfo`. A console pane takes a `Func<string, string>`, never a `DianaOSInterpreter`.

This is also why the two frontends share *widgets* but not *windows*: `CoretopWindow` consumes `ICoreTelemetry`, so it stays a file in each frontend even though almost everything inside it is now shared. See `EmuSen_LunaP_Gameplan.md` (in EmuSen) §2.

The name is Luna-P, Chibiusa's floating gadget ball, which becomes whichever tool is needed. It is **not** `Luna`, the reserved codename for the Nintendo DS core — see `EmuSen_Core_Naming_Scheme.md` (in EmuSen) §9.

---

## 2. The theme

### 2.1 Two halves, and the test that pins them

The palette is spelled twice on purpose:

- **`Theme/Palette.axaml`** — a `ResourceDictionary` of `Color` and `SolidColorBrush` resources, for XAML (`Foreground="{DynamicResource LunaText}"`).
- **`Theme/LunaPalette.cs`** — the same values as `ImmutableSolidColorBrush` statics, for controls built in C# (`Foreground = LunaPalette.MeterText`).

XAML cannot read C# constants and a static cannot cheaply resolve an app resource before the app exists, so one of the two has to be a copy. **`EmuSen.WiseMan/LunaP/LunaPaletteTests.cs` is what stops them drifting**: it resolves every key out of the live headless application and asserts the resolved brush equals the C# field, colour for colour. Add a colour to one half without the other and that test fails immediately. It also serves as a smoke test that the whole `StyleInclude` → `Styles.Resources` → `TryGetResource` chain works at all — if `LunaTheme.axaml` ever stops reaching `Application.Styles`, every key fails to resolve and the first assertion says so by name.

**Every value in the palette is a literal that was already in the codebase.** Nothing here was chosen; the audit in `EmuSen_LunaP_Gameplan.md` (in EmuSen) §1.1 is where each one came from.

| Key | Value | Role |
|---|---|---|
| `LunaSurface` | `#1E1E1E` | tool-window background |
| `LunaInputSurface` | `#252526` | text-input background |
| `LunaVoid` | `#000000` | letterbox/no-signal area behind a game frame |
| `LunaText` | `#D4D4D4` | body and monospace text |
| `LunaMeterText` | `#DCDCDC` | meter-row labels and values |
| `LunaMuted` | `#808080` | hints, group headers, disabled captions |
| `LunaSectionHeader` | `#9CDCFE` | section headings |
| `LunaWarning` | `#D08770` | inline caution text |
| `LunaNominal` / `LunaBusy` / `LunaHot` | `#32CD32` / `#FFD700` / `#FF4500` | the load ramp, §2.2 |
| `LunaMonoFont` | `Consolas,Menlo,monospace` | the monospace stack |
| `LunaHintFontSize` / `LunaHeaderFontSize` | `11` / `14` | |

**Why `LunaText` and `LunaMeterText` are still two greys, four steps apart.** They are the same role — body text — and they should almost certainly converge. But `#D4D4D4` was what the XAML said and `#DCDCDC` (`Brushes.Gainsboro`) was what the meter-building code said, and merging them would have changed rendered pixels during a phase whose whole guarantee was that nothing changed. **Converging them is a deliberate one-line decision for whoever builds `MeterRow` in Phase 2**, not something to do silently.

One brush was deliberately *not* absorbed: `InputSettingsWindow`'s conflict highlight is still a raw `Brushes.OrangeRed`. It happens to equal `LunaHot`, but "this binding collides with another" is not "this subsystem is at 85% load", and giving them one key would encode a relationship that does not exist.

### 2.2 The load ramp

`LunaPalette.ForLoad(percent)` returns green below 60, gold from 60, orange-red from 85. Those thresholds came from three separate hand-written `ColorForPercent` copies (both `CoretopWindow`s and `VstopWindow`) that agreed by luck; all three are deleted and call this instead. `LunaPaletteTests` pins the boundaries at 59.9/60 and 84.9/85 so a future edit cannot quietly shift them.

The convention deliberately matches DianaOS's own terminal `ColoredBar`, so the GUI dashboards and `coretop` in a real terminal never disagree about what counts as hot.

---

## 3. Bootstrap

`LunaApp.Configure<TApp>()` is the single Avalonia startup sequence: `UsePlatformDetect()`, `WithInterFont()`, `LogToTrace()`, and `UseX11()` on Linux (which `UsePlatformDetect` does not select on its own under a Wayland session — see `EmuSen_Project_Overview_v2.md` §2a). There is an overload taking a `Func<TApp>` because `EmuSen.Hotaru` constructs its `App` by hand: its `Main` has to fully resolve a ROM and build a core before any Avalonia type is touched.

Both `Program.cs` files are now one expression each.

### 3.1 Why the theme include matters to the test harness

`EmuSen.WiseMan/Serenity/TestAppBuilder.cs` used to hand-build `FluentTheme` + `ThemeVariant.Dark`, because it could not reference either frontend's `App.axaml`. Its own comment recorded the hazard: **without a theme, templated controls have no template, render as nothing, and every render assertion over them silently passes.** That is a bug class produced entirely by having no shared application setup — a divergence between the harness's theme and the real one could not be detected by any test, because the failure mode is a test that passes.

The harness now includes the same `avares://EmuSen.LunaP/Theme/LunaTheme.axaml` the frontends do, so there is one theme and no way for them to disagree.

---

## 4. What Phase 1 changed, and how "no visual change" was verified

Mechanically: every theme literal in both frontends became a resource reference (`#1E1E1E` ×5, `#D4D4D4` ×18, `#9CDCFE` ×15, `#252526` ×2, `#D08770`, `Gray` ×22, the monospace stack ×16), every code-behind theme brush became a `LunaPalette` static, and three `ColorForPercent` copies were deleted.

`grep -rn '#[0-9A-Fa-f]\{6\}\|"Gray"\|Consolas' --include=*.axaml EmuSen.Mistress EmuSen.Hotaru` now returns nothing. That is the check to re-run before claiming the palette is still centralised.

**The "pixel-identical" claim was measured, not asserted.** A `git worktree` of the pre-change commit and the current tree each rendered `CoretopWindow`, `DebugSettingsWindow` and `PreferencesWindow` through the headless Skia session, dumping raw RGBA. All three pairs compared **byte-for-byte identical** across 3,144,000 bytes. `VstopWindow` was deliberately excluded from that comparison rather than trusted: it prints live pid, uptime and CPU figures, so its pixels differ between any two runs and it can never be a reliable regression target.

The dump harness was a throwaway and was deleted. **Phase 5 of the gameplan is where it should come back properly**, as `UiTest.Capture` in WiseMan — a reusable golden-image comparison is worth having and hand-rolling it per migration is not.

### 4.1 One thing left alone

`ActiveCheatsWindow`'s cheat-detail column uses `FontFamily="monospace"` — the bare family, not the `Consolas,Menlo,monospace` stack everything else uses. Converting it would have changed which font actually resolves, and therefore pixels. It is a real (tiny) inconsistency, left for whoever migrates that window in Phase 6 to fix deliberately.

---

## 5. The control kit

Eleven controls in `Controls/`, all styled from `Theme/Controls.axaml`, all usable from XAML (`xmlns:luna="clr-namespace:EmuSen.LunaP.Controls;assembly=EmuSen.LunaP"`) and from C#. None of them names a core, a telemetry type or a DianaOS type — §1.

**Every control has a test that asserts its style actually applied**, because the failure mode of a style that stops matching is not an exception, it is a control that renders as plain or as nothing at all. §5.5 is a real instance of exactly that, caught by exactly that.

### 5.1 Text — `SectionHeader`, `HintText`, `MonoText`

Three `TextBlock` subclasses carrying no code at all; the whole definition is a style. `SectionHeader` is the blue bold heading (×15 in the old XAML), `HintText` the grey 11 pt wrapping explanation (×22), `MonoText` the monospace body used for register dumps and runtime figures (×16).

### 5.2 Meters — `MeterRow`, `MeterList`, `MeterEntry`

`MeterRow` is a label / percentage bar / value in a `140,*,55` grid — the exact layout all three hand-written `BuildMeterRow` copies used. Setting `Percent` recomputes `BarBrush` through `LunaPalette.ForLoad`, so the ramp cannot be forgotten at a call site.

`MeterList` takes an `IReadOnlyList<MeterEntry>` and rebuilds its rows wholesale on every assignment. **That is deliberate and is not the waste it looks like**: the original code rebuilt its rows from scratch four times a second on purpose, because a handful of cheap control allocations at 4 Hz is far simpler than diffing and updating a cached control per entry, and the refresh rate makes the cost irrelevant. Phase 3's "suspend the timer while hidden" removes even that.

**Grouping deliberately stays with the caller.** `coretop` groups its load bars by kind and labels each group with `DebugLoadKindText.Header(...)` — DianaOS vocabulary, which §1 forbids here. A window that needs groups emits a `SectionHeader` and a `MeterList` per group.

### 5.3 `RgbaImageView`

Takes a raw RGBA buffer and shows it, **reusing its `WriteableBitmap` across frames and reallocating only when the dimensions change**. Of the three implementations this replaces, only `FeedWindow`'s did that; both `CoretopWindow`s allocated a fresh bitmap on every one of their 4 Hz ticks. The better implementation is now the only one.

Since writing pixels does not change the bitmap instance, nothing downstream would know to repaint — the control invalidates its own `Image` part explicitly. A `0×0` buffer, or one shorter than `width * height * 4`, clears the view instead of throwing: "no tile memory" is a legitimate answer from a core, not an error.

### 5.4 Settings fields — `FieldRow`, `PathPickerRow`

`FieldRow` is bold label / optional grey hint / content, and collapses the hint entirely when it is empty rather than reserving blank space. `PathPickerRow` is the read-only path box plus `Browse...` button that appeared four times, wired to §6's pickers; it raises `PathPicked` only on a real selection, never on a cancel.

### 5.5 Bars — `StatusBar`, `ButtonBar`, and a trap worth knowing

`StatusBar` is the bottom strip: status text left, content right. `ButtonBar` is a right-aligned run of buttons.

**`ButtonBar` initially rendered as nothing, and this is the single most useful thing learned in Phase 2.** It derives from `ItemsControl`, and in Avalonia a control's *style key* defaults to its own runtime type — so `FluentTheme`'s `ControlTheme` for `ItemsControl` does not reach a subclass of `ItemsControl`. No template, no `ItemsPresenter`, no items, no error. It looked like a working control that simply had nothing in it.

The fix is to template it explicitly rather than inherit (the alternative, overriding `StyleKeyOverride` to point back at the base type, works too but silently re-couples the control's look to whatever the Fluent theme does next). **Anything added to this kit that derives from a templated Avalonia control needs its own `Template` setter and a test that finds a real part in the visual tree** — asserting on a property alone would have passed here.

That rule was restated three more times before anything enforced it (§14.1, §26.11, §27.3). **§28.1 is the enforcement**: every templated control in the kit is now found by reflection, shown, and required to have a visual tree, so a control added later is covered without anybody having read this paragraph.

### 5.6 `ConsolePane`

The terminal-shaped pane: scrolling output, prompt, input box, and Up/Down history recall — the whole of the byte-identical XAML plus the recall state machine both console windows had. It knows nothing about DianaOS:

- `Submitted` is an `Action<string>`; running the line is the caller's business.
- `HistorySource` is a `Func<IReadOnlyList<string>>`, which is exactly the seam the two callers need — Mistress reads its interpreter's history, Hotaru reads the *live core's* history once a game attaches.

**Output is held in the control, not in the `TextBlock`.** Both console windows print a welcome banner from their constructor, long before a template exists, so writing straight to the template part would have silently dropped it — a bug that would have appeared in Phase 6 as "the banner is gone" with nothing to point at. The pane buffers and flushes on `OnApplyTemplate`, and a test pins it.

---

## 6. Pickers

`Windowing/Dialogs.cs` wraps `StorageProvider` for folder, open-file and save-file selection, resolving the `TopLevel` itself so a caller passes only a control. A start location that no longer exists is not an error — the picker just opens where it would have anyway.

**This was pulled forward from Phase 3**, where the gameplan filed it, because `PathPickerRow` is meaningless without it. The rest of that phase's `Dialogs` — `ConfirmAsync`/`ErrorAsync` — genuinely does belong with the window scaffolding, since those need a window of our own rather than an OS dialog.

---

## 7. The gallery

`Gallery/GalleryWindow.cs` is every control once, with sample data, built entirely in C# — which also dogfoods the claim that the kit does not require XAML. Two tests cover it: one asserting each control type is realised in the visual tree, one taking a real Skia render pass and asserting the result is not a flat image.

That second test is the cheap net for the §5.5 failure mode across the whole kit at once. Its threshold is deliberately far above the ">8 distinct colours" the older window tests use, because the gallery's image-view ramp alone contributes hundreds; a templating failure collapses it to a handful.

The gallery ships in Release. It is ~110 lines with no dependencies beyond the kit, and a widget library without a visible reference page is much harder to extend correctly than one with.

---

## 8. The windowing layer

`Windowing/` is where the kit stops being widgets and starts being a framework. Still nothing consumes it — Phase 6 does that.

### 8.1 `ToolWindow`

The base class, and **deliberately thin: both of its features are opt-in, so inheriting it changes nothing by itself.** That was a design choice, not an oversight. Phase 6 rewrites a dozen windows onto this base, and a base class that silently altered how they close or where they open would make every one of those migrations a behaviour change hiding inside a refactor.

- **`WindowKey`** — set it and the window's size, position and maximised state are remembered in `windows.json`; leave it null and nothing is written at all.
- **`ClosesOnEscape`** — off by default, because Escape inside a console pane means "stop what I am typing", not "close the window".

Restoring geometry has one non-obvious rule: **a remembered position is checked against the attached screens before it is used.** A window last closed on a monitor that is no longer plugged in would otherwise reopen off every screen, where it cannot be dragged back. The check is split into a pure `IsOnAScreen(IReadOnlyList<PixelRect>, PixelRect)` precisely so it can be tested without a display, and "no screens known" is treated as *allow* — refusing there would strand the window at the default position for a reason no user could see.

A maximised window's own bounds are the screen's, so saving them would lose the restore size. Closing while maximised keeps the previously stored normal geometry and records only the flag.

### 8.2 `PollingWindow`

Declare `RefreshInterval` and override `Refresh()`. Timer construction, start, priming, stop-on-close and disposal happen once, here, instead of five times across two frontends.

**It also does something none of the five hand-written copies did: it stops while the window is hidden or minimised.** A forgotten-but-open dashboard was a permanent 4 Hz tax, which matters directly to the weak-machine work. Restoring the window refreshes immediately, so the first thing seen is current rather than however stale it got. Occlusion is not detectable portably and is not attempted.

Two details worth knowing before writing one:

- **`StartPolling()` is called by the derived constructor, not the base one.** Priming from the base constructor would call `Refresh()` before the derived class had assigned its own fields — `CoretopWindow` would render "no target" against a `_target` that was about to be set. `Opened` calls `StartPolling()` too, so forgetting it costs a slightly later first paint rather than a window that never updates.
- **`IsPolling` is public for the tests.** Asserting "it stopped" by counting ticks would mean racing a real clock inside a dispatcher the test is itself blocking; asserting on the timer's state is deterministic. That the tests are not vacuous was checked by mutation — replacing the visibility gate with `true` fails both of them.

### 8.3 `WindowSlot<TWindow>`

The "at most one of these, else bring it forward" pattern, which seven call sites hand-wrote (five in Mistress's `MainWindow`, two in Hotaru's `DebugWindows`), each with its own nullable field and its own `Closed` unhook.

```csharp
_coretop.Show(owner: this,
              create: () => new CoretopWindow(target),
              refresh: w => w.UpdateTarget(target));
```

`RefreshIfOpen` is the second, quieter half: it **never creates and never activates**. Hotaru needs exactly this after a `core <name> <path>` swap — refreshing a dashboard that happens to be open, without popping one up for someone who never asked and without stealing focus mid-game. That was a hand-written policy in one place; now it is a method.

Thread marshalling is absorbed, but **not by always posting**: the slot runs inline when it is already on the UI thread and posts otherwise. Always posting would make `Current` unset when `Show` returns, which is surprising for Mistress, where every call is already on the UI thread. Hotaru's calls arrive from the emulation and console-reader threads and are posted.

### 8.4 Confirm and error dialogs

`Dialogs.ConfirmAsync` and `ErrorAsync` complete §6's pickers. These are the half that needed a window of our own rather than an OS dialog, which is why they waited for this phase — `MessageWindow` is built from the Phase 2 kit. Confirm returns false for cancel, for Escape and for closing the window: anything that is not a deliberate yes.

---

## 9. The fluent surface

`Fluent/` is a terser spelling of what XAML already says — `Ui` for the layouts and kit controls a window is made of, and one extension method per layout attribute. It composes §5 and §8's types rather than raw panels, which is exactly why it was built last: written first, it would have been a fluent API over `StackPanel` that the controls then had to fight.

```csharp
Content = Ui.Scroll(Ui.Stack(8,
    _header,
    Ui.Section("Load",     _load),
    Ui.Section("Palette",  _palette)).Margin(12));
```

**Every extension is named after the XAML attribute it sets** — `Margin`, `Spacing`, `Width`, `Height`, `MaxHeight`, `Grow`, `Left`, `Right`, `Center`, `Dock`, `AtColumn`, `AtRow`, `Visible`, `Bold`, `FontSize`, `Wrap`. That is the whole contract, and a test asserts it property by property: the two ways of building a window stay one vocabulary, so nobody has to learn a second layout model.

**That naming turned out to be possible only by checking.** An extension method whose name matches an existing property looks like it cannot work — `Margin` *is* a property on `Layoutable`, so `control.Margin(12)` reads like invoking a `Thickness`. It compiles: C# only falls back to extension methods when member lookup fails to produce a *method group*, and a property is not one. This was verified with a throwaway probe project before the API was designed around it, because the alternative was an invented vocabulary (`Pad`, `Spaced`, `Sized`) that would have broken the one-vocabulary rule for no reason.

`Ui.Cols` is where the phase pays for itself:

```csharp
Ui.Cols("140,*,55", label, bar, value)   // instead of three Grid.SetColumn calls
```

Columns are assigned by position, and **an explicit `.AtColumn(2)` still wins** — the convenience never becomes a rule it imposes. Spans work the same way.

### 9.1 The success criterion, proved rather than claimed

The goal set in the gameplan was that a new dashboard is *a constructor and a `Refresh()` body*, with no `.axaml` file. `EmuSen.WiseMan/LunaP/DashboardShapeTests.cs` builds exactly that — an `ExampleDashboard` shaped like `CoretopWindow` but reading plain data instead of `ICoreTelemetry` — and drives it end to end: empty state on first paint, populated after a refresh, polling suspended when hidden. It is both the proof and the worked example for Phase 6.

`GalleryWindow` was rewritten onto the fluent surface as the second check. **Its render tests passed unchanged**, which is the useful part: the fluent spelling produces an equivalent visual tree, not merely a compiling one.

---

## 10. The test harness

`EmuSen.WiseMan/Fixtures/UiTest.cs` is the one place a UI test dispatches, captures and asserts. Five files were hand-rolling the capture (`CaptureRenderedFrame` → `Lock()` → `Marshal.Copy`) and two were hand-rolling the dump; all of them now call this.

- **`UiTest.Run(body)`** — dispatches onto the one headless UI thread the session owns.
- **`UiTest.Capture(window)`** → a `RenderedFrame` (RGBA8888, width, height) with `Hash`, `DistinctColours(stopAt)` and `SavePng`. The same shape `FrameHash` and `BmpFile` already take for the core's own frame buffer, so a captured window is directly comparable against `EmuSen.Pharaoh`'s `--autoshot` tooling.
- **`UiTest.AssertLaidOut(window, name, minColours = 8)`** — the always-on assertion: a window that failed to lay out, or whose controls have no template, renders as one flat colour. It dumps, asserts, and checks the baseline if one is configured.
- **`UiTest.AssertStable(name, build)`** — builds and renders twice, asserting the two are identical.

### 10.1 `EMUSEN_UI_DUMP` is now a directory

It used to be a *file path*, and that had already stopped working: `InputSettingsWindowRenderTests` appended `_{console}` to the basename to get three files out of one variable, and the two sites that used it disagreed about whether it wrote BMP or PNG. It now names a **directory**, and every capture in the run lands in it as `<name>.png`.

```
EMUSEN_UI_DUMP=/tmp/ui dotnet test EmuSen.WiseMan/EmuSen.WiseMan.csproj --filter "FullyQualifiedName~RenderTests"
```

### 10.2 Baselines, and why they are not committed

`AssertLaidOut` also calls `AssertMatchesBaseline`, which is **a no-op unless `EMUSEN_UI_BASELINE` is set**. That is deliberate: the surrounding test always has its own real assertion, so nothing becomes vacuous when the baseline is absent, and a fresh clone or a CI run has nothing to fail against.

The migration workflow is two commands — record on the commit before the change, compare after:

```
git worktree add /tmp/before HEAD
EMUSEN_UI_BASELINE=/tmp/frames EMUSEN_UI_BASELINE_MODE=write   dotnet test /tmp/before/EmuSen.WiseMan/...
EMUSEN_UI_BASELINE=/tmp/frames EMUSEN_UI_BASELINE_MODE=compare dotnet test EmuSen.WiseMan/...
```

A mismatch reports how many pixels differ, not just that something changed. **This was verified by mutation rather than assumed**: changing `LunaSectionHeader` from `#9CDCFE` to `#9CDCFF` — one channel, one value — failed the comparison with "gallery rendered 904 pixels differently from its baseline."

**Reference images are deliberately not committed.** They are binary blobs that churn on any font, Skia or theme change, and a stale one fails in a way that looks like a real regression. Recording a baseline from the previous commit costs one command and is never stale.

### 10.3 `AssertStable`, and the trap it encodes

Phase 1 found that `VstopWindow` can never be a baseline target: it prints live pid, uptime and CPU figures, so its pixels differ between any two runs. `AssertStable` makes that an explicit, testable property rather than something discovered when a comparison mysteriously fails — a window that shows a clock, a pid or a frame counter fails it by design, and the message says so.

The gallery is held to it, since it is the kit's own baseline target.

### 10.4 The layering rule is now enforced

`Common/LeafAssemblyTests.cs` already pinned Endymion, Serenity and Galaxia to their allowed references. **LunaP is in that list now.** §1's rule was documentation until this phase; adding one `ProjectReference` in a hurry is exactly the kind of thing that would otherwise go unnoticed until the launcher inherited the emulator.

What it asserts has since tightened twice: first to `EmuSen.Galaxia` and `EmuSen.Cauldron`, then — when the toolkit was made extractable — to **nothing at all**, as `LunaP_references_nothing_of_EmuSen`. Serenity's own assertion widened to `Cauldron, Galaxia, LunaP` in the same change, because it took what LunaP put down.

**A blind spot worth knowing about, found by sabotaging this guard and watching it pass.** These tests read `Assembly.GetReferencedAssemblies()`, and the C# compiler elides a reference used *only* for `const` values, because a constant is inlined at the call site. A first attempt to make the guard fail added a `ProjectReference` back to Galaxia and used `ConfigStore.ProgramDirName` — a `const string` — and the built assembly named Galaxia nowhere. The guard was correct and the sabotage was not. Repeating it against `ConfigStore.Directory`, an ordinary static property, reddened it immediately.

The consequence is real and applies to every assertion in that file: **a project can depend on another project's constants and these tests cannot see it.** That is a narrow hole — a `const` carries no behaviour, so nothing it inlines can drag an emulator in — but a reader should not believe the guard covers more than it does.

---

## 11. The migration

Six windows moved onto the kit, in six commits: `VstopWindow`, both `CoretopWindow`s, `FeedWindow`, both DianaOS console windows, `PreferencesWindow`, `DebugSettingsWindow`, plus every `WindowSlot` call site. **863 lines net removed from the two frontends**, and eight `.axaml` files deleted — the frontends are down to `App.axaml` plus five windows that were never in scope (`MainWindow`, `GameWindow`, `ActiveCheatsWindow`, `CheatDatabaseWindow`, `InputSettingsWindow`, `RomBrowserWindow`).

What actually went away: three `BuildMeterRow` copies with their `Grid.SetColumn` wiring, three RGBA→bitmap paths (two of which reallocated a `WriteableBitmap` on every 4 Hz tick), five hand-rolled `DispatcherTimer`s, seven "at most one, else `Activate()`" blocks, two copies of the Up/Down history recall state machine, and two byte-identical console layouts.

Every migrated dashboard also stops polling while hidden, which none of them did before.

### 11.1 What the verification caught

Three things, none of which a passing build would have shown:

- **`CoretopWindow`'s empty state was 11,060 pixels wrong on the first attempt.** `HintText` is 11 pt by definition; the original "No ROM loaded." was body-sized. It is a plain muted `TextBlock` now, and both states are byte-identical to the pre-migration render.
- **`PreferencesWindow` never showed its own Close button.** Rendering the pre-migration window from a `git worktree` showed it stopping at "ROM Directory": the content needed ~420 px in a window fixed at 330 with `CanResize=false` and no scrolling, so the only button on it was unreachable. **This long predates the toolkit** — the migration's verification is just what surfaced it. The window sizes to its content now.
- **An assumption of mine, not the code's.** I asserted `coretop`'s no-core state draws no `ProgressBar`; it draws one, because the sprite bar is a fixed part of the layout sitting at zero. Writing the tests against the *unmigrated* window is what caught that, and the migration preserves the behaviour.

`DebugSettingsWindow`, `CoretopWindow` (both states) and the gallery all came out byte-identical.

### 11.2 Two judgement calls

**`DrainPendingFromEmulationThread` goes through `slot.Current`, not `RefreshIfOpen`.** `RefreshIfOpen` marshals to the UI thread, which is correct for every other caller and exactly wrong for this one — it must run on the thread that owns the core. Hotaru's `UpdateCoretopWindowTargetIfOpen` is the opposite case and is now a single line.

**`FeedWindow` needs `.Grow()`.** `RgbaImageView` is left-aligned by default, which is right for a palette swatch in a column and wrong for a live game mirror that should fill the window. There is a test asserting the picture is actually wider than 400 px, because the wrong alignment renders as a working window that simply drew small.

### 11.3 What was still duplicated, and how it was actually resolved

*Superseded on 2026-08-04 by §16 — kept because the reasoning recorded here was wrong in an instructive way.*

The two `CoretopWindow`s were 138 lines each and **differed by six lines, all namespace or comment**. This section framed that as the deliberate consequence of sharing widgets but not windows, and named the only remaining option as "a small third assembly referencing LunaP *and* Cauldron."

**That framing was too narrow, and taking it at face value cost a wrong turn.** A third assembly was actually built before anyone asked the prior question: *does referencing Cauldron from LunaP violate what the layering rule is for?* It does not — see §16. The window is in `Dashboards/` and there is no third assembly.

### 11.4 Where the tests moved

Windows that build their own tree have no XAML namescope, so `GetControl<T>(name)` no longer resolves. Test lookups go through `FindNamed<T>` over the visual tree instead (the idiom `InputSettingsWindowLayoutTests` already used).

The eleven `DianaOSShellWindow` tests are the case worth noting: **their bodies were not touched at all**, only the three lookup helpers. Those tests drive real key routing — Enter through `KeyPress`, Up-arrow recall, live-shell attach — so keeping the bodies intact is what makes them a genuine safety net across the rewrite rather than a restatement of whatever the new code happens to do.

`CoretopWindow` and `FeedWindow` had no tests at all before this; they have sixteen now, along with a `FakeTelemetry` fixture that lets any dashboard be driven with no core loaded.

---

## 12. Themes

A theme is a file in **`/etc/EmuSen/themes/<name>.<ext>`**, written either as an `.axaml` `ResourceDictionary` (below) or as `.css` (§12.2). Both spell the same thing — overrides of whichever `Luna*` keys the theme cares about — and one name is one theme: `Available()` lists it once however many formats are on disk, and `.axaml` wins if both exist.

The `.axaml` form overrides whichever `Luna*` keys it cares about — the same category shape `cheats/<name>.json` already uses, so `man hier` covers where it lives. `LunaTheme.Apply(name)` merges it *last*, so its keys win, and persists the choice in `luna.json`; `LunaApp.Configure` calls `ApplySaved()` at startup.

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="LunaSurface" Color="#12131A" />
  <SolidColorBrush x:Key="LunaSectionHeader" Color="#7AA2F7" />
</ResourceDictionary>
```

Four properties, each pinned by a test:

- **Applying one repaints live**, with no restart, because everything resolves the palette through `DynamicResource`.
- **Keys a theme does not mention keep their built-in value**, so a two-line theme is a legitimate theme.
- **Switching replaces rather than stacks** — the previous dictionary is removed first, so a key the new theme is silent about reverts rather than keeping the old override.
- **A broken or deleted theme falls back without erasing the choice.** Fixing the file and restarting is enough to get it back; the failure is reported through `ConfigDiagnostics`, which became public for exactly this (a theme file is the same "user-editable file did not load, and why" case config files already had).

### 12.1 Two things in the kit would have frozen under a theme

Worth knowing because both were invisible until a test rendered them:

- **`MeterRow` computed its bar colour into a `BarBrush` property.** A computed brush can never follow a palette, so the load ramp would have stayed green/gold/red under every theme. It sets `:nominal`/`:busy`/`:hot` pseudo-classes now and the ramp lives in styles. `LunaPalette.LevelFor` keeps the thresholds in one place; `ForLoad` remains for code that genuinely cannot take a themed brush.
- **Window backgrounds were static `LunaPalette.Surface` assignments.** `ToolWindow` binds its own background instead, and the five migrated windows dropped the line.

`ToolWindow` *binds* rather than styles it, which is the non-obvious part: a plain `Style` selector lost to FluentTheme's own `Window` `ControlTheme` and the window stayed near-black. That was caught by a test asserting the rendered background, not by reasoning about priority order.

### 12.2 The CSS form

**`man theme` is the definition**, and it is the one a theme author should be reading: the complete token list, the element/state/part vocabulary, the properties, and what happens when a file will not load. This section is the argument behind it, not a second copy of it — the two cannot disagree, because `EmuSen.WiseMan/LunaP/ThemeVocabularyTests.cs` compares the page against `CssTheme`'s real allow-lists by **set equality in both directions**. A control added to the kit and left undocumented fails it; a token documented for a palette key that no longer resolves fails it too. That second direction is not hypothetical — it failed on the first run, against a `var(--luna-x)` placeholder in the page's own prose, which is exactly the class of thing hand-written reference text accumulates.

`Theme/CssTheme.cs` compiles a restricted CSS to the same `ResourceDictionary` §12 already merges, plus — for rule blocks — a `Styles` collection. It is a **format, not an engine**: there is no cascade, no specificity, no inheritance and no box model, and none is planned.

```css
/* Nocturne. */
:root {
  --luna-surface:        #12131A;
  --luna-section-header: #7AA2F7;
  --luna-mono-font:      "Fira Code", monospace;
  --luna-hint-font-size: 12;
}

section-header       { font-weight: normal; }
meter-row.hot .bar   { color: var(--luna-hot); }
console-pane .output { font-family: "Fira Code"; }
```

**`:root` is the palette.** `--luna-section-header` is the resource key `LunaSectionHeader` in kebab-case, and a colour defines *both* halves the palette spells (§2.1) — `LunaSectionHeaderColor` and the brush — because a theme that set only one would half-apply. Either spelling of the key is accepted and means the same declaration.

**The key's suffix decides the type, not the value's shape.** `…Size` is a number, `…Font` a font family, everything else a colour. Inferring from the value was the obvious alternative and is a coin flip: `monospace` and `gainsboro` are the same token shape. The cost of the rule is real and worth stating — a future palette key that is none of those three needs a line here, and until then it would be read as a colour and reported as unparsable.

**Colours may be written any CSS way**: `#RGB`, `#RRGGBB`, `#RGBA`, `#RRGGBBAA`, `rgb()`, `rgba()` (alpha `0`–`1`, unlike every other channel) and the named colours. The eight-digit form is the one place CSS and Avalonia genuinely disagree — `Color.Parse` reads `#AARRGGBB`, CSS puts alpha last — and **inside a `.css` file CSS wins**. A test pins both orders.

**Rule blocks are two allow-lists, deliberately.** A selector is `element[.state] [part]`: the element name is the control's own type name in kebab-case and is *derived from the type*, so a rename moves the CSS name with it instead of leaving a selector that silently matches nothing. States and parts are per-element; only `meter-row` has states today (`.nominal`/`.busy`/`.hot`, the pseudo-classes §12.1 introduced), and parts exist for `meter-row`, `filter-bar` and `console-pane`. Properties are `color`, `background`/`background-color`, `font-family`, `font-size`, `font-weight`, resolved against the *target's* type through `AvaloniaPropertyRegistry` — so `color` means `TextBlock.Foreground` on a text control and `TemplatedControl.Foreground` on a progress bar without the format needing to know they are different properties.

`var(--luna-hot)` compiles to a `DynamicResourceExtension`, so a rule that points at a token follows it. A rule that restated the colour instead would freeze exactly the way §12.1's computed brush did.

**Failure is two-tier, and the split is the design.** A *syntax* error refuses the whole file and leaves the previous theme in force, the same outcome a malformed `.axaml` theme already had: an unbalanced brace, a declaration with no colon, an unterminated comment, an at-rule, a nested rule. An *unknown* selector, state, part, property or unparsable value is reported through `ConfigDiagnostics` and skipped, and the rest of the theme applies — because a theme written against a later LunaP has to keep loading, and refusing the file would make every control added to the kit a breaking change for every theme on disk.

Reported line numbers are the file's own: comments are replaced by whitespace of the same shape rather than deleted, so a warning after a twenty-line comment block still names the right line. There is a test for that specifically, since it is the one part of a hand-written parser that silently drifts.

**Why a parser and not `AvaloniaRuntimeXamlLoader`.** Two arguments, and the second is the stronger one. Hand-editability is a stated goal for user-facing files here — the same argument `EmuSen_Stack.md` §4.1 makes for config staying JSON. And the XAML loader will instantiate *arbitrary Avalonia types* out of a file in `/etc`, where this parser structurally cannot: it emits brushes, doubles, font families and setters, or it emits nothing. The `.axaml` form keeps its capability and its exposure; the CSS form has neither.

**A negative result, recorded so nobody looks for the missing test.** The "this property does not apply to this target" guard is currently *unreachable*. Every property in the allow-list is registered on `TemplatedControl` or `TextBlock`, and every selectable target is one or the other, so no allow-listed property can miss. The check stays, because the two allow-lists are meant to grow independently and the first `Image` or `Panel` part will reach it — but no test covers it and none can be written today. A test asserting it was written, failed, and was deleted rather than weakened.

### 12.3 Mutating `Application.Styles` at runtime strips realized controls

This is the finding the CSS work turned up, and it had been sitting under the theme system since Phase 7 without being visible, because until rule blocks existed nothing ever added a style at runtime.

Measured, from a throwaway diagnostic run against a live `SectionHeader`:

```
before=#ff9cdcfe   mutated=White   reparented=#ff7aa2f7   clearedThenReparented=#ff9cdcfe
```

A header on screen shows its themed `#9CDCFE`. Adding a single `Style` to `Application.Styles` drops it to `White` — **not** to the new rule's colour and **not** back to the built-in one. The new style is not winning wrongly; the control has lost the LunaP style it already had, and removing the style again does not give it back. Controls realized *after* the mutation pick the new style up correctly, which is exactly why this is invisible at startup: `LunaApp.Configure` applies the saved theme before any window exists, so only a live theme *switch* can hit it.

Detaching and reattaching the window's content re-runs the style pass and fixes both directions. That is all `LunaTheme.Restyle(ContentControl)` is, and `ToolWindow` subscribes to `LunaTheme.StylesChanged` so every LunaP window does it for itself.

Three consequences worth carrying:

- **The event fires only when `Application.Styles` actually changed.** Every `.axaml` theme and most `.css` ones are palette-only, and those repaint through `DynamicResource` exactly as before at no cost. A test asserts the restyle count is zero for that case, so the cheap path cannot quietly become the expensive one.
- **A reattach is not free**: keyboard focus and scroll position inside the window are lost. That is acceptable for a deliberate, rare theme switch and would not be acceptable anywhere near a frame path. `ConsolePane` survives it only because it buffers output in the control rather than in the template part — §5.6's decision paying off a second time, for a reason that did not exist when it was made.
- **A window that is not a `ToolWindow` is not covered**, and the frontends still have five of those (§11). They need `LunaTheme.Restyle(window)` if a rule-block theme is switched while they are open. The uncovered case has its own test — asserting that a plain `Window` *does* lose its styling — so the hook is not deleted later as redundant.

---

## 14. The widgets

Four, all **wrapping** Avalonia's own controls rather than reimplementing them — the value added is the theme plus an API shaped like the calls the frontends actually make.

### 14.1 `LunaSwitch`, `Dropdown`, `Tabs`, and the style-key trap for the second time

All three wrappers pin `StyleKeyOverride` to their base type. §5.5 recorded this for `ButtonBar`, where the symptom was a control that rendered as nothing. **`LunaSwitch` is worse: `ToggleSwitch.OnApplyTemplate` does not degrade to blank, it throws on the missing `PART_MovingKnobs`.** The rule to carry forward: *anything here that derives from a stock Avalonia control needs its style key pinned to that control, and a test that finds a real template part.*

All three are covered by §28.1's sweep now, and one of them is the reason that sweep is honest about its limits: removing every `StyleKeyOverride` in the kit turns six of eight red, and `LunaSwitch`, `Dropdown` and `Tabs` are three of the six. The two that survive are `ActionButton` and `ActionToggle` (§26.11, §28.1).

`LunaSwitch` puts its `Label` into `OnContent` **and** `OffContent` rather than `Content`. That places the text beside the knob and keeps it there — the same single line the `CheckBox` it replaces already drew. `Content` stacks it above, and the stock On/Off captions say nothing the knob's own position does not.

`Dropdown.Fill(items, selected)` exists for a real bug from the other direction: setting `ItemsSource` then `SelectedItem` raises `SelectionChanged`, and `PreferencesWindow` already needed an `_initializing` flag so filling a list did not look like a user choice and get written straight back to config. `Fill` does that suppression once, and `Chose` fires only for a genuine pick.

`Tabs.Add(header, content)` and `RemoveFrom(index)` replace the "construct a `TabItem`, push it into `Items`" chore both frontends hand-wrote for their per-console tabs.

### 14.2 `FilterBar`

A search box, optionally preceded by a labelled facet dropdown. Two windows had built this independently, and it owns the detail one of them had a comment about:

> **It watches `TextBox.TextProperty`, not `TextChanged`** — only the property change reacts to a `Text` set that did not come from typing.

A test covers the programmatic case specifically, because that is the half a naive rewrite drops. `FilterBar.Matches` is the case-insensitive "empty matches everything" test both callers wanted, and `Submitted` is Enter in the search box.

The gap between facet and search sits on the *dropdown*, so it collapses with it: the library shows both, the cheat database only the search box, and neither gains a stray indent.

---

## 15. `Input/DefaultPadKeyMap` — moved to `EmuSen.Endymion`

**This no longer lives here.** It is `EmuSen.Endymion/Input/DefaultPadKeyMap.cs`, in the namespace `EmuSen.Endymion.Input`, and `EmuSen_Input.md` (in EmuSen) §4.3 is now the section that describes it. Everything below is kept because the reasoning that put it here is what decided where it went instead — see §19.3.

The keyboard scheme both frontends start from — arrows for the d-pad, `Z`/`X`/`A`/`S` for B/A/Y/X, `Q`/`W` for the shoulders, `Enter`/`RightShift` for Start/Select — plus the `Key → PadButton` reverse lookup they both need.

**It was spelled twice**, in Hotaru's `HotaruKeyMap` and Mistress's `ControllerKeyMap`, along with two copies of the reverse-lookup loop. A test asserted the two tables stayed equal, which is the shape of a problem being *guarded* rather than *fixed*.

**Why here and not in `EmuSen.Galaxia`, which is where the config models live.** Galaxia's csproj states the constraint plainly: it is a leaf with no `ProjectReference` and no `PackageReference`, because `EmuSen.DianaOS` references it and `EmuSen` references DianaOS — anything Galaxia depended on upward would close a cycle. So **Galaxia cannot name `Avalonia.Input.Key`**, and "the typed maps that use foreign enums keep their own homes" is that csproj's own conclusion. This table needs `Key` *and* `PadButton`, and LunaP was the one project that already referenced both Avalonia and Galaxia.

**And that is exactly why it had to leave.** "The one project that references both" was a statement about this repository, not about the toolkit's subject. A general Avalonia toolkit has no business naming a console gamepad button, and the argument above never claimed otherwise — it argued from what was convenient. Endymion already owns the mapping of physical input onto `PadButton` (`GamepadBindingMap`), already references Galaxia, and is already referenced by both frontends; it took one `Avalonia` base package reference to hold this file too, which is a smaller price than a toolkit that cannot leave the repository.

`Bindings()` returns a **fresh dictionary per call**, not a shared readonly instance: Mistress rebinds into its copy, and a shared instance would leak one frontend's edits into the other.

---

## 16. `Dashboards/` — moved to `EmuSen.Serenity`

**This no longer lives here either.** `CoretopWindow` is `EmuSen.Serenity/Dashboards/CoretopWindow.cs`, in the namespace `EmuSen.Serenity.Dashboards`, and the amendment below was withdrawn with it — §1's rule is back to its unamended form and is now stricter than it ever was: Avalonia and nothing else.

The amendment was not wrong. Cauldron is a dependency-free leaf and referencing it really did cost a consumer one small interfaces assembly rather than an emulator. What changed is the question being asked. It stopped being "does this reference hand the launcher a core" and became "can somebody outside this repository resolve it at all" — see §19. A reference to anything called EmuSen fails that second test whatever it costs.

Serenity took it because Serenity already *is* the core-agnostic Avalonia layer: it presents game frames while taking `(byte[] rgba, int w, int h)` rather than an `ICore`, which is the same standard a dashboard reading `ICoreTelemetry` is held to. It gained `EmuSen.LunaP` and `EmuSen.Cauldron`, both core-free, and `LeafAssemblyTests` pins the new list.

Everything below is the original reasoning, kept.

`Dashboards/` holds windows that are LunaP chrome plus an `ICoreTelemetry`. `CoretopWindow` is the only one today.

**This is the one place in the project allowed to name `EmuSen.Cauldron`**, and §1's rule is amended to permit it. The reasoning matters more than the amendment:

> §1's rule exists because *"the launcher's whole value is browsing a library with no core loaded, and one upward reference here hands it the whole emulator."* **`EmuSen.Cauldron` is a dependency-free leaf** — six files of read-only telemetry contracts, no `PackageReference`, no `ProjectReference`. Referencing it hands the launcher one small interfaces assembly and no core at all. The rule's *purpose* is untouched; only its letter changed.

The distinction that keeps this from becoming a slippery slope: **controls take plain data or a delegate, dashboards may take a contract.** A `MeterRow` still takes `(string, double, string)` and never a `DebugLoadInfo`. If a control wants an `ICoreTelemetry`, it is a control that should have taken plain data.

`EmuSen`, `EmuSen.DianaOS` and `EmuSen.Endymion` remain forbidden, and none of them is a leaf. A window needing `IDebugTarget` is not a dashboard — that interface stays in DianaOS (`EmuSen_Cauldron.md` (in EmuSen) §3.1) and such a window belongs in a frontend.

### 16.1 The wrong turn, recorded because the doc caused it

§11.3 named "a small third assembly referencing LunaP *and* Cauldron" as the only remaining option. **A whole project was created on that basis** — csproj, solution entry, references from both frontends and the test project, a codename claimed out of the Sailor Moon pool, its own reference doc — to hold one 137-line file. It was deleted the same day.

The prior question was never asked: *is Cauldron actually the kind of dependency this rule is about?* It is not. **An assembly per layering exception grows the project faster than an entry on an allow-list does**, and a codename is a permanent claim on a finite pool (`EmuSen_Core_Naming_Scheme.md` (in EmuSen) §11 carries the same lesson from the other side).

The general form, since a plan naming exactly one option is how this happened: **a doc that says "the remaining option is X" is recording what was considered, not what is possible.** Re-derive before building on it.

### 16.2 `CoretopWindow`

The GUI counterpart to DianaOS's own `coretop` (`man coretop`). A `PollingWindow` on the same 250 ms/4 Hz cadence the console version uses. Both frontends open the same class and differ only in how they reach it — Hotaru via `DebugWindows.ShowCoretopWindow` from `coretop -w`, Mistress via `MainWindow.OpenCoretopWindow` from the Hardware Dashboard menu item.

Two behaviours that are load-bearing and non-obvious, both pinned by tests:

- **`UpdateTarget(null)` is a real state**, not a defensive check — the window drops to "No ROM loaded." That text is a plain muted `TextBlock` and **not** a `HintText`, because it is the window's whole content in that state rather than an explanation under something. `HintText` is 11 pt by definition, and using it here measured 11,060 pixels wrong (§11.1).
- **The sprite bar stays on screen at zero when no core is loaded.** It is a fixed part of the layout, not a per-core meter. The natural assumption is that the empty state draws no `ProgressBar`; it draws exactly one.

The two frontends' `CoretopWindowTests` merged too. Mistress's was a strict superset — it covered the no-tile-memory case and the `UpdateTarget(null)` unload — so the merged file is its body, now in `EmuSen.WiseMan/Serenity/` beside the code it tests. What was genuinely dropped is the second pair of render baselines: with one window class there is one render to pin, and a second baseline under another name asserted the same pixels twice.

---

## 17. LunaP as a package, and the consumer outside this solution

`EmuSen.Pegasus` left this repository on 2026-08-09 for
<https://github.com/RedQuE3n/EmuSen.Pegasus>. It still builds its window from
LunaP, so LunaP is now packed and consumed as a NuGet package rather than as a
`ProjectReference`.

Three projects are packed at 0.1.0: `EmuSen.LunaP`, and the two leaves its
layering rule already allowed it to name, `EmuSen.Galaxia` and `EmuSen.Cauldron`.
The two leaves are packed only because a package's dependencies must themselves
be resolvable — a consumer outside this repository cannot follow a
`ProjectReference` into it.

**This does not relax the layering rule in the `.csproj`; it is the first thing
that has ever enforced it.** That rule — LunaP may reference Avalonia, Galaxia
and Cauldron and nothing else — exists so the eventual launcher can browse a
library with no core loaded. Until now it was a comment that a careless
`ProjectReference` could contradict. A package cannot reach up into a core at
all, so the constraint is now a property of the artifact rather than of
somebody's attention.

The practical consequence for work in this repository: **LunaP's public surface
has a consumer that does not appear in `EmuSen.sln`.** Renaming `LunaApp.Configure`,
`Windowing.ToolWindow`, the `Fluent.Ui` helpers, or the
`avares://EmuSen.LunaP/Theme/LunaTheme.axaml` URI will not break any build here
and will break Pegasus. That URI in particular is load-bearing across the
boundary: it resolves out of the packaged assembly's compiled resources, and a
consumer that fails to include it gets a window in which every control occupies
layout and draws nothing. That failure has shipped once — the account is now in
the Pegasus repository's `docs/Pegasus_Design.md` §11, having left with the
project, and `LunaTheme.axaml`'s own comment predicted it before it happened.
§5.5 and §14 are the in-repository cousins of the same failure, where an
untemplated control renders as nothing.

Two limitations, recorded now rather than discovered later:

- **The feed is a folder.** Pegasus's `NuGet.config` points at a `local-packages/`
  directory populated by `dotnet pack` here. GitHub Packages is the intended
  destination; nothing about the arrangement depends on which feed serves it,
  and the folder exists only because the packages have not been pushed yet.
- **Galaxia's catalogue schema does not travel.** `Library/Catalogue/*.sql` are
  `None` items copied to build output, which is not the same as packaged content,
  so the package carries the assembly and not the SQL. Pegasus never touches the
  catalogue, so this is free here. Anyone packaging Galaxia for a consumer that
  *does* want the catalogue must fix it first, and should not assume the 0.1.0
  package is a working example.

A version discipline is not yet established, and pretending otherwise would be
worse than saying so: 0.1.0 was chosen to start somewhere, and there is no
release process, no changelog and no automated republish. The first time LunaP
changes under Pegasus, that gap is what will be felt.

---

## 18. Where to look next

*Superseded on 2026-08-10 by §21, and this is the one section the move to a separate repository invalidated outright. Every document below lives in the EmuSen repository and none of them is in this one, so the "plan of record" this section points at is not reachable from here. It is kept rather than deleted because the five titles still say where the reasoning came from, which is worth something to anybody who has both checkouts. **For what to build next, read §21 instead.***

- **`EmuSen_LunaP_Gameplan.md` (in EmuSen)** — the plan of record: the full duplication audit (§1), the settled decisions (§2), Phases 2–6 (controls, window scaffolding, the fluent surface, harness support, migration), and the questions deliberately left open (§6).
- **`EmuSen_Launcher_Multicore_Gameplan.md` (in EmuSen)** — the launcher this project is eventually for. Its Phase 4 (theming) is why §2's palette is a resource dictionary rather than a set of constants.
- **`EmuSen_Cauldron.md` (in EmuSen)** — `ICoreTelemetry` and the snapshot/provider contract §16's dashboards consume; §3.1 for the Cauldron-versus-`IDebugTarget` split that keeps this reference safe.
- **`EmuSen_Core_Naming_Scheme.md` (in EmuSen) §11** — the name reservation and the `Luna`/`LunaP` collision note, plus the closing note on §16.1's near-miss.
- **`EmuSen_Input.md` (in EmuSen) §4.3** — `DefaultPadKeyMap`, in the project that owns it now.

## 19. Cutting the toolkit loose

`EmuSen.Pegasus` consumes LunaP from another repository already (§17), and it has turned out to be worth more than that: a general Avalonia toolkit — theme, chrome, remembered geometry, a fluent layout surface — is useful to people who will never run an emulator. The goal is its own repository, so it can be taken on its own terms.

That goal changes the question §16's amendment was answering. It stopped being *"does this reference hand a launcher a core"*, where `EmuSen.Cauldron` passed honestly, and became *"can somebody outside this repository resolve this at all"*, where nothing named EmuSen passes. `LunaP_references_nothing_of_EmuSen` is that question written as an assertion.

Three things carried the old references, and each went somewhere different:

| What | Reference | Where it went |
|---|---|---|
| `Windowing/WindowPlacementStore.cs`, `Theme/LunaTheme.cs` | `Galaxia.ConfigFile`, `ConfigStore`, `ConfigDiagnostics` | §19.1 — a seam, so the toolkit keeps the behaviour and stops naming the provider |
| `Input/DefaultPadKeyMap.cs` | `Galaxia.Input.PadButton` | `EmuSen.Endymion` — §15 |
| `Dashboards/CoretopWindow.cs` | `Cauldron.ICoreTelemetry` | `EmuSen.Serenity` — §16 |

**No new project was created, and §16.1 is why.** That section records a whole assembly being stood up — csproj, solution entry, references from three projects, a codename out of a finite pool — to hold one 137-line file, and deleted the same day. The same file was in play here. Two files needing a home is a stronger case than one, and it was still the wrong shape: each file has a subject, and each subject already had a project that owned it. Input mapping is Endymion's. A core-agnostic Avalonia window is Serenity's. Asking "what is this file *about*" got a better answer than asking "what does it *reference*".

### 19.1 The settings seam

`Settings/ISettingsStore.cs` is three methods — `Directory(category)`, `Load<T>`, `Save<T>` — and `Settings/LunaSettings.cs` holds the one the host has chosen, plus a `Diagnostics` sink for "this file would not load, and why".

`Settings/JsonSettingsStore.cs` is the default, and it is deliberately a near-copy of what Galaxia's `ConfigFile` does for these two files: indented JSON, comments and trailing commas tolerated, case-insensitive properties, and a full-write-then-rename so an interrupted save leaves the previous file intact rather than a truncated one. A toolkit that only worked when a host supplied a store would be a toolkit with a required setup step, so `LunaSettings.Store` resolves on first use to a store named after the entry assembly.

Two things Galaxia's `ConfigFile` does that this does not, both deliberate:

- **No migration from a legacy directory.** That is a fact about EmuSen's own history and belongs to the host, which can hand LunaP a store pointed wherever it likes.
- **No `SuggestingEnumConverterFactory`.** It exists for config files full of enum names; LunaP stores a `Dictionary<string, WindowPlacement>` and a one-string `ThemeChoice`, neither of which has an enum in it.

The two files themselves — `windows.json` and `luna.json` — are unchanged in name, location and content for EmuSen, because §19.2 points the store at the same directory.

### 19.2 What the frontends do about it

Two lines each, in `Program.cs`, beside the `ConfigDiagnostics.Sink` line that was already there:

    LunaSettings.Store = new JsonSettingsStore(ConfigStore.Directory);
    LunaSettings.Diagnostics = ConfigDiagnostics.Report;

That is the whole adapter, and its being two lines rather than a class is why no project was needed to hold it. EmuSen's files stay where EmuSen puts them and its diagnostics keep arriving on its own sink.

The three test fixtures that used to redirect `ConfigStore.OverrideDirectory` now assign `LunaSettings.Store` instead, which is a small improvement on top: they exercise the seam the toolkit actually ships rather than a provider it no longer knows about. `ThemeTests` and `CssThemeTests` capture through `LunaSettings.Diagnostics` for the same reason.

### 19.3 What is left before it can move

The references are gone and the guard holds. What has not been done: the repository itself, a `README` written for somebody who has never heard of EmuSen, and the package version stamping — LunaP, Cauldron, Galaxia and `EmuSen.Pegasus.Core` are all `0.1.0` and static, which is the root of the folder-feed trap `EmuSen.Chariot`'s `NuGet.config` records. Nothing in this section is blocked on that; it is the next thing.

---

## 20. The move

LunaP is its own repository as of this commit: <https://github.com/RedQuE3n/EmuSen.LunaP>.

**The history came with it.** Sixteen commits touch this project, from *"Start LunaP: one theme for both frontends"* to the one that cut the last EmuSen reference, and they are replayed here as their own line rather than squashed into an initial commit. Neither `git subtree` nor `git filter-branch` ships in the git build this was done on, so the replay is plumbing — `commit-tree` over each commit's `EmuSen.LunaP` subtree, skipping the commits where that subtree did not change, preserving author, committer and both dates. The result is 16 commits, which is the same 16.

### 20.1 What the repository looks like

    src/EmuSen.LunaP/        the toolkit
    tests/EmuSen.LunaP.Tests/  headless: controls, windowing, themes, render passes
    docs/LunaP.md            this file
    LunaP.slnx

The package id stays `EmuSen.LunaP` and the namespace stays `EmuSen.LunaP`. Renaming both would have been tidier for a reader who has never heard of EmuSen and would have broken every consumer for a cosmetic gain; the name is a Sailor Moon reference either way and carries no dependency with it.

**Version 0.2.0, and the bump is real.** 0.1.0 carried `Dashboards/` and `Input/`, which are gone, and did not carry `Settings/`, which is new — a consumer moving up has work to do. It also settles the folder-feed trap that made 0.1.0 painful: NuGet caches by id *and* version, so repacking at the same version does not propagate, and a build fails on code that was just written as though it did not exist.

### 20.2 The tests came too, minus one

The suite lived in `EmuSen.WiseMan`, EmuSen's test project, and 132 of its tests were about this toolkit. They are `tests/EmuSen.LunaP.Tests/` now, and they needed two fixtures rebuilt rather than moved:

- **`UiTest`** dispatched onto a headless session, captured a window, and asserted it was actually laid out. It reached `EmuSen.Common.Imaging.FrameHash` for a frame hash and `EmuSen.Hotaru.Imaging.FrameImageWriter` to dump a PNG — an emulator's own imaging code, for a hash and a file write. The hash is FNV-1a inline; the dump is Avalonia's own encoder. Neither was worth a dependency.
- **`TestAppBuilder`** built the headless application. It is the same shape, loading the same `avares://EmuSen.LunaP/Theme/LunaTheme.axaml` — which is the point of §3.1: a headless pass that misses the real theme asserts over untemplated controls and passes green.

**One test deliberately stayed behind.** `ThemeVocabularyTests` checks that every key in `Palette.axaml` is documented as a token in EmuSen's own `man theme` page. That asserts *EmuSen's documentation* stays in step with this project, which is EmuSen's business to keep and not this project's to enforce. It still runs there, against the package.

**And CI immediately found a race that had been sitting in the suite unnoticed.** `A_malformed_css_theme_leaves_the_previous_one_in_force` passed on the machine it was written on and failed on the first run against a two-core GitHub runner, on the assertion that LunaP had *reported* why the theme would not load.

The cause is that xunit parallelises across test classes, and three fixtures here configure statics that are global to the one headless application every test dispatches onto: `LunaSettings.Store`, `LunaSettings.Diagnostics`, and the applied theme's resource dictionary. `ThemeTests`' constructor takes the diagnostics hook away from `CssThemeTests` between that test's write and its assert, and the report goes to a lambda nobody is reading any more.

It is worth being exact about what was wrong, because the hazard predates the move: this suite has *always* been single-threaded in the only place that matters — `HeadlessUnitTestSession` owns one dispatcher, and every test body runs on it. What was parallel was the constructors and the assertions around those bodies, which is precisely where the shared statics are set and read. Parallelism was buying nothing across a four-second suite and was costing correctness, so the assembly now declares `DisableTestParallelization`.

The general form is worth keeping: **a suite that funnels through one shared resource is a suite that is already serial, and leaving it nominally parallel only means the serialisation is somewhere you did not choose.** It also says something for running CI on hardware unlike your own — this had been green locally every time.

### 20.3 What EmuSen does now

The four projects that took a `ProjectReference` take a `PackageReference` instead, resolved from a folder feed until this is on a real one — the identical arrangement `EmuSen.Pegasus` has used from the start, and for the identical reason: a consumer outside this repository cannot resolve a `ProjectReference`.

The consequence worth stating plainly: **iterating on the toolkit while working on EmuSen now costs a `dotnet pack` and a version bump.** That is the price of the split and it is a real one. `LeafAssemblyTests` still asserts what LunaP carries, now against the package assembly rather than a project in the same solution, and it still means the same thing.

---

## 21. Where the toolkit is thin

Everything above this section is a record of what happened. This one is the only forward-looking section in the document, and it is written to a rule that keeps it from becoming a wish list.

**The rule: an item earns a place here by having been written more than once, by somebody who was not trying to write a toolkit.** LunaP was never designed; it was extracted, and §1 is the shape that extraction left behind. The honest way to find the next piece is the same way the first ones were found — read what the consumers still hand-write, and count. So every item below carries its sites, and the count *is* the argument. Something hand-rolled once is a hazard note, not a roadmap entry; there is one exception and it is marked as such, because pretending the evidence pattern held there would be worse than admitting it did not.

The survey covering this section read `EmuSen.Pegasus`, `EmuSen.Mistress`, `EmuSen.Hotaru` and `EmuSen.Serenity`. `EmuSen.Chariot`, the fourth consumer of the Pegasus core, has no UI at all and contributes nothing.

One warning inherited from §16.1, which applies to this section more than to any other in the document: **a doc that says "the remaining option is X" is recording what was considered, not what is possible. Re-derive before building on it.** A roadmap is exactly the kind of writing that gets treated as a decision after enough time passes.

### 21.1 State and threading, which is where the duplication actually is

*Built, and recorded in §22 — which also carries a defect that building it turned up in the three implementations it replaces. A3 below is the one item deliberately left for §21.2; §22.4 says why.*

The largest count in the survey, and none of it needs anything but Avalonia.

**Latest-wins posting to the UI thread, written three times and byte-identical each time.** `EmuSen.Mistress/Views/MainWindow.axaml.cs:109,1205`, `EmuSen.Hotaru/Views/GameWindow.axaml.cs:209,838` and `EmuSen.Serenity/FramePresenter.cs:32,76` each hold an `int _presentScheduled`, swap the newest value in with `Interlocked.Exchange`, and post exactly one dispatcher callback with `Interlocked.CompareExchange(ref _presentScheduled, 1, 0) == 0`. Around ten further sites post raw. The toolkit already owns this correctly in exactly one place — `Windowing/WindowSlot.cs:44 OnUiThread`, which runs inline when it is already on the thread and posts otherwise — and that is precisely the one nobody rewrote, which is the whole argument in one observation.

The trap to design around is already recorded. §11.2 found that `RefreshIfOpen` marshals to the UI thread, *"which is correct for every other caller and exactly wrong for this one"* — the drain that must run on the thread owning the core. **A marshalling seam that cannot be opted out of would re-create that bug at the toolkit level, where it would be harder to see.**

**The "do not echo my own write back" flag, written six times.** `Pegasus/Shell.fs:72-73` carries two (`applying`, `syncingSelection`) with the comment that otherwise *"the list and the editor drive each other forever"*; `ActiveCheatsWindow.axaml.cs:37` has `_syncing` and `:183` has `_built`; `InputSettingsWindow.axaml.cs:39` has `_initialized`; `PreferencesWindow.cs:20` has `_initializing`. Every one guards the same thing: a handler firing during a programmatic update. And the kit already solves it for exactly one control — `Controls/Widgets.cs:46-55`, where `Dropdown.Fill` sets `_filling` around an items-and-selection swap so `Chose` does not fire for the reset. That solution was written for a dropdown and never asked whether it was about dropdowns. It is not.

**Rebuild the list, then go and find the selection again, written three times.** `Pegasus/Buddies.fs:63-76` (*"Losing it every time somebody else signs in would make the Open button unusable"*), `Pegasus/Shell.fs:75-88`, and `ActiveCheatsWindow.axaml.cs:196-201`. This pairs with the typed list in §21.2 — done together, one control absorbs both.

**Debouncing, which exists nowhere and is wanted in at least two places.** `MainWindow.axaml.cs:838` re-filters a ROM library on every keystroke and `CheatDatabaseWindow.axaml.cs:90` re-queries an on-disk cheat database on every keystroke. `Controls/FilterBar.cs` is where this belongs, and §14.2's existing care about *which* notification to watch — `TextBox.TextProperty` rather than `TextChanged`, so a programmatic `Text` set does not read as typing — is the precedent for how to do it without introducing the §21.1 flag problem all over again.

**Deliberately excluded, and recorded so it is not proposed later:** the emulation-thread lifecycle at `MainWindow.axaml.cs:89,941`, the two copies of `SleepUntil` spin-waiting on `Thread.SpinWait(100)` (`MainWindow.axaml.cs:1190`, `GameWindow.axaml.cs:594`), and `GameWindow`'s ten `volatile bool _request*` flags standing in for a command queue. All genuinely duplicated. None of it belongs here, on exactly the reasoning §15 used for `DefaultPadKeyMap`: **a frame pacer is a fact about emulation, not about chrome.** The count is not the only test an item has to pass.

### 21.2 The control kit

**A list that takes a type, because five sites take a string and keep a shadow array.** `MainWindow.axaml.cs:47` says it outright — *"Parallel to LibraryList's item strings, which are titles only"* — and `:857` projects entries to a formatted string; `CheatDatabaseWindow.axaml.cs:116` builds `$"{s.System}  ({s.Count})"` and then `:128` has to take the label back apart to recover the name it already had; `RomBrowserWindow.axaml.cs:336`, `Pegasus/Shell.fs:79` and `Pegasus/Buddies.fs:66` do the same in smaller ways. **Parsing a display string to recover a model field is the shape of a missing control.** A list taking `IEnumerable<T>`, a projection and a key selector removes the shadow array and the selection-restore dance together — and the row to generalise from already exists, hand-built at `ActiveCheatsWindow.axaml:62-81`: a checkbox, a 34-pixel badge, an ellipsised description, a monospace detail.

Note what this control must not become. §1's rule is that a control takes plain data or a delegate; a typed list takes a *projection*, not an interface the consumer implements. The moment it wants `IListItem` it has stopped being a LunaP control.

**An empty state, and the clearest single piece of evidence in the whole survey.** `EmuSen.Serenity/Dashboards/CoretopWindow.cs:21` declares a plain `TextBlock` with the comment *"Muted but body-sized, not a HintText: this one is the window's whole content when no core is loaded"*. That is a consumer stating, in a comment, that the kit's nearest seam was the wrong one. `MainWindow.axaml.cs:861-882` and `Pegasus/SignIn.fs:42-45` build the same thing by hand, the latter noting that otherwise *"an empty box with no explanation reads as something broken"*.

**The status-and-buttons footer, five times.** `MainWindow.axaml:65`, `ActiveCheatsWindow.axaml:45`, `CheatDatabaseWindow.axaml:23`, `InputSettingsWindow.axaml:19` and `VstopWindow.cs:34` all lay out `ColumnDefinitions="*,Auto"` with a status message on the left and a run of buttons on the right. The kit ships `StatusBar` and `ButtonBar` as separate controls and never the arrangement they are always in — and `luna:StatusBar` is instantiated by nobody at all, which is the more interesting half of the finding: **a control nobody uses and a layout everybody rewrites are usually the same bug.**

**A card surface, because three sites escape the palette to get one.** `ActiveCheatsWindow.axaml:19`, `CheatDatabaseWindow.axaml:46` and `MainWindow.axaml:64` all paint a `Border` with `{DynamicResource SystemChromeLowColor}` — FluentTheme's token, not LunaP's. §4's centralisation check exists to catch exactly this, and here the colour is not hard-coded but sourced from the wrong dictionary, which the grep in §4 does not see. Adding `LunaCard` (or admitting `LunaInputSurface` already means this) is a smaller change than it looks; noticing it required reading for the *absence* of a Luna key rather than the presence of a hex literal.

**Transient notification.** `StatusText.Text = …` is assigned seventeen times in `MainWindow.axaml.cs` and sixteen in `ActiveCheatsWindow.axaml.cs`, carrying results (*"Applied 12 cheat(s)"*), standing state (*"No ROM loaded"*) and failures (*"Save State failed: …"*) down one channel with no severity and no dismissal. Meanwhile `Dialogs.ConfirmAsync` and `ErrorAsync` are called **nowhere in any consumer** — verified by grep — and a destructive delete is hand-rolled as a two-click button at `CheatDatabaseWindow.axaml.cs:220`, where the button relabels itself `$"Delete {n} system(s)?"` and its second click consumes the plan its first click made. Before designing a toast, the question to answer is which of those two facts is the actual gap: **the kit may be missing a notification surface, or it may only be missing a reason for anybody to find the dialogs it already has.** §7 claims the gallery is how the kit is discovered, and the gallery does not show a dialog.

**Smaller, each with its site:** an in-app chooser (`RomBrowserWindow` is 81 lines whose entire job is to show a list and return the selection or null, consumed at `MainWindow.axaml.cs:339`); a `MeterRow` that accepts `n` out of `max` rather than only a percentage, which is why `CoretopWindow.cs:24,48` rebuilds one out of a bare `ProgressBar` and a `TextBlock`; busy-and-cancellable state for long work, absent at `CheatDatabaseWindow.axaml.cs:260-288` where a multi-megabyte download is managed by disabling two buttons in a `try` and re-enabling them in a `finally`; and the toggle-with-an-explanation-underneath composite, currently a magic negative margin at `DebugSettingsWindow.cs:106` (`Ui.Hint(…).Margin(24, -2, 0, 4)`) copied into XAML at `InputSettingsWindow.axaml:55,66,75`.

**The fluent surface has two shaped holes.** `Fluent/Ui.cs:38` offers `Cols` and there is no `Rows`, so header-and-body tables keep their two column strings in step by hand through a shared `const` (`InputSettingsWindow.axaml.cs:109,135,182,202`). And `Ui.Section` takes exactly one child, which is why section headings are written as bold `TextBlock`s in eight places rather than as `SectionHeader` — `DebugSettingsWindow.cs:97`, `InputSettingsWindow.axaml.cs:127`, `InputSettingsWindow.axaml:38,49,70`, `ActiveCheatsWindow.axaml:28,42`, `CheatDatabaseWindow.axaml:55,60`. There is also no `GridSplitter` anywhere in any of the three repositories, and `CheatDatabaseWindow.axaml:52` fakes a split pane as `ColumnDefinitions="2*,12,3*"` with the `12` as a spacer column.

**Counter-evidence, recorded because it complicates the section's own method:** `Pegasus/Shell.fs:189-193` calls `DockPanel.SetDock` four times in a row, and `Fluent/LayoutExtensions.cs:84` has shipped `.Dock(side)` the whole time. Nothing was missing there. **Not every hand-roll is a gap in the kit; some are a gap in the gallery, or in the README**, and a roadmap built only by counting will propose code where documentation was the answer.

### 21.3 A testing package, and the hazard it would ship

The strongest evidence in the survey, and the only finding that crosses a repository boundary.

`EmuSen.WiseMan/LunaP/VisualQuery.cs` is **byte-identical** to `tests/EmuSen.LunaP.Tests/VisualQuery.cs` except for line 7, the namespace — confirmed by `diff`, which reports that one line and nothing else. `EmuSen.WiseMan/Fixtures/UiTest.cs:16` re-declares `RenderedFrame`. And Pegasus, unable to reference either, wrote a third harness in F# at `tests/EmuSen.Pegasus.Tests/Headless.fs` with its own descendant queries and its own headless bootstrap. The cause is one line: `tests/EmuSen.LunaP.Tests/EmuSen.LunaP.Tests.csproj:16` is `<IsPackable>false</IsPackable>`, so there has never been another way to obtain `AssertLaidOut`.

That matters more than the duplication does. §3.1 records that a headless suite without the real theme asserts over untemplated controls and **passes green**, and §17 records that a consumer omitting the `avares://` include gets *"a window in which every control occupies layout and draws nothing"* — a failure that has shipped once. `AssertLaidOut` is the guard against precisely that, and it is currently the one part of this toolkit a consumer has to retype by hand to get.

**The decision: a second package, `EmuSen.LunaP.Testing`, and §1's rule does not bend.** A test harness needs xunit and `Avalonia.Headless`, which `EmuSen.LunaP` may not name. The rule as written in `EmuSen.LunaP.csproj` is about *this assembly* — *"a toolkit that names its first consumer is a toolkit only that consumer can use"* — and a sibling package that a consumer references from its test project only does not hand anybody the thing §1 was protecting against. Stating it explicitly because a reader arriving at this section will, correctly, check whether the headline claim just quietly loosened.

**And the package would ship a hazard, so it has to ship the instructions too.** §20.2 records the race CI found: xunit parallelises across test classes, three fixtures configure statics global to the one headless application, and `ThemeTests`' constructor takes the diagnostics hook away from `CssThemeTests` mid-assertion. **That hazard is not a property of this suite.** It is a property of `UiTest`'s single dispatcher plus `LunaSettings`' process-global statics — which means every consumer suite inherits it the moment it uses a packaged harness, and inherits it in the worst available form: green on the developer's machine, red on a two-core runner.

The corroboration is already on disk and predates the finding. Pegasus's hand-rolled harness declares `[<Xunit.CollectionDefinition "Avalonia">]` at `tests/EmuSen.Pegasus.Tests/Headless.fs:118` — **a second, independent discovery of the same constraint, in a different language, before the package it would belong to exists.** Two consumers reaching the same conclusion separately is the same kind of evidence as three copies of the same file, and it says the serialisation requirement is part of the contract rather than a footnote about this repository's tests.

So the package's documentation is part of its design, not an afterthought to it: which statics are global, why §19.1 made them so, and that a consuming suite must disable test parallelisation. Two questions stay genuinely open. Whether the assertions should throw xunit exceptions or return a result the caller asserts on — the second costs the xunit reference and costs ergonomics, and it is not obvious which matters more to somebody who is not using xunit. And whether the baseline machinery travels at all, given §10.2's finding that reference images are *"an artefact of one machine's font rendering"*; `AssertLaidOut` is portable in a way `AssertStable` and the `.frame` comparison are not.

### 21.4 Hygiene

**The palette has no way to say "this went wrong".** Six sites hard-code a status colour: `Pegasus/SignIn.fs:49` and `Pegasus/Buddies.fs:53` both set `Colors.IndianRed` from a copied `fail`/`note` pair, `Pegasus/Shell.fs:103,104,111` maps connection state onto `SeaGreen`, `IndianRed` and `Goldenrod`, and `InputSettingsWindow.axaml.cs:393` uses `Brushes.OrangeRed` for a binding conflict. §4's centralisation check — *"that is the check to re-run before claiming the palette is still centralised"* — returns all of these today.

§2.1 already argued this side of the question without meaning to. It refused to fold the input-conflict highlight into `LunaHot` because *"'this binding collides with another' is not 'this subsystem is at 85% load', and giving them one key would encode a relationship that does not exist."* **That is an argument for semantic tokens, not against them:** the reason those colours must not reuse the load ramp is exactly the reason they need keys of their own. What the load ramp is to a meter, an error/success/info triple would be to a message.

**`ConsolePane` has two defects, and they ship today.** `Controls/ConsolePane.cs:57` appends with `_text = _text + "\n" + text`, which reallocates the entire buffer per line and has no cap, and `:59` calls `ScrollToEnd()` unconditionally, so a reader scrolled back through output is dragged to the bottom by the next line that arrives. Both console windows depend on this control entirely (`DianaOSConsoleWindow.cs:27`, `DianaOSShellWindow.cs:24`). These are behaviours, not gaps — they belong in this section as defects with a reproduction, and the second one is the sort of thing that is invisible in a test and obvious within ten seconds of real use.

**Accessibility is the exception to this section's rule, and it is worth being blunt about why.** `ToolTip`, `AutomationProperties`, `TabIndex` and `IsTabStop` appear **zero times** across Pegasus, all three EmuSen frontends, *and this toolkit's own `src/`*. There is no hand-roll to count because nobody built it anywhere, so the method that produced every other item in this section produces nothing here. Focus is managed in exactly one place in the entire corpus (`Pegasus/SignIn.fs:82`). For a toolkit whose whole subject is chrome, that is the largest blank area on the map, and the absence of evidence is the finding rather than a reason to skip it.

> **Built, in §24 — and this paragraph was wrong about one thing worth correcting.** "No hand-roll to count" was true and led to the wrong conclusion, which was that this item had no evidence available. It had none of *this section's* kind; asking Avalonia's automation layer what it thought each control was produced a table immediately, and that table said something worse than missing labels. **Nine of the toolkit's controls were not in the automation tree at all.** §24.1 has the measurement. The lesson generalises past accessibility: when the house method finds nothing, that is a fact about the method.

**Keyboard handling has three answers to one question.** "Enter means submit" is implemented at `MainWindow.axaml.cs:896`, at `Pegasus/SignIn.fs:87`, and as `FilterBar.Submitted`. The tunnelled global handler is written twice with the same comment about focus navigation eating the arrows (`MainWindow.axaml.cs:218`, `InputSettingsWindow.axaml.cs:87`), and `MainWindow.axaml.cs:223` holds `TypingIntoATextField`, a completely generic guard living in an emulator. §15 draws the line and it is a clean one here: the F-key map at `GameWindow.axaml.cs:350` stays out, because it names a console's controls; `TypingIntoATextField` does not name anything.

**Six windows bypass `ToolWindow`, and `WindowKey` appears zero times in the EmuSen frontends.** `MainWindow.axaml.cs:39`, `ActiveCheatsWindow.axaml.cs:21`, `CheatDatabaseWindow.axaml.cs:19`, `RomBrowserWindow.axaml.cs:315`, `GameWindow.axaml.cs:91`, and `InputSettingsWindow.axaml.cs:22` — the last written as `Avalonia.Controls.Window`, fully qualified, which is a deliberate statement rather than an oversight. They lose the `LunaSurface` background, the `StylesChanged` restyle hook, Escape handling and placement memory in one go, which is the uncovered case §12.3 already flags. Two of them hand-roll a `DispatcherTimer` (`InputSettingsWindow.axaml.cs:35-36,92,409`, `MainWindow.axaml.cs:735`) while `Windowing/PollingWindow.cs` exists and its own comment says *"Five windows hand-rolled this; none of them stopped while hidden."* **The honest reading is that this is migration work in EmuSen and not toolkit work here** — but it is recorded here because a toolkit whose base class is refused by six of its consumer's windows should find out why before adding a seventh feature to it.

**The layering guard did not move, and this repository does not enforce its own headline rule.** `LunaP_references_nothing_of_EmuSen` lives in `EmuSen.WiseMan/Common/LeafAssemblyTests.cs` and still runs there against the package. Nothing in `tests/EmuSen.LunaP.Tests/` asserts it; the only statement of the rule inside this repository is a comment in `EmuSen.LunaP.csproj`. §10.4 records how much that guard was worth — *"§1's rule was documentation until this phase"* — and bringing it here means bringing its blind spot with it, stated rather than rediscovered: the C# compiler elides a reference used only for `const` values, so **a project can depend on another project's constants and this test cannot see it.**

**Version discipline, which §17 called an open gap, is now mostly closed.** The release runs off a tag (`.github/workflows/publish.yml`), the published version comes from that tag rather than the csproj so it cannot be written in two places, CI builds and tests on every push, and publishing uses NuGet Trusted Publishing so there is no stored key at all. `EmuSen.LunaP` 0.2.0 is on nuget.org and Pegasus restores it from there. What is left is smaller and should be carried at its real size: **there is still no changelog**, and `--skip-duplicate` means a mis-tagged publish fails silently rather than loudly. The question the tracks above raise is what a 0.3.0 owes a consumer who is on 0.2.0 — and that is a changelog question, not a release-plumbing one.

### 21.5 What none of this may do

Collected here because a roadmap is the likeliest place in a project for a rule to loosen without anybody deciding to loosen it.

- **Avalonia and nothing else**, for this assembly. §21.3's second package is the one considered exception in this section, and it is argued rather than assumed.
- **Plain data or a delegate, never a domain type** (§1). The typed list takes a projection; the moment it takes an interface, it is the wrong control.
- **Nothing that names a console, a core, or a gamepad button** (§15).
- **Every control gets a test that finds a real template part**, and its style key pinned if it derives from a stock control — §5.5 and §14.1, which record the same trap twice, the second time in the form where `ToggleSwitch.OnApplyTemplate` throws rather than degrading to blank.
- **New guards fail on purpose before being trusted** — §10.4, §8.2, and §10.2, the last verified by moving `LunaSectionHeader` one channel and watching 904 pixels differ.
- **§16.1 is the cautionary tale.** A whole assembly was stood up — csproj, solution entry, references from three projects, a codename out of a finite pool, its own reference document — to hold one 137-line file, and deleted the same day. Ask what a thing is *about*, not what it *references*.

There is also a cost that did not exist when the earlier sections were written, and every item above pays it. **`ThemeVocabularyTests` stayed in EmuSen** (§20.2) and compares `man theme` against `CssTheme`'s allow-lists by set equality *in both directions*. So a new control in §21.2, or a new palette token in §21.4, **turns EmuSen's suite red until EmuSen's `man theme` page is updated** — a failing test in a repository the change never touched. §20.2 chose to leave that test behind deliberately and gave a good reason; this is the invoice for it, and it should be expected rather than debugged.

### 21.6 Corrections this section makes

**§18 is superseded**, and is the one section the move invalidated outright. It is annotated in place.

**§17 is stale in four ways**, which matters because it is the section anybody asking how to consume LunaP will read. It says the packages are all 0.1.0 — the csproj says 0.2.0, and the published version now comes from the tag regardless. It says there is *"no release process, no changelog and no automated republish"* — two of those three are now false. It says the folder feed *"exists only because the packages have not been pushed yet"* — they have been. And it names **GitHub Packages as the intended destination**, where the package actually went to nuget.org. That last one is a changed decision rather than drift, and the reason is worth keeping: a public feed that needs no authentication to restore from is the one that matches what §19 was for, which was somebody outside this project being able to use the toolkit at all. §20.3's *"resolved from a folder feed until this is on a real one"* is stale for the same reason.

**The citations from source are in far worse shape than a first look suggested, and this correction corrects an earlier draft of this very section.** That draft said "two source files still cite the pre-move document". The real figures, counted rather than sampled: **68 citation sites across 43 files**, of which **55 name `EmuSen_LunaP.md`** — a document that does not exist in this repository under that name.

The filename was the smaller half. **Fourteen citation sites pointed at a section number that did not exist at all**, and a further four numbers — §12.1, §12.2, §12.3 and §13.2 — each resolved to *two different headings*, so a bare citation to any of them was undecidable. §13.2 was either the baseline machinery or `FilterBar`, and the two have nothing to do with each other.

**The cause, and it is the useful part: the `##` chapter numbers and the `###` subsection numbers came from different eras, and the code had stayed with the chapters.** `Windowing/` cited §8.1 to §8.4 and sits under `## 8. The windowing layer`, whose subsections were numbered §9.1 to §9.4 — so the *code* agreed with the parent heading and the document's own subsections did not. Likewise the widgets: code cited §14.x, the chapter was `## 13`, and there was no §14 in the document at all. The drift was in the document, in every case but one.

**It is fixed, and here is exactly what moved**, so that anybody holding an older checkout or an outside citation can convert:

| Chapter | Subsections were | Subsections are |
|---|---|---|
| `## 8. The windowing layer` | §9.1 – §9.4 | **§8.1 – §8.4** |
| `## 9. The fluent surface` | §11.1 | **§9.1** |
| `## 10. The test harness` | §13.1 – §13.4 | **§10.1 – §10.4** |
| `## 11. The migration` | §12.1 – §12.4 | **§11.1 – §11.4** |
| `## 12. Themes` | §12.1 – §12.3 | unchanged |
| `## 13. The widgets` → **`## 14`** | §13.1, §13.2 | **§14.1, §14.2** |

Moving the widgets to §14 shifts nothing after it, because §15 existed already. It does **not** close the gap in the chapter sequence, and saying it did would be wrong: the chapters now run 1–12 and 14–21, so the hole moved from §14 to §13 rather than disappearing. Closing it properly would mean renumbering §14 through §21 down by one, which would break every citation to §15, §17, §19 and §20 in this document, in the README, and in anything outside this repository that has cited them — a much larger blast radius than an empty number is worth. **The gap is left where it is, and it records that a chapter was removed at some point.** That is information, not damage.

What the pass does guarantee: every heading agrees with its parent, no number denotes two sections, and all 68 citations from source resolve — verified by extracting each one and matching it against the heading list, rather than by reading and believing.

**Five citations were wrong in a way no renumbering would have fixed**, and they were corrected in the same pass. `Controls/MeterRow.cs:47` and `Theme/Controls.axaml:52` cited §13.2 for the load ramp living in styles rather than in a computed brush, which is §12.1. `Theme/LunaTheme.cs:19` and `tests/ThemeTests.cs:13` cited §13 for themes, which are §12. And `Windowing/Dialogs.cs:10` cited §9.4 — the one place in the whole codebase that had followed the document's old subsection numbering instead of its chapters, which is why it was the last one left standing after everything else resolved.

Two things worth keeping from this. **`tests/UiTest.cs` was the file corrected during the move** (§20.2) and it came out with the new filename and a section number that resolved to nothing — the tidy-up introducing the defect it was tidying, which is an argument for checking that a citation resolves rather than that it looks current. And the reason this was worth doing at all is the rule the project already holds: a comment that has drifted from what it points at is worse than no comment, **because it is believed**. Fifty-five of these named a file that has not existed under that name since the move, and the suite was green throughout — nothing in a build or a test run can see any of it.

---

## 22. `Threading/` — the first track built

§21.1 argued that the largest duplication in the consumers was not controls but plumbing, and named four things. Three of them are built and one is deliberately not; this section records what they are, the defect building them turned up, and what each guard was made to fail against.

`Threading/` is four small types, each one thing, and all of them take a delegate and name nothing:

| Type | Replaces | Sites it was counted at |
|---|---|---|
| `UiThread` | `Dispatcher.UIThread.CheckAccess()`-then-post, hand-written | §21.1 |
| `Latest<T>` | the latest-wins frame poster, written three times byte-identically | §21.1 |
| `Suppressor` | the "do not echo my own write back" flag, written six times | §21.1 |
| `Debounce` | nothing — no consumer has one, and two want one | §21.1 |

### 22.1 `Latest<T>`, and the defect all three copies share

The three implementations this generalises are byte-identical, and **all three can strand a value.** They clear the scheduled flag *after* the hand-off:

```csharp
finally
{
    // Reset after the hand-off, so an in-flight Present keeps coalescing
    Interlocked.Exchange(ref _presentScheduled, 0);
}
```

A value offered while the UI thread is inside the callback sets `_pending`, then fails to schedule because the flag still reads 1. The flag then goes to 0 with nothing queued, and that value sits there until something else arrives to push it out.

**At sixty frames a second this is invisible**, which is why it survived three copyings: the next frame lands 16 ms later and carries the fix with it. It shows when the stream *stops*. Pause an emulator and the frame at risk is the last one — the one somebody is about to sit and look at.

The fix is to clear before presenting rather than after, so a late offer can always schedule again, plus a re-check afterwards for an offer that landed between the two interlocked operations. `Latest_does_not_strand_a_value_offered_while_presenting` pins it, and **the test was verified by reverting `Drain` to the copied ordering**, which failed that test and only that test.

This is a correction to something outside this repository and is recorded rather than fixed here: EmuSen's `FramePresenter`, `MainWindow` and `GameWindow` still carry the original. Whether it is worth changing there is EmuSen's call — at a running frame rate it is genuinely unobservable.

### 22.2 `Suppressor` is a counter, and that is the point

Every hand-rolled version of this guard is a `bool` assigned true and then false. That is correct exactly once. A refresh that calls a helper that refreshes something else re-enables notifications halfway through the outer update, and the code reads as though it is protected — which is worse than having no guard, because nobody looks again.

Depth counting makes nesting correct. `Suppressor_nests` pins it, verified by making the inner dispose reset the depth to zero — the bool semantics — which failed that test alone.

`Dropdown.Fill` moved onto it. That is where the kit had already solved this privately, for one control, in a way that was never about dropdowns.

Not thread-safe, deliberately: this guards UI event handlers, and making it interlocked would invite its use as a general mutual-exclusion primitive, which it is not.

### 22.3 `Debounce`, and the one property that had to default to nothing

`FilterBar.SearchDelay` defaults to `TimeSpan.Zero` and zero means synchronous, not "a zero-length timer". A `DispatcherTimer` with a zero interval still defers to the next dispatcher pass, which would quietly turn every existing consumer's synchronous `Changed` into one that arrives a frame late. **A control with consumers outside this repository does not get a new default.**

Enter flushes anything pending before raising `Submitted`, so a caller that filters on one event and acts on the other cannot act on the results of the previous keystroke. `FilterBar_enter_brings_a_pending_change_forward` asserts the ordering rather than merely that both fired.

The facet dropdown is never debounced. Picking from a dropdown is one deliberate act, not a stream of half-formed input, and delaying it would read as the application being slow.

**No test here races a clock.** §8.2 settled that for the polling tests and the same argument applies: the debounce tests use a thirty-second interval precisely so the timer cannot fire, and assert on pending state, on `Flush` and on `Cancel`. What is being tested is that three pokes are one pending action, not how long a clock takes.

### 22.4 What was not built, and why

**A3, the selection-preserving refresh, is deliberately absent.** It was counted at three sites and it belongs in the kit, but every one of those sites is rebuilding a list and hunting the selection back down — which is the typed list of §21.2 with the shadow array still attached. Building the helper now would mean building it twice: once standalone, and again as the thing the list does for you. It goes with §21.2.

### 22.5 The guards were made to fail

Four sabotages, each turning exactly one test red and leaving the other twelve green:

| Sabotage | Test that failed |
|---|---|
| `Drain` reverted to the copied clear-after-present ordering | `Latest_does_not_strand_a_value_offered_while_presenting` |
| `Suppressor` inner dispose resets depth to zero (bool semantics) | `Suppressor_nests` |
| `FilterBar` ignores `SearchDelay` | `FilterBar_defers_changed_when_a_delay_is_set` |
| `UiThread.Run` always posts instead of running inline | `Run_is_inline_when_already_on_the_ui_thread` |

**One branch has no test and it should be said rather than left to be discovered.** `Latest.Drain`'s final re-check — for an offer landing between the two interlocked operations — needs genuine concurrency to reach, and a single-threaded headless dispatcher cannot produce it. It is reasoned, not pinned. §12.2 records the same kind of admission about an unreachable guard in the CSS parser, and the same rule applies: a negative result recorded so nobody goes looking for the missing test.

The suite is 145, up from 132.

### 22.6 `ConsolePane`, and a test that could not fail

§21.4 recorded two defects in this control, both shipped, and both in code that two console windows depend on entirely. They are fixed.

**The append was quadratic.** `_text = _text + "\n" + line` allocates and copies the whole buffer per line, so printing *n* lines costs O(n²) and a console spends progressively more of its life copying its own history. It is a list of lines now, joined lazily. The join is still O(total length) — `SelectableTextBlock.Text` is one string property and there is no way around that short of a virtualised control — so the real bound is the new `MaxLines`, which defaults to 5000 and drops the oldest lines at the cap. Zero still means unlimited for anybody who wants the old behaviour.

**The scroll was unconditional.** Every arriving line called `ScrollToEnd()`, so scrolling back to read something was undone by the next line — on a busy console, every few hundred milliseconds, which makes the history effectively unreadable while anything is still printing. Following the tail is what you want right up until the moment you start reading, and the fix is to ask whether the reader was at the bottom *before* the text changed.

**And this is where a test was written that could not fail.** The obvious one — scroll to the top, append a line, assert the offset is still zero — passed. It also passed with the fix removed, which is the only reason it was caught: sabotage is what the project does before trusting a guard (§10.4), and this guard did not survive it.

The measurement, since a claim like that should not rest on inference. Under `Avalonia.Headless`, with 500 lines in the pane:

```
PROBE extent=20 viewport=20 offset=0
PROBE after set 0 -> offset=0
PROBE after ScrollToEnd -> offset=0
```

**The `ScrollViewer` reports its extent as equal to its viewport however much text it holds**, so nothing is ever scrollable, `ScrollToEnd` is a no-op, and any assertion about `Offset` is vacuous. A test over the wiring cannot be written in this harness at all.

What replaced it is the §8.1 pattern: the decision was split out of the control as `ConsolePane.Follows(offsetY, extentHeight, viewportHeight)`, a pure function of three numbers, exactly as `WindowPlacementStore.IsOnAScreen` is split so the rule is testable without a display. Five cases pin it, including the half-pixel one, and making `Follows` return `true` unconditionally — which *is* the behaviour being replaced — fails two of them.

**What remains untested is the wiring**, and it is worth saying plainly rather than leaving it to be assumed: that `AppendLine` asks the question before mutating the text, and scrolls only on a true answer, is reasoned from six lines of code and is not pinned by anything. §12.2 records the same kind of admission about an unreachable guard in the CSS parser. The general form is the one worth keeping: **a headless harness is not a screen, and a test that asserts about geometry it never laid out is measuring nothing.**

### 22.7 The layering guard, brought home

§21.4 recorded that this repository had no test enforcing its own headline rule. It has one now, in `tests/EmuSen.LunaP.Tests/LayeringTests.cs`.

The guard that existed stayed behind, deliberately and correctly: `EmuSen.WiseMan`'s `LeafAssemblyTests.LunaP_references_nothing_of_EmuSen` has run since §10.4 and still runs, now against the package. What it cannot do is answer for this repository. It asks *"did LunaP pick up something of EmuSen's"*, which was the question during the split; the rule written in `EmuSen.LunaP.csproj` is broader and was checked by nothing here — **"AVALONIA AND NOTHING ELSE. Not a settings library, not a logging library, not an application's own types."**

A toolkit whose central claim is verified only by a repository its consumers do not have is a toolkit making an unverified claim.

The guard asserts the whole allow-list rather than one prefix: every assembly LunaP references must be `Avalonia*`, `System*`, `netstandard` or `mscorlib`. Measured, that is eight Avalonia assemblies and ten of the base library, `System.Text.Json` among them because `JsonSettingsStore` uses it.

**It fails on purpose permanently rather than once.** The usual sabotage — add a `PackageReference`, watch red, take it out — leaves the demonstration in a commit message and nothing in the suite. So the rule is a function, and a third test points it at the test assembly, which references xunit and must therefore fail it. If that test ever passes, the rule has stopped detecting anything and the other two are worthless. Weakening the allow-list to accept everything turns exactly that test red, which is the check working.

**The blind spot came with it rather than being rediscovered.** §10.4 found that the C# compiler elides a reference used only for `const` values — the constant is baked into the consumer and no assembly reference survives — and that a first attempt to sabotage the original guard used a `const string` and produced a build naming nothing at all. The guard was correct and the sabotage was not. **A project can depend on another project's constants and none of these tests can see it.** That is a documented limit, not a gap waiting to be filled: there is nothing in the metadata to observe.

### 22.8 `EmuSen.LunaP.Testing`, and the bug the move exposed

§21.3 argued the harness should be a package because it was being copied by hand: `EmuSen.WiseMan/LunaP/VisualQuery.cs` byte-identical to this one apart from its namespace, `WiseMan`'s `UiTest` re-declaring `RenderedFrame`, and Pegasus writing a third harness in F# because it could reference neither. The cause was one line — `IsPackable=false` — and there was no other way to obtain `AssertLaidOut`.

It is `EmuSen.LunaP.Testing` now: `UiTest`, `VisualQuery`, `RenderedFrame`, `UiSession`, and `LunaHeadless.BuildApp()`. **LunaP's own suite consumes it as a package rather than owning the files**, which is what stops it drifting back into being private — the harness is now used the way a consumer uses it, by the people most likely to notice if that stops working.

**§1's rule does not bend for this.** A harness needs xunit and `Avalonia.Headless`, which `EmuSen.LunaP` may not name. The rule is about the toolkit assembly — *"a toolkit that names its first consumer is a toolkit only that consumer can use"* — and this is a sibling that a consumer references from its test project alone, so nothing an application ships gains a dependency. `LayeringTests` asserts it from the other side: the toolkit must not reference the harness.

It takes `xunit.assert` and not `xunit`. The harness calls `Assert` and nothing else, and the full metapackage would push `xunit.core`'s `Fact` and `Theory` into every consumer, including one running its own tests on a different runner. The parallelisation check reads its attribute **by name** for the same reason.

#### The bug that only exists once it is a package

`UiTest.Session` was `HeadlessUnitTestSession.GetOrStartForAssembly(typeof(UiTest).Assembly)`, and that was correct for as long as `UiTest` lived in the suite that used it. **Packaged, it is wrong in a way that could not happen before**: `GetOrStartForAssembly` reads `[AvaloniaTestApplication]` off the assembly it is handed, and that attribute is on the *consumer's* test assembly — never on the package's.

`UiSession` finds it instead, by scanning loaded assemblies for the attribute. Zero configuration, because a headless suite must carry that attribute already (§19.1's rule against a required setup step applies here just as it did to the settings store), and a loud, explanatory failure when there is no candidate or more than one.

This is worth recording as a general shape rather than a one-off: **extracting a library turns "which assembly am I" from a question with one obvious answer into a question with two, and the old answer keeps compiling.**

#### It ships the hazard, so it ships the refusal

§20.2 records a race that CI found and a developer machine never did: the statics around the one headless application are process-global, xunit parallelises across test classes, and one class's constructor takes another's diagnostics hook mid-assertion.

That hazard is not this suite's private problem — it belongs to `UiTest`'s single dispatcher plus `LunaSettings`' globals, so **every consumer inherits it the moment it uses the harness**, in the worst available form: green locally, red on CI, non-deterministic in between. It has already been discovered twice independently — Pegasus's hand-rolled harness declares its own xunit collection for the same reason, in F#, before this package existed.

So the harness **refuses to start** against a suite that has not disabled test parallelisation, with a message naming both valid fixes. `UiSession.ParallelismIsHandled` is the escape hatch for a suite that serialises another way, named so that setting it is a statement rather than a shrug.

`DisablesParallelization` is public because that is the only way to show the check works. Sabotaging it would mean editing a build file, which nobody would leave in; instead a test points it at this suite (which declares the attribute, and must return true) and at the harness package (which does not, and must return false). A check that returned true for everybody would protect nobody.

#### Versioning

Both packages ship at one version from one tag. The harness asserts about the toolkit's own controls, so a consumer pairing 0.4.0 of one with 0.2.0 of the other has a question nobody wants to answer. `publish.yml` packs both.

### 22.9 Semantic colour, a typed list, and an empty state

The last of the tracks, and the only one whose cost lands outside this repository.

#### Outcome is not load

Six sites across two applications hard-code a status colour — `IndianRed` twice from a copied `fail`/`note` pair, `SeaGreen`/`IndianRed`/`Goldenrod` for connection state, `Brushes.OrangeRed` for a binding conflict. §4's centralisation check finds every one of them.

`LunaError`, `LunaSuccess` and `LunaInfo` exist now, and they are **deliberately not the load ramp**. §2.1 refused to fold an input-conflict highlight into `LunaHot` because *"'this binding collides with another' is not 'this subsystem is at 85% load', and giving them one key would encode a relationship that does not exist"*. That argument reads as if it were against new tokens and is in fact the argument for them: the reason those colours must not reuse the ramp is exactly the reason they need keys of their own. What the ramp is to a meter, these three are to a message. The values are the ones the six sites had already chosen.

`LunaPaletteTests` pins them in both halves, as it does every other key — §2.1's "the palette is spelled twice on purpose" applies unchanged.

#### `LunaList<T>`

Five places project a model to a string, put the strings in a `ListBox`, and keep a parallel array to map the index back; three places then rebuild the list and go hunting for the selection again. `CheatDatabaseWindow` does the worst version of it, building `$"{s.System}  ({s.Count})"` and then taking the label apart to recover a name it already had. **Parsing a display string to recover a model field is the shape of a missing control.**

It takes a projection, not an interface. §1's rule — plain data or a delegate, *"a meter row takes `(string, double, string)`, never a `DebugLoadInfo`"* — makes `Label` and `Key` `Func`s; a list demanding an `IListItem` would be a list only an application that had adopted LunaP's vocabulary could use.

**`Refresh` restores the selection, and that is why §21.1's A3 was never built separately.** "Rebuild the list" and "keep the selection" are one operation that only looks like two, and the `Key` delegate is what makes it work for rows that are new objects on every poll — reference identity loses the selection precisely in the case that matters. When the selected row is gone the answer is -1 rather than a neighbour, because selecting the neighbour would be a guess about what somebody meant.

A generic control cannot carry a XAML style selector, so it pins its style key to `ListBox` and borrows that theme. Removing that line reproduces the §5.5 trap exactly — no template, no `ItemsPresenter`, no items, no error.

#### `EmptyState`

Three places build this by hand and one of them wrote the reason the kit's nearest seam was wrong, in a comment, before the control existed: *"Muted but body-sized, not a HintText: this one is the window's whole content when no core is loaded"*. A `HintText` is an aside under something else and is 11pt by definition. An empty state **is** the something else. `Message` and `Detail`, because every hand-rolled version wanted both — what is missing, and what would put something there — with the detail line hidden when there is none.

#### The fluent surface

`Ui.Rows` exists now; only `Cols` did, which is why a header-and-body table keeps two column strings in step by hand. `Ui.Section` takes any number of children; taking exactly one is why eight places write a bold `TextBlock` instead of using `SectionHeader` at all. The existing single-child-plus-spacing signature still binds — a test asserts the params overload did not steal it from callers.

#### Both new controls are in the gallery

§7 claims the gallery is how the kit is discovered, and §21.2 recorded the counter-evidence that not every hand-roll is a missing feature — `Shell.fs` calls `DockPanel.SetDock` four times with `.Dock()` already shipping. A control nobody can find is a control that will be written again, so `LunaList` and `EmptyState` are both in `GalleryWindow`, which means the render test covers them too.

#### Made to fail

| Sabotage | Test that failed |
|---|---|
| `Refresh` does not restore the selection | `A_luna_list_keeps_the_selection_across_a_refresh`, and the no-spurious-`Chose` test with it |
| `LunaList`'s style key left as its runtime type | `A_luna_list_is_templated_by_borrowing_the_list_box_theme` |
| `EmptyState`'s detail line always visible | `An_empty_state_hides_its_detail_line_when_there_is_none` |

#### The bill, which lands in EmuSen

§21.5 warned about this and here it is. `ThemeVocabularyTests` stayed in EmuSen (§20.2) and compares `man theme` against `CssTheme`'s allow-lists **by set equality in both directions**. This change adds one element (`empty-state`, with `message` and `detail` parts) and three palette tokens.

**So EmuSen's suite goes red the moment it takes this version**, in a repository this change never touched, and the fix is to add four entries to `EmuSen.DianaOS`'s `man theme` page. That is not a defect in either project — it is the arrangement §20.2 chose deliberately, working as designed. It is recorded here so that whoever bumps the package next knows the failure is expected and knows what closes it.

`LunaList<T>` is deliberately **not** in the CSS vocabulary. Element names are derived from the type name, and a generic's is `LunaList\`1`; it also carries no LunaP-specific styling to name, since it borrows `ListBox`'s theme wholesale. A theme that wants to restyle it should style `ListBox`.

---

## 23. The light column, and a defect the harness could not see

The palette had one column and `LunaTheme.axaml` included a bare `<FluentTheme />`. Those two facts do not sit together, and on a light desktop they came apart.

### 23.1 The measurement

`FluentTheme` follows the **system** theme variant. Every `Luna*` key was a fixed dark literal. So on a light-mode machine, Avalonia's own controls rendered for a light background inside a window LunaP had painted `#1E1E1E`:

```
variant=Dark    windowBg=#ff1e1e1e   buttonBg=#33ffffff
variant=Light   windowBg=#ff1e1e1e   buttonBg=#33000000
```

A stock button's overlay is white-over-dark in the first row and **black-over-dark** in the second. Fluent's foregrounds go the same way. The result is dark text on a dark surface, on any desktop set to light — which is the default on a great many of them.

**The suite could not see this, and the reason is worth more than the bug.** `LunaHeadless.BuildApp` pins `ThemeVariant.Dark`, so every test in this project has always run in the one variant where the mismatch does not exist. §3.1 records that a headless pass missing the theme asserts over untemplated controls and passes green; this is the same shape one level up — **a harness that fixes an environment variable cannot test the code's behaviour across that variable.** The probe above had to set the variant by hand to see anything at all.

### 23.2 What the fix is

`Theme/Palette.axaml` is keyed by variant now — `ResourceDictionary.ThemeDictionaries` with a `Dark` and a `Light` entry. **The dark column is untouched**, so nothing already on a screen has moved.

The light column is a re-derivation rather than an inversion. A hue that carries on `#1E1E1E` is usually far too pale to carry on `#F3F3F3`, so each was darkened until it read against the light surface. That is a judgement, and judgements about colour are exactly the kind that decay, so it is pinned: **every light foreground is asserted at 4.5:1 or better against the light surface**, the WCAG AA floor for body text. Setting `LunaMuted` to a plausible-looking `#9A9A9A` measures 2.54:1 and fails.

The brushes stay **outside** the theme dictionaries, declared once with their `Color` bound by `DynamicResource`. One brush declaration serves both columns, so there is no second place for them to drift — §2.1 already calls the palette "spelled twice on purpose", and spelling it four times would be worse.

That decision has a consequence sharp enough to have caught out the first attempt at testing it:

```
v=Dark   color=#ff1e1e1e  brush=#ff1e1e1e
v=Light  color=#fff3f3f3  brush=#ff1e1e1e     <- the brush, not the palette
```

**A brush is one instance and reports whichever variant is currently active.** `TryGetResource("LunaSurface", ThemeVariant.Light)` hands back the dark colour while the app is in dark mode. That looks exactly like the light column not working and is nothing of the sort — the `Color` keys resolve per variant correctly, and a live `ToolWindow` under Light really does paint `#F3F3F3`. So the tests assert **colours** per variant and assert **brushes** only through a control that is actually on screen. Anything else measures the lookup rather than the palette.

### 23.3 Dark stays the default, and that is the whole of the "pin"

`LunaTheme.Variant` defaults to `ThemeVariant.Dark` rather than to the system, and `LunaApp.Configure` applies it before the saved theme.

**This is not a preference, it is the absence of a behaviour change.** Every consumer of this toolkit has been dark since it existed, because the palette had no other column. Making it follow the desktop would mean an application on a light machine looks different after a version bump that its author took for something else — and §9.1 already refused a base class that altered behaviour merely by being inherited. A palette that alters behaviour merely by being upgraded is the same objection.

Following the desktop is one line, and the test that stops the default drifting is one assertion:

    LunaTheme.Variant = ThemeVariant.Default;

`ApplyVariant` is separate from `ApplySaved` so an application that builds its own `AppBuilder` can get the variant right without taking the theme loader too — the §17 hazard is that a consumer who hand-rolls three quarters of `LunaApp` silently drops the quarter they did not know about.

### 23.4 A contrast shortfall, measured and left alone

Holding the light column to 4.5:1 raised the obvious question about the dark one, and the answer is not comfortable: **`LunaMuted` on the dark surface measures 4.22:1.** Below AA.

It is not changed here. §2.1's rule is that a palette literal is a deliberate one-line decision, not something adjusted in passing while doing something else — the same reasoning that has kept `LunaText` and `LunaMeterText` two greys since the audit. So the dark test carries an explicit floor of 4.2 with the number and the reason written beside it, rather than a global 4.5 that would have to be quietly weakened to pass.

Recording it costs nothing and hides nothing. Changing it is a decision somebody can now make on evidence, which they could not before, because until this section nobody had measured it.

### 23.5 What is still missing

A light column is not accessibility. `ToolTip`, `AutomationProperties`, `TabIndex` and `IsTabStop` still appear **zero times** in this toolkit and in every consumer of it, which remains the largest blank area on the map (§21.4). Contrast is one property of one part of it, and the only one this section touched.

> **Superseded for the toolkit by §24.** The zero above was true when it was written and is now true only of the consumers. `src/` uses `AutomationProperties` in nine controls and a control template; the count that matters turned out not to be that one anyway, for the reason §24 opens with.

---

## 24. Accessibility, and a toolkit that was not in the tree

§21.4 flagged this as the one item on the roadmap with no hand-roll to count, because nobody had built it anywhere, and said that the absence of evidence was the finding rather than a reason to skip it. That was right about the absence and wrong about what to do next: the method that produced every other roadmap item — count what consumers rewrite — produces nothing here, so this section had to get its evidence somewhere else.

**It got it by asking the framework what it thought each control was.** Avalonia exposes `ControlAutomationPeer.CreatePeerForElement`, which is the same route a screen reader's platform bridge takes, so a probe can walk a real window and print what assistive technology would find. That turned an item with no evidence into an item with a table, and the table said something considerably worse than "the labels are missing".

### 24.1 The measurement

Seventeen controls in a shown `ToolWindow`, under the headless harness. The column that matters is the last one:

```
CONTROL         | PEER                      | TYPE        | NAME              | IN CONTROL VIEW
MeterRow        | NoneAutomationPeer        | None        |                   | False
EmptyState      | NoneAutomationPeer        | None        |                   | False
FieldRow        | NoneAutomationPeer        | None        |                   | False
PathPickerRow   | NoneAutomationPeer        | None        |                   | False
FilterBar       | NoneAutomationPeer        | None        |                   | False
ConsolePane     | NoneAutomationPeer        | None        |                   | False
StatusBar       | NoneAutomationPeer        | None        |                   | False
MeterList       | NoneAutomationPeer        | None        |                   | False
RgbaImageView   | NoneAutomationPeer        | None        |                   | False
ButtonBar       | ItemsControlAutomationPeer| List        |                   | True
LunaSwitch      | ToggleButtonAutomationPeer| Button      |                   | True
```

**Nine of the toolkit's controls were not in the automation tree at all.** Not unlabelled — absent. `Control.OnCreateAutomationPeer` returns a `NoneAutomationPeer` unless a control overrides it, and a `NoneAutomationPeer` reports `IsControlElement = false`, which removes the node from the *control view*: the filtered view of the tree that assistive technology navigates.

That alone would be recoverable, because the labels are right there in each template. **They are not, and the second half of the mechanism is the part worth keeping.** Avalonia also hides a templated control's own template parts from the control view, on the entirely reasonable assumption that the control speaks for its parts. Walking into a `MeterRow`:

```
NoneAutomationPeer        type=None        isControlElement=False  name=""        <- the row
  TextBlockAutomationPeer   type=Text        isControlElement=False  name="CPU"     <- its label
  ProgressBarAutomationPeer type=ProgressBar isControlElement=True   name=""        <- the bar
  TextBlockAutomationPeer   type=Text        isControlElement=False  name="62.0%"   <- its value
```

The label is hidden because it is a template part of a control that was supposed to speak for it. The control is hidden because it never volunteered to. **The only thing left in the control view is a nameless progress bar**, so a dashboard of eight meters presented as eight anonymous percentages with nothing to say what any of them measured.

Three particular cases are worth naming, because each is sharper than the general finding.

**`EmptyState` was the worst of them, and it is almost funny.** The one control in the kit whose entire job is to explain why a window is empty was the one thing a screen reader could not see. Both of its lines are template parts, so both were hidden. A sighted user got "No cores loaded — open a ROM to begin"; a screen reader got an apparently empty window and silence. §22.9 built that control because `CoretopWindow` had written a comment explaining why an empty state is the window's whole content rather than an aside — and then it was the one piece of content that did not reach a reader.

**`LunaSwitch` was a defect with a documented cause.** §14.1 decided the label goes in `OnContent` and `OffContent` rather than `Content`, so that the text sits beside the knob and stays there. Avalonia's `ToggleButtonAutomationPeer` takes its name from `Content`. So every switch in every settings window announced as an unnamed button. The §14.1 decision was still the right one — it is about layout, and it is correct about layout — but it had a consequence in a second system that nobody went to look for. This is the shape §12.1 already recorded once, where a value that was right in one representation was frozen in another.

**`ButtonBar` was reporting the wrong kind of thing rather than nothing.** `ItemsControl`'s stock peer says `List`, so a row of OK/Cancel announced as a two-item list — inviting a reader to navigate it as data instead of pressing one of them.

Then the keyboard. The probe listed every focusable tab stop in the window and asked each for its name. Thirteen entries, of which **three had names**: one `Button` reading "Browse...", and two `ListBoxItem`s that read as themselves. Two of the thirteen turned out to be unreachable (§24.5), leaving **eleven reachable tab stops of which eight announced as nothing at all** — five text boxes, a dropdown, a selectable text block, and the switch.

The five text boxes are the ordinary case and the most damaging. `FieldRow`'s content, `PathPickerRow`'s path, `FilterBar`'s search: in each, the words that name the box are in a **sibling** — a label next door in the same template — which a reader has no reason to connect to it. And a settings page with four `PathPickerRow`s had four buttons, all called "Browse...", with nothing to say which was which.

### 24.2 What the fix is

**`Automation/LunaAutomationPeer.cs`, one peer rather than nine.** Every LunaP control needs the same three answers — what kind of thing am I, what am I called, what else is worth saying — so the control type is a constructor argument and the strings are delegates:

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new LunaAutomationPeer(this, AutomationControlType.ProgressBar,
            name: () => Label, status: () => ValueText);

Delegates and not strings, because a meter's reading changes several times a second and a peer holding the value it was built with would report whatever the number was when the window opened. There is a test for exactly that, and it fails when the value is captured.

**The caller always wins.** Each override consults `base.GetNameCore()` first — which reads `AutomationProperties.Name` and falls back to `LabeledBy` — and uses the control's own property only when that is empty. The other order would make the attached property silently useless on precisely the controls that most need it, and a toolkit that ignores the standard Avalonia way of naming a control is worse than one that names nothing, **because the caller's fix looks applied**.

The types each control now reports, and the property each name comes from:

| Control | Type | Name from |
|---|---|---|
| `MeterRow` | `ProgressBar` | `Label`, with `ValueText` as item status |
| `EmptyState` | `Text` | `Message`, with `Detail` as help text |
| `FieldRow` | `Group` | `Label`, with `Hint` as help text |
| `PathPickerRow` | `Group` | `BrowseTitle` |
| `StatusBar` | `StatusBar` | `Status` |
| `LunaSwitch` | `Button` | `Label` |
| `ButtonBar` | `ToolBar` | — |
| `FilterBar`, `ConsolePane`, `MeterList` | `Group` | — |
| `RgbaImageView` | `Image` | — |

The four unnamed ones are unnamed **on purpose**, and it is the same judgement §5.2 made when it left meter group headers with the caller. A `MeterList` does not know whether it is showing core load or audio buffers; an `RgbaImageView` is showing a live buffer of pixels only the caller can describe. A toolkit-supplied name there would be a guess presented as a description, which is worse for a reader than an admitted blank: **a wrong alt text is believed, a missing one is asked about.**

**A meter's reading is item status, not part of its name.** A name is how you refer to a thing; if it changed every frame it would not be one. And `ValueText` is deliberately not exposed as a range value, because the property's own contract — recorded when it was written — is that *"the caller decides whether that is `62.0%` or `13/128`"*. A row showing 13 of 128 slots sets `Percent` to 10 so the bar is the right length and `ValueText` to "13/128" for the reader. An `IRangeValueProvider` would announce "10 percent" over the top of that: a number the caller never asked for, and for every use that is a count rather than a proportion, simply wrong.

**The template's `ProgressBar` is `AccessibilityView="Raw"`.** With the row itself now reporting `ProgressBar`, leaving the inner one in the control view would put a second, nameless bar beside it saying the same number with nothing attached to it. One node, named, is the whole point.

**`FieldRow` lends its label to whatever is inside it**, using `LabeledBy` rather than writing `Name` onto the caller's control — so an application that has already named its own `TextBox` keeps that name. Writing `Name` would win that argument silently and leave the caller no way to say "no, mine", and there is a test that fails if it does. The pairing is re-made when the content is swapped, because a settings page that rebuilds a row's editor would otherwise end up with a labelled row containing an unlabelled control.

**"Browse..." stays "Browse...".** The obvious fix — rename the button to "Choose a save folder" — is the wrong one: **an accessible name that does not contain the visible label breaks voice control**, because somebody saying "click browse" needs "Browse" to be in the name of the button that says Browse. So the name is untouched and `BrowseTitle` becomes the button's `HelpText`, which is announced after it. `BrowseTitle` needed no new property invented for it; it already held the one thing that distinguishes one picker row from another.

**A placeholder is not a label.** `FilterBar`'s search box takes its name from `Placeholder` *as well as* using it as the placeholder, because a placeholder disappears the moment the user types — which is exactly when they might want reminding what they are searching.

**The status line is a live region**, `Polite`, set in the style rather than in code so a caller can turn it off. This is the one place in the toolkit where text arrives to be *read* rather than *found*: "Applied 12 cheats", "Save State failed". Polite and not Assertive because a status line carries far more that can wait than that cannot, and Assertive on anything that updates often makes an application unusable with a screen reader running — a failure mode worse than the silence it was meant to fix. A caller whose status updates twice a second should set it to `Off`, and it is an attached property precisely so that stays their decision.

### 24.3 The fluent surface

`Fluent/AccessibilityExtensions.cs`: `.AccessibleName(…)`, `.HelpText(…)`, `.LabeledBy(…)`, `.LiveRegion(…)`, `.Decorative()`.

§9's rule is that every name in the fluent surface is the XAML attribute it sets. Four of these follow it exactly. `.AccessibleName` does not, and cannot: the toolkit has shipped a `.Name(…)` that sets `Control.Name` since §9 was written, and `AutomationProperties.Name` is a different property entirely — two unrelated things one word apart would be a worse trap than the inconsistency.

The case for these existing at all is that a caller could always have written `AutomationProperties.SetName(box, "…")` and **across three repositories and four applications did so zero times** (§21.4). A fluent chain that has to break to call a static setter on the line between `.Width(200)` and `.Margin(8)` is a chain people stop writing. Naming a control has to cost one call in the middle of a builder or it does not get done.

### 24.4 What this does not do

**No screen reader has been run against any of this.** Every measurement here is of Avalonia's automation tree — the thing a platform bridge reads — and not of Orca, NVDA or VoiceOver reading it aloud. That is a real gap and it is stated rather than papered over: the control view is what assistive technology navigates, so being in it is necessary, but "necessary" is not "verified end to end". Pinning it properly would mean a test that drives a real AT-SPI or UIA client, which is a different kind of test than anything this project currently runs.

**`ConsolePane` output cannot be announced line by line.** The output is one `SelectableTextBlock` holding the whole joined buffer, so a live region there would re-read the entire history on every `AppendLine`, which is worse than silence. Announcing per line would mean output became a list of line items — a different control, and one that gives back the per-line cost §22.6 removed. It is left alone, with the trade recorded, rather than half-solved.

**`ToolTip` is still unused everywhere**, and `TabIndex` is still unset. Neither turned out to be the problem: the tab *order* follows the visual tree and reads correctly in every window measured, and a tooltip is a poor substitute for a name that is always available. They are named here only because §21.4 listed them and a reader will want to know they were considered rather than forgotten.

**Focus visuals were not touched.** FluentTheme supplies them for the stock controls, and none of LunaP's own templated controls is focusable, so there was nothing to draw. If a future LunaP control takes focus, it needs one.

**The consumers are untouched.** This is toolkit work; four applications still name nothing of their own, and every `Group` and `Image` above is a name a consumer must supply. What changed is that supplying it now works.

### 24.5 Two findings about Avalonia 12.1.0

**`TextBlockAutomationPeer` ignores `AutomationProperties.Name`.** It overrides `GetNameCore` to return `Text` and never consults the attached property. Reproduction, on 12.1.0:

```
SelectableTextBlock  text=""          attached="Console output" -> peerName=""
SelectableTextBlock  text="line one"  attached="Console output" -> peerName="line one"
TextBlock            text="visible"   attached="attached name"  -> peerName="visible"
```

Every other peer in the framework honours the attached name; this one does not. It was found the way these things are found — by setting the property in `ConsolePane`'s template, believing it, and only then measuring. **That attribute has been removed rather than left in place**, because one line of ignored XAML with a comment claiming it names the output is exactly the drifted comment this project holds is worse than none. The practical effect is small: a console's output names itself with its own text, which is what a reader wants from a console, and an empty console has no name and nothing to read.

**A correction to this section's own first measurement.** The initial probe reported thirteen focusable tab stops, two of which were nameless with no apparent owner. They belong to Avalonia's `ComboBox` template — a hidden `TextBox` carried for the editable mode — and they are `IsVisible = false`, so no keyboard reaches them. The probe was filtering on `Focusable && IsTabStop` and not on `IsEffectivelyVisible`. **Two of the eight "unnamed tab stops" in the first draft of §24.1 were not defects and not LunaP's**, and the figures above are the corrected ones. The guard carries that filter with the reason written beside it, because it is a mistake worth making only once.

### 24.6 The guards, and what each sabotage turned red

`tests/EmuSen.LunaP.Tests/AccessibilityTests.cs`, thirty tests. The suite is **237** (was 207). The load-bearing assertion is `IsControlElement`, not the name: a control with an empty name is one somebody forgot to label, a control outside the control view is one no amount of labelling reaches.

Nine sabotages, each run and each caught:

| Sabotage | Turned red |
|---|---|
| `EmptyState`'s peer removed | `Every_control_is_in_the_control_view(EmptyState)`, `A_control_names_itself(EmptyState)` |
| Name precedence reversed, control beats caller | `An_explicit_name_beats_the_controls_own` |
| `LunaSwitch`'s peer removed | `A_switch_announces_its_label_and_keeps_its_toggle`, plus the whole-window guard |
| Meter status captured at construction | `A_meters_status_follows_the_property_rather_than_being_captured` |
| `AccessibilityView="Raw"` removed from the inner bar | `A_meter_row_does_not_also_expose_a_nameless_progress_bar` |
| `PART_Label` renamed | both `FieldRow` labelling tests |
| The status bar's `LiveSetting` setter removed | `The_status_bar_is_a_live_region` |
| `FilterBar`'s search name removed | `Nothing_the_keyboard_can_reach_is_unnamed` |
| `FieldRow` writing `Name` instead of `LabeledBy` | `A_field_row_does_not_overwrite_a_name_the_caller_set` |

The last one is there because it was the one test the other eight sabotages left green, which made it the one test not yet known to be a test. §5's rule is that a guard is not trusted until it has failed, and a guard that survives every sabotage aimed at its neighbours has not been aimed at.

`Nothing_the_keyboard_can_reach_is_unnamed` is the one most likely to catch something added later: it builds a window of six controls, walks every reachable tab stop and fails with the type names of any that announce as nothing. It does not know what the controls are, so a control added to the kit next year is covered by it without anybody remembering to.

`Every_control_is_in_the_control_view_and_says_what_it_is` is a `TheoryData` listing every control by name. A control added to the kit and not added to that list is a control this file cannot speak for — which is a weaker guarantee than the whole-window one and is why both exist.

### 24.7 What this costs a consumer

**Nothing to rebuild, and one thing to know.** No control changed shape, no palette key moved, no template part was removed. `ThemeVocabularyTests` in EmuSen (§21.5) compares `man theme` against `CssTheme`'s allow-lists by set equality in both directions — **and this section adds no control and no token**, so that bill does not come due here. It is worth stating explicitly, because §21.5 warned it would come due for anything in §21.2 or §21.4 and a reader is entitled to check.

The one behaviour change is that `ButtonBar` reports `ToolBar` where it used to report `List`. Nothing in any consumer queries an automation control type, so it breaks nothing today; it is in the changelog because it is the kind of thing that breaks a UI test written tomorrow.

---

## 25. The relicence, and a term that was never chosen

LunaP has been GPL-3.0-or-later since its first commit, and **it was never a decision about LunaP**. It was EmuSen's licence, and LunaP was a folder in EmuSen. §19 and §20 record the toolkit being cut loose and given its own repository on the argument that "a general Avalonia toolkit — theme, chrome, remembered geometry, a fluent layout surface — is useful to people who will never run an emulator". The licence did not come along for that reasoning; it came along because nobody moved it.

That is a defect of the same kind §1's layering rule exists to prevent. The rule says the toolkit must not name its first consumer, because a toolkit that names its first consumer is a toolkit only that consumer can use. A copyleft licence inherited from the first consumer is that same mistake spelled in a different file: it does not make LunaP *reference* EmuSen, but it does make LunaP usable only by people who make the choice EmuSen made. §19 got the references out and left the term in.

**As of 0.6.0 both packages are MIT.**

### 25.1 Whether it could be relicensed at all

Three things had to hold, and each was checked rather than assumed.

**One copyright holder.** 26 commits, 23 authored `Tristan Pelland <twistdfrost@gmail.com>` and 3 authored `RedQuE3n <98552311+RedQuE3n@users.noreply.github.com>`. That is one person, not two: the three are the merge commits for pull requests #1, #2 and #3, made through the GitHub web interface, which attributes them to the account's noreply address. There is no outside contributor to ask, and no contributor licence agreement to have wished for earlier. The sixteen commits replayed out of EmuSen in §20 carry their original authorship, and it is the same.

**No copyleft arriving through a dependency.** A permissive licence on the tin is worth nothing if something linked underneath imposes more. Read out of the `<license>` expression in each package's own `.nuspec`:

| Package | Licence |
|---|---|
| `Avalonia`, `Avalonia.Desktop`, `Avalonia.Markup.Xaml.Loader` | MIT |
| `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Avalonia.X11` | MIT |
| `Avalonia.Headless`, `Avalonia.Skia` (harness only) | MIT |
| `xunit.assert` (harness only) | Apache-2.0 |

Every dependency of the toolkit is MIT. The one non-MIT reference in either package is `xunit.assert`, which is Apache-2.0 — permissive, compatible, and confined to the harness, which a consumer references from a *test* project so nothing an application ships gains it (§22.8). **Nothing was forcing the GPL.** The rule that "a new `PackageReference` is a decision and gets a `§`, including its licence" was already doing its job; what it turned up here is that the strictest term in the whole tree was the one LunaP applied to itself.

**No third-party code in the tree.** No attribution headers, no SPDX identifiers, no vendored sources. The one entry that needed a second look is `Settings/JsonSettingsStore.cs`, which §19.1 describes as "deliberately a near-copy of what Galaxia's `ConfigFile` does". That section is describing a behaviour copy — indented JSON, tolerant reader, write-then-rename — and enumerates two things it deliberately does *not* do, which is not what a text copy looks like. `EmuSen.Galaxia` is the same author's in any case, so the answer does not change; it is recorded because a reader auditing this section deserves to find the awkward file already named rather than discover it themselves.

### 25.2 What MIT was chosen over

**LGPL-3.0 is the obvious step down and is wrong for .NET.** It is the answer for a library that wants to stay replaceable inside an application that is not open, and it gets there by requiring the user be able to relink against a modified version. A modern Avalonia desktop application ships trimmed, often AOT-compiled, sometimes single-file — deployment shapes in which "relink against a different LunaP" ranges from awkward to meaningless. A term whose central mechanism the target platform has quietly stopped supporting is a term that generates compliance anxiety and delivers no freedom, which is the worst trade available here.

**MPL-2.0 was the real alternative.** File-level copyleft: changes to LunaP's own files stay open, and it links into anything. If the goal were to keep improvements flowing back it would be the right answer, and it was not rejected because it is bad. It was rejected because it still asks a consumer's legal review a question, and this toolkit's entire premise is not asking. §17 and §24.7 both measure a release by *what it costs a consumer* — a consumer who, per §1, cannot patch this and can only take a version. Weak copyleft costs a reading; MIT costs a line in a notice file.

**Apache-2.0 was rejected on the same axis, with more regret.** Its patent grant and contribution terms are genuinely better engineering than MIT's silence, and it is what a project expecting corporate contributors should take. LunaP has one contributor and no patents. What it would add today is a `NOTICE` file to maintain and a "state your changes" obligation on a consumer's fork — cost now, against a benefit that begins if this project's shape changes. Recorded here so that if the shape does change, the alternative is already argued rather than rediscovered.

**MIT is also what Avalonia is**, and LunaP is a thin layer over Avalonia. A consumer who has already accepted the licence on the substrate finds nothing new on the layer.

### 25.3 The four versions that stay GPL

`v0.2.0`, `v0.3.0`, `v0.4.0` and `v0.5.0` are tagged and published, and each carries `<PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>` in its `.nuspec`. **They stay that way, and nothing here changes them.** nuget.org does not permit a published package's metadata to be edited — a version can be unlisted but not altered — and a licence grant already made to somebody who took a package cannot be withdrawn from them afterwards.

This costs nothing and is worth stating plainly, because the alternative belief is the dangerous one. A relicence is not a recall. It does not reach backwards, it does not put anybody who took 0.5.0 in the wrong, and it does not require unlisting anything: 0.2.0 through 0.5.0 remain honest offers of that code under that term. What changes is that 0.6.0 onwards is a different offer of the same code, and anybody may take it instead.

The four are left listed. Unlisting them would hide a real history to make the package page tidier, which is the same instinct this document's header refuses on its own account.

### 25.4 What this costs a consumer

**Nothing to rebuild, and one thing to gain.** 0.6.0 is byte-for-byte the same toolkit as 0.5.0 — no control changed shape, no palette key moved, no template part was removed, no automation peer changed what it reports. The version bump exists solely to carry the licence, because a licence change is the one thing a consumer most needs a version number to signal, and there is no mechanism other than a version for signalling it.

The gain is the reason for doing it: an application linking `EmuSen.LunaP` no longer inherits a term obliging it to offer its own source. That was always the practical effect of GPL-3.0-or-later on a UI library linked into a desktop application, and it is the effect that made LunaP unusable in exactly the projects §19 said it was worth cutting loose *for*.

**One hazard, and the licence table does not reach it.** `LunaApp.Configure` calls `WithInterFont` (§3), so a consumer's application ships the Inter typeface itself, not merely a reference to it. What `Avalonia.Fonts.Inter` 12.1.0 actually contains, read out of the package in the local cache:

- Six embedded font assets in `lib/*/Avalonia.Fonts.Inter.dll` — `Inter-Thin.ttf`, `Inter-Light.ttf`, `Inter-Regular.ttf`, `Inter-Medium.ttf`, `Inter-SemiBold.ttf`, `Inter-Bold.ttf`.
- `MIT` in the `.nuspec` `<license>` expression, and `MIT` again in the CycloneDX BOM at `_manifest/cyclonedx/bom.cdx.json`.
- **No licence file of any kind.** No `OFL.txt`, no `LICENSE`, no notice accompanying the fonts. The package is eight files and none of them is one.

So the table in §25.1 says MIT for that row because that is what the package declares, and the declaration is about the package. **Whether it is also the correct term for a typeface embedded inside it is a question this document cannot answer and should not pretend to.** Inter upstream is widely distributed under the SIL Open Font License 1.1, which permits commercial use and embedding but expects its notice to travel with the font and reserves the font's name. If that is the operative term, the notice is not travelling, and it was not travelling under the GPL either — this is inherited, not introduced.

Recorded as a hazard rather than a defect because the finding is real and its consequence is not established: it is Avalonia's declaration to make, LunaP only passes it along, and nothing about MIT versus GPL-3.0-or-later on LunaP's own code changes any of it. A consumer whose legal review asks about bundled fonts should be pointed here and then upstream, rather than at a table that reads MIT and stops.

---

## 26. The shell, and a method this document did not have

Everything in the toolkit before this section arrived the same way: somebody wrote it by hand in a
consumer, then wrote it again somewhere else, and §21 counted the copies. **The question that
produced this section was a different one — "what does Qt6 offer that this does not"** — and that
is a top-down method §21 was written specifically against. Its rule is that *"an item earns a
place here by having been written more than once, by somebody who was not trying to write a
toolkit"*, and it says outright that something hand-rolled once is a hazard note rather than a
roadmap entry.

So each piece below says which method chose it, rather than letting a Qt-shaped list borrow §21's
evidence. **Some of it turned out to have evidence after all**, which was found by running the
survey rather than by assuming either way: §26.1 is that survey, and it opens by correcting the
first draft of itself.

### 26.1 The survey this section should have started with

**This subsection replaces one that was wrong, and the error is the reason to read it.** The first
draft of §26.1 said that the shell had no hand-rolls to count, and offered as support that the §21
survey "found no menu bars, no toolbars and no context menus either". Nobody had looked. That
sentence was an inference from §21.4's finding about `ToolTip` and `AutomationProperties`,
presented as though it were a result — which is the one thing this document's header rules out
absolutely. So the survey was run, over five consumers this time, and it found something.

**There is a menu bar, and it is a catalogue of everything `LunaAction` exists to fix.**
`EmuSen.Mistress/Views/MainWindow.axaml:12-60` declares one by hand: **29 `MenuItem`s**, four of
them `ToggleType="Radio"`, three `ToggleType="CheckBox"`, across File, Emulation, View and
Settings. What is around it matters more than the count:

- **Seven `SubmenuOpened` handlers**, whose whole job is to re-derive state that nothing carries.
  `MainWindow.axaml.cs:563-583` is fifteen lines setting `IsEnabled` on seven items, `IsChecked` on
  five, and rewriting two headers, **every time the menu is opened**.
- **The consequence is already recorded in the code**, at `MainWindow.axaml.cs:573`: *"Only synced
  here, never from Pause/ResumeEmulation."* The pause tick is correct while the menu is open and at
  no other time. That is not a bug somebody missed; it is a comment somebody wrote, because with no
  command object there is nowhere else to put the truth.
- **A hand-rolled `ICommand`**, `SelectSlotCommand` at `MainWindow.axaml.cs:638-645`: eight lines,
  and its `CanExecuteChanged` is `{ add { } remove { } }` — an event that never fires, which is
  precisely the enabled-state plumbing that has to be replaced by a `SubmenuOpened` handler.
- **Radio groups by hand**, `MainWindow.axaml.cs:576-579`: four `IsChecked = _baseSpeedPercent == …`
  assignments, which is `ActionGroup` written as arithmetic.
- **A second, unrelated shortcut subsystem.** `Input/HotkeyBindingMap.cs:8-40` maps seven
  `HotkeyAction`s to keys — and **six of the seven name a command that also has a menu item**:
  Save State (F5), Load State (F8), Pause (P), Fullscreen (F11), Exit to Library (Escape), Fast
  Forward (Tab). The menu and the hotkey map share no object, no label and no enabled state.
- **`InputGesture` appears zero times in the entire corpus.** The application has hotkeys and its
  menu never tells anybody what they are. §26.5 called that trap a hazard; it is not a hazard, it
  is what the one real menu bar in the corpus does today.

So the honest table:

| Piece | Chosen by | Evidence |
|---|---|---|
| `LunaAction`, `ActionGroup`, `MenuBar` | **counted** | one menu bar, 29 items, 7 state-sync handlers, a hand-rolled `ICommand` with a dead `CanExecuteChanged`, four hand-written radio assignments, and a parallel hotkey map duplicating six of its commands |
| shortcut binding | **counted** | `HotkeyBindingMap` versus the menu, with `InputGesture` used zero times |
| `SplitPane` | **counted** | `CheatDatabaseWindow.axaml:52` fakes one as `ColumnDefinitions="2*,12,3*"`, the `12` being a spacer; no `GridSplitter` in any repository (§21.2) |
| `Card` | **counted** | three sites paint a `Border` with FluentTheme's `SystemChromeLowColor`, a key LunaP's theme cannot reach (§21.2) |
| `ToolBar`, context menus | **Qt parity** | genuinely zero: no toolbar, no `ContextMenu`, no `ContextFlyout` anywhere in five repositories |
| `SidePanel`, `AppWindow` | **Qt parity** | no docked panel anywhere; the shell arrangement is Qt's, not a count |

**§21's rule is that one hand-roll is a hazard note rather than a roadmap entry, and this is one
site.** It is worth saying why it is treated as more than that anyway: the rule counts *sites*
because a thing written twice is a thing whose shape has been confirmed by two authors. Here one
site contains twenty-nine items, seven handlers, a bespoke `ICommand` and a whole second subsystem
for the same commands — the repetition is inside the site rather than across sites, and it is the
same repetition the rule is trying to detect. A reader who disagrees with that reasoning should
know it is reasoning and not a count of two.

**What the survey did not find is left standing.** There is no toolbar and no context menu in any
consumer, so `ToolBar` and `Menus.Context` are Qt-parity choices with no evidence behind them, and
they are marked as such above rather than folded into the counted rows. The same goes for
`SidePanel` and `AppWindow`: the argument for them is "Qt has one and every application that grows
past one window wants one", which is an argument, not a measurement.

**One thing the survey settles that the first draft got backwards.** It is not true that the
toolkit's consumers never attempted a shell. One of them built most of a menu bar, badly, and
wrote comments explaining which parts of it are only correct some of the time. The reason §21
missed it is that §21 was looking for *duplication between* sites and this is duplication *within*
one — which is a fact about the method, exactly as §21.4's correction says.

### 26.2 The gap, in Qt's vocabulary

What a `QMainWindow` application is made of, and what LunaP had before this section:

| Qt | LunaP before | LunaP now |
|---|---|---|
| `QAction` | nothing | `Commands/LunaAction.cs` |
| `QActionGroup` | nothing | `ActionGroup`, same file |
| `QMenuBar` / `QMenu` | nothing | `Controls/MenuBar.cs`, `Commands/LunaMenu.cs` |
| `QToolBar` | `ButtonBar` (a run of buttons the caller owns) | `Controls/ToolBar.cs` |
| context menus | nothing | `Menus.Context` |
| `QShortcut` / `QKeySequence` | nothing | `Menus.BindShortcuts` |
| `QSplitter` | nothing (`2*,12,3*`) | `Controls/SplitPane.cs` |
| `QGroupBox` | nothing (FluentTheme's key) | `Controls/Card.cs` |
| `QDockWidget` | nothing | `Controls/SidePanel.cs`, minus floating — §26.7 |
| `QMainWindow` | `ToolWindow` (chrome, geometry, Escape) | `Windowing/AppWindow.cs` |
| `QStatusBar` | `StatusBar`, instantiated by nobody | the same control, now with a window that puts it somewhere |

The last row is the §21.2 observation restated: *"a control nobody uses and a layout everybody
rewrites are usually the same bug."* `StatusBar` was never missing. What was missing was a window
with a bottom to put it at.

### 26.3 `LunaAction`, and why a command object rather than a handler

"Save" is a menu item, a toolbar button, a context-menu entry and a `Ctrl+S` binding. Without a
command object that is four declarations kept in step by hand: four labels, four enabled states,
and a shortcut written once in a `KeyBinding` and once again as the grey text on the right of the
menu, where nothing checks that the two agree.

The failure mode is worth naming because it is quiet: **the menu item greys out and the toolbar
button does not.** The button stays clickable and does nothing, and the user's evidence says the
application is broken rather than that the command is unavailable.

Three decisions inside it:

- **It is an `ICommand`.** Avalonia's `Button`, `MenuItem` and `KeyBinding` all take one already,
  so an action drops into a control this toolkit has never heard of. A LunaP-shaped command that
  only LunaP controls could consume would be §1's mistake in a new place.
- **A checkable action flips before its handler runs**, which is Qt's order and the only one that
  makes a one-line handler possible. Setting `IsChecked` directly does *not* invoke the handler:
  that is the difference between the application saying what is true and the user asking for a
  change, and collapsing the two is how a settings dialog applies everything twice on open.
- **A separator is an action with nothing to do**, not a second item type. One list type, one
  loop, and the divider is written where it goes.

**It carries no icon, and that is a gap rather than a decision that closed.** Qt's `QAction` has a
`QIcon`; LunaP has no icon system at all — no set, no resolver, no theming for one — and an
`object Icon` property would invite a raw `Bitmap` into a toolkit that could not restyle it with
the theme. §26.12.

### 26.4 The menu bar, the toolbar, and one item type

`MenuBar` takes `LunaMenu` objects and builds controls from them; nothing built is kept. A
top-level entry is a plain `MenuItem` and **not** an action, deliberately: *File* cannot be
invoked, checked, disabled or bound to a key, and giving it an action would put three uninvokable
actions in front of the shortcut binder.

`ToolBar` is not `ButtonBar` and neither gained a mode switch. `ButtonBar` is a right-aligned run
of `Button`s the caller built and owns, for the bottom of a window. `ToolBar` is built from
actions, left-aligned, and every item follows its action's label, enabled state and checked state.

`ActionMenuItem`, `ActionButton` and `ActionToggle` **follow** an action rather than copying it,
and the subscription is attached and detached with the logical tree. An action lives as long as
the application and a menu item lives as long as its window, so subscribing in a constructor and
never detaching leaks every closed window's menu items into the action's `Changed` list — a leak
that grows with the number of times a dialog is opened.

**One ordering problem is designed around rather than resolved.** Avalonia's `MenuItem` toggles
its own `IsChecked` when clicked, and whether that happens before or after the `Click` event
reaches a handler is an implementation detail of a control this toolkit does not own. Rather than
encode a guess, `ActionMenuItem` treats the action as the only truth and puts `IsChecked` back
whenever anything else moves it. Both orders converge, and it picks up a case neither covers: a
grouped action clicked while already checked, where an unguarded toggle would uncheck the only
checked member of a radio set.

### 26.5 Shortcuts, and a trap in the menu itself

**`MenuItem.InputGesture` is display only.** It draws "Ctrl+S" on the right of the item and binds
absolutely nothing. That is worth stating loudly because the resulting menu looks like a working
one: the shortcut is written in the exact place a user goes to learn it, and pressing it does
nothing at all. `Menus.BindShortcuts` is the separate act that makes it real, and `AppWindow` does
it for every action in its menus and its toolbar.

**Two commands on one key is a defect, and a silent one.** Avalonia runs the first matching binding
and stops, so the second action never fires while the menu goes on advertising its shortcut. It is
reported through `LunaSettings.Diagnostics` rather than thrown, on the same principle the theme
loader follows: a menu that is wrong in one entry should still open.

That widens what the diagnostics sink is for. It was *"this file would not load, and why"* (§19.1)
and is now also *"this shortcut will not fire, and why"*. Same shape of problem — recoverable,
invisible, and the on-screen evidence says it should have worked — so it goes to the sink a host
has already installed rather than to a second one nobody would.

**The same action in two places is not a conflict**, and getting that wrong would have made the
diagnostic worse than useless: an action in the File menu *and* on the toolbar is the arrangement
this whole design is for, and reporting "Ctrl+S is bound to both Save and Save" trains a reader to
ignore the message that catches the real collision.

### 26.6 `SplitPane`, and why the divider is remembered in pixels

This one has §21's kind of evidence: a consumer faking a split pane with a spacer column, and no
`GridSplitter` anywhere in three repositories. Not because nobody wanted a draggable divider, but
because getting one means three column definitions, a splitter, two minimum widths and somewhere
to keep the size the user chose — and at that point people write `2*,12,3*` instead.

**One pane is fixed and the other elastic**, which departs from `QSplitter`'s proportional model
and is the more useful behaviour for what a splitter is used for: a sidebar next to a document
does not want to grow when the window is widened, and the document does. Qt says the same thing
less directly with stretch factors. `Fixed` names which side, so a right-hand panel works as well
as a left-hand one.

**The divider is remembered in pixels, not as a fraction.** A fraction survives a window resize by
moving the divider, which is precisely what the user was being exact about when they dragged it:
they made the sidebar wide enough for the longest filename, not wide enough for 22% of a window
they may never open at that size again.

Two implementation notes worth keeping:

- **A drag can only be observed through the grid definition.** `GridSplitter` writes the new length
  straight onto the `ColumnDefinition`; there is no "dragged" event reporting where it ended up. So
  `SplitPane` subscribes to the definition's `PropertyChanged` and mirrors it into `FixedSize`,
  with a `Suppressor` (§22.2) keeping the two from driving each other.
- **The write is debounced by 400ms and flushed on the way out of the visual tree.** A drag raises
  a property change per frame, and `panes.json` is a full read-modify-write; without the debounce
  that is the whole file rewritten sixty times a second. Without the flush, a drag followed
  immediately by closing the window loses the last thing the user did.

`PaneLayoutStore` exposes `Update(key, edit)` rather than a whole-record `Save`, and that is not
tidiness. **One key has two writers**: the divider owns `Size` and the panel owns `Collapsed`, and
they share an entry on purpose because "the explorer is 320 wide and currently shut" is one fact
about one panel. Two whole-record writers would mean whichever saved last discarded the other's
field — presenting as a panel that forgets its width whenever you close it.

### 26.7 `SidePanel`: docking without floating

This is `QDockWidget` with the floating taken out, and the omission is the design rather than an
unfinished edge of it.

What a dock widget is used for — an explorer down the left, an output pane along the bottom, a
properties panel on the right — needs a title, a way to shut it, a remembered width and a menu
entry to bring it back. What costs the other nine tenths of `QDockWidget` is tearing it off into
its own window, dragging it to a different edge, and tabbing two together: a drag protocol,
floating windows, hit-testing against every dock site, and a layout format to serialise the
result. §26.12 records that as a gap with its name on it.

**The toggle is an action**, which is where this section meets §26.3. Qt has
`QDockWidget::toggleViewAction()` for the same reason: the View-menu entry that shows and hides a
panel must be *the same object* as the panel's own close button, or the tick and the panel drift
apart the first time somebody uses the button.

**A closed panel is removed from the layout, not hidden in place.** An invisible pane still owns
its grid track and its divider, so hiding it leaves the middle of the window ending 240 pixels
early with nothing in the gap.

### 26.8 `AppWindow`

It extends `ToolWindow` and changes nothing it inherited. Remembered geometry, the restyle hook
and the Escape behaviour work exactly as before, and every part of the shell is empty until
something is put in it: **an `AppWindow` with no menus, no toolbar, no panels and no status line
lays out identically to a `ToolWindow` with the same content**, and a test says so.

That is not politeness. §9.1 refused a base class that altered behaviour by being inherited, and
§21.4 found six windows in one application that had already refused `ToolWindow` itself — a shell
that levied an empty menu strip on everything deriving from it would earn the same treatment.

**The three dividers are built once and left in the tree with a null pane where no panel is
docked.** `SplitPane` collapses a null pane and its divider together, so this costs an empty grid
track and buys the alternative's whole problem: re-parenting the central content every time
somebody toggles a panel, which is how a control ends up with two logical parents and an exception
nobody can reproduce. The consequence is visible in one test — a shell with one panel contains
four `SplitPane`s, three of them the shell's own.

Nesting is left outermost, then right, then bottom, which is what makes a left panel full height
and a bottom panel sit under the middle of the window rather than across the whole of it.

**The status bar appears when something is put in it and then stays**, even if the message is
cleared. A strip that came and went with the text would move every control in the window by its
own height each time an operation finished.

### 26.9 `Card`, and the one new palette token

Three consumers paint a `Border` with FluentTheme's `SystemChromeLowColor` (§21.2). The
interesting half of that finding is that §4's centralisation check greps for hard-coded hex and
these three are not hard-coded — they are sourced from the wrong dictionary, so a theme that
restyles the whole application leaves all three behind. The card is not new chrome; it is the
chrome those three already drew, on a key a theme can reach.

**`LunaBorder` is the first token in this palette that is neither a surface nor something written
on one**, and the shell needed it four times over: a card's edge, the rule under a panel header,
the divider a splitter is dragged by, and the line under the toolbar. One token rather than four,
because four would be four things to keep in step for a distinction nothing in the kit draws.

**It was chosen against 3:1, and the obvious value fails that.** `#3C3C3C` is what a dark theme
reaches for and it measures **1.51:1** against `LunaSurface` — fine for decoration and not fine
for a splitter, because WCAG 1.4.11 asks 3:1 of anything you must see in order to use it, and
§26.11 records that the divider is a control a keyboard can focus and move. Measured:

| | on `LunaSurface` | on `LunaInputSurface` |
|---|---|---|
| Dark `#6E6E6E` | **3.27:1** | 3.00:1 |
| Light `#8C8C8C` | **3.03:1** | 3.36:1 |
| *(rejected)* dark `#3C3C3C` | 1.51:1 | 1.39:1 |
| *(rejected)* light `#C8C8C8` | 1.51:1 | 1.67:1 |

`PaletteVariantTests` holds both columns at 3:1. Note that this is a **different floor from the
tests either side of it**: those are WCAG 1.4.3 and about reading text, where the dark column is
held to 4.5:1 with one measured exception (§23.4).

A theme that wants a subtler divider can have one — `split-pane .rule` is in the CSS vocabulary —
which is the right place for that decision, because somebody overriding it has decided it for
themselves rather than inheriting it by default.

### 26.10 What this cost, in numbers

- **Twelve new files**: `Commands/LunaAction.cs`, `Commands/LunaMenu.cs`, `Commands/Menus.cs`,
  `Controls/ActionControls.cs`, `Controls/MenuBar.cs`, `Controls/ToolBar.cs`, `Controls/Card.cs`,
  `Controls/SplitPane.cs`, `Controls/SidePanel.cs`, `Controls/LunaTable.cs` (§27),
  `Windowing/AppWindow.cs`, `Windowing/PaneLayoutStore.cs`.
- **The suite went from 237 tests to 304**, all passing, in the same ~3 seconds.
- **One new palette token** (`LunaBorder`), spelled in both halves (§2.1) and in both variants.
- **Five new CSS elements** — `menu-bar`, `tool-bar`, `card`, `split-pane`, `side-panel` — with
  six template parts between them.
- **No new `PackageReference`.** Every piece of this is Avalonia and `System.Windows.Input`, so
  §1's rule is untouched and there is no licence to record.
- **The gallery is now an `AppWindow`**, which is the only honest way to show a shell: a menu bar
  is not something you look at next to a meter row.

### 26.11 The guards, and what each sabotage turned red

Twelve deliberate breakages, run one at a time against the suite. **Two of them turned nothing
red, and those are the useful entries.**

| Sabotage | Turned red |
|---|---|
| `ActionMenuItem` loses `StyleKeyOverride` | **nothing — the guard did not cover its own trap** |
| *(after adding a templated-at-all assertion)* the same | `A_menu_item_follows_its_actions_label_and_enabled_state` |
| `ActionButton` loses `StyleKeyOverride` | **nothing** |
| `ActionToggle` loses `StyleKeyOverride` | **nothing** |
| `MenuBar` loses `StyleKeyOverride` | 2 tests |
| `ActionMenuItem`'s `IsChecked` write-back guard removed | `A_menu_items_tick_cannot_be_moved_behind_the_actions_back` |
| `ActionButton` stops mirroring the enabled state | `A_toolbar_button_follows_its_action` |
| `Menus` stops recognising the same action twice | `The_same_action_in_two_places_is_not_a_conflict` |
| `SplitPane` keeps the divider when a pane is empty | `An_empty_pane_takes_the_divider_with_it` |
| `SplitPane` stops saving on the way out of the tree | `A_dragged_divider_is_remembered_under_its_key` |
| the splitter loses its accessible name | `A_divider_can_be_found_and_moved_from_the_keyboard`, and the shell-wide guard, reporting *"Focusable but unnamed: GridSplitter"* |
| `LunaBorder` set to `#3C3C3C` | the contrast guard, reporting *"1.51:1, below 3:1"* |
| `Card` stops collapsing an absent header | `A_card_with_no_header_collapses_the_header_row` |

Three findings came out of this rather than out of writing the code:

**The style-key trap does not bite uniformly.** Dropping the override turns tests red for `Menu`
and `MenuItem` and turns nothing red for `Button` and `ToggleButton` — Avalonia finds those a
theme anyway. **Why they differ was not established**, and the overrides stay on all four: one
line each, against a fallback that demonstrably does not apply to every control in the same file.
The comment at the top of `Controls/ActionControls.cs` used to claim all three were load-bearing;
it says this instead, because a comment that has drifted from the code is worse than none.

**A property assertion is not a rendering assertion, and this pass proved it.** Every assertion in
the menu-item test — `Header`, `IsEnabled`, `InputGesture` — passes for a control that draws
absolutely nothing. The guard was written for §5.5's trap and did not cover it until an
"it was templated at all" assertion was added. §5.5 has said this since the beginning and it was
still got wrong here.

**`GridSplitter` is a keyboard-operable control, and it was unnamed.** The whole-window
accessibility guard, extended over a shell, reported it immediately. Measured before deciding what
to do about it: the splitter is `Focusable`, is a tab stop, takes focus, and **one Left press moves
a 200pt pane to 190** — so it is not an inert focus stop to be removed from the tab order, it is a
real control that a screen-reader user reached and was told nothing about. It has a name now,
`AppWindow` names each of its own after the panel it resizes, and a test holds both halves: that
it is reachable, and that it does something when reached. That is the second time this particular
test has found a control nobody had thought about; §24.1 was the first.

**One Avalonia behaviour measured, and worth knowing.** Binding `Command` on a `Button` disables it
through `IsEffectivelyEnabled` and **leaves `IsEnabled` true**. So `ActionButton` (which binds
`Command`) and `ActionToggle` (which syncs the property directly) answered differently for the
same disabled action. `ActionButton` now mirrors the state onto the property as well, so two
controls in one file cannot disagree about what disabled means.

### 26.12 What this does not do

Named rather than left to be discovered, because a shell that is 80% of `QMainWindow` invites the
assumption that it is all of it.

- **No floating or re-dockable panels, no tabbed dock groups, no MDI.** §26.7 is the argument. One
  panel per side, and a second on the same side replaces the first rather than silently stacking.
- **No icons anywhere.** No `QIcon` equivalent on an action, so a toolbar is a row of words. This
  is the single most visible difference from a Qt toolbar and it needs an icon system, not a
  property (§26.3). The §26.1 survey is mildly reassuring here and no more than that: `Icon=` is
  used **zero times** across five repositories, so nobody is currently working around its absence
  — but nobody has had a toolbar to put an icon on either.
- **No vertical toolbar.** `QToolBar` can run down a side; this one is horizontal.
- **No `QToolButton` drop-downs**, no split buttons, no overflow chevron when a toolbar is wider
  than its window — it clips.
- **No native macOS menu bar.** Avalonia has `NativeMenu` for putting a menu in the system bar,
  and `MenuBar` does not use it, so a macOS application gets an in-window menu strip where the
  platform expects one at the top of the screen.
- **No `QWhatsThis`, no status-bar tips**, and no tooltip on a menu item (only on toolbar items).
- **Item views: a flat table exists, a tree does not.** `LunaTable<T>` is in §27. This entry
  originally said the route was decided — `Avalonia.Controls.TreeDataGrid`, "MIT" — and **that
  was wrong about the licence**: the package requires a paid Avalonia Accelerate key, which §27.1
  documents out of the artifact. No dependency was taken; the table is built on stock Avalonia,
  and `QTreeView`'s hierarchy is still absent with no evidence yet asking for it.

### 26.13 What this costs a consumer

**Nothing breaks.** Every addition is additive: no control changed shape, no palette key moved, no
template part was removed or renamed, and no automation peer changed what it reports. A consumer
on 0.6.0 who upgrades and changes nothing has the same application.

Two invoices, both expected rather than surprising:

- **§21.5's invoice comes due.** `ThemeVocabularyTests` stayed in EmuSen (§20.2) and compares
  EmuSen's `man theme` page against `CssTheme`'s allow-lists **by set equality in both
  directions**. One new palette token and five new CSS elements therefore turn EmuSen's suite red
  until EmuSen's documentation is updated — a failing test in a repository this change never
  touched. §21.5 predicted exactly this and called it the invoice for leaving that test behind. It
  should be expected, not debugged.
- **A second settings file.** `panes.json` appears next to `windows.json` for any application that
  gives a pane or a panel a key. Nothing writes it otherwise; it is opt-in like everything else
  the kit persists.

---

## 27. Item views, and a dependency that was not taken

`LunaList<T>` is a flat list of strings. Everything above it — a table, a tree, columns — was
recorded in §26.12 as absent, with a note that the route was decided and only the code was
missing. **The route was decided on a false statement about its licence, and this section is what
happened when somebody checked.**

### 27.1 `Avalonia.Controls.TreeDataGrid` requires a paid licence

The decision to use it was taken on the claim that it is "an official Avalonia package, MIT".
**That claim was wrong.** Read out of the 12.1.0 package itself:

| Where | What it says |
|---|---|
| `README.md`, shipped in the package | *"As of 11.2.0 this package requires an Avalonia Accelerate license: https://avaloniaui.net/accelerate"* |
| `.nuspec` | **no `<license>` element and no `licenseUrl` at all**; `<copyright>Copyright 2019-2026 © AvaloniaUI OÜ</copyright>` |
| `build/Avalonia.Controls.TreeDataGrid.props` | `<AvaloniaUILicenseKeyProduct Include="Avalonia.Controls.TreeDataGrid" />` |
| dependencies | `AvaloniaUI.Licensing 3.1.1`, whose own description is *"Internal license validation infrastructure for Avalonia Pro and XPF components. Not intended for direct use or reference."* |
| `_manifest/cyclonedx/bom.cdx.json`, shipped in the package | `Avalonia.Controls.TreeDataGrid: licenses=None` — while `Avalonia`, `Avalonia.Remote.Protocol`, `Avalonia.BuildServices` and `MicroCom.Runtime` in the same BOM each declare `MIT` |
| `themes/README.md` | *"the assembly is distributed in obfuscated form"* |

Two further notes so nobody re-derives this from scratch. **It is not a change in the 12 line**:
11.3.2 carries the same `AvaloniaUI.Licensing` dependency and the README dates the requirement to
11.2.0. And **it is not a copyleft problem**, which is what §25 was watching for — it is a
commercial one, which the licence table in §25.1 has no column for.

**That ends it, on rules this project already had.** §1 says the toolkit references Avalonia and
nothing else; the csproj already takes six Avalonia packages, so an official seventh would have
been an easy argument to make. The rule that actually bites is the other one: *"a consumer
inherits what is linked, and a copyleft dependency would hand them a term the licence on the tin
does not mention."* A licence-keyed component is that same sentence with a worse ending — LunaP
says MIT on the tin, and a consumer who used one LunaP control would need to buy an Avalonia
Accelerate licence to ship it. §25 spent a whole section getting a term off this package's
consumers; this would have put a heavier one back.

**Recorded rather than quietly avoided**, because the next person to want a data grid will reach
for the same package for the same good reasons, and the licence is not visible from the NuGet
listing or from the package name.

### 27.2 What the evidence asks for, which is smaller than a data grid

The §26.1 survey was extended to cover this, over all five consumers:

- **Zero `DataGrid`s. Zero `TreeView`s. Zero hierarchical views of any kind.** Six `ListBox`es, one
  of which carries an `ItemTemplate` — the hand-built row §21.2 already recorded.
- The one place a columnar view exists is **not in a consumer of this toolkit yet**. It is
  `bima/viewer.py:460`, in the Python application `BIMA-C` is porting:

      self.tree.setHeaderLabels(["name", "type", "pg"])
      …
      QTreeWidgetItem(self.tree, [f.name, f.type, str(f.page + 1)])

  A `QTreeWidget` with every row added at the top level: **a three-column list with the tree part
  unused.** `BIMA-C`'s `docs/Port.md` stage 4 is the window, and this list is in it.

So the thing to build is a flat list with columns, and `LunaTable<T>` is exactly that and nothing
more. A hierarchical view has **no** evidence behind it in any repository, and building one
speculatively is what §21's rule exists to stop.

**One site is one site**, and §21 calls that a hazard note rather than a roadmap entry. What makes
this different from a speculative control is that the site is a specification rather than a
sighting: it is the UI a scheduled port has to reproduce, so the alternative to building it here is
`BIMA-C` hand-rolling it there — which is the exact shape of every item in §21.

### 27.3 A generic control cannot be styled by name, and the trap in the workaround

`LunaTable<T>` needs a header row, so unlike `LunaList<T>` it cannot borrow `ListBox`'s theme and
be done. It needs a template, and a template comes from a style, and **a style selector cannot
name a generic type**. The answer is a non-generic abstract base — `LunaTable`, same name,
different arity — that the theme can select and the generic half derives from.

**And the first draft of that style matched nothing at all.** `Selector="luna|LunaTable"` is an
EXACT type match in Avalonia: it selects instances whose type is precisely `LunaTable`, and every
instance there will ever be is a `LunaTable<T>` deriving from it. The selector has to be
`:is(luna|LunaTable)`. The symptom was §5.5's symptom for the fourth time in this document — a
control with no template, no exception, and nothing on screen.

Every other style in `Controls.axaml` names a type that is instantiated directly, which is why
this trap has not appeared here before and why it is worth its own paragraph: **the moment a
control is subclassed or made generic, a bare type selector stops reaching it.**

The same exactness is why `luna-table` is **not** in the CSS theme vocabulary. `CssTheme` compiles
an element name to `OfType(...)`, so a `luna-table { … }` rule would silently match no instances;
`LunaList<T>` is absent from that vocabulary for the same reason and always has been. Making the
CSS compiler use `Is(...)` instead would change what every existing element name matches, which is
a bigger decision than this control needs.

### 27.4 What a reader hears, and what the control declines to claim

A row is a `Grid` of `TextBlock`s, which announces as its concatenated text at best — *"Site text
1"* — three values with nothing to say which column each came from. So every row is given a name
built from its own cells paired with their headers: **"name: Site, type: text, pg: 1"**. That is
the information the column layout is carrying visually, and it is the half of a table a reader
actually needs.

**The control reports itself as a `Group`, deliberately not as `DataGrid` or `Table`.** Those UIA
types come with `IGridProvider` and `ITableProvider` — the patterns that let assistive technology
ask for "row 4, column 2" and navigate a grid as a grid. This control implements neither, because
it is a list of rows that happen to be laid out in columns. Claiming the type would advertise
navigation that is not there, which is worse than the honest smaller claim: §5.3's argument about
`RgbaImageView` refusing to invent alt text is the same argument.

### 27.5 What it does not do

- **No hierarchy, no expanders.** The evidence is a flat list; a tree is a different control.
- **No sorting**, by header click or otherwise, and no column reordering or drag-resizing.
- **No cell editing**, no multi-column selection, no frozen columns.
- **It does virtualise**, and that is measured rather than assumed: the gallery's table sits below
  the fold of a scrolled page and realises exactly **one** row container, which is why the gallery
  test asserts "at least one row" and `TableTests` asserts all three against a table on screen.
- **Column widths are the caller's**, given as GridLength strings. `Auto` works — every column is
  put in a shared size group and the root is a shared size scope, because a header grid and a row
  grid are separate grids that would otherwise size an `Auto` column independently and line
  nothing up.

### 27.6 The guards, and a defect a render found

Five sabotages, each run against the suite:

| Sabotage | Turned red |
|---|---|
| the style goes back to an exact type selector | 9 of 12 table tests, all with "no matching element" for `PART_Header` |
| columns stop sharing a size group with the header | `Columns_share_a_size_group_with_their_header` |
| a row stops naming its cells by column | `A_row_announces_its_cells_with_the_column_they_are_in` |
| `Refresh` stops restoring the selection | 2 tests |
| a selection made before the template is dropped | `A_row_selected_before_the_template_existed_is_still_selected` |

**The last one is a defect the tests did not find and a picture did.** A window in this toolkit is
built in its constructor — fill the table, select a row, show the window — and `Select` returned
quietly when there was no template yet, so the selection was thrown away. Twelve passing tests, and
the render dump showed a table with nothing highlighted. `FilterBar.SetFacets` holds pending facets
for precisely this reason (§14.2), which is the second time this toolkit has needed that pattern
and the first time it was noticed by looking rather than by asserting.

Worth stating as a general point, because §10 is a whole section about a headless suite: **a render
pass proves a window is not blank; it does not prove the window is right.** Looking at the picture
is still a separate act.

**And a separate act is not a guard**, which is what §28.2 does about it: the same script is run
before the template and after it, and the two runs are each other's expected value. That turns
"somebody has to look" into an assertion, and the sabotage table in §28.3 puts this exact defect
back to prove it fails.

---

## 28. Two traps, and the guards that end them

§27 closed with two findings, and both had the same shape: a defect this repository has met
several times, fixed each time, and each time written up as a paragraph asking the next author to
remember something. **A paragraph is not a guard.** The evidence is in the count — the style-key
trap has now been recorded four times (§5.5, §14.1, §26.11, §27.3) and the before-the-template
trap three (§5.6, §14.2, §27.6), each time by somebody noticing, never by the suite.

This section is what replaced the two paragraphs. Neither guard asks anybody to remember anything:
both find their subjects by reflection, so a control added tomorrow is covered the moment it
compiles.

### 28.1 A style that matches nothing, found by reflection rather than by memory

`tests/EmuSen.LunaP.Tests/TemplateReachTests.cs`. Every concrete templated control in the kit is
found by reflection, put in a `ToolWindow`, shown, and required to have visual children. That is
the whole assertion, because that is the whole symptom: a control whose style never matched has no
template, no exception, and nothing on screen.

**Twenty-three controls are swept**, which is every public non-abstract `TemplatedControl` in
`EmuSen.LunaP.Controls`. `TemplatedControl` is exactly the right net rather than a convenient one:
`ContentControl`, `ItemsControl`, `Menu` and `TabControl` all derive from it, so `Card`, `ToolBar`,
`MenuBar` and `Tabs` are in; `SectionHeader`, `HintText` and `MonoText` are `TextBlock`s styled by
property setters, with no template to fail to get, and fall outside by construction rather than by
an exclusion list somebody has to maintain.

Generic controls are closed over `string`, which satisfies both `where T : class` constraints in
the kit. Which argument is used does not matter, and that is the point: §27.3's trap is that no
selector can name `LunaTable<T>` for *any* `T`.

**The first attempt was a lint over the selector text, and it was abandoned for two reasons.** It
would only have caught the one cause — `luna|X` where `:is(luna|X)` was needed — while "no
template" also comes from a missing `StyleKeyOverride`, a style file not included, or a renamed
part. And then the decisive one, which is a fact about Avalonia worth recording on its own:

> **Avalonia's XAML compiler strips compiled `.axaml` from the resource blob.** Enumerating
> `AssetLoader.GetAssets(new Uri("avares://EmuSen.LunaP"), null)` on a built LunaP returns exactly
> one entry, `avares://EmuSen.LunaP/!AvaloniaResourceXamlInfo`. `Controls.axaml` is compiled to IL
> and is not there. Reading it would have meant reading the developer's working copy and calling
> it the shipped artefact.

**What the sweep is worth, measured rather than assumed.** Removing all eight `StyleKeyOverride`
pins in the kit at once turns **six of the eight** red: `Tabs`, `MenuBar`, `LunaList<T>`,
`LunaSwitch`, `ActionMenuItem` and `Dropdown`. The two that stay green are `ActionButton` and
`ActionToggle` — which is §26.11's finding reproduced exactly, from the other direction and
without anybody choosing what to test. Avalonia finds `Button` and `ToggleButton` subclasses a
theme anyway; why it does was not established then and is not established now. **The sweep is
therefore not a complete guard against the style-key trap and is not claimed as one.** It is a
complete guard against a control rendering nothing, which is the symptom that matters.

**The CSS vocabulary gets the same rule by the other route.** `CssTheme.TryCompile` builds
`OfType(spec.Target)`, which is the same exact type match, so an element name for a subclassed type
would let a host write a rule that parses, warns about nothing, and styles nothing — worse than a
rule that was refused, because the host has no way to tell. §27.3 left the `OfType`-versus-`Is`
choice open rather than change what every existing element name matches; a test now asserts that no
type in the vocabulary is subclassed, which is what makes leaving it open safe. The day that stops
being true the choice gets made, rather than discovered by a theme author whose rule does nothing.

### 28.2 State set before the template, found by running it in both orders

`tests/EmuSen.LunaP.Tests/TemplateOrderTests.cs`. A window in this toolkit is built in its
constructor — make the control, fill it, select something, and only then show it. So every
imperative method on a control is called **before** its template exists at least as often as
after, and a method that writes straight to a template part does nothing at all on that path.

§27.6 concluded that a render pass proves a window is not blank but not that it is right, and that
looking at the picture is still a separate act. That is true, and it is not something a suite can
rely on. **This is the same finding turned into an assertion**: run the identical script before the
template and after it, and require the same answer. There is no baseline to eyeball and no picture
to interpret, because the two runs are each other's expected value — which is also what makes a
new case cheap, since nobody has to compute an expected result and then keep it true.

Nine cases, covering every imperative method in the kit that can reach a template part:
`ConsolePane.AppendLine`/`Clear`, `FilterBar.SetFacets`, `LunaTable.Column`/`Refresh`/`Select`,
`LunaList.Refresh`/`Select`, `Dropdown.Fill`, `Tabs.Add`/`RemoveFrom`, `MenuBar.SetMenus`,
`ToolBar.SetActions`, `RgbaImageView.SetFrame`/`Clear`.

**Styled properties are not covered, and that is not a gap.** Avalonia stores them on the control
and the template binds to them, so order cannot matter and there is nothing to guard. Only
imperative methods can drop state.

**Every imperative method is covered or explicitly excused.** A second test reflects over every
public, non-inherited, non-accessor instance method on a kit control and requires it to be either
in the case list or in an `Exempt` table with a stated reason. Four are excused today —
`ConsolePane.FocusInput` and `FilterBar.FocusSearch` (moving focus is an act, not state),
`ConsolePane.ResetHistoryRecall` (one private field the template never sees) and
`SplitPane.SaveNow` (writes to the settings store, not to the control's appearance). Static methods
never reach the list at all, having no control and therefore no part to miss. **A method added
tomorrow fails this test until somebody decides which it is**, which is the difference between a
registry and a wish.

**Two things the harness had to learn, both found by the guard failing on its own first draft.**

- **A dispatcher drain is not a layout pass.** `RunJobs` applies templates, but a `TabControl` does
  not realise its `TabItem`s into the visual tree until something lays the strip out, so the `Tabs`
  case read back an empty string in *both* orders. It captures a frame now, which is the public way
  to force layout and the same one `UiTest.AssertLaidOut` uses.
- **A comparison of two empty reads passes while asserting nothing.** That is §26.11's hollow guard
  in a new costume, and it is guarded against explicitly: each case checks that the after-the-
  template read is non-empty and does not begin with a zero count *before* comparing the two. It
  earned its place immediately — the first draft of the `Tabs` case called `RemoveFrom(0)`, and
  `RemoveFrom` means "drop everything from here on", not "drop the one at this index", so the case
  emptied the control and would otherwise have compared two blanks and passed.

### 28.3 The guards, and what each sabotage turned red

Seven deliberate breakages, run against the suite.

| Sabotage | Turned red |
|---|---|
| `:is(luna|LunaTable)` reverted to the exact selector `luna|LunaTable` | the sweep, for `LunaTable<T>` — *"was shown and has no visual children"* |
| all eight `StyleKeyOverride` pins removed at once | the sweep, for **six of eight**: `Tabs`, `MenuBar`, `LunaList<T>`, `LunaSwitch`, `ActionMenuItem`, `Dropdown`. `ActionButton` and `ActionToggle` stayed green (§28.1) |
| `LunaTable.Select` drops a selection made before the template — §27.6's defect, put back | `LunaTable.Column/Refresh/Select`, reporting *"0 row selected, model "* against *"1 row selected, model beta"* |
| `ConsolePane` stops flushing its buffer on `OnApplyTemplate` | `ConsolePane.AppendLine/Clear`, reporting an empty pane against two lines |
| `FilterBar` stops applying its pending facets | `FilterBar.SetFacets`, reporting *"0 items, showing "* against *"3 items, showing Video"* |
| a new `public void Poke()` added to `SplitPane` | `Every_imperative_method_on_a_control_is_covered_or_excused`, naming `SplitPane.Poke` |
| `ActionButton`'s factory removed from the sweep's table | `Every_kind_can_be_constructed_or_has_a_factory`, **and** the sweep itself for `ActionButton` |

The three middle rows are the ones worth reading twice: each is a defect this repository has
actually shipped, put back deliberately, and each is now caught by a test rather than by somebody
looking at a picture.

### 28.4 What this does not do

- **It does not prove a control looks right**, only that it was templated and that its state does
  not depend on call order. §10's render baselines and §7's gallery pass remain what they were.
- **It does not catch every style-key removal**, measured at six of eight (§28.1).
- **It does not cover windows.** `ToolWindow`, `PollingWindow`, `AppWindow` and `MessageWindow` are
  `Window`s with their own lifecycle; `ShellTests` and `WindowingTests` cover those, and folding
  them into a sweep that shows a control inside a window would mean showing a window inside a
  window.
- **It does not cover a control's own template parts being renamed** — a part renamed in both the
  theme and the code is consistent and invisible to both guards, which is what `LunaPControlTests`
  and the per-control suites are for.

---

## 29. One file per control, and a style that was never applied

`Theme/Controls.axaml` had reached 508 lines and 23 selectors, and it grew every time a control was
added — §26 put five elements into it, §27 a sixth. Nothing was wrong with any line of it. The
problem was the shape: a file that accumulates has no natural place to stop, and "where does this
style go" had exactly one answer, which meant the answer carried no information.

`Theme/CssTheme.cs` had the same curve for the same reason, at 547 lines.

### 29.1 The rule: the theme file mirrors the source file

`Controls/` was already almost one file per control, so the split takes that structure rather than
inventing one. `Theme/Controls/SplitPane.axaml` sits opposite `Controls/SplitPane.cs`, and CLAUDE.md's
"a change that does not belong in any existing file wants a new file" now applies unchanged to both
halves. Sixteen files came out of the one:

| File | Selectors | Mirrors |
|---|---|---|
| `TextControls.axaml` | 3 | `Controls/TextControls.cs` |
| `EmptyState.axaml` | 1 | `Controls/EmptyState.cs` |
| `MeterRow.axaml` | 4 | `Controls/MeterRow.cs` |
| `MeterList.axaml` | 1 | `Controls/MeterList.cs` |
| `RgbaImageView.axaml` | 1 | `Controls/RgbaImageView.cs` |
| `FieldRow.axaml` | 1 | `Controls/FieldRow.cs` |
| `PathPickerRow.axaml` | 1 | `Controls/PathPickerRow.cs` |
| `Bars.axaml` | 2 | `Controls/Bars.cs` |
| `FilterBar.axaml` | 1 | `Controls/FilterBar.cs` |
| `MenuBar.axaml` | 1 | `Controls/MenuBar.cs` |
| `ToolBar.axaml` | 2 | `Controls/ToolBar.cs` |
| `Card.axaml` | 1 | `Controls/Card.cs` |
| `SplitPane.axaml` | 1 | `Controls/SplitPane.cs` |
| `LunaTable.axaml` | 2 | `Controls/LunaTable.cs` |
| `SidePanel.axaml` | 1 | `Controls/SidePanel.cs` |
| `ConsolePane.axaml` | 1 | `Controls/ConsolePane.cs` |

**Three control files have no style file, and that is the informative part of the table rather than
an omission from it.** `ActionControls`, `LunaList` and `Widgets` (`LunaSwitch`, `Dropdown`, `Tabs`)
all carry `StyleKeyOverride` and borrow FluentTheme's templates wholesale, so they have no styles of
their own. Before the split that fact was invisible; now it is the absence of a file.

`LunaTheme.axaml` becomes the index, and the order in it is the order the single file had. That
order is **not** load-bearing — every style names a distinct control type, so none overrides another
— but keeping it makes the split diffable against what it replaced, which is worth more than an
alphabet.

### 29.2 What each omission costs, measured

The split introduces a failure mode the single file did not have: a style file that exists, compiles,
and is not named in the index does nothing at all. That is the §5.5 symptom — no template, no error,
nothing on screen — now reachable by forgetting one line.

So it was measured rather than reasoned about. Each `StyleInclude` was deleted in turn and the suite
run against the result:

| Omission | Turned red |
|---|---|
| `SplitPane.axaml` | **11 tests** — `TemplateReachTests` naming `SplitPane`, nine `ShellTests`, and the gallery pass |
| `TextControls.axaml` | **13 tests** — three `LunaPControlTests`, four `ThemeTests`, six `CssThemeTests` |
| `MenuBar.axaml` | **nothing** — see §29.3; **1 test** after §30 fixed what that uncovered |

The first two are the two mechanisms, and it is worth knowing they are different ones.
`TemplateReachTests` catches a templated control because it requires a visual tree (§28.1). It cannot
catch `SectionHeader`, `HintText` or `MonoText` at all — those are `TextBlock`s styled by property
setters, with no template to fail to get, and §28.1 says so explicitly. What catches those is
`LunaPControlTests`, which reads the palette back off a realised header. Fifteen of the sixteen files
were covered, by one guard or the other, without anybody having written a guard for the split itself.

**The sixteenth was not covered, and chasing that is what found §30.** It is covered now, by a third
guard written for the defect rather than for the split.

### 29.3 The sixteenth file, and the defect it was hiding

`MenuBar.axaml` turned nothing red, and the reason is not a gap in the guards.

**The style in it has never applied.** `MenuBar` sets `StyleKeyOverride` to `typeof(Menu)`, and it
must — without that the Fluent `ControlTheme` never reaches it and the bar draws no menus at all
(§5.5, §14.1, and `MenuBar.cs` records it as load-bearing and measured). But Avalonia matches a type
selector against the **style key**, not the runtime type. So `luna|MenuBar` asks for a control whose
style key is `MenuBar`, and by construction there is no such control. The same override that makes
the menu bar render is what stops its own style from ever selecting it.

Measured three ways, on Avalonia 12.1.0:

- Setting the `Background` setter to `Red` and reading the property back off a realised `MenuBar`
  returns **`Transparent`**. Neither setter arrives.
- The same probe run against the tree **before** the split returns `Background=Transparent
  Padding=0,0,0,0` — identical. The split did not cause this and did not change it.
- Changing the selector to `Menu.luna-menu-bar` and adding that class in the constructor makes
  `Background` apply — the probe returns `Red`.

**The bar looks correct anyway, which is why nothing caught it.** `Transparent` is Avalonia's own
`Menu` default, so the intent in the comment and the appearance on screen agree by luck. They would
part company the moment FluentTheme changed that default, and the `Padding="2,0"` never arrived at
all.

#### 29.3.1 A correction, recorded because the wrong version was written down first

This section originally ended by saying the defect was **left unfixed**, on the grounds that the
class fix made `Background` apply but `Padding` still read `0,0,0,0` — "a second cause that has not
been isolated". **That was wrong, and it was wrong because of how it was measured.** Reading
`bar.Padding` off a control in one probe run and comparing it against a different probe run's build
is not a measurement; the second cause never existed.

`AvaloniaObject.GetDiagnostic` settles it, and is the tool that should have been reached for first:

| Property | Before the fix | After |
|---|---|---|
| `Padding` | `0,0,0,0`, priority **`Unset`** | `2,0,2,0`, priority `StyleTrigger` |
| `Background` | `Transparent`, priority `Style` | `Transparent`, priority `StyleTrigger` |

`Unset` is the whole answer. **Nothing anywhere was setting `Padding`** — not LunaP, not Fluent — so
there was no competing value to lose to, and one cause explains both setters. `Background` read
`Transparent` at `Style` priority from FluentTheme, which is the coincidence that hid the defect.
§30 records the fix, which turned out to be the one this section had already found and mistrusted.

**This is a limit of §28.1 worth stating in its own terms.** That sweep catches a control that
renders *nothing*. It cannot catch a control that renders *the wrong thing*, and a control which
borrows a template through `StyleKeyOverride` always has a visual tree no matter what its own style
does. That hole was found by sabotage rather than by reading, and §30 shows it was never confined to
`MenuBar`.

### 29.4 `CssTheme`, split four ways and still one type

547 lines became four files, chosen by what makes each of them change:

- **`Css/CssTheme.cs`** — the entry point, `CssThemeResult`, and the vocabulary queries `man theme`
  reads back. Changes when the public surface does.
- **`Css/CssVocabulary.cs`** — the element and property allow-lists. **This is the half that grows
  with the control kit**, and it is also what a consumer's documentation test reads through
  `ElementNames`, so an addition here is an addition to a published vocabulary (§26.13).
- **`Css/CssParser.cs`** — the parse itself. Changes only when the grammar's shape does.
- **`Css/CssValues.cs`** — text to Avalonia values: colours, numbers, font lists, resource keys.

It stays **one `partial` type**, and the namespace stays `EmuSen.LunaP.Theme` even though the files
now sit in `Theme/Css/`. Both are deliberate. `Parser` reaches the private allow-lists and the
private converters, and nesting it inside the partial class is what keeps all three `private` rather
than promoting them to `internal` so that separate types could see each other. And `CssTheme` is a
public name a consumer has already written a `using` for — moving it to match a directory would be a
breaking change bought with nothing but tidiness.

### 29.5 What this does not do

- **It does not change a single rendered pixel.** 339 tests passed before and after, and the
  `MenuBar` probe returns the same two values on both trees. This is a move, not a repair.
- **It does not guard the index directly.** There is no test asserting that every file in
  `Theme/Controls/` is included, and there cannot easily be one: §28.1 established that Avalonia
  strips compiled `.axaml` from the resource blob, so the shipped assembly cannot be asked what
  style files exist. Enumerating the directory would mean reading the developer's working copy and
  calling it the shipped artefact — the same reasoning that rejected linting selector text. The
  coverage in §29.2 is indirect, and it is fifteen of sixteen rather than sixteen.
- **It does not fix `MenuBar`.** §29.3 records the defect, the mechanism, and the half-fix that
  works, and leaves the decision.
- **It does not split `Palette.axaml` or `LunaPalette.cs`.** At 127 and 79 lines they are flat
  key-value pairs a reader reads top to bottom, and §2.1's whole point is that the two halves are
  compared against each other — splitting either would make that comparison harder to see.

---

## 30. A type selector matches the style key, and four elements that styled nothing

§29.3 found one dead style on `MenuBar` and treated it as a fact about `MenuBar`. It was not. It is a
fact about **`StyleKeyOverride`**, and the moment that is stated properly the question becomes: what
else in this toolkit pins a style key?

Eight controls do — `ActionMenuItem`, `ActionButton`, `ActionToggle`, `MenuBar`, `LunaList<T>`,
`LunaSwitch`, `Dropdown` and `Tabs` — and every one of them does it for the reason §5.5 and §14.1
record, which is that without it the stock Fluent theme never reaches the subclass and the control
renders as nothing, or in `LunaSwitch`'s case throws on `PART_MovingKnobs`. The override is correct
and stays.

But it is not free, and the price was never written down: **a control styled as `Menu` cannot be
selected as a `MenuBar`.** Avalonia resolves a type selector against the style key, so the same line
that buys the template spends the type.

### 30.1 What that cost, measured across the vocabulary

Four of the eight are published CSS element names: `menu-bar`, `luna-switch`, `dropdown` and `tabs`.
`CssTheme.TryCompile` built `OfType(spec.Target)` for all of them, which asks for a control whose
style key is `Dropdown` — and there is never one.

Measured by writing a real theme, loading it through `LunaTheme.Apply`, and reading the property
back off a realised control:

| Element | Rule | Theme loaded | Foreground after |
|---|---|---|---|
| `section-header` | `color: #FF00FF` | yes | **Fuchsia** |
| `menu-bar` | `color: #FF00FF` | yes | White |
| `luna-switch` | `color: #FF00FF` | yes | White |
| `dropdown` | `color: #FF00FF` | yes | White |
| `tabs` | `color: #FF00FF` | yes | White |

**No warning was raised for any of them**, and that is what makes this the worst failure this format
has. §12.2 chose non-fatal warnings so a theme written against a newer LunaP still loads, and the
whole value of that choice is that a theme author is told what was skipped. Here the rule parsed,
compiled to a `Style`, was added to `Application.Styles`, matched nothing, and reported success. A
refused rule tells the author something. A rule that is accepted and silently does nothing tells
them their CSS is correct and their eyes are wrong.

**These four have been in the vocabulary since different releases** — `luna-switch`, `dropdown` and
`tabs` since the format existed, `menu-bar` since §26.13 sold it as part of 0.7.0's new vocabulary.
Every one of them was advertised and none of them worked.

### 30.2 Why the existing test passed throughout

`CssThemeTests` has had `A_rule_block_becomes_a_style_that_really_applies` since §12. It is a good
test. It is written against `section-header`.

`SectionHeader` is a `TextBlock` that pins no style key, so it is precisely the case that works, and
one example was taken as covering a vocabulary of twenty-one. This is the same shape as §26.11's
finding that dropping `StyleKeyOverride` turns tests red for `Menu` and `MenuItem` and turns nothing
red for `Button` and `ToggleButton`: **a mechanism sampled once is a mechanism assumed uniform.**

### 30.3 The fix, and why it is a class rather than a cleverer selector

The style key cannot be given up — it is what makes these controls render. So the selector has to
name the style-key type and then narrow it back down, and a style class is what Avalonia has for
that:

- Each affected control adds a class to itself in its constructor and publishes it as a
  `public const string StyleClass` — `luna-menu-bar`, `luna-switch`, `luna-dropdown`, `luna-tabs`.
  A const because the XAML, the C# and the CSS vocabulary all spell the same string, and a literal
  in three places is a typo waiting for a release.
- `ElementSpec` gains `StyleKey` and `StyleClass`. `TryCompile` builds
  `OfType(StyleKey ?? Target)` and adds `.Class(StyleClass)` when there is one, so
  `ToggleSwitch.luna-switch` reaches a `LunaSwitch` and never a host's own `ToggleSwitch`.
- `Theme/Controls/MenuBar.axaml` becomes `Menu.luna-menu-bar` for the same reason.

**What was rejected.** Selecting the bare style-key type (`Menu { … }`) would reach every menu in the
consumer's application, which is a toolkit reaching outside itself. Dropping `StyleKeyOverride` and
writing a full `ControlTheme` per control is what §14.1 already declined, and §26.11 measured what
removing the override costs. `:is(luna|MenuBar)` does not help: `Is` matches the style key too, so it
asks the same impossible question in a longer form.

### 30.4 The guard, and what it turned red

`Every_element_in_the_vocabulary_can_actually_be_styled` in `CssThemeTests`. It takes its subjects
from `CssTheme.ElementNames` — the published list, not a copy of it — writes `element { color:
#FF00FF; }`, loads it as a real theme, and requires the control's `Foreground` to have changed.
Twenty-one cases, one per element, and a new element joins the sweep the moment it is published.

The property is resolved with `AvaloniaPropertyRegistry.FindRegistered`, the same lookup `CssTheme`
itself uses, so a control whose `Foreground` comes from `TextElement` rather than `TemplatedControl`
is read back through the mechanism that set it rather than through a guess.

Sabotaged by restoring `OfType(spec.Target)`:

| Sabotage | Turned red |
|---|---|
| `TryCompile` selects `spec.Target` again | **4 of 21** — `menu-bar`, `luna-switch`, `dropdown`, `tabs`, and no others |
| `MenuBar.axaml` dropped from the index | `A_menu_bar_is_styled_by_the_kit_and_not_only_by_fluent` |

A third guard, `StyleKeyTests`, enforces the class itself rather than its effect - §30.6 records what
it covers that this sweep does not, and why that gap was the one the original defect came through.

The second row is the one §29.2 could not produce. That omission cost nothing before this section;
it costs a red test now, which closes the last of the sixteen style files and makes §29.2's table
complete.

### 30.5 What this does not do

- ~~**It does not cover template parts.**~~ The sweep tests each element, not `card .header` or
  `console-pane .output`. A part whose selector reaches nothing would still be silent, and the same
  argument that justifies this test justifies extending it — it has not been. **Closed by §39, which
  found thirteen broken pairs from three separate causes — including this same style-key defect one
  layer down (§39.3).**
- ~~**It does not cover the other four overriding controls.**~~ **Superseded by §30.6**, which
  enforces the class on all eight rather than on the four the vocabulary happens to name.
- **It does not make the warning list catch this.** A rule that compiles against a real element name
  still produces no warning when it matches no instance, because nothing in the parse knows what is
  on screen. The sweep catches it at test time; a host loading a bad theme at runtime is not told.
- **It changes rendering**, unlike §29. The menu bar gains its `2,0` padding, and a CSS theme naming
  any of the four elements starts doing what it always said it did — which for a consumer who wrote
  such a rule and worked around its not applying is a visible change. `CHANGELOG.md` says so.

### 30.6 The gap §30.5 named, closed

§30.5 ended its third bullet with *"it needs a class first, and nothing enforces that yet"*. That
sentence is the exact shape §28 exists to abolish — a trap written up as a paragraph asking the next
author to remember — and it was written knowing that, which makes leaving it worse rather than
better. So it is an assertion now: `tests/EmuSen.LunaP.Tests/StyleKeyTests.cs`.

**What was actually uncovered, stated precisely,** because §30.4 already covers more than the bullet
implied. A control that pins a style key can lose its styling two ways:

| It gains… | Caught by | Before §30.6 |
|---|---|---|
| a CSS vocabulary entry | `Every_element_in_the_vocabulary_can_actually_be_styled` (§30.4) | **covered** |
| an `.axaml` style file | nothing | **uncovered** |

The second row is not hypothetical — **it is what actually happened.** `MenuBar` gained a style file
in §26 with a `luna|MenuBar` selector, and it was dead on arrival for four releases. `TemplateReachTests`
structurally cannot see it, because a control that borrows a template always has a visual tree
however dead its own style is (§28.4 says as much). And the `.axaml` cannot be read back to lint the
selector, because Avalonia's compiler strips it from the resource blob (§28.1).

So the rule is enforced from the other end, and made uniform rather than conditional: **every control
that pins `StyleKeyOverride` declares `public const string StyleClass` and adds it to itself.** All
eight, including the four with nothing to style today. The class costs a string, and the day one of
them gains a style file or an element name, the selector already has something that can match and the
idiom is sitting there to copy. Conditional rules are the ones that get forgotten; this one cannot be,
because the test finds its subjects by reflecting for a declared `StyleKeyOverride` override rather
than reading a list.

`ActionMenuItem`, `ActionButton`, `ActionToggle` and `LunaList<T>` gained classes here. Nothing
selects them yet, and that is the point.

**The sabotages, and what each turned red:**

| Sabotage | Turned red |
|---|---|
| `Tabs` declares `StyleClass` and never adds it | the sweep, naming `Tabs` and quoting `Classes.Add(StyleClass)` as the remedy |
| `Tabs.StyleClass` deleted outright | **the build** — `CssVocabulary.cs` names it, so a vocabulary member's class cannot be removed at all |
| `ActionButton.StyleClass` deleted outright | the sweep, naming `ActionButton` |

The middle row is worth keeping: for the four controls in the vocabulary the const is load-bearing at
compile time, so the test is the guard only for the four that are not. Both halves were needed, and
neither was obvious before it was tried.

A second assertion keeps the sweep honest about its own subjects. A `[Theory]` with no cases is a
pass, so a refactor that removed every override would turn this file green by emptying it;
`The_sweep_has_subjects` requires at least the eight that existed when this was written, and says so
in its message rather than leaving a bare number.

---

## 31. The package shipped the code and kept the explanation

This document, and the comment blocks it is cited from, exist because of a claim CLAUDE.md makes in
its first paragraph: a consumer cannot patch this, so the reasoning is the deliverable as much as
the code. `Threading/Suppressor.cs` carries six lines above the type explaining what it replaces and
why it is deliberately not thread-safe. `Theme/Controls/SplitPane.axaml` explains why the rule is a
separate un-hit-testable border rather than the splitter's own paint.

**None of it reached anybody.** A consumer who stepped into `SplitPane` from their own application
got decompiled IL: correct behaviour, every comment gone, no file to open. The package shipped the
code and kept the explanation, and had done since 0.2.0.

### 31.1 What was missing, and what it costs

Nothing exotic — four properties nobody had written:

| Property | What a consumer gets without it |
|---|---|
| `IncludeSymbols` + `SymbolPackageFormat=snupkg` | no symbols at all; no line numbers in a stack trace through LunaP |
| `PublishRepositoryUrl` | a PDB naming file paths on the machine that built it |
| `EmbedUntrackedSources` | generated sources — every `Theme/Controls/*.axaml` — unresolvable |
| `ContinuousIntegrationBuild` | published PDBs pointing at a CI runner's scratch directory |

**And none of it costs a `PackageReference`**, which is the reason this was cheap enough to be
embarrassing. SourceLink used to be a set of NuGet packages; since .NET 8 it ships inside the SDK —
`Microsoft.SourceLink.GitHub` is a directory under `Sdks/` in the installed SDK, and
`PublishRepositoryUrl` is enough to turn it on. §1's layering rule is untouched and there is no
licence to record, which is not true of most things that improve a package.

### 31.2 One file, because two would drift

`Directory.Build.props` at the repository root, not six lines duplicated into two `.csproj` files.
The argument is the one §2.1 makes about the palette in reverse: the palette is spelled twice **and
guarded by a test that fails the moment the halves disagree**, which is what makes duplication safe
there. There is no comparable guard for publication settings, and a symbol package produced for the
toolkit but not the harness is a defect nobody notices until somebody steps into the wrong half. So
it is spelled once, and both `.csproj` files carry a comment saying where it went.

### 31.3 Deterministic under CI and nowhere else

`ContinuousIntegrationBuild` is conditioned on `GITHUB_ACTIONS`, which is a decision rather than a
convenience. It normalises source paths, which is exactly what a published package wants and exactly
what a developer debugging their working copy does not — turned on locally it sends the debugger
looking for `/_/src/…` instead of the file open in the editor.

Measured by packing both ways and reading the SourceLink map out of the PDB:

| Build | `documents` mapping |
|---|---|
| local | `/home/red/Projects/EmuSen.LunaP/*` → `raw.githubusercontent.com/RedQuE3n/EmuSen.LunaP/9019fa4…/*` |
| `GITHUB_ACTIONS=true` | `/_/*` → the same URL |

The commit in that URL is the commit being packed, so a consumer debugging 0.7.0 fetches the source
0.7.0 was built from rather than whatever `main` says today.

### 31.4 Packing is part of the build now

Until this section, `dotnet pack` ran for the first time **in `publish.yml`, on a tag** — the worst
moment to find a missing README, a wrong `PackagePath`, or a symbol package that was never produced.
`ci.yml` packs both projects on every push and throws the output away; what is being tested is that
both packages can be made at all.

It then asserts that every `.nupkg` has a `.snupkg` beside it, because **nothing else in the build
would fail if `IncludeSymbols` were deleted.** That is the whole reason the assertion exists rather
than a comment asking somebody not to delete it.

**The sabotages:**

| Sabotage | Result |
|---|---|
| a `.snupkg` removed from the output folder | the check exits 1, naming the package that lost it |
| `IncludeSymbols` set to `false` in `Directory.Build.props` | `dotnet pack` succeeds, produces **only** the `.nupkg`, and the check catches it |

The second row is the one that matters: the build stays green, the package still installs, and the
only visible difference is the one a consumer would find years later with a debugger. That is
precisely the failure this repository keeps saying it does not want to ship.

### 31.5 What this does not do

- **It does not put the symbols in the main package.** They are a separate `.snupkg` on nuget.org's
  symbol server, so a consumer needs symbol servers enabled in their debugger. That is the standard
  arrangement and the alternative — `DebugType=embedded` — puts megabytes into every consumer's
  build output to serve the few who step in.
- **It does not backfill 0.2.0–0.6.0.** Those are published without symbols and stay that way;
  nuget.org does not accept a symbol package for an already-published version (§25 records the same
  constraint about licence metadata).
- **It does not verify SourceLink end to end.** What is asserted in CI is that a symbol package
  exists. That the URL inside it resolves was checked by hand, once, at this commit — a real check
  would need network access from a test, which is not something this suite has ever wanted.
- **It says nothing about the source being readable.** A consumer can now fetch the file; whether
  the comment they find explains anything is the subject of every other section here.

---

## 32. The public surface, written down

CLAUDE.md's first paragraph says a consumer cannot patch this and can only take a version. Every
convention in this repository follows from that sentence — the layering rule, the man page, the
changelog, §31's symbols. And the thing that sentence is actually about, the surface a consumer
compiles against, was the one thing nothing checked.

Sixty-three public types and around six hundred members, and a renamed parameter, a widened return
type or a property quietly turned `internal` would have shipped in a version bump with the first
report arriving from somebody whose build broke.

### 32.1 A reflected snapshot, and not `PublicApiAnalyzers`

`Microsoft.CodeAnalysis.PublicApiAnalyzers` is the obvious tool and was the plan. It was not taken,
for three reasons in order of weight:

1. **It is a `PackageReference`**, and CLAUDE.md is explicit that one is a decision carrying a `§`
   and a licence. It would be `PrivateAssets="all"` and reach no consumer, so §1's rule survives —
   but *no dependency at all* is strictly better than *a dependency that does not escape*, and here
   it costs about eighty lines.
2. **This repository already has the mechanism.** `EMUSEN_UI_BASELINE` regenerates render baselines
   from an environment variable (§10.2). `EMUSEN_API_APPROVE=1 dotnet test` regenerates this one the
   same way, so there is one convention for "rewrite the thing I am asserting against" and not two.
3. **The guards this suite already trusts find their own subjects by reflection** — §28.1, §28.2,
   §30.6. This is the fourth of the same shape, and a reader who understands one understands all.

What is given up is real and worth naming: no IDE code fix, and no built-in shipped/unshipped split.
The second matters least here because `CHANGELOG.md` already carries the release story.

### 32.2 This baseline is committed, unlike the other one

§10.2 says `*.png` and `*.frame` are never committed, because a render baseline is one machine's
font rendering — worthless elsewhere and misleading in a diff. This looks like the same kind of
artefact and is the opposite kind.

**An API surface is identical on every machine that compiles the code.** So it is not a local
artefact but a reviewable document, and the diff on it *is* the review: a version bump that changes
`ApiSurface/EmuSen.LunaP.txt` is a version bump whose consumer-visible surface changed, visible in
the pull request rather than discovered later.

### 32.3 Two things deliberately left out

**The XAML compiler's public types.** Avalonia's compiler emits
`CompiledAvaloniaXaml.!AvaloniaResources` with a `Build:/` and a `Populate:/` method **per `.axaml`
file**, plus `!XamlLoader` and two `XamlIlContext` helpers. Nothing can call them — the names contain
`:` and `!` — and they change whenever a theme file is added or renamed, which §29 did sixteen times
in one commit. Left in, a refactor that changed no API would have churned thirty-odd lines, and a
baseline that cries wolf is a baseline somebody approves without reading. They are excluded by
namespace rather than by keeping only `EmuSen.LunaP.*`, deliberately: a public type appearing in some
third namespace is exactly the leak this file should show.

**Inherited interfaces.** `Type.GetInterfaces()` is transitive, so every control listed the fifteen
interfaces it inherits from Avalonia's `Control` — unreadable, and worse, an Avalonia upgrade adding
one interface to a base class would have churned every line. What a LunaP type declares is a LunaP
decision; what it inherits is Avalonia's, and is recorded once as the base type.

### 32.4 The guard, and the reporting bug the first sabotage found

The interesting entry is not that the guard caught the change. It is that **it could not explain it.**

The first version compared the two files as flat sets of lines. The first sabotage — `public void
Poke()` added to `Card` — turned the test red and reported *"(only ordering changed)"*, naming
nothing. `Threading.Debounce` already has a method with that exact signature, and a set difference
cannot see that one line became two.

That is a defect in the half of a guard that matters second-most: a test that fails without saying
why sends the next person to a six-hundred-line file with no starting point. **A member signature is
only unique within its type**, so the report is now built the way the file is — grouped by type,
with member counts compared as multisets rather than sets.

| Sabotage | Reported |
|---|---|
| `public void Poke()` added to `Card` | `Card` → `NEW: public void Poke()` |
| a member present in the baseline and not in the code | `Card` → `GONE: public void Removed()` |
| `Card`'s base changed to `ContentControl` | `Card` → `was:` / `now:`, with both base types |

The third row is the one no member-by-member check would have caught. A control that stops deriving
from `HeaderedContentControl` breaks every `is` check and every selector a consumer wrote, and the
member list can be identical either side of it.

### 32.5 What this does not do

- ~~**It does not capture nullability.**~~ `string?` and `string` are the same line here, so
  tightening a parameter from nullable to non-nullable — a source-breaking change for a consumer with
  `<Nullable>enable</Nullable>` — passes silently. `NullabilityInfoContext` could read it; that is
  the first extension this file should get. **Closed by §32.6.**
- **It does not capture attributes.** Adding `[Obsolete]` is invisible to it, and so is removing one.
- **It does not distinguish shipped from unshipped.** There is one baseline, at the surface as it
  stands. `CHANGELOG.md` is where a release says what moved, and §32.1 accepted that trade.
- **It does not check behaviour**, only shape. A method that keeps its signature and changes what it
  does is a worse break than a rename and is invisible here; that is what the other 370 tests are.
- **It cannot tell an intended change from an accident.** It can only make somebody look. The
  approval is a committed file, so the looking happens in review rather than in a dialog box.

### 32.6 The gap §32.5 named, closed

`string?` and `string` are different lines now. The snapshot reads annotations through
`NullabilityInfoContext`, and the regeneration moved **104 lines** in `EmuSen.LunaP` and 2 in the
harness — every one of them a promise a consumer was already relying on and this file could not see.

**Read state or write state is a real choice, not a detail.** A member can differ in the two:
`string? Name { get; set; }` on a type that never returns null reads non-null and writes nullable.
The file reports **what a consumer is promised**, because that is what breaks them — `ReadState` for
returns, fields, properties and `out` parameters, `WriteState` for ordinary parameters. Backwards,
and the guard would call the wrong direction of change safe.

It recurses. `Func<string, string?>` and `Func<string?, string>` are different contracts and neither
is `Func<string, string>`, so the walk follows `NullabilityInfo.GenericTypeArguments` and
`ElementType` the way the type walk follows generic arguments and array elements. Real lines it now
carries:

```
public LunaAutomationPeer(Control owner, AutomationControlType type,
    System.Func<string?>? name = null, System.Func<string?>? help = null, ...)
public System.Func<T, object?> Key { get; set; }
public static readonly DirectProperty<RgbaImageView, Avalonia.Media.IImage?> SourceProperty
public abstract string Directory(string? category)
```

**The baseline cannot guard its own renderer**, which is the trap this could have fallen into.
Delete the nullability walk, regenerate, and the file agrees with itself again while silently saying
less than it did — the exact failure §32.5 described, reintroduced by the fix for it. So
`The_snapshot_records_nullability_in_both_directions` asserts three properties against the
description directly, each chosen to break a different plausible shortcut: a non-null return beside a
nullable parameter *on one member*, a `?` inside a generic argument, and no `??` anywhere (a `?`
asked of both `Nullable<T>` and the annotation renders `int??`).

**The sabotage:** making `Suffix` return `string.Empty` turns it red on the first assertion.

### 32.7 What §32.6 still does not do

- **Attributes remain invisible**, per §32.5. Adding or removing `[Obsolete]` moves no line.
- **`NullabilityState.Unknown` is rendered as nothing**, which is right — an unannotated context
  promises neither — but it means a project that turned `<Nullable>` off would quietly lose every
  annotation rather than failing. Both `.csproj` files enable it, and nothing asserts that they do.

---

## 33. Sixty-three public types, and an empty tooltip

§31 got the source to a consumer's debugger. This is the same gap one step earlier: what a consumer
sees before they ever reach a debugger, which is whatever their editor pops up when they type
`new LunaAction(`.

Until this section that was nothing at all. `GenerateDocumentationFile` was off, so no `.xml` was
built and none was packed, and all sixty-three public types offered IntelliSense a bare name.

### 33.1 The convention was right and had a blind spot

CLAUDE.md says the convention is `//` and not `///`, and that is not a mistake to be corrected — it
is why `Threading/Suppressor.cs` can spend six lines explaining what it replaces and why it
deliberately is not thread-safe. XML doc comments are a bad container for that: `<summary>` is
rendered in a tooltip, and a tooltip is not where an argument goes.

**The blind spot is who the convention serves.** Every word of it is addressed to somebody with the
file open. A consumer resolving `EmuSen.LunaP` from nuget.org has a DLL and, now, a symbol package —
and their editor reads exactly one thing, the `.xml` beside the assembly.

So the amendment is additive rather than a reversal. The `//` block keeps the *why* at whatever
length it needs. A one-line `///` summary above the declaration says *what*. **Two readers, two
containers**, and the summary never tries to carry the argument.

### 33.2 Why members are not required, which is the real decision

Turning on `CS1591` would have demanded a sentence for **all 460 public members**. Counted rather
than estimated:

| Members | |
|---:|---|
| 460 | public members across both packages |
| 41 | Avalonia `StyledProperty` / `DirectProperty` fields |
| 58 | protected overrides of `OnApplyTemplate`, `OnPropertyChanged`, `OnCreateAutomationPeer` |

Ninety-nine of them have no sentence available except the one that restates the name.
*"Gets or sets the Text."* teaches nobody anything, and this repository's own position — a comment
that has drifted is worse than none because it is believed — applies with more force to filler,
which is that failure arriving pre-drifted.

So `CS1591` is suppressed in both `.csproj` files with the reason written beside the switch, and the
rule is enforced where it carries information: **every public type, and members whose names do not
already say it.**

### 33.3 The guard

`tests/EmuSen.LunaP.Tests/DocumentationTests.cs`, two assertions per package:

- **Every public type has a `<summary>`.** Subjects come from reflecting over the assembly and the
  answer from parsing the generated `.xml`, so a type added tomorrow is in the sweep the moment it
  compiles. It uses the same exclusion §32.3 uses, for the same reason — the XAML compiler's
  generated types are public, uncallable, and not ours to document.
- **No summary is empty or a placeholder.** The failure this guard most invites is a `///` added to
  silence it, so a summary under twelve characters, or literally `TODO`, fails. The threshold is
  deliberately low: it catches an empty tag or a stub, not prose somebody thought about.

A third check falls out of the first: if `GenerateDocumentationFile` is ever turned off, there is no
`.xml` to parse and the tests say so by name. Nothing else in the build would have noticed.

**Verified end to end** by packing and reading the result out of the `.nupkg` rather than out of the
build directory — `lib/net10.0/` now contains `EmuSen.LunaP.xml` beside the DLL, and
`T:EmuSen.LunaP.Commands.LunaAction` in it reads *"One command object standing behind a menu item, a
toolbar button, a context-menu entry and a key binding."*

### 33.4 What this does not do

- **It does not document members**, by design, and that is the part most likely to be revisited. The
  argument above is about the 99 that cannot be usefully documented; it is not an argument against
  documenting `Refresh`, `Fill` or `SetMenus`, which have real semantics a tooltip could carry. That
  work is open, and the guard will not ask for it.
- **It does not check that a summary is TRUE.** It checks that one exists and is not a stub. A
  summary that drifts from its type is exactly the failure CLAUDE.md warns about, and no test here
  catches it.
- **It does not cover `<param>`, `<returns>` or `<exception>`.** `LunaTable<T>.Column` takes a header
  and a projection and says so in neither.
- **It does not backfill the packages already on nuget.org.** 0.2.0–0.6.0 shipped without a `.xml`
  and cannot gain one, the same constraint §31.5 and §25 both hit.

---

## 34. What "production ready" still means here

§31, §32 and §33 closed the three things that made this package worse than the code inside it: a
consumer could not read the reasoning, could not be protected from an accidental break, and got a
blank tooltip. **This section is the register of what is still open**, written down because the
alternative is remembering it — and §28 exists precisely because this project has watched four
paragraphs of "the next author should" go unread.

It is a register, not a roadmap. Items leave it by being done and struck through with the `§` that
did it, the way §30.6 closed §30.5's third bullet. Nothing earns a place here by being a good idea;
it earns one by being a gap somebody can already point at.

### 34.1 The documentation debt, which is the largest item

~~**Every public type now says what it is; almost no member does.**~~ **Closed by §41.** §33.2 argued that 99 of the 460
members — Avalonia `StyledProperty` fields and protected framework overrides — have no sentence
available except one restating the name, and that argument stands. **It says nothing about the other
~361**, which is the real job:

- `LunaTable<T>.Column`, `LunaList<T>.Refresh`, `Dropdown.Fill`, `MenuBar.SetMenus`,
  `ConsolePane.AppendLine`, `SplitPane.PaneKey` — every one has semantics a caller has to get right
  and can currently only learn by reading the source.
- **No `<param>`, `<returns>` or `<exception>` anywhere.** `Column` takes a header and a projection
  and a width string, and the `.xml` says so for none of them.
- The delegate seams are where this bites hardest, because they are the whole design: a control
  takes `Func<T, string>` or `Action<LunaAction>`, and *when* it is called and *on which thread* is
  the contract. `LunaList<T>.Key` defaulting to reference identity — right for a cached model, wrong
  for rows rebuilt on every poll — is documented in a `//` comment a consumer never sees.

`DocumentationTests` deliberately will not ask for any of this. When it is done, it should. **It does now — `MemberDocumentationTests`, added in §41, asks for all four.**

### 34.2 The rest of the register

| Open | Where it is already argued | Note |
|---|---|---|
| ~~Member-level API docs~~ | **closed by §41** | 379 entries ship, with 212 `<param>`, 85 `<returns>` and 14 `<exception>`; the guard finds its own subjects, and excuses only what §33.2's argument covers |
| ~~Version decision~~ | **closed by §42** | 0.7.0, because it was never published; nothing was removed since 0.6.0, measured. Tagging is a person's decision and has not been done |
| ~~Nullability absent from the API snapshot~~ | **closed by §32.6** | 104 lines gained annotations; read state vs write state was the part that needed deciding, not the reflection |
| ~~CI runs on one Linux runner~~ | **closed by §35** | matrix added; the audit found one platform branch, and it turned out to be untested, undocumented here, and currently a no-op (§35.1) |
| Trimming: **measured, not fixed** (AOT: never a stated goal, see §36.4) | §36 | ~~`CssTheme` is the problem~~ — **that guess was wrong (§36)**. Three real sites: the JSON settings seam and runtime `.axaml` theme loading. Trim-safe is reachable, AOT-safe is not while the default store is reflection JSON; §36.3 sets out the two designs |
| `net10.0` only | — | Avalonia 12.1.0 ships `net8.0` too, so LunaP is stricter than its own dependency — but .NET 8 leaves support around 2026-11-10 and .NET 9 already has, so multi-targeting buys a dying LTS. **Decided against, recorded so it is not re-derived.** |
| ~~CSS template parts unswept~~ | **closed by §39** | 14 parts swept for two colours; 13 broken pairs found and fixed, one exemption that expires. Fonts are still unswept (§39.5) |
| A theme rule that matches nothing at runtime is silent | §30.5 | the parse cannot know what is on screen; the sweep catches it at test time, a host loading a bad theme is not told |
| No icons | §26.12 | needs an icon system, not a property; `Icon=` was used zero times across five repositories |
| No native macOS menu bar | §26.12 | a genuine platform defect rather than a missing feature — macOS puts a menu strip in the window where the platform expects one at the top of the screen |
| Contrast shortfall | §23.4 | measured and left alone |
| `LunaSettings` process-global statics | §21.3 | every consumer suite inherits the race; the harness ships the refusal, the hazard is structural. **The store's default root was a separate defect and is closed by §43** — the race is not |
| ~~Nothing enforces that a cited `§` resolves~~ | **closed by §44** | 116 distinct citations, all resolving; `CitationTests` now fails the build on one that does not, with a second assertion so a scan that reads nothing cannot pass quietly |
| macOS first-draw warm-up: **scope narrowed, mechanism inferred** | §38.4, §38.6 | not per-process and not per-window; `Redraw` is correct under any mechanism with that scope, so this is characterisation debt rather than a defect |
| `ConsolePane` cannot announce line by line | §24.4 | the trade is recorded; a live region would re-read the whole buffer |

### 34.3 Versioning, when this is called done

**0.7.0 is not released.** There is no `v0.7.0` tag, so everything since 0.6.0 — the shell (§26), the
table (§27), the two reflection guards (§28), the theme split (§29), the style-key defect and its
fix (§30), and §31 through §33 — is unpublished and currently rides in 0.7.0.

Two things to do at that point, in this order:

1. **Both `<Version>` elements move together.** The toolkit's and the harness's, because §22.8's
   rule is that the harness tracks the toolkit rather than keeping its own number, and a consumer
   pairing two different ones has a question nobody wants to answer. The `.csproj` value is only the
   default for a local `dotnet pack`; the published number comes from the tag.
2. **`CHANGELOG.md` says what it means to somebody who cannot patch it**, which for this release
   includes one entry that is not additive: §30's fix changes rendering, because four CSS elements
   and the menu bar's own style start doing what they always claimed to.

~~**Whether it is 0.7.0 or 0.8.0 is a real question and is deliberately not answered here.**~~ The
version was set to 0.7.0 when §26 was the whole story. What has landed since is larger than that
number was chosen for, and §30 in particular repairs behaviour that has been wrong since 0.2.0 —
which a consumer reading a minor bump would not expect to find. **Answered in §42: 0.7.0, because it
was never published and skipping it would invent a release that never existed. The worry above is
real and belongs in the `CHANGELOG`, which now opens with it.**

---

## 35. One runner, and a correction nobody can read

§34.2 listed "CI runs on one Linux runner" as an open item on the grounds that a cross-platform UI
toolkit had never been built on Windows or macOS. Closing it turned out to be four lines of YAML.
Auditing *before* writing them is what produced this section.

### 35.1 The audit, and the one line it could not verify

**The portability audit came back nearly empty, which is the good outcome.** No hard-coded POSIX
paths anywhere in `src` or `tests`; no `HOME`, `XDG_*` or `USERPROFILE`; every settings path already
goes through `LunaSettings.Store` and `Path.Combine`. Exactly **one** `OperatingSystem.Is*` branch
exists in the whole toolkit, and it is the `UseX11()` call in `LunaApp.Configure`.

That one line turned out to be three problems stacked on each other.

**It was untested, and structurally so.** `LunaApp.Configure` had no test at all — the only public
entry point in the toolkit without one. The reason is not carelessness: the headless suite builds
its own `AppBuilder` through `LunaHeadless.BuildApp` so that the harness and a real frontend share a
theme (§3.1), which is right, and which means the sequence a consumer's `Program.cs` actually calls
is never executed by anything. Every consumer's first line of Avalonia code was unguarded.

**Its justification is unreachable.** The comment cited `EmuSen_Project_Overview_v2.md §2a` — a
document that stayed behind when LunaP left EmuSen (§19, §20). §3 in this document defers to the
same missing file. So the measurement behind the correction cannot be read by anybody working here,
which makes it exactly the kind of claim §21.6 and §29.3.1 exist to flag rather than repeat.

**And on Avalonia 12.1.0 it is a no-op.** Measured by reflecting the builder's private
`WindowingSubsystemInitializer` on this machine:

| Builder | Windowing initializer |
|---|---|
| `AppBuilder.Configure<T>().UsePlatformDetect()` | `<UseX11>b__0_0` |
| `LunaApp.Configure<T>()` — i.e. `…UsePlatformDetect().UseX11()` | `<UseX11>b__0_0` |

**Identical.** Platform detection already chooses X11 on Linux, so the explicit call changes nothing
observable at the builder.

The consequence for testing is the important part, and it is why this section exists rather than a
new assertion. The obvious test — *"on Linux the windowing subsystem is X11"* — **passes with the
`UseX11()` call deleted.** That is a test that cannot fail, and §22.5's rule is that one of those is
not a test. It was written, run, and thrown away.

**The line stays.** The original correction was made against a real symptom on a real session; the
failure it prevents would appear at `Setup` on somebody's Wayland desktop rather than in any suite
here; and removing a guard because its justification became hard to read is how the symptom comes
back. What `LunaApp.cs` says now is what is actually known, including that it is currently
unobservable. **Retiring it honestly needs one thing this repository cannot do from a test: a real
window on a real Wayland session, on the Avalonia version in use.** Until somebody does that, it is
a hazard note and not a behaviour.

### 35.2 What is asserted instead

`BootstrapTests` pins the parts of the sequence that *can* fail:

- **`UsePlatformDetect` is in the chain**, proven through what it installs rather than its own name —
  it is what brings Skia and HarfBuzz, so `RenderingSubsystemName` and `TextShapingSubsystemName` are
  the evidence. Removing the call turns two tests red.
- **The application type survives both overloads**, including the `Func<TApp>` one `EmuSen.Hotaru`
  needs because its `Main` resolves a ROM before any Avalonia type is touched. An overload that
  silently skipped the shared setup would hand that application a window with no theme.

All three assertions stop at the builder and never call `Setup` or `Start`, which is what lets them
run inside a headless session without a second `Application` fighting the session's own.

### 35.3 The matrix, and the step that would have failed on Windows

`ci.yml` now runs on `ubuntu-latest`, `windows-latest` and `macos-latest`, with **`fail-fast: false`**
— "fails on macOS only" is the single most useful thing this matrix can say, and fail-fast would
hide it behind whichever runner finished first.

Writing it surfaced a defect in §31's own work. The symbol-package assertion is a bash loop using
`${pkg%.nupkg}` and `[ -f ]`, and **GitHub defaults `windows-latest` to PowerShell**, where that is
not a syntax error but a different language. The step would have failed on Windows for a reason
having nothing to do with the packages — a red matrix cell pointing at the wrong thing, which is
worse than no matrix. It carries `shell: bash` now.

A `.gitattributes` came with it, for one reason worth stating: `ApiSurface/*.txt` is pinned to `eol=lf`
because its whole value is that the diff is the review (§32.2), and a Windows checkout rewriting it
to CRLF would make a regeneration there look like a six-hundred-line change containing nothing.

### 35.4 A correction: the matrix has still never run

**§35.4 below says "the matrix's first run happens on the next push". That is wrong**, and checking
rather than assuming is what found it.

`ci.yml` triggers on `push` to `main` and on `pull_request` targeting `main`. **`shell-and-actions`
is neither.** Pushing this branch runs nothing, and `gh run list --branch shell-and-actions` returns
no rows at all — not a failure, not a queue, nothing. Every commit in this arc (§26 through §36) has
been verified on exactly one machine, and the three-runner matrix added above has never executed on
any of them.

That is a larger statement than it looks. Everything this document says about these changes being
green is a local result. **The matrix is a change to a file, not yet evidence about Windows or
macOS**, and it becomes evidence at the moment a pull request against `main` is opened and not
before.

Whether CI should also run on branch pushes is a separate question with a real trade — the current
arrangement keeps runner minutes for merges and pull requests, which is the ordinary choice — but it
means a long-lived working branch gets no cross-platform signal at all, which is exactly the
situation this section was written in.

### 35.5 What this does not do

- ~~**It does not prove the suite passes on Windows or macOS.**~~ **It ran on PR #5 and found
  something: Windows green, macOS red on a render-stability assertion. §37 is the measurement and
  the fix.** The original caveat stands as written — this section could not know — and §35.4
  corrects how it put the mechanism. The audit is an argument
  for expecting green, not a substitute for seeing it. **If a runner comes back red, that result
  belongs in §35.5 and the audit above is what should be read sceptically.**
- **It does not test the X11 branch**, per §35.1, and no headless suite can.
- **It does not cover packaging on Windows beyond `dotnet pack` succeeding** — the `.nupkg` contents
  are asserted only for the symbol package, not compared across runners.
- **It says nothing about rendering differences between platforms.** §10.2 already keeps render
  baselines out of the repository because they are one machine's font rendering; a matrix makes that
  argument stronger, not weaker.

---

## 36. Trim and AOT, measured — and the register was wrong about where

§25 rejected LGPL partly because this is "a platform that ships trimmed and AOT'd", and §34.2 carried
"trim/AOT unverified" as an open item with a guess attached: *"`CssTheme` resolves properties through
`AvaloniaPropertyRegistry` at runtime, which a trimmer cannot see."*

**That guess was wrong, and measuring it is the point of this section.** Turning on
`IsAotCompatible` — which enables the trim, AOT and single-file analyzers together — produces six
warnings across three sites, and `CssTheme` is not among them. `AvaloniaPropertyRegistry.FindRegistered`
is a dictionary lookup against a registry Avalonia populates at type initialisation, not reflection
over members, so there is nothing for the trimmer to fail to see. The register entry has been
corrected rather than quietly deleted.

### 36.1 What is actually dynamic

| Site | Warning | Cause |
|---|---|---|
| `Settings/JsonSettingsStore.Load<T>` | IL2026, IL3050 | `JsonSerializer.Deserialize<T>` — reflection over `T`'s members |
| `Settings/JsonSettingsStore.Save<T>` | IL2026, IL3050 | `JsonSerializer.Serialize<T>` |
| `Theme/LunaTheme.Read` | IL2026, IL3050 | `AvaloniaRuntimeXamlLoader.Load` — runtime XAML compilation |

**Neither has a cheap fix, and they fail for opposite reasons.**

The JSON one cannot be source-generated *by LunaP*, because `T` is the host's type. That is §19's
entire design: `ISettingsStore` is the seam, and LunaP deliberately does not know what a host
persists. A `JsonSerializerContext` can only be written by somebody who knows the types, which is the
host.

The XAML one cannot be fixed at all. Compiling a theme a user wrote after the application shipped is
dynamic by definition.

### 36.2 The finding worth keeping: the CSS format is the AOT-safe one

`LunaTheme.Read` branches on the file extension — `.css` goes to `ReadCss`, anything else to
`AvaloniaRuntimeXamlLoader`. So **§12.2's restricted CSS dialect is AOT-safe and the `.axaml` theme
format is not**, because the CSS path is LunaP's own parser producing `Style` objects by hand while
the XAML path is a compiler.

Nobody argued that when the CSS format was introduced. §12.2 justified it on grammar and blast-radius
grounds — a syntax error refuses one file rather than taking the application down — and this is a
second, unplanned argument for the same decision, found by measuring something else.

### 36.3 Why nothing was annotated, measured rather than assumed

The obvious next step is `[RequiresUnreferencedCode]` on the two seams, which propagates the warning
to callers instead of hiding it. **It was tried, and the cascade is the reason it is not in this
commit.** Annotating `ISettingsStore.Load<T>`/`Save<T>` — on the interface, because an implementation
may not add `RequiresUnreferencedCode` its interface lacks (IL2046) — took the build from 6 warnings
to 28, across seven sites:

| Site | Feature it makes AOT-unsafe |
|---|---|
| `Theme/LunaTheme.cs:110,115` | the remembered theme (§12) |
| `Windowing/WindowPlacementStore.cs:28,37` | remembered window placement (§8.1) |
| `Windowing/PaneLayoutStore.cs:38,62` | remembered pane layout (§26.6) |
| `Theme/LunaTheme.cs:176` | `.axaml` themes (pre-existing) |

That is not an explosion, it is a **boundary**: every "remember what the user did" feature in the
toolkit is reflection-JSON-backed, and all three are opt-in. Propagating the attributes further means
annotating `ToolWindow`, `SplitPane.PaneKey` and `LunaTheme.ApplySaved` — public API a consumer sees.

**Two designs are available and this section picks neither**, because choosing between them is an API
decision and not a build-flag one:

1. **`[DynamicallyAccessedMembers]` on `T` rather than `Requires*`.** A constraint, not a warning:
   the trimmer preserves whatever `T` each call site names, and LunaP's own calls name concrete
   POCOs (`WindowPlacement`, `PaneLayout`, `ThemeChoice`), so the cascade disappears. This makes
   LunaP **trim-safe**. It does **not** make it AOT-safe — reflection-based `System.Text.Json` needs
   dynamic code regardless — so IL3050 would have to be suppressed, and suppressing it would be a
   lie.
2. **Propagate `Requires*` to the public API**, which is honest and tells an AOT consumer exactly
   which three features to avoid, at the cost of annotations on types most consumers use.

**The likely truth is that LunaP can be trim-safe and cannot be AOT-safe while the default store is
reflection JSON**, and that is a sentence worth being sure of before it is written into attributes a
consumer compiles against.

### 36.4 A correction: nobody ever chose AOT

**This section opens by saying §25 "rejected LGPL partly because this is a platform that ships
trimmed and AOT'd", and that is an overstatement of what §25 says.** Read it back:

> LGPL-3.0 … gets there by requiring the user be able to relink against a modified version. **A
> modern Avalonia desktop application ships trimmed, often AOT-compiled, sometimes single-file** —
> deployment shapes in which "relink against a different LunaP" ranges from awkward to meaningless.

That is an observation about what Avalonia applications **in general** do, made to show that LGPL's
central mechanism is hollow on this platform. It is not a decision that LunaP targets AOT, and no
such decision appears anywhere in this document. §34.2's register entry — "trim/AOT unverified" —
invented the requirement by summarising that sentence as a goal, and this section then cited §34.2
back as though it were one.

**The two halves are not equally supported, and the measurement above is worth keeping for one of
them.** §25 says "ships trimmed" flatly, so a consumer trimming their application is a real
expectation and knowing which three features break under it is real information. "Often
AOT-compiled" is hedged and about other people's applications; nothing here has ever asked LunaP to
be AOT-clean.

So §36 stands as a **measurement of what happens if a consumer does this**, not as progress against
a goal. Which is also why §36.3's conclusion is the useful part: trim-safe is reachable and worth
reaching, AOT-safe is not currently reachable and nobody has said it must be.

### 36.5 What this does not do

- **It does not declare `IsAotCompatible`, and deliberately.** Setting it would silence nothing and
  claim something untrue; §34's register now says so with the measurement attached.
- **It does not run a real AOT publish.** Everything above is analyzer output at compile time. A
  `PublishAot` build of a sample consumer would be the stronger evidence and needs a toolchain this
  machine has not been asked for.
- **It leaves the analyzers off.** Turning them on permanently would add six warnings to a build that
  currently emits none, and §31.4's argument is that a warning nobody can act on trains people to
  ignore the log. They go on with the fix, not before it.
- **The API baseline would not have caught the annotation change**, which is worth noting where it
  will be read: §32.5 already records that attributes are invisible to the surface snapshot, and this
  is the first time that gap had teeth — adding `[RequiresUnreferencedCode]` to a public interface is
  a real change to what a consumer compiles against, and `ApiSurface/*.txt` would not have moved.

---

## 37. macOS renders the first frame differently, measured

§35 added a three-platform matrix and §35.4 recorded that it had never run. Opening PR #5 ran it, and
it produced the result it was added to find: **`ubuntu-latest` and `windows-latest` green,
`macos-latest` red**, on `GalleryRenderTests.The_gallery_renders_the_same_way_twice`.

`fail-fast: false` is why that sentence can be written. With the default, one runner's failure would
have cancelled the others and "macOS only" — the whole diagnosis — would have been invisible.

### 37.1 The first failure named the wrong cause

`AssertStable` rendered twice and, on a mismatch, **stated a cause**: *"it shows something live (a
clock, a pid, a counter). It is not a valid baseline target."*

That is a hypothesis, and it was wrong. The gallery has no clock, no pid and no counter — grepping it
for `DateTime`, `Now`, `Random`, `Guid` and `Stopwatch` returns nothing — and the identical binary is
stable on two other runners and across three consecutive local runs. **An assertion that names a
cause it has not measured sends its reader to look for something that is not there**, which is
§29.3.1's failure wearing a different costume.

So it was rebuilt to report rather than assume: three renders, and a verdict derived from which ones
match. One CI round-trip then produced the answer.

| | |
|---|---|
| hashes | `15EFF8639918669A`, `DB3C094C0223FB27`, `DB3C094C0223FB27` |
| frame 1 vs 2 | **12,741 of 3,225,600 bytes — 0.39%** |
| frame 2 vs 3 | **0 of 3,225,600 bytes — byte-identical** |
| size | 720×1120 throughout |

**The first render differs from the steady state; every render after it is identical.** A 0.39%
difference spread across a page full of labels is the shape of glyph-edge antialiasing, and the
cause is a warm-up — font rasterisation caches and lazily realised platform resources — not live
content. Linux and Windows show none of it.

### 37.2 The fix, and why discarding a frame is not weakening the test

`AssertStable` renders once and throws the frame away before it starts comparing.

That sounds like sanding off a failure and is not, because of what the method exists to answer:
**"can this window be compared against a baseline?"** On macOS the answer is *yes, from the second
frame onwards*. A comparison that included the cold frame would report every macOS window as
unstable and be wrong about all of them. What the assertion still catches is exactly what it caught
before — a window whose **steady state** moves — and the three-frame message survives for when that
happens.

**The message is the part worth keeping.** It now carries three hashes, the frame sizes and the count
of differing bytes, because two frames differing in forty bytes along one glyph edge and two
differing in half the buffer are different problems, and the old message could not tell them apart.
`AssertStable` ships in `EmuSen.LunaP.Testing`, so a consumer's failures get that too.

### 37.3 The hazard this leaves on the baseline path

`AssertMatchesBaseline` compares whatever frame it is handed, so the warm-up `AssertStable` now does
for itself is **the caller's job** there. On macOS a baseline written from a cold frame and compared
against a warm one mismatches on twelve thousand bytes of antialiasing with nothing actually wrong.

It is a hazard rather than a defect: the baseline machinery is opt-in behind two environment
variables, off in CI, and §10.2 already keeps `.frame` files out of the repository because they are
one machine's font rendering. This is that finding arriving from a new direction — **the same
machine renders differently on its first frame**, which §10.2 did not know. The method's comment says
so where somebody will be standing when it bites.

### 37.4 What this does not do

- ~~**It does not explain the 0.39% at the pixel level.**~~ The cause is inferred from the shape of
  the difference and from renders two and three being byte-identical. Nobody has dumped the two
  frames and diffed them visually, which `EMUSEN_UI_DUMP` exists to make possible. **§38.5 closes
  this: the failure message now reports the bounding box and the peak channel delta, which is the
  distinction that mattered, and `AssertStable` dumps all three frames when asked.**
- ~~**It does not establish whether the warm-up is per-process or per-window.**~~ One discarded
  render before each comparison is enough either way, and cheap. **§38.4 establishes it — both of
  those answers are wrong, and the evidence was already in this section when it was written.**
- ~~**It does not fix `AssertMatchesBaseline`**, per §37.3 — that path takes a frame rather than
  building one, so warming it would mean changing its signature.~~ **Fixed in §38.3, and the
  signature never had to change: an overload that takes the window does the rasterising itself.**
- **It says nothing about Windows**, which passed both runs without a warm-up frame and is not
  covered by the discard being there.

---

## 38. What a draw actually is, and the fix that would have been a no-op

§37 left three things open, and closing them turned out to need something nobody here had
established: **when does Avalonia actually draw?** The answer changed the fix.

The obvious repair for §37.3 was to capture the window twice and keep the second frame. It compiles,
it reads correctly, it costs one extra capture, and **it does nothing at all.**

### 38.1 Counting draws, which needs the right unit

`Control.Render` does not draw. It records a **draw list**, which is why it is called once and stays
called once however many frames come out. A **custom draw operation** runs during the real Skia
pass. So a custom operation that increments a counter counts draws, and everything below is in those
units — measured on Avalonia 12.1.0, headless with `UseSkia()`, Linux.

| action on a shown, unchanged window | draws | `Control.Render` calls |
|---|---|---|
| the first capture | 1 | 1 |
| capture it again | **1** | 1 |
| capture it a third time | **1** | 1 |
| `InvalidateVisual()` on the grandparent `Border`, then capture | **1** | 1 |
| `InvalidateVisual()` on the parent `StackPanel`, then capture | **1** | 1 |
| `InvalidateVisual()` on **every visual**, then capture | **2** | 2 |
| `InvalidateVisual()` on the drawing control itself, then capture | **3** | 3 |

Two findings, and both are load-bearing:

- **Capturing an unchanged window does not draw it.** `CaptureRenderedFrame` runs the dispatcher and
  forces render-timer ticks, then calls `GetLastRenderedFrame` — which **copies** the bitmap the
  window impl is already holding. Two captures of a clean window are the same picture *by
  construction*, not by agreement. The Avalonia source says so directly: `Lock()` allocates a fresh
  `WriteableBitmap` per real draw, and `GetLastRenderedFrame` locks the stored one and copies it out.
- **Invalidation does not travel down the tree.** Every visual owns its own draw list, so dirtying an
  ancestor leaves its children's lists — and their glyphs — untouched.

A first probe looked like it had answered this and had not: each capture returns a *different*
`WriteableBitmap` instance, which reads as "a new frame was produced". It is the copy on the way out.
**Object identity was measuring the wrong thing**, and only counting draw operations settled it.

### 38.2 So the fix that suggested itself was a no-op

Had `Redraw` been written as "capture twice, keep the second", it would have returned **the same
cold frame**, and §37.3 would have been recorded as closed while nothing changed. That is the worst
category of defect this project can produce: a consumer cannot patch it, and the document would have
said it was fixed.

What forces a real second pass is invalidating **every visual in the window** and then capturing. It
is the whole of `UiTest.Redraw`, and deleting the one loop turns it back into a copy.

This is also why `AssertStable` throws away a whole *build* rather than calling `Redraw`: a freshly
constructed window has genuinely never been drawn, so its capture is a real pass. It keeps the build
for a second reason — comparing two separately constructed windows also catches construction that is
not deterministic, which redrawing one window cannot see.

### 38.3 What is fixed

- **`UiTest.Redraw(Window)`** — forces a genuine pass and captures that. New public API on the
  harness.
- **`UiTest.AssertMatchesBaseline(string, Window)`** — an overload that owns the rasterisation, so it
  cannot be handed a first draw. §37.4 said fixing this "would mean changing its signature"; it meant
  adding an overload, and the frame-taking one stays for callers who already hold a frame.
- **`UiTest.AssertLaidOut`** now rasterises through the same path, because it ends by handing its
  frame to `AssertMatchesBaseline`. The colour count never cared; the baseline comparison is the
  reason.

`RenderPassTests` pins every row of the table in §38.1. **This is a pin on Avalonia, not on anything
LunaP owns**: if a future version redraws clean windows or propagates invalidation downward, those
assertions go red, which is the notice that `Redraw` is doing unnecessary work and that the reasoning
in its comment has gone stale.

**The sabotage.** Removing the invalidation loop from `Redraw` turns `A_redraw_draws` red with
`Expected: 2, Actual: 1` — the no-op stated in draw passes. The other four stay green, correctly:
they assert Avalonia's behaviour, not LunaP's.

### 38.4 The warm-up is neither per-process nor per-window

§37.4 recorded this as unestablished and not worth establishing. Both halves were wrong: **the
evidence was already in the CI log**, and it rules out both of the answers that were offered.

- **Not per-process.** `TemplateOrderTests` calls `UiTest.Capture` on freshly shown `ToolWindow`s —
  real first draws, containing text — and it ran at positions **40 through 48** of the 379 results on
  that macOS run. The gallery's stability failure was at position **285**. Well over a hundred real
  draws happened first, and the gallery's first frame was *still* different from its second. A
  one-time, content-independent warm-up would have been consumed long before.
- **Not per-window either.** `AssertStable` builds a **new** `GalleryWindow` for every frame, so all
  three captures are first draws of their own instance. Under a per-window effect all three would be
  cold in the same way and frames one and two would have matched. Frames two and three were
  byte-identical to each other and different from frame one.

What survives is **process-global but content-dependent**: state that outlives a window being closed,
yet was not warmed by a hundred draws of *different* windows. That is the shape of a glyph or
typeface cache, which is what §37 guessed — but the *scope* above is established by elimination from
measurements, while the *mechanism* remains an inference. The distinction is the point: `Redraw`
warms the specific window it is handed, so it is correct under any mechanism with that scope.

### 38.5 The failure message describes the shape now

§37.4 wanted somebody to dump the frames and look at them. The reason was never curiosity — it was
that a byte count cannot tell these apart:

| | pixels | spread | peak channel delta |
|---|---|---|---|
| antialiasing | many | most of the image | small |
| content that moved | many | one tight box | large |

So `AssertStable` reports the differing **pixel** count, the **bounding box**, and the **peak
per-channel delta** alongside the byte count, and a reader gets the shape without opening anything.
`EMUSEN_UI_DUMP` now also yields `<name>.render1/2/3.png` from a failing run, and the message says
so, which is the part that was missing — the facility existed and the failure never mentioned it.

### 38.6 What this does not do

- **It does not explain the macOS warm-up at the pixel level.** §38.4 narrows the scope by
  elimination; nothing here dumps a macOS frame and inspects the glyph edges, because no macOS
  machine is available to this repository outside CI.
- **It does not make `AssertMatchesBaseline(string, RenderedFrame)` safe.** That overload still
  compares whatever it is handed. It cannot rasterise — it has no window — so the fix is the
  overload beside it and a comment saying which to reach for.
- **It does not warm anything automatically.** A consumer who calls `window.CaptureRenderedFrame()`
  directly and compares frames by hand is outside the harness and gets a first draw.
- **The §38.1 table is one platform.** The draw-counting was measured on Linux. It is a statement
  about Avalonia's headless renderer, which is shared code, but no runner has confirmed the counts
  on Windows or macOS — `RenderPassTests` runs everywhere, so the matrix answers this on the next
  push rather than leaving it to argument.

---

## 39. Every template part, swept — and three ways a part selector dies

§30.5's first bullet: *"It does not cover template parts. The sweep tests each element, not
`card .header` or `console-pane .output`. A part whose selector reaches nothing would still be
silent, and the same argument that justifies this test justifies extending it — it has not been."*

It has been now. **Fourteen parts across seven elements**, of which exactly one — `meter-row .bar` —
had a test. The sweep found **thirteen broken (part, property) pairs**, from three unrelated causes.

The bullet understated the risk, and the reason is worth keeping. A part selector is strictly more
fragile than an element one: it is `Template()` **plus** a type **plus** a `PART_` name, and each of
the three can be wrong independently. **The case with more ways to fail was the one going
unchecked.**

### 39.1 The sweep asks the parser what to test

Cases are generated, not listed: for every element, for every part, for `background` and `color`.
Both colours, because one is not a proxy for the other — a part whose template pins `Foreground` and
not `Background` passes one and fails the other, and the first draft of this sweep tested only
`background` and would have reported nine of these thirteen as fine.

**Inapplicable combinations are not defects and are never emitted.** `CssTheme.Parse` already refuses
them — `'color' does not apply to 'split-pane .rule'`, because a `Border` has no `Foreground` — so
the generator parses each candidate first and drops the ones the vocabulary itself rejects. The
product's own validation decides the subjects, and a part that gains a property gains its case on the
same commit.

That leaves **26 cases**: 14 parts × 2 colours, less that one refusal and less the one exemption in
§39.4. `The_part_sweep_has_subjects` guards the count, because a `[Theory]` with no cases is a pass
(§30.6 learned that once already).

**The assertion is deliberately not "look up `PART_Header` and read it back".** That would prove the
*test* can find the part, not that the *selector* can — and a selector naming the wrong type is
precisely the defect being swept for. So it applies the rule and asks whether anything inside the
control now carries the sentinel colour, which is the only claim the rule actually makes.

### 39.2 A value written into a `ControlTemplate` outranks any theme

Nine of the thirteen, and the finding worth taking away from this section.

`Background="{DynamicResource LunaBorder}"` written as an **attribute inside a `ControlTemplate`**
binds at `BindingPriority.Template`. A style — including every rule a host theme can write — binds at
`BindingPriority.Style`, which is **lower**. So the rule parsed, produced a valid selector, matched
the part, and lost.

Measured with `GetDiagnostic` rather than reasoned about, because §29.3.1 is the record of what
asserting an unmeasured cause costs here:

| after applying the rule | part found | value | priority |
|---|---|---|---|
| `split-pane .rule { background }` | `PART_Rule` (`Border`) | `#ff6e6e6e` | **`Template`** |
| `card .header { color }` | `PART_Header` (`ContentPresenter`) | `#ff9cdcfe` | **`Template`** |
| `console-pane .output { color }` | `PART_Output` (`SelectableTextBlock`) | `#ffd4d4d4` | **`Template`** |
| `empty-state .message { color }` | `PART_Message` (`TextBlock`) | `Gray` | **`Template`** |
| `side-panel .close { background }` | `PART_Close` (`Button`) | `Transparent` | **`Template`** |

**The fix is where the value is written, not what it is.** Each moved out of the template and into a
style setter targeting the part:

```xml
<Style Selector="luna|SplitPane /template/ Border#PART_Rule">
    <Setter Property="Background" Value="{DynamicResource LunaBorder}" />
</Style>
```

**The defaults did not change, and that was checked rather than assumed.** With no host theme
applied, every one of the thirteen values is byte-identical to what it was as an attribute — only the
priority moved:

| | as an attribute | as a setter |
|---|---|---|
| `card` `PART_Header` `Foreground` | `#ff9cdcfe @Template` | `#ff9cdcfe @Style` |
| `console-pane` `PART_Input` `Background` | `#ff252526 @Template` | `#ff252526 @Style` |
| `side-panel` `PART_Close` `Background` | `Transparent @Template` | `Transparent @Style` |
| `split-pane` `PART_Rule` `Background` | `#ff6e6e6e @Template` | `#ff6e6e6e @Style` |

What changed is **who is allowed to override them**, which is the whole point.

Every property the CSS vocabulary publishes was moved, not only the two the sweep tests — so
`console-pane .input { font-family }` works for the same reason even though nothing asserts it yet
(§39.5). `CaretBrush` stays an attribute on purpose: no CSS property maps to it, so nothing can be
competing for it, and moving it would only suggest otherwise.

### 39.3 `PART_Facet` was declared a `TextBlock` and is a `Dropdown`

Two of the thirteen, and **§30's defect in a new place.** The vocabulary said
`["facet"] = new(typeof(TextBlock), "PART_Facet")`. The template contains a `Dropdown`. The selector
asked for a `TextBlock` named `PART_Facet`, which cannot exist, so `filter-bar .facet` had never done
anything in any release that shipped it.

The correction is **`ComboBox`**, which looks wrong and is not, for the reason §30 exists: `Dropdown`
pins `StyleKeyOverride` to `ComboBox`, and **a type selector matches the style key, not the runtime
type**. Naming `Dropdown` here would have been dead in exactly the same way for a different reason —
the same trap, one layer down, caught the same afternoon it was written.

No style class is needed as it is for element selectors: the `PART_` name already makes it unique.

### 39.4 One exemption, and it expires

`meter-row .bar { color }` cannot be made to work, and should not be.

The bar takes its colour from three state styles — `:nominal`, `:busy`, `:hot` — and **a selector
carrying a pseudo-class binds at `StyleTrigger`, which outranks `Style`.** Measured: after applying
the rule the bar is `LimeGreen` at priority `StyleTrigger`. No stateless rule can win. A meter whose
colour stopped tracking its load would be a worse control, so the limit is correct; a host that wants
other colours names the state, and `A_rule_can_reach_a_state_and_a_template_part` has proved that
form works since §12.

It is an entry in an `Exempt` table with that reason, following §28.2 — never a quiet deletion from
the sweep. **And the exemption asserts that its own limitation is still real.**
`An_exemption_is_still_needed` requires the stateless rule to keep failing, so if some future change
makes it work, the test turns red saying *delete this entry* rather than leaving a stale excuse in a
table nobody rereads. An exemption list that cannot expire is how the four dead selectors in §30
survived four releases.

### 39.5 What this does not do

- ~~**It sweeps two properties, not six.**~~ `background` and `color`; `font-family`, `font-size` and
  `font-weight` are generated the same way and are not tested. The selector reach is
  property-independent, so §30.5's actual gap is closed — but a template that pins `FontFamily` as an
  attribute would block `font-family` while `color` passed, which is a real and untested hole. It is
  narrowed rather than open: every CSS-settable property in the six templates touched here was moved
  to a setter. Widening `Colours` is a two-line change and a sentinel per property. **Widened in
  §40.2 — 65 cases now, and the hole this bullet describes was already closed by the fixes above,
  which is why it found nothing new.**
- **It does not sweep states.** `meter-row.hot .bar` has one test; the other state × part
  combinations do not.
- **It does not check the value is correct**, only that the part received it. A rule that sets the
  wrong shade is indistinguishable from one that works.
- **It cannot see a part that is never realised.** `Configure` gives `Card` a header and `SidePanel`
  a title because those parts collapse when empty (§26.9) — correct behaviour that would otherwise
  read as a dead selector. A part that no configuration realises would still be invisible here.
- **It says nothing about a host's own `.axaml` styles.** The priority rule found in §39.2 applies to
  anything a consumer writes as a template attribute in their own controls, and nothing here can
  reach that.

---

## 40. The exemption was the wrong answer, and this file said so already

§39.4 exempted `meter-row .bar { color: … }` from the sweep, documented why it cannot win, and built
an expiring-exemption test so the entry could not rot. All of that machinery was answering the wrong
question.

**The rule still parsed.** A host could write it, get no warning, and see nothing happen — which is
the defect §30 exists to abolish, arrived at from the other direction. And this repository had
already written the rule that settles it, in the failure message of the element sweep, where it has
sat since §30:

> *"A rule that is accepted and does nothing is worse than one that is refused."*

The exemption made the **test** honest about a promise the **product** was still making.

### 40.1 Refused, with the two spellings that work

`PartSpec` gained a `Shadowed` map: CSS property → what overrides it. The parser checks it after the
property resolves and warns instead of emitting a setter.

```
meter-row .bar { color: #FF00FF; }
  warning: 'color' on 'meter-row .bar' is always overridden by the :nominal, :busy and :hot
  state styles. Name the state - 'meter-row.busy .bar { color: ... }' - or restyle all three
  at once by setting the --luna-nominal, --luna-busy and --luna-hot tokens
```

**A bare refusal would not have been enough.** Whoever wrote that rule cannot tell a shadowed
property from a typo, and the two things want opposite responses. So the warning names both working
spellings — and the tokens are named first among equals because they are what the host almost always
wants: `--luna-nominal`/`--luna-busy`/`--luna-hot` restyle the meter in every state at once, and were
the designed path before the part vocabulary existed.

**Three properties of the refusal are pinned**, because each is a way this could be subtly wrong:

| | |
|---|---|
| `meter-row .bar { color }` | refused, one warning, naming the state form and the tokens |
| `meter-row.busy .bar { color }` | **accepted, no warning** — it binds at `StyleTrigger` like the style shadowing it, and being applied later it wins. Warning about the spelling that works would be worse than silence |
| `meter-row .bar { background }` | **accepted, no warning** — the shadow is per property, not a ban on the part |

The sweep needs no exemption logic at all now: it already skips whatever the parser rejects, so the
case removes itself. §39.4's `Exempt` table, `Exemptions()` and `An_exemption_is_still_needed` are
gone, replaced by the three assertions above.

**This is not a retraction of §39.4's reasoning, only of its conclusion.** The priority argument
there is correct and is why the refusal is right rather than a workaround: the rule genuinely cannot
win, so the only question was whether to say so.

### 40.2 Every themeable property, swept

§39.5's first bullet named the remaining hole: a template pinning `FontFamily` blocks `font-family`
while `color` passes. The sweep now covers all five, with a sentinel per property that cannot be a
default — `Probe Sentinel Mono`, `33.5`, `black` — compared as Avalonia values rather than as text.

**65 cases**: 14 parts × 5 properties, less the 5 the parser refuses — four on `split-pane .rule`,
since a `Border` has no `Foreground` and no font at all, and the shadowed one above.

**It found nothing new, and that is a result rather than an anticlimax.** §39.2 moved *every*
CSS-settable property in the six templates it touched, not only the two under test, so the hole was
closed before this could measure it. What matters is that the claim is now asserted rather than
argued.

**A sweep that finds nothing has to be shown capable of finding something.** Putting
`FontFamily="{DynamicResource LunaMonoFont}"` back as an attribute on `PART_Output` turns exactly one
case red — `console-pane .output`, `font-family` — and nothing else.

### 40.3 What this does not do

- **`Shadowed` is a hand-maintained list of one**, and nothing derives it. It does, however, expire:
  the sentence that stood here said "a test *could* assert the shadow is still real the way the
  exemption did", which is precisely the paragraph-asking-the-next-author-to-remember that §28 exists
  to abolish — four of those had already been written before they became assertions. So
  `The_shadow_on_the_meter_bar_is_still_real` asserts the *reason*: the bar's `Foreground` arrives at
  priority `StyleTrigger`. If MeterRow ever stops colouring its bar from state styles that drops to
  `Style`, a stateless rule starts winning, and the test turns red asking for the entry to be
  removed. A refusal that outlives its reason is worse than the silence it replaced.
- **It only covers what the vocabulary publishes.** A host writing a rule that loses to something
  other than a state style still gets silence; nothing in the parse knows about priorities in
  general, and §30.5's third bullet still stands.
- **The sweep still does not check the value is correct**, only that the part received it.
- **States other than `meter-row.hot` remain unswept**, per §39.5.

---

## 41. Every member says what it does, and what its arguments mean

§33 documented sixty-three public types and stopped, on an argument that was right: `CS1591` would
have demanded a sentence for all 460 public members, and 99 of them are Avalonia property fields and
protected framework overrides where the only available sentence restates the name. §34.1 wrote down
what that left — *"almost no member does"* — and called it the largest item in the register.

It is done. **The shipped `EmuSen.LunaP.xml` now carries 379 entries**, read out of the `.nupkg`
rather than the build directory:

| | |
|---:|---|
| 63 | types |
| 144 | methods and constructors |
| 109 | properties |
| 51 | fields and constants |
| 12 | events |
| **212** | `<param>` tags, across 122 members |
| **85** | `<returns>` |
| **14** | `<exception>` |

§34.1's own example, `LunaTable<T>.Column`, now ships a summary, a sentence for each of its three
parameters, and what it hands back.

### 41.1 The rule, chosen so filler cannot satisfy it

1. Every member that is not excused has a `<summary>`.
2. **Every parameter has a `<param>`.** This is the half that was entirely absent and carries the
   most: a name and a type do not say whether a `Func` is called once or on every row.
3. Every non-void return has a `<returns>`.
4. None of the above is empty, `TODO`, or under twelve characters.

The threshold caught one of my own: `<param name="text">The label.</param>` is ten characters and
says nothing the parameter name did not.

### 41.2 What is excused, and why none of it is laziness

§33.2's argument extended rather than repeated. A member is excused when the only available sentence
restates something the reader already has:

| Excused | Why |
|---|---|
| `StyledProperty` / `DirectProperty` fields | the backing field for a property that carries the sentence (§33.2) |
| protected framework overrides | `OnApplyTemplate`'s contract is Avalonia's; restating it here is how it comes to disagree with Avalonia's |
| **overridden properties** | `StyleKeyOverride`, on all eight controls that borrow a stock template — the same argument, and the methods-only rule missed it |
| **enum `value__`** | the storage the compiler emits. There is nowhere to put a `///` on it |
| **parameterless constructors** | `new ButtonBar()` takes no arguments and makes the type it is named after |
| property and event accessors | the property carries the sentence |

**Excused means not required, not forbidden.** `ToolWindow()` is a parameterless constructor and does
carry something — *"restores its own position and closes on Escape"* — so it has one.

### 41.3 The delegate seams, which is where this was worth most

§34.1 said the seams bite hardest, and named the case: `LunaList<T>.Key` defaulting to reference
identity, *"right for a cached model, wrong for rows rebuilt on every poll"*, documented in a `//`
comment a consumer never sees. It is a `<remarks>` on both `LunaList<T>.Key` and `LunaTable<T>.Key`
now, in those words.

The same applies wherever a delegate crosses the boundary — `Label` and `text` are called for every
item on every `Refresh`, so they must be cheap and free of side effects; `HistorySource` is called
each time recall starts, so a live list needs no notification; `WindowSlot.create` runs **only** when
nothing is open and `refresh` runs either way, which is the whole reason both parameters exist.

### 41.4 The guard on the guard

`MemberDocumentationTests` builds XML documentation IDs from reflection — generic arity as `` `1 ``,
a type's generic argument as `` `0 `` and a method's as ``` ``0 ```, byref as `@`. **A bug in that
construction would report every member as undocumented forever, or worse, match nothing and pass.**

So `Every_documented_member_is_one_this_test_knows_about` walks the other way: every `M:`/`P:`/`F:`/`E:`
key in the `.xml` must be one this file also generated. It earned its place twice — first on `T:`
keys, which belong to `DocumentationTests`, and then on `ActionSync`, an **internal** type whose
members are documented deliberately. Documenting an internal type is good practice and must not fail
here, so the check tolerates any member of a non-public type while still catching an ID it cannot
construct.

**The sabotages:** deleting the summary on `Card.HasHeader` names it; deleting one `<param>` from
`LunaTable<T>.Column` reports *"no `<param>` for: width"* — the member and the parameter, not just a
count.

### 41.5 What this does not do

- **It does not check that a sentence is TRUE.** Twelve characters of confident nonsense passes. This
  is the same exposure every comment in the repository has, and CLAUDE.md's answer applies here with
  more force, because a consumer reads these without the code beside them.
- **`<exception>` is documented where the toolkit throws, and nothing enforces that.** Fourteen tags,
  found by grepping for `throw new` and checking each public member. A new guard clause added
  tomorrow gets no tag and nothing says so — reflection cannot see a `throw`, so this would need
  Roslyn or an analyzer.
- **It does not require `<remarks>`, `<example>` or `<see cref>`.** They are used where they carry
  something and are nowhere enforced.
- **Nothing renders this as browsable documentation.** It reaches IntelliSense, which was the point;
  a docs site is a separate decision nobody has made.
- **The twelve-character threshold is arbitrary and deliberately low.** It catches an empty tag or a
  stub, not a sentence somebody thought about and got wrong.

---

## 42. The version, decided: 0.7.0

§34.3 left this open on purpose: *"Whether it is 0.7.0 or 0.8.0 is a real question and is
deliberately not answered here."* The reason it gave was that the number was chosen when §26 was the
whole story, and what landed since is larger than that — §30 in particular repairing behaviour wrong
since 0.2.0, *"which a consumer reading a minor bump would not expect to find."*

That observation is correct and points at the wrong instrument. **It is 0.7.0.**

### 42.1 Because 0.7.0 was never claimed

Checked rather than assumed, against nuget.org and against the tags:

| | |
|---|---|
| `EmuSen.LunaP` on nuget.org | 0.2.0, 0.3.0, 0.4.0, 0.5.0, **0.6.0** |
| `EmuSen.LunaP.Testing` on nuget.org | 0.3.0, 0.4.0, 0.5.0, **0.6.0** |
| git tags | `v0.2.0` … `v0.6.0` |

**Nobody has 0.7.0.** It is not published, not tagged, and not resolvable. Skipping it would leave a
permanent hole in the sequence that implies a release which never existed, and a consumer scanning
the version list would reasonably wonder what 0.7.0 was and whether they had missed it.

**A version number identifies a release; it does not express magnitude.** Under 0.x semantics minor
is the only lever there is — it carries features and breaking changes alike, and there is no such
thing as a bigger minor. Reaching for 0.8.0 to signal "this one is substantial" would be using the
version to say something the `CHANGELOG` is for, and creating a phantom release to say it.

So §34.3's worry is answered where it belongs: the 0.7.0 entry now **opens** with the fact that the
release is not purely additive, and names both places, before it gets to the shell.

### 42.2 Nothing was removed, measured

The question a consumer actually asks of a version bump is whether their code still compiles.
`git diff v0.6.0..HEAD` over `src/` reports eleven public declarations removed — and all eleven are
`CssTheme` and `CssThemeResult` members that §29.4 **moved between files** when the 547-line theme
file was split. The type and its namespace deliberately did not change (§29.4 says why), and all
eleven are present in the committed API baseline today.

**No public member was removed or renamed since 0.6.0.** The release is additive in surface, and
changes behaviour in the two places §42.1 names.

### 42.3 Why not 1.0.0, which is the question this arc invites

The whole arc from §31 to §41 was "make this production ready", and 1.0.0 is what that usually gets
called. It would be wrong, because **1.0.0 is a promise that a breaking change requires 2.0.0**, and
§34.2's register still holds items that cannot be delivered without one:

- **No icons.** §26.12 is explicit that this needs an icon *system* rather than a property, and a
  system reaches `LunaAction`, `ToolBar` and `Menus` at once. `Icon=` was used zero times across five
  repositories, which is why it was deferred — not because it is small.
- **`LunaSettings` process-global statics** (§21.3). Every consumer suite inherits the race; the
  harness ships a refusal rather than a fix because the hazard is structural. Fixing it changes the
  seam.
- **No native macOS menu bar** (§26.12), recorded as a genuine platform defect rather than a missing
  feature. Fixing it changes how `AppWindow` builds its menu.

Calling this 1.0.0 would freeze a surface three open register entries say has to move. 0.x is not a
lack of confidence in what is here; it is an accurate statement about what is not.

### 42.4 What this does not do

- **It does not tag or publish.** The tag is what publishes (`publish.yml`), a version on nuget.org
  cannot be replaced, and that is a decision for a person rather than a side effect of a pass.
- **Both `<Version>` elements already read 0.7.0** and are unchanged, so there is no bump here — only
  a decision, and a `CHANGELOG` that now leads with what the number cannot say. §22.8's rule that the
  harness tracks the toolkit still holds, and both still agree.
- **It does not re-derive the register.** §34.2 is where the open work lives; §42.3 quotes three
  entries from it and does not restate the rest.

---

## 43. The default settings root was one directory shared by every project on the machine

`JsonSettingsStore.ForApplication()` named itself after the entry assembly, so an application that
never configures LunaP still got its own folder. Under `dotnet test` the entry assembly is
`testhost` — and that is not a guess that happens to be wrong. It is **the same wrong answer for
every project on the machine**.

### 43.1 What was actually on the machine, and a correction to what was first said about it

`~/.config/testhost/` on the development machine held three things:

| File | Contents | Written by |
|---|---|---|
| `windows.json` | `pegasus`, `pegasus-signin` | **Pegasus's test suite** |
| `luna.json` | `{"Name": "Probe"}` | a throwaway probe run while writing §39 |
| `themes/Probe.css` | `side-panel .title { color: #FF00FF; }` | the same probe |

**The first account of this was wrong and is corrected here rather than quietly replaced.** It was
reported as *"LunaP's test suite writes into your real config directory"*, on the evidence that the
files existed and LunaP's fixtures touch `LunaSettings.Store`. That is presence, not causation, and
the two were never separated.

They are separable, and the measurement is cheap: move the directory aside, run the suite, look. The
directory was **not** recreated, and all 465 tests passed. LunaP's own suite redirects the store in
every fixture that touches it — `WindowingTests`, `ShellTests`, `ThemeTests` and `CssThemeTests` all
assign one in the constructor and reset it in `Dispose`. It was clean, and the claim that it was not
was made without checking.

**What the evidence actually showed was worse.** `pegasus` appears nowhere in this repository except
one comment in `UiSession.cs`. Those keys were written by a *consumer* — `Shell.PegasusWindow` sets
`WindowKey <- "pegasus"`, `UiTests.fs` shows the window and closes it, and `ToolWindow` saves
placement on close through the default store. Pegasus is on LunaP 0.6.0, and its suite never touches
`LunaSettings.Store` because nothing ever told it that it should.

So the defect was never this suite's hygiene. It was **the toolkit handing every consumer's test
suite a live write into a directory shared with every other project on the machine**, and it had
already happened to somebody.

### 43.2 The read is worse than the write, and it is why this is not merely untidy

Litter in `~/.config` is a nuisance. The direction that matters is the other one: `ToolWindow`
*restores* from the same file, keyed by `WindowKey`. Two projects whose windows are both called
`"main"` — not a stretch — restore each other's geometry, and a test asserting a default size
passes or fails according to **what else has been built on that machine, and when**. That is a
failure with no local cause, no reproduction on a fresh checkout, and no reason for anybody to
suspect the toolkit.

`LunaTheme` reads the same way. A saved theme name lands in `luna.json` at the shared root, so one
project's test suite can select a theme for another's.

### 43.3 The fix, and the mistake that would have cost more than the bug

The measurement first, because it decided the destination. Under VSTest:

    entry.Name      = testhost
    entry.Location  = <the test project's own bin>/testhost.dll
    BaseDirectory   = <the test project's own bin>/
    ApplicationData = /home/red/.config

The entry assembly's *name* is shared; its *location* is not. So the diverted root is
`AppContext.BaseDirectory` + `lunap-settings` — per-project by construction rather than by
heuristic, already ignored by git, wiped by `dotnet clean`, and where somebody hunting for the file
would think to look. A temp directory keyed by a hash of that same path was considered and rejected:
identical isolation, none of the legibility.

Three rules, each of which is an assertion in `SettingsRootTests`:

- **A name the host passes is honoured whatever it says**, including `"testhost"`. Only a name that
  was *guessed* is ever overridden.
- **The divert is stated, not silent.** It reports through `LunaSettings.Report`, which is where
  every other "carried on, but you should know" message in the toolkit goes (§26.5).
- **The match is exact, or the name followed by a dot.** `testhost` ships `testhost.x86` and
  `testhost.arm64`, which is why there is a suffix rule at all.

That last one was first written as a bare `StartsWith`, which is wrong in the expensive direction.
It matches an application genuinely called `TestHostApp`, and matching one would move a **real
user's** settings out of their configuration directory and into a bin folder that the next
`dotnet clean` deletes. Weigh the two errors: a missed runner litters a directory; a wrong match
destroys somebody's window layout and theme. The false positive is far more costly, so the rule is
the tight one.

**And nothing else in the suite could have caught that**, which is the point worth keeping. Every
assertion in `SettingsRootTests` runs inside a test host, so every one of them exercises the
diverting branch — widen the match until real applications hit it too and all of them stay green.
`Matching_is_tight_enough_not_to_catch_a_real_application` is a `[Theory]` over the predicate
itself, reached by reflection because it is private and should stay private; adding public API to a
package a consumer cannot patch, for the benefit of one test, is the wrong trade. `TestHostApp` and
`testhostile` are cases in it so the bare-`StartsWith` version cannot come back.

**Sabotage.** Replacing the predicate with `=> false` turns three of the six behavioural assertions
red — the two about where the root is, and the one about the report — and correctly leaves the other
three green, since a name the host passed does not go through that branch at all. Reverted; all
pass.

**Not a breaking change.** A real application's entry assembly is never named `testhost`, so no
consumer's settings move. This is a patch.

**Not fixed in the harness, deliberately.** The obvious place looked like `EmuSen.LunaP.Testing`,
which already refuses to start a suite that has not disabled parallelisation (§22.8). It is the
wrong place: Pegasus rolls its own harness in F# and never references that package, so a fix there
would have missed the one consumer known to have hit this. The toolkit is what every consumer has.

## 44. Every `§` now has to resolve

CLAUDE.md has said "every `§` cited from code must resolve" since it existed, and §34.2's register
has carried "a `§`-citation guard" as an open item for as long. Checking it took one pass over the
tree: **116 distinct sections cited across the toolkit, the harness, the suite, `README.md`,
`CHANGELOG.md` and `CLAUDE.md` — and every one of them resolved** against the 199 sections the man
page defines.

That is a good result, and on its own it would be an argument for leaving the rule as a rule. **The
argument against that is §16.1, which is this document's own record of the rule failing badly.** At
the time of the move: **68 citation sites across 43 files, of which 55 named a document that did not
exist under that name**, fourteen pointed at section numbers that did not exist at all, and four
numbers each resolved to two different headings, so a citation to any of them was undecidable. The
file corrected *during* the move came out of it citing a section that resolved to nothing — the
tidy-up introducing the defect it was tidying.

**The suite was green throughout all of it.** Nothing in a build or a test run could see any of it,
and it took a deliberate hand audit to find. That is the whole case: this is not a rule that has
held because it is easy, it is a rule that has already been broken at scale, silently, and was
restored by one person choosing to check.

The likely cause is not a typo either — it is renumbering. Split a section, fold one into its
neighbour, drop a number from a heading, and thirty pointers that were correct on Tuesday are wrong
on Wednesday with nothing to say so.

So it is `CitationTests` now, and the paragraph asking the next author to remember is deleted.

**The trap it would otherwise have fallen into.** The assertion passes if the scan finds nothing at
all — a renamed directory, a glob that quietly stops matching `.axaml`, a path that resolves
somewhere empty. A guard that goes quiet when its input vanishes is worse than no guard, because the
green tick is what gets read. `The_scan_actually_reads_something` puts floors under all three
inputs, set well below the real counts so ordinary editing never touches them and a collapse to zero
is caught at once.

It reads the source tree rather than the build output, since neither the man page nor the sources
being scanned are copied to `bin`. Getting from `bin` back to the tree is the only interesting part
of it, and the first attempt was wrong.

### 44.1 `[CallerFilePath]` does not survive this package's own build settings

The first version found the repository with `[CallerFilePath]` and walked up from the test file.
That works locally and **failed on all three CI platforms at once**, with:

    Walked up from /_/tests/EmuSen.LunaP.Tests/CitationTests.cs without finding docs/LunaP.md

`/_` is the tell. §31 turned on SourceLink and symbol packages so a consumer can step into LunaP's
own source in a debugger, and CI sets `ContinuousIntegrationBuild=true`, which switches on
**deterministic source paths**: every source path the compiler embeds is rewritten to a fixed root
so that two checkouts in different directories produce byte-identical binaries. `[CallerFilePath]`
is one of the things it rewrites. The path compiles to somewhere that has never existed.

**This is a property of LunaP's own packaging decisions, not a CI quirk**, which is why it is worth
a subsection rather than a commit message. Anybody reaching for `[CallerFilePath]` in this
repository will hit it again, and it will look like a CI failure rather than a consequence of §31.

**It reproduces locally in one command**, which is the part worth keeping, because the whole cost of
this mistake was a round trip through CI to discover something a laptop can show in ten seconds:

    dotnet test -c Release -p:ContinuousIntegrationBuild=true

Measured both ways, with a throwaway probe returning `[CallerFilePath]`:

| Build | What the compiler embedded |
|---|---|
| `dotnet test` | `/home/red/Projects/EmuSen.LunaP/tests/EmuSen.LunaP.Tests/PathProbe.cs` |
| `-p:ContinuousIntegrationBuild=true` | `/_/tests/EmuSen.LunaP.Tests/PathProbe.cs` |

That flag is the right way to check **any** change that could behave differently under a packaging
build, not only this one — it is what CI does, and nothing else here runs it.

`AppContext.BaseDirectory` has none of the problem: it is resolved at run time from where the
assembly actually sits, which is `<repo>/tests/EmuSen.LunaP.Tests/bin/<config>/net10.0`, and walking
up from there finds the man page on every platform.

**The guard failed loudly rather than quietly, and that was the design working.** A version that
silently found no files would have reported "all citations resolve" — a green tick meaning nothing,
on the exact rule §44 exists to stop being taken on trust. `RepoRoot` asserts rather than returning
a best guess for precisely this reason, and it is the reason the mistake cost one CI run instead of
becoming permanent.

## 45. The version: 0.7.1

A patch, and the reasoning is short because the change is narrow.

**No public surface moved.** `ForApplication`'s signature is unchanged and
`ApiSurfaceTests`' committed baseline needed no edit — the only additions are a
`<remarks>` block on the method and two private members. Nothing was removed or
renamed, so nothing a consumer compiles against can break.

**No application behaviour moved.** The divert fires only when the entry assembly
is a test runner, and a real application's never is. A consumer who upgrades and
changes nothing sees no difference at run time; only their *test* process does,
and only if it was relying on the default store, which is the defect.

**So the argument for a minor was weighed and refused.** §42.1's rule for 0.7.0
was that a release stops being additive when it changes what a consumer already
sees, and by that rule this qualifies: a suite that read back placement written
under the shared root will now read back nothing. But what it read back was
another project's data or its own by luck, `windows.json` is machine-local state
that no build depends on, and the fix restores the behaviour the documentation
already described. Calling that a feature change would say the old behaviour was
a feature.

**§44 ships nothing.** `CitationTests` is in the test project, which is not
packaged.

Both `<Version>` elements read 0.7.1 and agree, per §22.8. As with §42.4: this
records a decision and does not tag or publish, because a version on nuget.org
cannot be replaced and that is a person's call.
