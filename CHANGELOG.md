# Changelog

What changed between released versions, for somebody deciding whether to take
one. The reasoning lives in `docs/LunaP.md` and is cited by `§`; this file says
what moved and what it costs to follow.

Versions are the git tag. `EmuSen.LunaP` and `EmuSen.LunaP.Testing` ship from
the same tag at the same number, because the harness asserts about the toolkit's
own controls and pairing two versions of them is a question nobody wants to
answer.

---

## 0.5.0

### Fixed

- **Nine of the toolkit's controls were not in the automation tree at all**, so a
  screen reader never reached them — `MeterRow`, `MeterList`, `EmptyState`,
  `FieldRow`, `PathPickerRow`, `FilterBar`, `ConsolePane`, `StatusBar` and
  `RgbaImageView`. Avalonia's default peer reports `IsControlElement = false`,
  and a templated control's parts are hidden on the assumption that the control
  speaks for them; these controls never did, so label and value vanished with
  them. A dashboard of meters reached a reader as anonymous percentages with
  nothing to say what they measured (`§24.1`).
- **`EmptyState` was silent** — the one control whose job is to explain why a
  window is empty was the one thing a screen reader could not see.
- **`LunaSwitch` announced as an unnamed button.** Its label lives in
  `OnContent`/`OffContent` (`§14.1`) and Avalonia's toggle peer reads `Content`.
- **Eight of eleven reachable tab stops announced as nothing** — five text
  boxes, a dropdown, a selectable text block and the switch. `FieldRow` now
  lends its label to the control inside it via `LabeledBy`, and `FilterBar` and
  `PathPickerRow` name their parts from properties they already had.
- **A page of `PathPickerRow`s was a page of buttons all called "Browse..."**
  The name stays "Browse..." — an accessible name that drops the visible label
  breaks voice control — and `BrowseTitle` becomes the button's help text.

### Added

- `Automation/LunaAutomationPeer` — one peer, taking a control type and
  delegates, so a control reports its live property rather than a captured
  string. An explicit `AutomationProperties.Name` always wins (`§24.2`).
- `Fluent/AccessibilityExtensions` — `.AccessibleName()`, `.HelpText()`,
  `.LabeledBy()`, `.LiveRegion()`, `.Decorative()`. The attached form was
  available all along and used zero times across four applications (`§24.3`).
- `StatusBar` is a `Polite` live region by default; set `LiveSetting` to `Off`
  if your status updates continuously.

### Changed

- **`ButtonBar` reports `ToolBar` where it used to report `List`.** A row of
  OK/Cancel is a run of commands, not a two-item list. Nothing in any known
  consumer queries an automation control type, so this breaks nothing today —
  it is here because it would break a UI test written tomorrow.

### Known

- **No screen reader has been run against any of this.** Every measurement is of
  Avalonia's automation tree, not of Orca, NVDA or VoiceOver reading it. Being
  in the control view is necessary and is not the same as verified end to end
  (`§24.4`).
- `ConsolePane` output cannot be announced line by line: it is one text block
  holding the joined buffer, so a live region would re-read the whole history on
  every append. The trade is recorded rather than half-solved (`§24.4`).
- **Avalonia 12.1.0's `TextBlockAutomationPeer` ignores
  `AutomationProperties.Name`**, returning `Text` instead. Reproduction in
  `§24.5`.
- The consumers are untouched. Every `Group` and `Image` above still needs a
  name only the application can supply.

---

## 0.4.0

### Added

- **A light palette.** Every colour key now has a `Light` column alongside its
  `Dark` one, keyed by theme variant. `LunaTheme.Variant` selects; the built-in
  `Theme/Palette.axaml` carries both (`§23`).
- `LunaTheme.ApplyVariant()`, applied by `LunaApp.Configure` before the saved
  theme.

### Fixed

- **Stock controls rendered for the wrong background on a light desktop.**
  `LunaTheme.axaml` includes a bare `<FluentTheme />`, which follows the *system*
  variant, while every Luna key was a fixed dark value. On a light system the
  window stayed `#1E1E1E` and Fluent drew its controls for a light background —
  a stock button's overlay measured `#33000000` instead of `#33FFFFFF`, dark on
  dark. The suite could not see it, because the harness pins Dark (`§23.1`).

### Unchanged on purpose

- **The default is still dark.** `LunaTheme.Variant` defaults to
  `ThemeVariant.Dark`, not to the system, so an existing consumer looks exactly
  as it did. Following the desktop is opt-in and one line:

      LunaTheme.Variant = ThemeVariant.Default;

  Making it the default would be a behaviour change arriving inside a version
  bump for everybody on a light machine, which is the thing `§9.1` refused for
  `ToolWindow` and the same argument applies here (`§23`).

- Every dark literal. The light column is additive; nothing already on a screen
  has moved.

### Known

- `LunaMuted` on the dark surface measures **4.22:1**, below the 4.5:1 WCAG AA
  floor the light column is held to. It predates the toolkit having a name, and
  `§2.1`'s rule is that changing a palette literal is a deliberate decision
  rather than something done in passing. Measured, named, left alone (`§23.2`).

---

## 0.3.0

### Added

- **`EmuSen.LunaP.Threading`** — `UiThread`, `Latest<T>`, `Suppressor`,
  `Debounce`. Each replaces something consumers were writing by hand; `§21.1`
  has the counts and `§22` the build (`§22.1`–`§22.3`).
- **`EmuSen.LunaP.Testing`**, a second package carrying `UiTest`, `VisualQuery`,
  `AssertLaidOut` and `LunaHeadless.BuildApp()`. The toolkit itself still
  references Avalonia and nothing else — that is why the harness is a separate
  package rather than a bent rule (`§22.8`).
- `LunaList<T>` — a list that keeps hold of the type it was given and restores
  the selection across a refresh (`§22.9`).
- `EmptyState` — body-sized, not a `HintText`, because an empty state *is* the
  window's content rather than an aside under something else.
- `LunaError`, `LunaSuccess`, `LunaInfo` palette keys. Deliberately not the load
  ramp: `§2.1` refused to give a binding conflict the same key as a hot
  subsystem, and that argument is why these are separate.
- `Ui.Rows(...)`, and `Ui.Section` taking any number of children.

### Fixed

- **`ConsolePane` appended in O(n²)** — one accumulating string, reallocated per
  line. It is a line list with a `MaxLines` cap (default 5000) now (`§22.6`).
- **`ConsolePane` scrolled to the bottom on every line**, so reading back
  through output was undone by the next line to arrive. It follows the tail only
  when the reader was already at the bottom.

### Changed

- `Dropdown.Fill` uses `Suppressor` instead of a private `bool`. Identical for a
  single call; a nested one no longer re-enables `Chose` halfway through.

### Breaking

- **`ConsolePane.MaxLines` defaults to 5000.** A console that relied on keeping
  unbounded history needs `MaxLines = 0`.

### Known

- `Latest<T>`'s final re-check branch needs real concurrency to reach and has no
  test. `ConsolePane`'s scroll *wiring* cannot be tested headlessly — under
  `Avalonia.Headless` a `ScrollViewer` reports `extent == viewport` however much
  text it holds, so only the extracted rule is pinned (`§22.6`).

---

## 0.2.0

First version published to nuget.org, from a git tag, using NuGet Trusted
Publishing — no stored credential.

### Removed

- `Dashboards/` and `Input/`, which named things of EmuSen's. They moved to the
  projects that own those subjects (`§15`, `§16`).

### Added

- `Settings/ISettingsStore` — the seam that replaced the toolkit's one remaining
  dependency. Set nothing and it writes JSON under your entry assembly's
  application-data directory (`§19.1`).

### Breaking

- A consumer moving from 0.1.0 has work to do: the two folders above are gone
  and `Settings/` is new.

---

## 0.1.0

Chosen to start somewhere. Published to a folder feed, never to nuget.org.
