# Changelog

What changed between released versions, for somebody deciding whether to take
one. The reasoning lives in `docs/LunaP.md` and is cited by `§`; this file says
what moved and what it costs to follow.

Versions are the git tag. `EmuSen.LunaP` and `EmuSen.LunaP.Testing` ship from
the same tag at the same number, because the harness asserts about the toolkit's
own controls and pairing two versions of them is a question nobody wants to
answer.

---

## 0.8.0

**Your form controls will look different. That is the release.**

LunaP ships `<FluentTheme />` and always will, so every stock Avalonia control an
application reaches for — a `TextBox`, a `CheckBox`, a `Slider` — worked and
painted in *Fluent's* palette rather than this one, accent `#0078D7` included. An
application built mostly of form controls came out mostly Fluent, and the join
showed in accents, borders and the focus ring. It no longer does: LunaP's colours
are handed to 46 of FluentTheme's own resource keys, so the templates are
untouched and the values they look up are ours (`§48`).

**This is not additive, and it is the first release since 0.7.0 where that is
true.** If you use stock Avalonia controls anywhere, they change colour when you
upgrade. Nothing about their behaviour, layout or API moves — only what they are
painted in. There is no switch to turn it off; if you had restyled these controls
yourself, your own styles still win, because this changes resources and not
templates.

**A minor bump rather than a patch for exactly that reason.** A consumer reading
`0.7.2` would not expect their text boxes to be repainted.

Two new palette tokens come with it, `LunaAccent` and `LunaOnAccent` — the first
in this palette for something the toolkit does not draw itself.

**Fields and cells can now be wrong, and say so.**

- `FieldRow.Error` shows what is wrong with a field. Empty means valid; there is
  no `IsValid` beside it, because the message *is* the state (`§49`).
- `LunaColumn<T>.Commit` and `.Validate` make a table column editable. Null
  `Commit` means read-only and is the default, **so no existing table changes
  behaviour** (`§50`). Double-click or F2 to open, Enter to commit, Escape to
  cancel.
- `ErrorText` is a new text idiom, themeable through CSS like the other three.

**One accessibility defect fixed, and it had been there since 0.7.0.** Every
`LunaTable` row builds a spoken name — "name: Site, type: text, pg: 1" — and it
was being set on a node screen readers do not visit. What a reader actually heard
was your model's `ToString()`: for most callers, a .NET type name, once per row.
The name now goes where the control view can reach it (`§50.5`). If you shipped a
table, this is the entry to care about.

Also: `LunaTable` rows expose `ISelectionItemProvider` and editable cells expose
`IValueProvider`, so a screen reader can select a row and set a cell — going
through your `Validate` first, exactly as typing does (`§50.6`).

**`RgbaImageView` stops copying every frame twice, and can scale by whole
pixels.**

- `SetFrame` now takes a `ReadOnlySpan<byte>` or an `nint` as well as a `byte[]`.
  If your pixels were already in native memory you were marshalling them into an
  array so this control could copy them straight back out — 8.29 MB per frame at
  1080p, about 498 MB/s at 60fps, for nothing. The `byte[]` overload is unchanged
  and now delegates to the same path (`§53.1`).
- `IntegerScale` scales by a whole number of pixels and centres the result, which
  is what stops nearest-neighbour shimmering at a fractional factor — a 160×144
  frame at 4.17× has most rows 4 device pixels tall and every sixth one 5
  (`§53.3`). **Off by default**, so nothing moves unless you ask.
- **A latent stride bug is fixed.** The copy assumed the framebuffer's rows were
  exactly `width × 4` bytes; it now reads `RowBytes` and copies row by row when
  they are not. No backend measured here pads, so this was not visible — it was
  an assumption about one platform (`§53.2`).
- One correction: `Stretch`'s documentation said it defaulted to preserving the
  aspect ratio. It defaults to `Stretch.None`, which does not scale at all. The
  value never changed, only the sentence describing it (`§52`).

---

## 0.7.1

**Your test suite was writing into a directory shared with every other project
on the machine. It no longer is.**

`JsonSettingsStore.ForApplication()` names itself after the entry assembly. Under
`dotnet test` that is `testhost` — the same name for every project anybody has
ever built — so window placement, pane layout and the saved theme name all went
to one `testhost` folder in your real per-user configuration directory, shared
with every other repository's test suite on that machine (`§43`).

**Who this affected.** Any suite that showed a `ToolWindow` with a `WindowKey`
set, or saved a theme, without assigning `LunaSettings.Store` first. It needed no
mistake on your part; nothing said you had to. It was found by looking at a
machine where one project's `windows.json` held another project's window keys.

**The read is the part that bites.** `ToolWindow` restores from that same file by
key, so two projects whose windows are both called `"main"` restored each other's
geometry — a test that passes or fails according to what else has been built on
the machine, with no local cause and no reproduction on a fresh checkout.

**What you get instead.** When the entry assembly is a test runner *and* you
passed no name, the store roots itself at `<your test project's bin>/lunap-settings`
and says so through `LunaSettings.Diagnostics`. A name you pass is honoured
whatever it says, including `"testhost"`.

**Nothing moves for an application.** A real entry assembly is never named
`testhost`, so no user's settings change location. If you already assign
`LunaSettings.Store`, nothing changes for you either.

**If you want the old files back**, they are in `~/.config/testhost` (or the
platform equivalent) and can be deleted — but check what is in there first, as
more than one project may have written it.

### Fixed

- The default settings root under a test runner (`§43`).

### Internal

- `CitationTests` fails the build on a `§` citation that does not resolve to a
  section of `docs/LunaP.md`. 116 citations, all resolving (`§44`). No effect on
  the packages.

---

## 0.7.0

**A shell, and a class of theme rule that never worked.**

The shell is the headline: actions, menus, a toolbar, context menus, keyboard
shortcuts, a draggable splitter, docked side panels, a card surface, and an
`AppWindow` that puts them where they go (`§26`). A table (`§27`). Symbols and
source links in the package (`§31`), a guarded public API surface (`§32`), and
IntelliSense for all 379 members of it rather than none (`§33`, `§41`).

**But this release is not purely additive, and the two places it is not are
worth reading before you take it.**

1. **Seventeen CSS theme rules did nothing and now work** — four element names
   since 0.2.0, and thirteen template parts (`§30`, `§39`). If you wrote one and
   worked around its not applying, the workaround is now doubled. If you wrote
   none, nothing moves: every default was measured before and after.
2. **One rule is now refused rather than silently ignored**: `meter-row .bar
   { color: … }` could never win against the state styles, so it warns and tells
   you what to write instead (`§40`). The theme still loads.

An earlier draft of this entry said *"everything is additive: if you upgrade and
change nothing, nothing changes."* That was true when the shell was the whole
release and is not true now — the sentence is corrected here rather than
deleted, because it is the one a consumer would have relied on.

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
- **IntelliSense for the whole surface, not just the type names.** The shipped
  `EmuSen.LunaP.xml` went from 63 entries to 379: every member now says what it
  does, with a sentence for every parameter (212 of them), what it returns (85),
  and what it throws (14). The delegate seams say when they are called and how
  often — `LunaList<T>.Key` and `LunaTable<T>.Key` in particular, whose
  reference-identity default loses the selection on every refresh when rows are
  rebuilt rather than reused (`§41`).
- **`UiTest.Redraw(window)` and `UiTest.AssertMatchesBaseline(name, window)`**, in
  `EmuSen.LunaP.Testing`. On macOS a window's **first** draw is not its steady
  state, so a render baseline written from one and compared against any later
  frame mismatches with nothing wrong. `Redraw` forces a genuine second pass and
  captures that; the new `AssertMatchesBaseline` overload does it for you. Note
  that capturing twice does **not** work — a capture of an unchanged window
  copies the frame already drawn (`§38`).

### Changed

- **The gallery is an `AppWindow` now.** A menu bar is not something you look at
  next to a meter row, so the gallery *is* a shell with the samples inside it.
- `LunaSettings.Diagnostics` now also carries "two commands claim one shortcut",
  alongside the "this file would not load" it already carried (`§26.5`).

- **IntelliSense now says something.** Both packages ship an XML documentation file, so every one
  of the sixty-three public types describes itself in your editor instead of appearing as a bare
  name. Members are documented where the name does not already say it, and deliberately not
  otherwise — 99 of the 460 are Avalonia property fields and framework overrides where the only
  available sentence restates the name (`§33`).
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
- **`UiTest.AssertLaidOut` no longer compares a first draw against your baseline**, which on macOS
  could fail on ~0.4% of the buffer with nothing actually wrong. If you keep `.frame` baselines,
  regenerate them once: frames written by the old code came from a different render pass than the
  ones the new code compares (`§37`, `§38`).
- **`UiTest.AssertStable` failures now report the differing pixel count, the bounding box and the
  peak channel delta**, and name the `EMUSEN_UI_DUMP` variable that gets the frames out. A byte
  count alone cannot tell antialiasing from content that moved (`§38.5`).
- **Thirteen CSS template-part rules did nothing and now work.** If you wrote
  `card .header { color: … }`, `console-pane .output { color: … }`, `side-panel .title { color: … }`,
  `split-pane .rule { background: … }`, `filter-bar .facet { … }` or any of the others, the theme
  loaded, no warning was raised, and the part kept its default. Two causes: the default was written
  as an attribute inside the `ControlTemplate`, which binds at a **higher priority than any style**,
  and `filter-bar .facet` named the wrong type outright — the same style-key defect as above, one
  layer down (`§39.2`, `§39.3`).
- **Nothing changes if you wrote no such rule.** Every default was checked before and after: same
  values, only the priority moved, so the rendered result is identical (`§39.2`).
- **`meter-row .bar { color: … }` is now refused with a warning instead of silently doing nothing.**
  The bar's colour comes from its `:nominal`/`:busy`/`:hot` state styles, which outrank a stateless
  rule, so it never could work. The warning names both spellings that do: `meter-row.busy .bar
  { color: … }`, or the `--luna-nominal`/`--luna-busy`/`--luna-hot` tokens to restyle all three at
  once. **The theme still loads** — warnings are not fatal — but a rule that is accepted and does
  nothing is worse than one that is refused (`§40`).

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
