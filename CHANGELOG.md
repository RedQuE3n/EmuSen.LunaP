# Changelog

What changed between released versions, for somebody deciding whether to take
one. The reasoning lives in `docs/LunaP.md` and is cited by `§`; this file says
what moved and what it costs to follow.

Versions are the git tag. `EmuSen.LunaP` and `EmuSen.LunaP.Testing` ship from
the same tag at the same number, because the harness asserts about the toolkit's
own controls and pairing two versions of them is a question nobody wants to
answer.

---


## Unreleased

- **Drawn controls for a themed surface, new and additive (`§98` to `§101`).** `NormalizedCanvas` places children by
  fractions of itself, with an origin, a rotation and a real-valued depth. `FittedImage` shows a raster or SVG file
  fitted, covered, stretched or tiled, tinted by a colour or a gradient, desaturated and rounded. `FontText` sets text in
  a typeface read from a file by `FontFiles`, which caches one per path and never installs it in the font manager.
  `SvgDocument` and `SvgPicture` draw a subset of SVG (paths, shapes, groups, transforms, fill rules, strokes, linear
  gradients, clip paths, CSS classes) and refuse whole any file needing more. `TextRowList`, `ImageCarousel`,
  `StarRating`, `BadgeStrip`, `HintBar`, `ClockLabel` and `DeviceStatusBar` complete the set. Nothing existing changed,
  and LunaP still references Avalonia and nothing else: the image effects are done on the CPU once per picture, size
  and effect, not through a Skia lease. Two things to know: the processed-image cache is unbounded (`§98.2`), and an
  `ImageBrush` in `TileMode.Tile` with an absolute `DestinationRect` offset draws its tiles at twice the offset in
  Avalonia 12.1.0, which `FittedImage` works round and your own brushes may not (`§98.2`).
  Corrected before release (`§101.7`): a wrapping `ImageCarousel` repeats its items to fill the row; `TextRowList`'s
  selected background reaches past the list by its margins; `NormalizedCanvas`, `FontText` and `TextRowList` keep
  computed pixels to hundredths, so float noise in a fraction no longer becomes a whole extra pixel.
- **`SliderList` and `SliderItem`, new and additive.** A scrolling column of `SliderRow`s under headings that builds
  only the rows in view: give it `SliderItem`s (a row each) and strings (a heading each) as `ItemsSource`, listen to
  `ValueChanged(item)`, and read or set each item's `Value`, which is kept while no row shows it. A thousand numbers
  cost what the dozen in view do. Two things to know: a row is rebuilt whenever it comes back into view, so hold
  values in the items and not in rows; and new items put the list back at the top (`§97`).
- **`GroupedList`'s scroll bar stays usable on a long list.** It no longer auto-hides, and its thumb never goes under
  40 points; before, a list of thousands of rows had a thumb a point or two tall behind a hairline bar (`§96`).

## 0.11.0

**Four new controls for a library window and the game it opens, all additive.** Nothing existing
changed shape; these are new types, one new palette token, and nothing to migrate. `§88`.

- **`TileGrid<T>`** — a virtualised grid of fixed-size tiles (OpenEmu's cover grid), with `TileWidth`,
  `TileHeight`, `Spacing`, `CreateTile`/`BindTile`, `Label`, `Key`, `Refresh`, `Select`, `Selected`,
  `Columns`, and `Chose`/`Activated`. It realises the rows in view plus one either side and reuses its
  containers: 16 of them for 5,000 items in an 800×600 window, where a `ListBox` over a `WrapPanel`
  builds 5,000. **Two things to know before using it**: `BindTile` must set everything a tile shows,
  because a tile control is reused for other items; and the grid needs a bounded height, or it
  realises every tile. It has two companion public types — `TileGrid`, a non-generic base a style
  selector can name, and `TileGridItem`, the container that draws the selection ring and carries the
  `selected` class (`§88.2`).
- **`SourceList`**, with `SourceListGroup` and `SourceListItem` records — OpenEmu's sidebar: grouped
  24 px rows under small capital headings, an optional badge, selection by a string key (`§88.3`).
- **`OverlayBar`** — a bar of controls over another element, revealed by pointer movement over
  `Watch` and concealed `HideAfter` (1.5 s) later, never while the pointer or focus is on it or
  `KeepOpen` is set. `Conceal()` hides it regardless (`§88.4`).
- **`NoticeLayer`** — `Show(text)` fades a notice in and out over `Duration` (1.75 s), and a second
  call replaces the first. `Current` says what is showing (`§88.5`).
- **`LunaHudSurface`**, a new palette token (`LunaPalette.HudSurface`, `#E61C1C1C`), the same in both
  variants. `CssTheme.TokenNames` has 21 entries where it had 20, and `--luna-hud-surface` is valid in
  a `:root` block. The CSS element vocabulary does **not** include the four new controls.
- **`Dialogs.PromptAsync`** asks for one line of text - a name for a new collection, a rename -
  and answers it trimmed and never empty, or null for Cancel, Escape and closing. The accept button
  waits for text, so a caller has no blank answer to handle. The fourth small modal beside Confirm,
  Error and Message (`§89`).

- **`SheetLayer`** shows a window's content on a sheet inside its owner, for a session that shows one
  window at a time (a handheld's game mode, a television). `SheetLayer.Show(window, owner)` presents when
  the owner is presented or hosts a layer with `PresentsWindows` set, and shows an ordinary owned window
  otherwise; `WindowSlot.Show` and the four `Dialogs` modals now go through it, so **nothing changes for a
  consumer that places no layer**. `ToolWindow` gains `DialogResult` and a `Close(object?)` that records
  it, which hides `Window.Close(object?)`: a call through a reference typed as `Window` reaches the base
  and records nothing (`§90`).

- **`OnScreenKeyboard`** and **`KeyboardLayout`** — a keyboard drawn over a window and steered one key at a
  time (`Move`, `Press`, `Type`, `Erase`, `NextLayout`, `Finish`, `Cancel`, or the arrow keys), typing into a
  text box at its caret, with three layouts: `Code` (hexadecimal and separators), `GameGenie` and `Letters`
  (`§91`).
- **`PathPickerRow.IsEditable`**, false by default: when set, a path can be typed into the box as well as
  picked, committed on Enter or on leaving the box, and raises `PathPicked` like a pick (`§91.4`).
- **`LunaApp.EmbedPopups(embed)`**, an `AppBuilder` extension: on Linux it binds `X11PlatformOptions` with
  `OverlayPopups` set, so dropdown lists, menus and tooltips are drawn inside their window rather than as windows
  of their own; `EmbedPopups(false)` leaves the builder as it was. For a compositor that scales every new window
  to fill the screen, such as gamescope (`§92`).
- **An open dropdown's accent follows the focus.** While a `ComboBox` is open, the focused item is painted in the
  accent and the selected one in the input surface, so the arrow keys or a pad show which item Enter will choose.
  Closed, or opened by a pointer, nothing looks different (`§93`).
- **`EmbeddedPopups.IsEnabled`**, an attached property: every popup under an element that sets it is drawn in the
  window's overlay layer (`Popup.ShouldUseOverlayLayer`), on any platform, through the new theme class
  `luna-embedded-popups`. **`SheetLayer` sets it on every sheet**, so a presented window's dropdowns no longer open
  windows of their own (`§92.5`).
- **`GroupedList<T>`** — a virtualised list of models under group headings, each row a wrapped name, an optional
  muted `Detail` line and an optional `Badge` pill, with `Group`, `Label`, `Key`, `ShowGroupCounts`, `Refresh`,
  `Select`, `Selected`, `Models` and `Chose` as `LunaList<T>` has them. A heading is drawn on the first row of its
  group, so the list holds one row per model and the arrow keys never stop on a heading; **pass the models already
  grouped**. A new theme file, `GroupedList.axaml`, paints the row rather than its container (`§94.1`).
- **`SliderRow`** — a labelled slider over `Minimum` to `Maximum` moved by `Step`, showing its value, its
  `DefaultValue` while the two differ, and a Reset button above the slider's right end. `Value` set in code raises
  nothing; `ValueChanged` is a person's move or Reset (`§94.2`).
- **`FilterBar.MatchesWords(search, fields)`** — every word of a search, in any case, in at least one of several
  fields, so "royale kuro" finds `crt-royale-kurozumi` (`§94.3`).
- **`GroupedList.Refresh` keeps the keyboard focus** on the row it keeps selected, or on the first row, unselected,
  when none is kept, if a row had the focus. Before, the focused container went with the old rows and the focus with
  it, so a keyboard or pad user's next arrow press did nothing (`§94.5`).
- **`TileStrip<T>`** — one row of fixed-size tiles scrolling sideways, virtualised, with `TileGrid<T>`'s contract
  (`TileWidth` 160, `TileHeight` 120, `Spacing` 12, `CreateTile`/`BindTile`, `Label`, `Key`, `Refresh`, `Select`,
  `Selected`, `Chose`/`Activated`) plus `SelectedIndex` and `Move(by)`, a user's step for a host mapping its own
  input. It reuses `TileGridItem`, so the grid's ring styles it; `TileStrip` is its non-generic base. The selected tile
  is centred, and Left, Right and `Move` stop at the ends rather than wrapping (`§95`).

As in `LunaList<T>`, `Chose` on both lists is raised only by a person — pointer, keyboard, or a
screen reader's select — never by `Refresh`, `Select` or `Fill`.

**Five findings from one consumer's first week, four of them additive and one that says what the
0.10.0 audit could not see.** BIMA-C built an application shell on 0.10.0 — `AppWindow`, a menu bar,
a settings store behind `ISettingsStore` — and reported these against the published surface. `§84`.

- **`LunaAction.HasHandler` says whether invoking would reach anything.** `Invoked` is an event, and
  a C# event exposes no invocation list outside the type that declares it, so there was no way at
  all to tell a working action from `new LunaAction("Open PDF")`. The guard this enables is one
  sweep over a menu — `Assert.True(action.HasHandler)` — and it is the guard `AppWindow` invites,
  since a shell taken early has its menus in place before the features they will hold. **It is
  about wiring and not state**: a disabled action still answers true, because a disabled placeholder
  is still a placeholder; a separator and a submenu owner answer false, having nothing to run
  (`§84.1`).
- **`Dialogs.MessageAsync` is the informational third.** Confirm asks a question, Error reports a
  fault, and "Open a PDF first" is neither — so callers were reaching for `ErrorAsync` and dressing
  a normal state as a failure, which teaches a user that this application's errors are not worth
  reading. LunaPY has had `message` beside `confirm` and `error` since it was written. No mechanism
  is added: `ErrorAsync` was already this call with an error's title (`§84.2`).
- **`MessageWindow` is now public, and is a different control from the one that had the name.** It
  shows a body of read-only, selectable, monospaced text for output too long to belong in a dialog —
  build output, a validation report, an exception trace, all of which are read *while* looking at
  what produced them. This is LunaPY's control ported. The internal modal behind `ConfirmAsync` and
  `ErrorAsync` was also called `MessageWindow` and is now `DialogWindow`; **that rename is invisible
  to you** — it was internal, and the API baseline shows no change for it. The trap it removes is a
  grep: the file existed, so the capability looked present, and the type was unreachable (`§84.3`).
- **`ToolWindow.ClosesOnEscape` is false by default, and its summary said true.** The code was
  right, its test was right, and only the sentence was wrong — so nothing failed. **If you read that
  summary and wrote code against it, this is the entry to care about**; the behaviour has not
  changed and is not changing (`§84.4`).
- **Both `LunaAction` constructors document a compile error you will otherwise meet.**
  `new LunaAction("Quit", Close)` inside a `Window` is CS0121: a method group matches both `Action`
  and `Action<LunaAction>` whenever the method is overloaded, and `Window` has `Close()` and
  `Close(object?)`. Wrap it in a lambda. Neither constructor can go, so this is a `<remarks>` rather
  than a fix (`§84.5`).

**Two new guards, because a summary naming a default was a class of claim nothing could check.** §80
probed 544 summaries by *calling* things, and a default value is not reached by calling anything —
it is what is already true beforehand. `DocumentedDefaultTests` now holds every summary that names a
default, two-sided: the phrase asserted against the published XML, the value against a live
instance, so editing either without the other fails. A third test sweeps for any new summary
claiming a default and fails until it is checked or excused. **And the API baseline records
`StyledProperty` default values** — thirty-nine of them — because a changed default is a breaking
change nothing else could see: no signature moves, no consumer's build moves, and every control that
had not set the property explicitly behaves differently (`§84.4`).

**Every stock Avalonia control now paints in this palette, not FluentTheme's — 29 of them did not
before.** §48 bridged nine form controls and its guard swept those same nine, so nothing could see
the other ninety-one. A sweep that finds its own subjects — every control Avalonia ships, reflected
rather than listed — found 29 still painting Fluent's colours, and four of this kit's own with them.
All are fixed. `FluentBridge.axaml` went from 51 overrides to 102 (`§85`).

**This is a visual change to your application, and it is the entry to read twice.** If you use any
of these, they will look different after upgrading — the same greys and accent the rest of LunaP
already used:

- **Popups and overlays**: `ContextMenu` (including the one `Menus.Context` builds for you),
  `ToolTip`, flyouts, menu flyouts, `NotificationCard`. These were Fluent's `#2b2b2b`, so an
  application whose windows were LunaP's greys grew a Fluent-grey menu the first time anybody
  right-clicked (`§85.8`).
- **Four controls this kit ships**: `LunaList<T>`, `ActionToggle`, `ActionMenuItem` and `Tabs`. They
  borrow FluentTheme's templates by design, and borrowing a template turned out to mean borrowing
  its colours — nothing said so and nothing checked. `Tabs` was showing Fluent blue on its selected
  tab (`§85.3`).
- **Structure and buttons**: `Expander`, `GroupBox`, `SplitView`, `ListBox`, `TableView`, `TabItem`,
  `Separator`, `GridSplitter`, `HyperlinkButton`, `SplitButton`, `RepeatButton`, `PipsPager`,
  `NavigationPage`, `DrawerPage` (`§85.9`).
- **Date and time**: `Calendar`, `DatePicker`, `TimePicker` and both flyout presenters (`§85.10`).
- **Validation text was yellow.** Fluent draws `DataValidationErrors` in `#fff000`; it is now this
  palette's error colour, so a failed field no longer reports itself in a colour nothing else in
  your application uses (`§85.9`).

**If you had overridden any of these keys yourself, you still win** — this changes resources, and a
resource you set closer to your control is found first. Nothing here touches a template, so keyboard
handling and accessibility behaviour remain Avalonia's.

**What is not covered, said plainly**: controls are swept at rest, in the dark variant, on
background, foreground and border. Hover, pressed, checked and disabled states are keyed separately
by Fluent and are **not** swept — a control correct at rest can still show Fluent's blue under the
pointer. That is a known gap with a section to itself rather than a silence (`§85.11`).

### What upgrading costs

**Nothing in the API, and a repaint in the UI.** No signature moved, nothing was removed or renamed
in the public surface, and no behaviour changed — every API item above is new API or a corrected
sentence.

The colours are the exception, and they are the point of `§85` rather than a side effect: 29 stock
controls and 4 of this kit's change appearance. If your application deliberately wanted FluentTheme's
look for one of them, set that resource key yourself and yours wins.

**A diagnostic with nowhere to go now goes to standard error instead of nowhere.** Until now
`LunaSettings.Diagnostics` started null and `Report` discarded everything until a host installed a
sink, which meant this toolkit defaulted to silence about the one category of thing it should be
loud about — a theme file that would not parse, a settings write that failed, two menu commands
claiming one keyboard shortcut. The measurement that forced it came from a consumer: deleting the
sink installation from BIMA-CSharp's `Program.cs` left all twenty of its tests green, because nothing
in that suite runs `Main`. The whole channel could be disconnected and nothing anywhere noticed
(`§86.4`).

**If you have already installed a sink, nothing changes for you** — it still receives every message,
unprefixed, and standard error still gets nothing. What changes is the default:

- **Nothing installed** → `LunaP: <message>` on standard error. It is prefixed because it arrives in
  a stream this toolkit does not own, and an unattributed line sends somebody looking in their own
  code first.
- **Want silence?** It is still available and it is now a decision: `LunaSettings.Diagnostics = _ => { }`.

**One thing this does not do, and it is worth knowing before you rely on it.** `dotnet test` does
not surface the test host's standard error at default verbosity, so a diagnostic raised during your
suite is still invisible unless you install a sink that writes somewhere you look. The new default
helps an application that is *run*; it does not help a suite. `§86.10`.

### Breaking: `GalleryWindow` is no longer in the package

**If you referenced `EmuSen.LunaP.Gallery.GalleryWindow`, it is gone from the toolkit.** It moved to
`src/EmuSen.LunaP.Gallery`, an application in the repository that is not published. Two entries left
the public API — the class and its constructor — and 11,776 bytes left `EmuSen.LunaP.dll`
(305,152 → 293,376), which every consumer's application was carrying to show a demo window it never
would (`§86.13`).

**It is also runnable for the first time**, which is the part worth knowing if you were ever curious
what this kit looks like:

    cd src/EmuSen.LunaP.Gallery && dotnet run

There was no `OutputType` anywhere in this repository before now, so the gallery could not be opened
by anybody — not a consumer evaluating the toolkit, not a maintainer checking a new control. `§7` has
claimed since it was written that the gallery is how the kit is discovered; it now is.

### Breaking: `ISettingsStore` has two methods, not three

**`string Directory(string? category)` is gone from the interface.** If you implement
`ISettingsStore`, delete your implementation of it — that is the whole migration, and your class
gets shorter.

It was there for one caller: `LunaTheme`, looking for the folder of hand-written theme files. A path
is a filesystem, so its presence meant **every** settings store had to be file-backed or hand back
something it did not mean. A consumer keeping settings in SQLite implemented it anyway and wrote in
its own notes that the seam "leaks a file model on purpose" — which is a consumer documenting a
defect in our interface (`§86.5`, `§86.11`).

**If you keep settings in a file, nothing else changes.** `JsonSettingsStore.Directory` is untouched,
same signature, still public — a file-backed store still has a directory. It is the *seam* that
stopped naming a storage medium.

**Themes now come from `LunaTheme.Source`, and this is the part that can move your users' files.**
`LunaTheme.Directory` used to be `LunaSettings.Store.Directory("themes")`, so the themes folder
followed whatever store you installed:

- **You never assigned `LunaSettings.Store`** → nothing changes. The default source is a
  `FolderThemeSource` over the same folder as before.
- **You assigned a `JsonSettingsStore` with your own root, or your own `ISettingsStore`** → themes no
  longer follow it. Point the source at wherever you were keeping them:

      LunaTheme.Source = new FolderThemeSource(Path.Combine(myRoot, LunaTheme.ThemeCategory));

### A window can own its settings store, and everything inside it follows

**`LunaSettings.StoreProperty` is an inherited attached property**, so a store set on a window
reaches the split panes, the side panels and the tables inside it — including the ones `AppWindow`
builds for you and the ones you nest three levels down in your own layout. `ToolWindow.Settings` is
the convenient spelling:

    var window = new AppWindow { WindowKey = "main" };
    window.Settings = myStore;    // before Show(): placement restores in OnOpened
    window.Show();

**If you set nothing, nothing changes.** A control in a tree nobody set a store on resolves the
process-wide `LunaSettings.Store` exactly as before, so this is additive for every existing
consumer. What it makes possible is two windows keeping two sets of placement, pane and table
layout in one process, which nothing could express before — every control reached for the global,
so "which store" was not a question a caller was allowed to answer (`§86.12`).

`WindowPlacementStore`, `PaneLayoutStore` and `TableLayoutStore` each take an optional
`ISettingsStore` now. Null still means the process-wide one, so existing calls are unchanged.

**Two things deliberately stayed process-global.** The remembered *theme* choice, because the
applied theme is `Application.Current.Resources` — one dictionary for the process — so a per-window
theme choice would be a setting that cannot be honoured. And `LunaSettings.Diagnostics`, for the
same reason. **And this does not let a LunaP test suite run in parallel**: the applied theme's
resource dictionary is the third process-global behind that rule and this pass does not touch it.

`LunaTheme.Directory` still exists and still answers the folder — but only when `Source` is a
`FolderThemeSource`, and empty when it is not, because a source that is not a folder has no path to
give. If you call `Directory.CreateDirectory(LunaTheme.Directory)` to show a user where to drop a
theme file, hold the `FolderThemeSource` and call `EnsureExists()` instead.

**And `IThemeSource` is a seam you can now fill.** Themes can come from resources compiled into your
application, from a database, or from a dictionary — `Names()`, `Open(name)` and an `Origin` that is
a message rather than a path. The whole of `LunaTheme` is reachable without touching a disk, which is
the gate this change was defined by (`§86.11`).

---

## 0.10.0

**Five audit passes over the whole repository, and eighteen findings.** 0.9.0's
two came from a consumer using the package; every one below came from taking the
toolkit's own documentation literally and asserting what it claimed — 542 `///`
summaries, 29 `<exception>` tags, 105 `<returns>` tags, and the guard that is
supposed to make a breaking change visible. `§§79–83`.

**Two things behave differently and one guard sees more**, which is what makes
this a minor bump rather than a patch; the rest is documentation. The full cost of
upgrading is at the end of this entry.

**The first pass took the documentation literally and found six defects, before
publishing rather than after** (`§79`).

- **A saved table layout could be applied to a table that had outgrown it.** If
  you added a column to a table with a `TableKey` between releases, your users'
  remembered widths landed on the first columns of the new table: a five-column
  table declared entirely at `100` came back `[500, 500, 100, 100, 100]` against
  a layout saved when it had two. `Restore` refuses a layout whose column count
  differs — but it runs after every `Column()` call, and a five-column table *is*
  a two-column table for one call while it is being built. **If you ship a table
  with a `TableKey`, this is the entry to care about** (`§79.2`).
- **A settings write that failed said nothing at all.** `JsonSettingsStore.Save`
  caught everything and returned `false`, while its own summary promised the
  failure was reported — and none of the four callers inside this toolkit reads
  that bool. On a read-only configuration directory or a full disk, window
  geometry, table columns, pane sizes and the chosen theme were lost in silence.
  Failures now reach `LunaSettings.Diagnostics`, which is where you were already
  told to look (`§79.3`).
- **A misspelled palette token in a CSS theme is no longer accepted.**
  `--luna-surfce` parsed, invented a resource nothing reads, left the real
  `LunaSurface` at its default and warned about nothing. `:root` was checked for
  the `--luna-` prefix and never against the tokens that exist. New:
  **`CssTheme.TokenNames`** lists all twenty (`§79.4`).
- **`EmuSen.LunaP.Testing` gains `UiTest.Settle`.** For a control that builds its
  children during a layout pass — a table with `VirtualizeColumns` on, or
  anything driven from `LayoutUpdated` — the pass that adds a child is not the
  pass that arranges it, so one `UpdateLayout` leaves the new children with no
  bounds and assertions about their position read **zero**. `Redraw` forces a
  render, not a layout, and does not help. This lived as a private helper in this
  repository's own suite, where no consumer could reach it (`§79.6`).
- **Building a table read `tables.json` once per column.** Thirty columns, thirty
  reads and thirty full JSON parses of the file every table in the application
  shares. Now once per key. Construction only; nothing recurred per refresh
  (`§79.5`).
- **The API baseline could not tell `init` from `set`**, so all nine of
  `LunaColumn<T>`'s configuration properties were published as `{ get; set; }`
  when assigning them after construction is `CS8852`. Nothing about the type
  changed — the file describing it did. It now reads `init`, which also means a
  future `init`↔`set` swap is a visible change rather than an invisible one
  (`§79.1`).

Two corrections to this file and one to a comment: the 0.8.0 entry above named
`Tap` and `WhenSelected` as `EditGestures` members and they have never existed
(`§79.7`); `LunaTheme.axaml` counted sixteen style includes where there are
seventeen.

**A second pass audited what the code *says* rather than what it does** — the 544
published `///` summaries and all 29 `<exception>` tags, taken as literal claims
and probed by calling them (`§80`).

- **`FilterBar.Changed` is no longer raised by setting `SearchText`**, which its
  summary has always promised and which its three sibling controls all deliver.
  `SearchDelay` defaults to zero and the template binding pushed the value into
  the box, so an application restoring a saved filter fired a re-query nobody
  asked for — a ROM library and an on-disk cheat database, for the two consumers
  in `§21.1`. **One of the two behaviour changes in this release**, and half the
  reason it is a minor bump: if you leant on the raise, call your handler yourself
  after setting the value (`§80.1`).
- **`LunaColumn<T>`'s template constructor names the right argument.** A null
  `spoken` threw `ArgumentNullException` naming `"text"` — the private
  constructor's parameter, which that overload has not got — sending a caller to
  look at an argument they never passed. The check-column form had a helper
  guarding against exactly this and the template form did not (`§80.2`).
- **`CssTheme.Parse` documents that a syntax error throws.** It summarised itself
  as collecting rather than throwing, which is true of a declaration it cannot
  *use* and false of a file it cannot *parse*. No application was affected —
  `LunaTheme.Read` catches it — but a consumer calling `Parse` directly had no
  warning. `FormatException` is now a documented tag (`§80.4`).
- **Fifteen public members now document the exceptions they throw**, against 24
  that already did; while both were true of the surface, "no tag" could not be
  read as "does not throw". Two were more than null guards: `ActionGroup.Add`
  refuses an action owned by another group, and `UiSession.TestAssembly` throws
  when no assembly carries `[AvaloniaTestApplication]` or several do (`§80.5`).
- **Three summaries stopped promising a directory that is never created.**
  `ISettingsStore.Directory`, `JsonSettingsStore.Directory` and
  `LunaTheme.Directory` said "created if it does not exist"; they resolve a path.
  Corrected in the sentences rather than the code, because creating a folder as a
  side effect of asking where one would be is worse than not doing it — `Save`
  already creates on demand (`§80.3`).

- **`LunaSelectionMode.None` now refuses a programmatic `Select` too.** Its
  summary reads *"Rows cannot be selected at all"*, and it was implemented as a
  hit-test — which stops the user and not the caller, so a table declared
  unselectable could be given a selection in code and would show one. The cell
  path had always refused it, and setting the mode already cleared any existing
  selection for the stated reason that it must not read as "no *new* selections";
  the row path was the third of three and the only one not told. `Select(null)`
  still clears, since that is how a caller says "no selection" (`§81.1`).

- **The API surface baseline now records generic constraints and extension
  methods.** `where T : class` on four public types, `where T : Control` on
  twenty-nine public methods, and the `this` on every fluent helper were all
  absent from the file this project treats as its review artefact — so tightening
  a constraint, or dropping `this` from a parameter, could have been approved
  without a line moving. Both are source-breaking for consumers. The baseline
  gains 42 constraint clauses and 29 `this` markers; no API changed (`§82.1`).
- **`LunaMenu.Commands()` documents what it actually returns.** Its `<returns>`
  claimed submenu owners were left out; they are returned, before the actions they
  contain, and have been since the walk was written. **The code was right and the
  tag was wrong** — the walk and its test date from 2026-08-12, the tag from a
  bulk "document every member" pass the day after (`§82.2`).

Sixteen new tests pin the audit's negative results as well as its defects: every
`<exception>` tag with the parameter name it promises, twenty-five "does not
raise" claims, and the README's vocabulary counts, which were correct and are now
guarded because a count in prose rots silently (`§80.6`).

### What upgrading costs

**Three things behave differently, and all three were defects rather than
behaviour worth preserving.** They are why this is a minor bump: no signature
moved, but a consumer could have been leaning on any of them.

- `FilterBar.Changed` is not raised by setting `SearchText` (`§80.1`).
- `LunaSelectionMode.None` refuses a programmatic `Select` (`§81.1`).
- A stale saved layout is no longer applied to a table you have since widened
  (`§79.2`).

Everything else is additive or documentation. The `Chose` change is wording only,
`ActionGroup.Checked` gains a setter without altering the getter, and the API
baseline's new constraint and `this` markers describe API that did not change.

**If you are on 0.7.x**, note that 0.7.1 was prepared and never tagged, so its
settings-root fix reaches you here for the first time (`§51.1`).


## 0.9.0

**Published by accident, and it is a real release rather than a mistake to hide.**
The tag was pushed while the work below it was still uncommitted, so 0.9.0 carries
only what was in the repository at that moment: the two README rewrites and §78's
three fixes. Everything the version number was chosen *for* — the audit passes of
`§§79–83` and the two behaviour changes that argued it up from a patch — is in
0.10.0 above and reached no consumer here.

It is deprecated on nuget.org pointing at 0.10.0, and left listed and installable,
because it is tested working code and nothing in it is wrong except its number.

`§51.1` records 0.7.1 as prepared and never released. This is the mirror of that —
released and never prepared — and it is written down for the same reason: the
version history is only worth keeping if it says what actually happened. `§83.3`.


**Two `///` summaries were wrong, and one of them is fixed by making the code
match the sentence rather than the other way round.** Both were found within a
day of a consumer adopting 0.8.0 (`§78`).

- **`ActionGroup.Checked` now has a setter**, which its summary has been
  promising since before the property existed: *"Setting it checks that one and
  unchecks the rest without running any handler."* It was get-only, so a
  consumer writing the obvious `group.Checked = member` met `CS0200` and had to
  discover `member.IsChecked = true` instead. The mechanism was never missing —
  only the spelling. Assigning `null` unchecks everything; assigning an action
  that is not a member throws `ArgumentException`; **no handler runs**, so a
  window showing the current selection cannot apply it by displaying it
  (`§78.1`).
- **`LunaList<T>.Chose` and `LunaTable<T>.Chose` say what they mean now.** Both
  read *"Raised when the user picks a row"*, which is equally good English for
  double-clicking, and a consumer wired a modal dialog's close to it — turning a
  list that wanted a double-click into one that ended the dialog on a single
  click. **No behaviour changed**; the summaries now say *"This is a selection,
  NOT an activation"* and name `DoubleTapped`/`KeyDown` as the activation
  gestures. `LunaList`'s also records that a direct write to `SelectedIndex`
  raises it, which `Refresh` and `Select` do not (`§78.2`).

- **`LunaTable<T>` forwards its automation name to the list inside it.** A caller
  names the table, because the table is the control they declared; the template
  put an unnamed `ListBox` underneath, so a screen reader walking the tree found
  an anonymous list inside a named table — and no consumer could fix it, because
  that list lives in a template they do not own. Forwarded rather than hidden, so
  the rows stay navigable; a name the caller set on the inner list themselves is
  kept; and it is re-applied when the name changes, because a window built in a
  constructor usually gets its name *after* the template (`§78.4`).

## 0.8.0

**Any window can go full screen, and one that was already remembering its place
now remembers the right thing.**

```csharp
window.ToggleFullScreen();               // or: window.IsFullScreen = true
window.FullScreenChanged += on => full.IsChecked = on;
```

- `IsFullScreen` is read from the window rather than stored beside it, so it stays
  right when the platform's own affordance or a window-manager shortcut is what
  moved it. `FullScreenChanged` exists for the same reason: a checkable "Full
  Screen" menu item that kept its own tick would say the opposite of the window
  the first time somebody used one (`§75.2`).
- Leaving full screen returns the window to the state it came from, so a maximized
  window is still maximized afterwards (`§75.3`).
- **F11 is not bound by LunaP.** It is yours, as every other key is.
- Full screen is deliberately **not** remembered by `WindowKey`, while maximized
  still is: a window reopening maximized has its title bar and close button, and
  one reopening full screen has neither (`§75.5`).

**The pointer can get out of the way.** `IdleCursor` hides it once it has been
still for a while and brings it back the moment it moves:

```csharp
_idle = new IdleCursor(this);                            // the whole window, three seconds
_idle = new IdleCursor(screen, TimeSpan.FromSeconds(1)); // or just the framebuffer
```

- It attaches to **any control**, not just a window, because "hidden over the
  video and visible over the toolbar beside it" is the common case and a
  window-level flag cannot express it (`§76.1`).
- **Dispose it** — the cursor is restored on disposal, and one left hidden is an
  application whose pointer never comes back.
- Only pointer *movement* counts as activity. Keystrokes deliberately do not, or
  an application somebody is holding four keys down in would never hide it
  (`§76.5`). `Show()` is the seam for your own idea of activity.
- A child that sets its own cursor keeps it, so the pointer reappears over a
  sortable table heading (`§76.2`).

**Files can be dropped onto any control.** `FileDrop` hands you their local paths:

```csharp
_drop = new FileDrop(this, paths => Load(paths[0]));
_drop.Accept = paths => paths.Count == 1;   // refuses while the drag is still moving
```

Avalonia already extracts the files; what this removes is four lines of wiring
with two silent failures in them — forgetting `AllowDrop`, so no drag event is
raised at all, and forgetting to set an effect in `DragOver`, so the platform
refuses the drop before your handler runs. Neither produces an error or a mark on
screen (`§77.2`). **Dispose it**, and your previous `AllowDrop` is restored.

**Four things LunaP deliberately does not wrap**, now written down rather than
left open: `Window.Topmost`, `Window.Icon`, `TopLevel.Clipboard` and
`ExtendClientAreaToDecorationsHint` all already exist and work, so a LunaP name
for them would only add a thing to keep in step (`§77.1`). Keeping the display
awake, single-instance and a custom title bar are absent with reasons (`§77.3`).

**A defect fixed, and it affects any window with a `WindowKey`.** A window closed
while maximized *or* full screen saved the **screen's** bounds as its own restored
size if it had nothing stored from a previous run — so a window maximized on its
first run and closed reopened as a "normal" window the size of the display, with
its title bar off the top. It now records the flag and no geometry, and reopens at
its own default size (`§75.6`). The maximized half of this has been present since
0.2.0.

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

**A table cell no longer has to be text.**

- A **checkbox column**: `new LunaColumn<T>("req", r => r.Required, (r, on) =>
  r.Required = on)`. Leave the third argument off and the column is read-only —
  which means genuinely read-only, including to a screen reader (`§57.3`).
- A **template column**: `new LunaColumn<T>("kind", r => BuildMyControl(r), r =>
  r.Kind)`. The third argument is **required**, and it is what a screen reader
  hears in place of your control. There is no way to declare a cell nobody can
  read (`§57.2`).
- Both are ordinary constructors, so `Width`, `Sort`, `MinWidth`, `IsVisible` and
  the rest apply exactly as they do to a text column (`§57.1`).
- A `Toggle` that declines to write leaves the tick where it was — the table
  re-reads your model rather than trusting the box. What it cannot do is say
  *why*, which a text column's `Validate` can; that gap is recorded rather than
  approximated (`§57.4`).
- `TryGetCell` now returns `Control?` and finds all three kinds.
- **Nothing changes for a table of text columns**, which is still every column
  you have declared so far.

**Rows can be dragged into a new order.** `CanReorderRows = true`, and off by
default so nothing moves for a table that does not ask.

- **The table reorders nothing itself.** `RowDropped` tells you what landed
  where; you move your own rows and call `Refresh`. It holds a copy of your list,
  so reordering it here would be undone by your next refresh - the same rule a
  checkbox column already follows, where a `Toggle` that declines leaves the tick
  where it was (`§71.1`).
- `LunaRowDrop<T>` is your models, the model it landed on, and a position:
  `Before`, `After`, or `Inside` - which only happens in a tree, where it means
  reparent rather than reorder (`§71.5`).
- `CanDrop` refuses a drop before the indicator promises it will work.
- **Alt+Up/Down moves the selected row**, raising the same event, because a
  reorder only a pointer can do is a feature half your users do not have
  (`§71.4`). A bare arrow still moves the selection.
- Dragging a row that is part of a multi-selection takes the whole selection;
  dragging one outside it takes only that row.

One difference from `TreeDataGrid`, stated rather than left to be found: this is
pointer capture rather than the platform's drag-and-drop, so a row can be
reordered inside its table but **cannot be dragged out of it** into another
control (`§71.2`).

**A table can be a tree.** One projection, and null — the default — is a flat
table, so nothing changes for a table that does not set it:

```csharp
files.Children = node => node.Kids;      // null is a flat table
files.ExpanderColumn = 0;                // which column carries the toggle
```

`Expand`, `Collapse`, `ExpandAll`, `CollapseAll` and `IsExpanded` drive it from
code; `IndentSize` sets the step. A projection rather than an interface, so your
model needs no base class and no knowledge that LunaP exists — a model that keeps
its children elsewhere writes `n => index[n.Id]`, which an interface could not
express (`§55.1`). Sorting applies **at every level**, so a tree stays a tree
(`§55.2`). Expansion is keyed by your model, so it survives a `Refresh` that
rebuilds every object (`§55.4`).

**A table can select more than one row.**

```csharp
fields.SelectionMode = LunaSelectionMode.Multiple;   // None, Single (default), Multiple
```

`SelectedItems` gives them in display order. `None` is a real mode rather than an
omission — a table nobody can select a row in is a reasonable thing to want.

**Columns gained bounds, visibility, and a way to find things.**

- `LunaColumn<T>.MinWidth` and `.MaxWidth` bound a column under a resize drag.
  Both nullable, and null — the default — leaves the Grid's own 0 and infinity,
  so no existing column moves.
- `LunaColumn<T>.IsVisible` hides a column **without moving any index**: a hidden
  column keeps its place, so a remembered layout, a sort and `Edit(item, 2)` all
  still mean what they meant.
- `BringRowIntoView`, `TryGetRow` and `TryGetCell` navigate. The two `TryGet`
  methods answer **false** for a row that is not currently realised, rather than
  forcing one into existence — which is why `BringRowIntoView` exists.

**Grid lines, edit gestures, and two lifecycle events.**

- `GridLines` is `None` (the default), `Horizontal`, `Vertical` or `All`. None is
  what every table drew before, and is the better default for an instrument panel
  where a meter list should read as a block rather than a spreadsheet (`§56.2`).
- `EditGestures` is a `[Flags]` set — `None`, `DoubleTap`, `F2`, and `Default`
  being both — rather than a mode, because they compose: an enum of named
  combinations grows a member per pair (`§56.1`). **This entry named `Tap` and
  `WhenSelected` as members until the release above.** They are not; they are two of the
  three values `§56.1` names as TreeDataGrid's and deliberately absent here, and
  the sentence turned "absent" into "included". Corrected in place rather than
  below, because a released entry that hands a consumer a member their compiler
  will reject is the one kind of error this file must not preserve for the
  record (`§79.7`).
- `RowPrepared` and `RowClearing` fire as rows are realised and recycled, and
  `CellValueChanged` fires when a commit or a toggle writes. **`CellPrepared` and
  `CellClearing` are deliberately absent** — refused with an argument rather than
  missed (`§56.3`).

**A wide table can build only the columns it can show.** `VirtualizeColumns =
true`, off by default, so nothing moves for a table that does not ask.

- Measured on 120 columns of 120 pixels in an 800-wide viewport, where 6.7 of
  them are visible: a refresh went from **42.7ms to 6.0ms**, and one row held
  eight cells instead of 120 (`§72.1`).
- **Only fixed-width columns are ever left out.** An `Auto` or star column takes
  its width from its content, so dropping its cells would change how wide it is —
  a star column measured 175 pixels at rest and **0** while scrolled past, moving
  every column to its right by that much (`§72.3`). Frozen columns are on screen
  at every offset by definition. A table of star columns therefore gains nothing,
  which is the same table that never scrolls sideways anyway.
- **A column that is not built has no cell**, so `TryGetCell` answers false for it
  and a screen reader walking cells does not reach it — the same trade row
  virtualization has always made for a row scrolled away. Editing and the arrow
  keys are unaffected: both bring a column back before going looking for it
  (`§72.4`).
- The sentence a screen reader hears for the **row** is unchanged, because it is
  built from your columns rather than from the cells that happen to exist.

This closes `§54`'s parity arc with `Avalonia.Controls.TreeDataGrid`.

**Columns can be aligned, and sorted without a click.**

- `LunaColumn<T>.Alignment` and `.VerticalAlignment` say where a column's content
  sits. Both are nullable and null - the default - leaves every cell kind exactly
  as it was. A right-aligned column of numbers is the case this exists for:
  left-aligned, a run of 9, 10, 11 puts the units under the tens.
- The heading follows the column, so a right-aligned column of sizes no longer
  sits under a left-aligned word (`§70.2`).
- `SortBy(column, descending)`, `ClearSort()`, and `SortedColumn` /
  `SortedDescending` to read it back. `SortBy` **refuses a column with no `Sort`
  comparison** rather than falling back to sorting the displayed text, which is
  the "10 before 9" bug `Sort` exists to prevent (`§70.3`).
- A remembered layout still wins over a sort you set in code, because what your
  user clicked last time outranks what your application declared this time.

**A sort you left was never written down. It is now.** If you use `TableKey`, this
is the entry to care about: clicking a heading did not schedule a save, and the
table never flushed when its window closed - so a user who sorted and closed lost
the sort, unless you happened to call `SaveNow()` yourself. Column *widths* were
saved, which is why this looked like it worked. Both halves are fixed: every
change schedules the write, and the table flushes on its way out of the visual
tree the way `SplitPane` always has (`§70.4`).

**A table can select cells instead of rows.** `SelectionUnit = Cell` beside the
`SelectionMode` you already have, because *how many* and *what kind* are separate
questions and one enum cannot answer both (`§67.1`). Row is the default, so
nothing moves for a table that does not ask.

- Arrow keys walk the columns, Home and End go to the ends, Shift extends a
  **rectangle** rather than a run, and Ctrl+click adds one cell at a time.
- `SelectedCell` and `SelectedCells` are `LunaCell<T>` — your model and a column
  index, never two positions, so a coordinate survives a `Refresh` that rebuilds
  every object (`§67.2`).
- `SelectedItems` still answers with rows: in a cell unit, a row is selected when
  any of its cells is.
- **F2 opens the cell you are on** rather than the first editable column. In a
  row unit it still opens the first editable column, so no existing table's F2
  changes.
- Changing the unit clears the selection. A row has no column to become, and
  turning a cell into its whole row would select more than was asked for.

**A screen reader can now ask a table what is selected, and move it.** This is
the entry to read if you ship to screen-reader users, and none of it needs a line
from you.

- The table reports itself as a **data grid** rather than a group, with
  `ISelectionProvider` and `IScrollProvider` behind the claim. `§27.3` refused
  that control type and `§68.1` is the correction: the patterns it was refused
  over do not exist in Avalonia at all.
- **What is selected comes back as the cells themselves** — so a reader that
  finds a checkbox cell in the selection can still tick it, and a template cell
  keeps whatever its own control provides.
- **Every cell is named for its column.** A reader landing on one hears
  "armed" and then the state, instead of a bare value with nothing to say which
  column it came from.
- **A template cell finally says what it means.** `§57.2` made the spoken
  sentence mandatory and then only ever used it in the row's name; the cell
  itself was anonymous, so a coloured dot announced as nothing. It now carries
  that sentence as its item status.
- **A cell no longer goes stale when a different cell changes it.** A template
  column reading a field that a checkbox two columns over writes was left
  describing the old value — on all three write paths, including the one a
  screen reader uses (`§68.4`, `§69.1`).

**A template cell you gave a size to is no longer centred in its column.** Every
other kind of cell starts at the column's left edge; a template cell was the
exception, because Avalonia centres an element that has an explicit width and no
alignment of its own. `new Ellipse { Width = 8 }` in a 120-wide column sat 56
pixels in, beside a checkbox that started at zero.

If you had written `HorizontalAlignment` yourself, you keep it — this only fills
in an answer where there was none. **A template cell with no explicit width still
stretches to fill its column**, so a progress bar or a coloured background in a
cell is unchanged (`§69.2`).

Still missing, and stated rather than left to be found: a tree row exposes no
`IExpandCollapseProvider`. The expander is a real focusable button named "Expand
&lt;row&gt;", which is the capability without the pattern (`§68.7`).

**A defect fixed in the same work.** `ExpanderColumn` was wrong for every value
except its default: a tree whose expander was not in the first column drew that
cell on top of column 0's and left its own column empty. If you have a tree with
`ExpanderColumn` set to anything but 0, this is the entry that matters (`§66`).

**A table can have a gutter down the left.** `RowHeader` takes the row and its
*displayed* index, so `(_, i) => (i + 1).ToString()` numbers the rows and
`(row, _) => row.Address.ToString("X4")` labels them from the model. Null - the
default - means no gutter and no change (`§58`). `RowHeaderCaption` puts a
heading over it and `RowHeaderWidth` sizes it.

The gutter **stays put when the table scrolls sideways**, whatever
`FrozenColumns` says, because a row label that scrolls away leaves your user
reading a line of values with nothing to say which row it belongs to (`§63.2`).

**Columns past the right edge of a table are now reachable.** They were not: a
table whose columns did not fit resolved every column to the width it asked for
and then clipped the grid at the viewport, with no scrollbar, no wheel and no
keyboard route to the rest. If you have ever declared absolute column widths that
added up to more than the window, some of your columns were invisible and nothing
said so. The table scrolls sideways now and the header follows it (`§59`).

A table of star-width columns - the default - fits by definition, shows no
scrollbar and is unchanged.

**And the first columns can be frozen.** `fields.FrozenColumns = 1` pins them
while the rest scroll underneath, with a seam drawn where the pinning stops.
Zero — the default — pins nothing, so no existing table moves.

- Counted in **your** columns, in the order you declared them; a gutter is pinned
  on its own account and takes none of the count (`§63.2`). A hidden column takes
  one of the places, like every other index this control uses.
- **A band with no room pins nothing at all.** Freezing is a refinement of
  scrolling and does not get to remove it, so a band as wide as the viewport —
  from freezing too much, or from a window dragged narrow — leaves you an
  ordinary scrolling table rather than one whose far columns cannot be reached.
  It returns by itself when there is room (`§64.1`).
- Not remembered by `TableKey`, on purpose: that file holds what your *user* did,
  and this is what *you* declared (`§65.4`).

`§59.3` said this needed a different control, and it was wrong — the correction
and the walk are in `§60`.

**Two defects fixed in the same work, both of which shipped inside this release's
own development and neither of which any test caught.** If you take 0.8.0 you
have neither, but they are the entries worth reading if you build on this: a
table's header stopped following any scroll it did not cause, including every
scroll caused by opening an editor (`§64.2`), and a cell editor's own inner
`ScrollViewer` permanently hijacked the one the table was watching (`§64.3`).

**Disabled checkboxes change colour, including ones you built yourself.** Fluent's
disabled checkbox is translucent white, which on the light surface put a white
tick on light grey at **1.78:1** — unreadable. `FluentBridge` now overrides five
keys so a disabled box holds 3:1 in both variants, checked and indeterminate. WCAG exempts disabled controls
from any contrast requirement; that exemption assumes you never need to *read*
one, and a read-only cell breaks the assumption (`§57.3`). This affects any
`CheckBox` in your application, not just table cells.

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
