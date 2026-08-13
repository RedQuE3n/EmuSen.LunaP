# Changelog

What changed between released versions, for somebody deciding whether to take
one. The reasoning lives in `docs/LunaP.md` and is cited by `§`; this file says
what moved and what it costs to follow.

Versions are the git tag. `EmuSen.LunaP` and `EmuSen.LunaP.Testing` ship from
the same tag at the same number, because the harness asserts about the toolkit's
own controls and pairing two versions of them is a question nobody wants to
answer.

---

## 0.7.0

**A shell.** Actions, menus, a toolbar, context menus, keyboard shortcuts, a
draggable splitter, docked side panels, a card surface, and an `AppWindow` that
puts them where they go. Everything is additive: if you upgrade and change
nothing, nothing changes (`§26`).

### Added

- **`LunaAction` — one command object behind a menu item, a toolbar button, a
  context-menu entry and a key binding.** It is an `ICommand`, so it also drops
  into any Avalonia control that takes one. Changing its label or its enabled
  state changes every surface showing it, which is the four-declarations problem
  it exists to remove (`§26.3`).
- **`ActionGroup`**, for mutually exclusive checkable actions — a theme picker,
  a view mode. Qt's `QActionGroup` (`§26.3`).
- **`MenuBar`, `ToolBar`, and `Menus.Context(...)`**, all built from the same
  actions. `ToolBar` is not `ButtonBar`: one is built from actions and follows
  them, the other is a run of buttons you own (`§26.4`).
- **`Menus.BindShortcuts`, which is what actually makes a shortcut work.**
  `MenuItem.InputGesture` draws "Ctrl+S" in the menu and binds nothing, so a
  menu can advertise a key that does nothing at all. `AppWindow` binds every
  action in its menus and toolbar for you (`§26.5`).
- **`SplitPane`** — a draggable divider with one fixed pane and one elastic one,
  remembered in pixels under an opt-in `PaneKey`. The divider is keyboard
  operable and now says what it is (`§26.6`, `§26.11`).
- **`SidePanel`** — a titled, closable panel docked to an edge, with a
  `ToggleAction` for your View menu that is the *same object* as its close
  button. `QDockWidget` without the floating; `§26.7` says what that leaves out.
- **`Card`** — a titled surface on LunaP's own key. If you were painting a
  `Border` with FluentTheme's `SystemChromeLowColor`, this is that, except a
  theme can reach it (`§26.9`).
- **`AppWindow`** — menu bar, toolbar, central content, status line, panels. It
  extends `ToolWindow` and changes nothing it inherited: empty, it lays out
  identically to a plain `ToolWindow` (`§26.8`).
- **`LunaTable<T>`** — a list with columns. Columns are `(header, projection)` pairs, the model
  comes back on selection, and `Refresh` keeps the selection across a rebuild, exactly as
  `LunaList<T>` does. Flat: no tree, no sorting, no cell editing (`§27`).
- **`LunaBorder`**, one new palette token, in both variants and both halves of
  the palette. Chosen against WCAG 1.4.11's 3:1 rather than by eye, because the
  splitter it draws is a control you have to see to use — the subtle value a
  dark theme reaches for measures 1.51:1 (`§26.9`).

### Changed

- **The gallery is an `AppWindow` now.** A menu bar is not something you look at
  next to a meter row, so the gallery *is* a shell with the samples inside it.
- `LunaSettings.Diagnostics` now also carries "two commands claim one shortcut",
  alongside the "this file would not load" it already carried (`§26.5`).

- **The public surface of both packages is pinned by a test.** Sixty-three types and their members
  are written down in `tests/…/ApiSurface/`, and any change to them — a rename, a widened return
  type, a changed base class, a property turned `internal` — fails the build until somebody
  regenerates the file and commits it. It is a promise about future versions rather than a feature:
  an accidental break can no longer reach you in a version bump without having been reviewed
  (`§32`).
- **Both packages now ship symbols and source links.** A `.snupkg` goes to nuget.org's symbol
  server with every release, and the PDBs carry SourceLink pointing at the exact commit the package
  was built from — so stepping into LunaP gives you the real file, with the comments that explain
  why the code is the way it is, instead of decompiled IL. Nothing was missing but four build
  properties, and none of them costs a dependency: SourceLink ships inside the .NET SDK. Symbols
  cannot be added to 0.2.0–0.6.0 retroactively, so this starts here (`§31`).
- **`StyleClass` on the eight controls that pin a style key** — `MenuBar`, `LunaSwitch`, `Dropdown`,
  `Tabs`, `ActionMenuItem`, `ActionButton`, `ActionToggle`, `LunaList<T>`. Each adds its class to
  itself, so `ToggleSwitch.luna-switch` reaches a `LunaSwitch` and not your own `ToggleSwitch`. If
  you have been trying to style one of these from your own `.axaml` and finding that
  `luna|Dropdown` matched nothing, this is the selector you needed and `§30` is why it did not work.

### Fixed

- **Four CSS element names have never worked, and now do: `luna-switch`, `dropdown`, `tabs` and
  `menu-bar`.** If you wrote `dropdown { color: … }` in a `.css` theme, the theme loaded, **no
  warning was raised**, and nothing changed. The first three have been broken since the CSS format
  shipped in 0.2.0; `menu-bar` since 0.7.0 advertised it. The cause is one line of Avalonia
  semantics: a type selector matches a control's **style key**, and these four pin
  `StyleKeyOverride` to a stock control so that they get a template at all — so the selector asked
  for a control that cannot exist. They now select their style-key type narrowed by a class each
  control adds to itself (`§30`).
- **The menu bar's own styling now applies at all.** `Theme/Controls/MenuBar.axaml` used a
  `luna|MenuBar` selector that matched nothing, so its `Padding="2,0"` never arrived — measured at
  priority `Unset`, meaning nothing anywhere was setting it. Its background looked right only
  because Avalonia paints a `Menu` transparent anyway (`§29.3`, `§30`).
- **This is a visible change if you worked around any of the above.** A rule you wrote that quietly
  did nothing will start doing what it says, and the menu bar gains 2px of horizontal padding.

### Known

- **`Avalonia.Controls.TreeDataGrid` was considered for the table and rejected: it requires a paid
  Avalonia Accelerate licence.** No `<license>` in its nuspec, a `AvaloniaUILicenseKeyProduct`
  build property, a dependency on `AvaloniaUI.Licensing`, and its own README saying so since
  11.2.0. Taking it would have meant every LunaP consumer needing a key to ship a LunaP control,
  which is the term `§25` spent a whole section removing. LunaP's dependencies are still all MIT
  (`§27.1`).
- **If you consume LunaP and check its vocabulary against your own docs, this
  release will turn that test red.** One new palette token and five new CSS
  elements (`menu-bar`, `tool-bar`, `card`, `split-pane`, `side-panel`). EmuSen
  has exactly such a test and `§21.5` predicted this invoice.
- **`panes.json` is a new file**, written next to `windows.json` for any pane or
  panel you give a key. Nothing is written without one.
- **No icons.** An action has no icon property, so a toolbar is a row of words.
  This needs an icon system rather than a property, and there isn't one
  (`§26.12`).
- **No floating or re-dockable panels, no MDI, no native macOS menu bar, and no
  hierarchical tree view.** `§26.12` and `§27.5` are the honest lists.

---

## 0.6.0

**Both packages are now MIT.** No code changed — this release exists only to
carry the licence, because a version number is the only way to signal one.

### Changed

- **`EmuSen.LunaP` and `EmuSen.LunaP.Testing` are MIT, where 0.2.0 through
  0.5.0 were GPL-3.0-or-later.** You can link this into a closed application.
  That was always the term the GPL denied you, and it was never a decision
  about LunaP: it was EmuSen's licence, inherited because LunaP was a folder in
  EmuSen, and §19 got the *references* out of the toolkit while leaving the
  term behind (`§25`).
- Nothing else. 0.6.0 is the same toolkit as 0.5.0 — no control changed shape,
  no palette key moved, no template part was removed, no automation peer
  changed what it reports. If you are on 0.5.0 the upgrade is a version number.

### Known

- **0.2.0 through 0.5.0 stay GPL-3.0-or-later, and stay listed.** nuget.org
  cannot edit a published package's metadata, and a grant already made is not
  withdrawn by a later one. If you took one of those versions you are not in
  the wrong and nothing is being recalled; take 0.6.0 if you want the looser
  term (`§25.3`).
- `EmuSen.LunaP.Testing` links `xunit.assert`, which is Apache-2.0 — the one
  non-MIT reference in either package. It is a test-project dependency, so
  nothing your application ships carries it (`§22.8`).
- **The Inter typeface is not covered by this and has not been checked.**
  `Avalonia.Fonts.Inter` is an MIT package, but `LunaApp.Configure` calls
  `WithInterFont`, so your application ships the font, which carries its own
  terms. Recorded as an open question rather than an answer (`§25.4`).

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
