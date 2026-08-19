# Changelog

What changed between released versions, for somebody deciding whether to take
one. The reasoning lives in `docs/LunaP.md` and is cited by `§`; this file says
what moved and what it costs to follow.

Versions are the git tag. `EmuSen.LunaP` and `EmuSen.LunaP.Testing` ship from
the same tag at the same number, because the harness asserts about the toolkit's
own controls and pairing two versions of them is a question nobody wants to
answer.

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
