# Working plan — `LunaTable<T>` grows up, in house

**This is a working document, not part of the record.** `docs/LunaP.md` is the design record and
stays the only one. This elaborates §48 of `PLAN-general-purpose.md` into a design; the intent was
that §52 below becomes a section of the man page as it is built, and this file is deleted.

**It was not deleted, and is committed as of 0.9.0 — see `docs/LunaP.md` §83.2.** Four comments in
shipped source cite §6's trap analysis and §2.1, and `TableEditingTests` is built around the four
traps §6 named before any of the editing existed. Those citations were pointing at a file no clone
had. A working document that shipped code depends on is not disposable, whatever its header said;
the choice was to commit it or to move §6 into the man page and repoint the comments, and committing
it keeps the pre-build reasoning where the tests already reference it. It is still not the record:
where this and `docs/LunaP.md` disagree, the man page wins, and where this and the code disagree, the
code does.

Written 2026-08-13, on branch `settings-root`, against LunaP 0.7.1 and `LunaTable.cs` at 293 lines.

---

## 1. What this decides

Match the parts of `Avalonia.Controls.TreeDataGrid` that LunaP's consumers can show evidence for,
in house, with no dependency, and with an API that a consumer can still read in a year.

The dependency is refused on **API shape first and licence second** — see §3. That ordering matters:
a licence-only refusal invites the next author to take the MIT community fork the moment they find
one, and the shape argument does not care whether the fork is MIT.

---

## 2. What was verified (measured, not recalled)

| Finding | How |
|---|---|
| **Avalonia 12.1.0 exposes exactly seven UIA providers**: `IExpandCollapse`, `IInvoke`, `IRangeValue`, `IScroll`, `ISelection`, `IToggle`, `IValue` | symbol scan of `Avalonia.Controls.dll` 12.1.0 |
| **There is no `IGridProvider` and no `ITableProvider` in Avalonia** | same scan |
| **TreeDataGrid implements neither either** — its peers use `IExpandCollapse`, `ISelection`, `ISelectionItem`, `IScroll`, `IToggle`, `IValue` | symbol scan of `Avalonia.Controls.TreeDataGrid.dll` 12.2.1 |
| `Avalonia.Controls.DataGrid` **12.1.2 is still MIT** — `<license type="expression">MIT</license>`, public repo, commit resolves, zero licence strings in the assembly, depends on Avalonia 12.1.0 | nuspec + assembly scan |
| TreeDataGrid public surface: **71 documented types** | count of `<member name="T:` in the shipped XML docs |
| The MIT line of TreeDataGrid ends at **11.1.1** (2025-01-30, commit `0cb3b3a5`), not 11.1.0 | nuspec `<repository>` commits resolve for 11.1.0/11.1.1 and **do not exist** for 11.2.0+ |
| The **AGPL-3 relicence announced in issue #307 never happened** — `licence.md` has one commit in its history, 2022-03-01, and GitHub reports the archived repo as MIT | GitHub API |

### 2.1 A correction to §27.3's framing, in LunaP's favour

§27.3 records that `LunaTable` reports as `Group` rather than `DataGrid`, because it implements
neither `IGridProvider` nor `ITableProvider` and *"claiming the type would advertise navigation that
is not there."* That reasoning is exactly right and the comment at `LunaTable.cs:39-45` should stay.

What was **not** known when it was written is that **no Avalonia control can implement those
interfaces, because Avalonia does not define them.** `Group` is not a lesser answer to a better one
that exists elsewhere — it is the platform ceiling, and TreeDataGrid sits on the same ceiling with
more peers on top of it.

So the accessibility gap between `LunaTable<T>` and TreeDataGrid is **not** grid semantics. It is
two providers that Avalonia *does* define and LunaP does not yet use:

- `ISelectionItemProvider` on a row, so a reader can say "selected" and "select this"
- `IValueProvider` on an editable cell, once §5.3 exists

Both are reachable. Phase 4 below closes it. **This deletes one of the three reasons to have wanted
the dependency** and should be recorded as a correction rather than folded in silently.

### 2.2 A correction to `PLAN-general-purpose.md` §48

That section says *"Both dependency routes are closed."* The second one is not. `Avalonia.Controls.DataGrid`
is MIT, maintained, and targets LunaP's exact Avalonia version. It is closed on §1 and on Avalonia's
own performance advice — **not** on licence. Worth fixing, because "both routes are closed" makes the
home-grown table look forced when it was chosen.

---

## 3. Why in house, in one paragraph, so it is not re-litigated

**The dependency could not be private.** TreeDataGrid's entire API *is* its source object —
a consumer writes `new FlatTreeDataGridSource<T>(items) { Columns = { new TextColumn<T, string>(…) } }`.
So LunaP either re-exports those types from its own public API, which puts a third-party name in
LunaP's signature and hands the licence obligation straight to the consumer, or it wraps the whole
thing and re-exposes perhaps seven of seventy-one types — having paid for all of them, and still
shipping the gate. Neither is a toolkit that says *"Avalonia and nothing else"* on the tin.

And the thing that makes the in-house version cheap is that **.NET already has the model layer.**
`IEnumerable<T>` is the relation, LINQ is the query, `IComparer<T>` is `ORDER BY`. TreeDataGrid's
71 types are largely a model layer that C++ needed and C# does not. `Refresh(IEnumerable<T>)` at
`LunaTable.cs:149` already *is* the `SELECT … WHERE` seam: the caller runs the query and hands over
the result set.

Which draws the boundary precisely, and it is the design principle for everything below:

> **The caller owns `SELECT` and `WHERE`, in LINQ, before `Refresh`. The control owns `ORDER BY`,
> because the gesture that triggers it — a click on a column header — lives on the control.**

Sorting belongs here for one reason only: the header is inside the control. Everything that does not
have that property stays with the caller.

---

## 4. The API, and the decisions behind it

### 4.1 One new overload, and it is the last one — **settled, Pass D, 2026-08-13**

**Decision: keep both forms.** The terse overload stays for a plain column; the descriptor carries
anything with behaviour. Taken deliberately rather than inherited, because the argument that
originally produced it — binary compatibility — no longer applies (§10), and the shape had to stand
on its own or be replaced.

It stands on its own for three reasons. The terse form is what a reader meets first and what every
existing call site and the whole gallery already use, so keeping it costs nothing and removing it
costs a rewrite that buys uniformity and no capability. Most columns genuinely have no behaviour —
the measured shape (§27.2) is a three-column list of strings — so the common case should not pay for
the uncommon one. And "one way to declare a column" was never quite true anyway: the descriptor's
optional properties mean there are already many shapes a column declaration can take.

**One implementation rule, so two forms do not become two behaviours:** the terse overload
constructs a `LunaColumn<T>` and delegates. There is exactly one code path from a declaration to a
`ColumnSpec`, and a guard asserts the two forms produce identical columns.

```csharp
// shipped in 0.7.x. Unchanged, forever.
public LunaTable<T> Column(string header, Func<T, string> text, string width = "*")

// new. The last Column overload that will ever be added.
public LunaTable<T> Column(LunaColumn<T> column)
```

```csharp
/// <summary>One column of a LunaTable: a heading, a projection, and whatever else that column does.</summary>
public sealed class LunaColumn<T>(string header, Func<T, string> text) where T : class
{
    public string Header { get; } = header;
    public Func<T, string> Text { get; } = text;

    public string Width { get; init; } = "*";
    public Comparison<T>? Sort { get; init; }
    public Action<T, string>? Commit { get; init; }
    public Func<T, string, string?>? Validate { get; init; }
    public TextAlignment Align { get; init; } = TextAlignment.Left;
}
```

Reads as:

```csharp
table.Column("name", f => f.Name)                                   // the terse form still works
     .Column(new LunaColumn<Field>("size", f => f.Size.ToString("N0"))
     {
         Width = "Auto",
         Align = TextAlignment.Right,
         Sort  = (a, b) => a.Size.CompareTo(b.Size),
     });
```

**Why a descriptor and not more parameters.** Adding an optional parameter to `Column` is
source-compatible but **binary-breaking**: a consumer who upgrades the package without recompiling
gets a `MissingMethodException`. §26.13's standard is that a consumer who upgrades and changes
nothing has the same application, and a consumer who does not recompile is that consumer. Adding an
`init` property to a sealed class breaks nothing, needs no new overload, and is the only growth path
that stays additive indefinitely.

Positional constructor for the two things a column cannot exist without, `init` properties for
everything else — so the compiler enforces the required half without the `required` keyword, which
appears nowhere else in this codebase.

**Alternatives rejected** (record in the §22.4 style so they are not retried):

- **Optional parameters on the existing `Column`.** Binary-breaking on every addition, and it
  telescopes — five features means `Column("x", f => f.X, null, null, null, "Auto")` at the call site.
- **An overload per combination.** Two features is four overloads; four features is sixteen.
- **A fluent column handle** — `.Column(…).Sortable(…).Editable(…)`. Chaining across two object
  types: the reader cannot tell whether `.Column` returned the table or the column, and the
  terminator that gets back to the table is noise.
- **A builder class.** `init` properties with more code and an extra type in the public surface.

### 4.2 A sort key is not display text

`Comparison<T>` over the **model**, never over the projected string.

Sorting the display text is a bug that looks like it works: `"10"` sorts before `"9"`, a formatted
number sorts by its thousands separator, and `"2/1/2026"` sorts by the day. The type that knows how
to compare two `Field`s is `Field`, and the caller has one. This is the same reason `Text` is a
projection rather than an interface — the caller's model needs no attribute, no base class and no
knowledge that LunaP exists (`LunaTable.cs:78-80`).

*Considered and rejected:* a `LunaColumn.By(f => f.Size)` helper returning a `Comparison<T>`.
Inference across two type parameters forces the caller to type the lambda —
`LunaColumn.By((Field f) => f.Size)` — which is longer than the thing it replaces.

### 4.3 Sorting is stable, and the unsorted order survives

Use LINQ's `OrderBy`, **not** `List<T>.Sort`. `List<T>.Sort` is an unstable introsort: equal rows
shuffle on every re-sort, and a secondary ordering is impossible. Sorted rows are a *further*
projection over `_items` rather than a reordering of it, so the order the caller passed to `Refresh`
is still there for the third click.

### 4.4 Three-state sort — **settled, Pass D, 2026-08-13**

Ascending → descending → **unsorted**.

Two-state is the more common convention and this departs from it knowingly. The order a caller
passes to `Refresh` is usually meaningful in this toolkit — log order, file order, discovery order —
and once a header has been clicked there is otherwise no way back to it short of the consumer
wiring their own reset. A toolkit for instrument panels should not make arrival order unreachable.

**The cost is real and is accepted:** a third click surprises somebody used to other applications.
Two things reduce it and both are Pass E's job — the sort glyph must be *absent* in the unsorted
state rather than showing a neutral mark, so the cycle reads as "two sorted states and off"; and the
header's automation name must say which state it is in, since a screen-reader user cannot see the
glyph at all.

**And the guard has to be chosen the way §46.3 says.** The discriminating assertion is that after
three clicks the rows are in **the order `Refresh` was given** — and it only discriminates if the
fixture arrives in an order that is neither ascending nor descending. Given a fixture that happens
to arrive sorted, a two-state implementation passes the same assertion, because its third click
lands on ascending and ascending is the arrival order. The fixture is part of the guard here, not
scenery.

### 4.5 Sort state lives on the control, and survives a refresh

Sort state is a field, not a property of the header UI. `Refresh` re-applies it, so new data arriving
under an active sort stays sorted. The selection-restore path at `LunaTable.cs:153-167` already
matches by `Key` and needs no change — a sort is a rebuild, and rebuilds already keep the selection.

### 4.6 Header cells become focusable and invokable

They are `TextBlock`s today (`LunaTable.cs:233-239`) — not focusable, not keyboard-reachable. A
click-only sort is an inaccessible sort, and §24 is the section about exactly this class of miss.

Header cells become a focusable, invokable control carrying `IInvokeProvider`, styled through the
`StyleKeyOverride` precedent §29.1 already set for `ActionControls` — borrowing Fluent's keyboard
behaviour without inheriting Fluent's paint. Automation name carries the state:
`"size, sorted ascending"`, the same technique the row naming already uses at `LunaTable.cs:276`.

### 4.7 Every new public method must work before the template

This is a hard constraint, not a preference. `TemplateOrderTests` (§28.2) **fails the build** for a
new public method unless it answers identically before and after the template is applied, or earns
an entry in that file's `Exempt` table with a reason.

So `SortBy`, `Persist` and `BeginEdit` all store intent on the control and replay it in
`OnPartsAttached`, following the `_pending`/`_hasPending` pattern already at `LunaTable.cs:88-91`.
This is not overhead — it is the reason `Select` works when a window fills its table in the
constructor, which is how every window in this toolkit is built.

### 4.8 Editing writes through a delegate; validation returns a message

```csharp
Commit   = (field, text) => field.Name = text,
Validate = (field, text) => string.IsNullOrWhiteSpace(text) ? "A name is required." : null,
```

String in, string out. Parsing, conversion and domain rules stay with the caller, which is the only
place that knows them — and `null` for valid keeps the common case quiet. Presentation of the error
reuses whatever §47 settles on for `FieldRow`, so an invalid cell and an invalid field look the same.

Begin editing on **double-click or F2**; commit on **Enter or focus loss**; cancel on **Escape**,
restoring the prior text. Type-to-edit is deliberately not in scope.

### 4.9 Persistence goes through the seam that already exists

```csharp
table.Persist("fields");   // column widths and sort state, via ISettingsStore
```

Column widths and sort order land next to `windows.json` and `panes.json`, through
`Settings/ISettingsStore` — no new dependency, no new seam, and opt-in like everything else the kit
persists (§19.1, §26.11). TreeDataGrid has no equivalent; a consumer wires it themselves.

---

## 5. Phases

### Phase 1 — sorting

`Comparison<T>` on the descriptor, header click and keyboard activation, three-state cycle, glyph,
stable ordering, survives `Refresh`, keeps the selection. Roughly 80 lines on top of 293, plus the
header control from §4.6.

This is the whole of what §48 called *"the most-missed feature"* and it needs nothing else built
first.

### Phase 2 — column widths, resize, and persistence

Drag grips between header cells; widths become mutable. The shared-size-group machinery at
`LunaTable.cs:280-289` should propagate a width change to every row for free — **verify that before
relying on it** (§7).

Then `Persist` on top, since by then there are two things worth saving.

### Phase 3 — editing and validation

The fiddly one, and the cost is in focus and commit semantics rather than in line count. Roughly 150
lines. Three traps are known in advance and are written as §6 below.

### Phase 4 — the automation depth §2.1 identified

`ISelectionItemProvider` on rows, `IValueProvider` on editable cells. Small, bounded, and it closes
the real gap rather than the imaginary one.

---

## 6. Traps, each of which becomes a guard

Per §22.5 and §22.6, **every one of these gets a test that is made to fail on purpose before it is
trusted.**

1. **A recycled row must not carry an editor.** `supportsRecycling: true` at `LunaTable.cs:245` means
   the row that scrolls back into view is the row that was being edited. Sabotage: scroll an editing
   row out and back, assert no `TextBox`.
2. **An edit that changes the sort key must not re-sort until the next `Refresh` or header click.**
   A row that leaps away mid-edit is hostile. Sabotage: edit the sorted column, assert the row index
   is unchanged.
3. **A row's automation name is built once** (`LunaTable.cs:276`) and must be rebuilt after a commit,
   or a reader announces the old value forever.
4. **Sorting must not mutate the caller's collection**, and must not lose the unsorted order.
   Sabotage: sort, sort again, cycle to unsorted, assert the original order came back.
5. **Sort state must survive `Refresh`.** Sabotage: sort by size, refresh with new rows, assert still
   sorted by size.
6. **Two tables on one page must not sort each other**, the same way they already must not size each
   other's columns — the per-table scope name at `LunaTable.cs:226` is the existing precedent.
7. **A header must be reachable by keyboard alone.** Sabotage: Tab to it, Space, assert the sort
   changed.

---

## 7. The two verifications — both run, 2026-08-13

Measured through `tests/EmuSen.LunaP.Tests/ProbeTests.cs` (temporary; two of its probes become
permanent guards, the rest is deleted).

### 7.1 Row virtualization: confirmed, and it settles the cell question too

10,000 models in a 500×300 window:

| | |
|---|---|
| models | 10,000 |
| realized `ListBoxItem`s | **10** |
| realized `TextBlock`s | **30** |
| items panel | `VirtualizingStackPanel` |
| build + show + refresh | 369 ms |

So the `ListBox` at `LunaTable.axaml:44` virtualizes rows by default, and **cell virtualization
follows for free**: cells are built by the row's `FuncDataTemplate`, so only realized rows have any.
Thirty `TextBlock`s exist, not thirty thousand.

This closes the argument in §8 rather than merely supporting it. TreeDataGrid's cell virtualization
buys *horizontal* virtualization — many columns, only visible ones realized. At three columns it
buys nothing measurable.

### 7.2 Shared size groups: **they do not work, and `LunaTable` is shipping the bug**

The question was whether a runtime width change propagates. The answer is that **nothing propagates,
because the columns were never sharing a size in the first place.**

Visual x of each header label against its first-row cell, in the table's own coordinates:

| col | width | header x | cell x | delta |
|---|---|---|---|---|
| 0 `name` | `2*` | 12.0 | 12.0 | 0.0 |
| 1 `type` | **`Auto`** | 416.0 | 422.0 | **6.0** |
| 2 `pg` | `40` | 448.0 | 448.0 | 0.0 |

The `Auto` column is misaligned by six pixels on screen, today, in shipped code. The header sized to
its bold *"type"* (32 px), the row sized to *"text"* (26 px), and the star column absorbed the
difference.

**Root cause, isolated and matched to an upstream fix.** Two plain sibling `Grid`s in one scope,
same shape both ways:

| | wide | narrow | equalized |
|---|---|---|---|
| `ColumnDefinitions` **assigned** to the Grid | 147.0 | 47.0 | **no** |
| `ColumnDefinitions` **populated** via `.Add()` | 148.0 | 148.0 | **yes** |

Assigning a whole `ColumnDefinitions` collection never registers those definitions with the shared
size scope. Avalonia PR **#21848**, *"fix(grid): register assigned definition collections with their
shared size group"*, merged to `main` **2026-07-26** — after **12.1.0** shipped on **2026-07-09**.
(Companion: **#21837**, *"allow shared size groups to shrink"*, merged 2026-07-24, closing #21562.)
**12.1.1** shipped 2026-07-29 and is the latest stable; whether the fix was backported into it is
**not verified** and does not need to be, because the workaround is free.

`LunaTable` assigns in both places — `LunaTable.cs:228` and `LunaTable.cs:250` — so it lands squarely
in the bug.

**The fix is to populate rather than assign**, in both sites. It works on 12.1.0 as it stands, needs
no Avalonia upgrade, and is invisible to callers.

### 7.3 Two comments and one test that are now known to be wrong

Under *"a comment that has drifted from the code beside it is worse than no comment, because it is
believed"*, these are not optional cleanups:

- **`LunaTable.cs:120-126`** — *"AUTO IS ACCEPTED AND MADE TO WORK, which takes a little machinery …
  Every column is therefore put in a shared size group and the root is a shared size scope, which is
  Avalonia's own mechanism for exactly this."* The machinery is wired and has no effect.
- **`LunaTable.axaml`** — *"Grid.IsSharedSizeScope is what makes an Auto column line up"* — same
  claim, same problem.
- **§27 of the man page** carries the argument and needs a correction subsection, plus a §12.3-style
  dependency-defect entry: version, reproduction, PR number.

- **`TableTests.Columns_share_a_size_group_with_their_header` is a test that cannot fail.** It
  asserts that the `SharedSizeGroup` *names* match between header and row, and never that the widths
  do. It has passed for the whole life of the control while the control was misaligned. This is
  exactly the §22.5/§22.6 category, found the way those were found — by measuring an effect instead
  of a wiring.

  **The replacement asserts position**: for every column, header x equals cell x. It fails today,
  which is the proof §22.5 asks for, and it passes once the assign→populate fix lands.

### 7.4 Done, 2026-08-13

- `Definitions(scope)` became `Define(grid, scope)`, populating rather than assigning, at both
  sites. The `//` block above it carries the measurement, the symptom, and AvaloniaUI/Avalonia#21848.
- `LunaTable.cs:119-127` and `LunaTable.axaml` corrected: the shared-size claim now says which half
  of the mechanism is not obvious.
- `Columns_share_a_size_group_with_their_header` → `An_auto_column_lines_up_with_its_own_heading`,
  which measures x positions through an **Auto** column whose heading is wider than its cells.
  The name assertion survives as a smaller, honestly-labelled claim.
- **Sabotage run.** Reverting `Define` to an assignment fails the new guard with
  *"Column 1 (classification) heading starts at x=356.0 but its cell starts at x=422.0"* — a 66 px
  gap, and the message names the site to fix. Restored; 489 tests pass, 0 warnings.
- `A_long_table_realizes_only_the_rows_that_are_visible` added — pins §7.1 so a change of items
  panel cannot quietly undo it.
- `Avalonia_still_ignores_an_assigned_definition_collection` added as an **upstream canary**: it
  fails the day a version carrying #21848 is taken, which is the notice that `Define`'s comment has
  become history rather than a live hazard.

### 7.5 The same trap is latent in the fluent surface

`Ui.Cols` and `Ui.Rows` (`Fluent/Ui.cs:62`, `Fluent/Ui.cs:82`) both assign:

    var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(definitions) };

**Not broken today**, and the distinction is worth stating precisely rather than raising an alarm:
those definitions are parsed from a comma-separated string, which has no syntax for a
`SharedSizeGroup`, so nothing there is trying to share and nothing fails.

It is a trap rather than a defect — and it sits at exactly the site that invites the use.
`Ui.Rows`'s own comment cites §21.2, *"a header-and-body table ends up keeping two column strings in
step by hand"*, which is the problem shared sizing exists to solve. The first person to reach for it
will set a group on a returned grid's definitions and watch it do nothing.

Fixed in Pass B: populate instead of assign, zero behaviour change today, trap gone.

---

## 8. What this will not do, said out loud

§26.12 is the model here: a table that is 80% of a data grid invites the assumption that it is all
of one.

- **No hierarchy.** Zero `TreeView`s and zero hierarchical views across five consumers (§27.2). If
  evidence ever appears it is a separate control, not a mode on this one.
- **No cell selection.** 200+ lines and its own focus and keyboard model, and it is where "a list of
  rows laid out in columns" stops being an honest description of the architecture. Revisit only with
  evidence.
- **No grouping.** This is the clause that drags a whole collection-view abstraction in behind it —
  and that abstraction is the `QAbstractItemModel` layer §48 already rejected, wearing a SQL hat.
- **No cell virtualization.** It pays at twenty-plus columns; the measured need is three.
- **No SQLite or other data-source backing.** Rejected on its own terms in §9 below.

---

## 9. The rejected alternative worth writing down: a data-source backing

A SQL-backed table was considered — SQLite as the model, `ORDER BY` for sorting, `WHERE` for
filtering, `LIMIT`/`OFFSET` for virtualization. It is refused, and the reasons are worth keeping
because the idea is a good one in a different context.

| Package | Licence | |
|---|---|---|
| `Microsoft.Data.Sqlite` | MIT | a facade over the two below |
| `SQLitePCLRaw.core`, `bundle_e_sqlite3` | **Apache-2.0** | |
| `SQLitePCLRaw.lib.e_sqlite3` | **none declared** | **39 MB**, 45 native binaries, 25+ RIDs |

1. **It breaks the all-MIT property on purpose.** Apache-2.0 is permissive and MIT-compatible — this
   is not a copyleft problem — but its §4 carries a NOTICE redistribution obligation and its §3 a
   patent-termination clause, neither of which MIT has. That is the *"a term the licence on the tin
   does not mention"* sentence again, in a milder key, and it is the third time that rule has fired
   across this arc.
2. **A 39 MB native payload in a UI toolkit**, per-RID, into every consumer's publish output, plus
   trimming and AOT complications and a diamond conflict for any consumer already on a different
   SQLitePCLRaw.
3. **It inverts who owns the data.** The measured consumer has a `List<Field>` from a PDF parse. A
   SQL-backed table would have it declare a schema, open an in-memory database, `INSERT` 200 rows
   across a native boundary and `SELECT` them back — to draw 200 rows.
4. **The performance argument runs backwards.** SQL wins when data exceeds memory; against a resident
   collection it is SQL parsing plus marshalling plus row materialization against a microsecond LINQ
   sort. And `LIMIT`/`OFFSET` is the wrong virtualization primitive regardless — offset paging is
   O(n) per page and re-runs the query on every scroll tick.

**Where the idea is right, and what the answer would be.** A dataset that genuinely does not fit in
memory — a log viewer over millions of rows, or data that already lives in a database — is a real
instrument-panel need. The LunaP-shaped answer is not a database dependency but a windowing
delegate:

```csharp
table.Window(count: () => _total, page: (skip, take) => _query(skip, take));
```

The consumer brings SQLite, Postgres, a memory-mapped file or nothing at all; LunaP names no data
library and §1 holds. It is the same seam shape as `ISettingsStore` (§19.1) and the same move §49
makes for the graphics door. **Not built now** — no consumer has asked — but named, so the next
person reaches for the seam rather than the dependency.

---

## 10. What the freedom to break things changes

Recorded because it changes a decision that was already argued, and because it will stop being true.

**There are no external consumers today.** That makes a breaking change nearly free now and
expensive later, so anything that wants to break should break in this arc rather than after it.

**What it changes:** §4.1's descriptor was argued primarily on binary compatibility — a consumer who
upgrades without recompiling must not break. That argument is gone. The descriptor still stands on
its own merits, but the shape is now a taste decision rather than an obligation, and there are two
honest answers:

- **Keep both forms.** `Column("name", f => f.Name)` for the common case, the descriptor for
  anything with behaviour. Two ways to declare a column, and the terse one covers most of them.
- **Collapse to the descriptor.** One way to declare a column, at the cost of
  `Column(new LunaColumn<Field>("name", f => f.Name))` for the simplest case there is.

**Taken in Pass D, 2026-08-13: keep both**, with the terse overload delegating to the descriptor so
there is one code path. The argument is written up at §4.1, where the decision lives.

The three-state sort cycle (§4.4) was the other decision this latitude reopened, and it was settled
the same day and kept. Both were flagged in this plan as places to push back; both were pushed back
on and survived, which is the difference between a decision and a default.

**What it does not change.** Nothing else in this plan was constrained by compatibility. The
assign→populate fix, the guards, and every phase are additive on their own terms.

---

## 11. The passes

Ordered. A pass is finished when its guards exist **and have been made to fail on purpose** (§22.5),
and when anything new is in the gallery (§7) and the automation tree (§24).

### Pass A — the record

**Blocking, and small.** `LunaTable.Define` currently cites `§27` because the subsection it wants
does not exist yet, and `CitationTests` fails the build for a citation that does not resolve.

Write into `docs/LunaP.md`:

- **§27.7, a correction subsection** in the §21.6 style — what §27 claimed about shared sizing, what
  is actually true, and the fact that the guard could not fail. Never an edit that makes the record
  look like it was always right.
- **The dependency defect**, in the §12.3 style that mutating `Application.Styles` set: version
  (Avalonia 12.1.0, released 2026-07-09), reproduction (assigned versus populated, with the two
  measurements), effect (6 px on an Auto column), upstream fix (#21848, merged 2026-07-26, after the
  release), and the workaround.
- **The two-holed guard**, in the §22.5/§22.6 style: a test asserting group *names* while the columns
  shared nothing, on data that contained no `Auto` column — so the one feature the comment claimed
  was the one feature never exercised.

Then repoint `Define`'s citation at §27.7.

### Pass B — the latent trap in `Ui.Cols` / `Ui.Rows`

Populate instead of assign, both sites (§7.5). No behaviour change today; it removes a trap sitting
where §21.2 says people will reach for it.

**Guard:** two `Ui.Cols` grids in one shared size scope, an `Auto` column, assert equal resolved
width. Sabotage by reverting to assignment.

### Pass C — the audit: guards that check wiring rather than effect

**This is the pass that answers "stop tripping over previous decisions", and it is the one worth
doing properly.** The table's guard was not a bad test; it was a test of the wrong *kind*, and
nothing about it looked wrong.

The sweep is one question asked of every assertion in the suite:

> **If the mechanism under test silently did nothing, would this still pass?**

Sizing: **594 `Assert.` calls; 60 references to bounds, geometry, visual descendants or a render
capture.** Roughly one assertion in ten reaches for an outcome. That ratio is not damning on its own
— plenty of tests are legitimately about logic — but it is the right place to start, and the
candidates are the ones asserting a name, a class, an attached property or a style key with no
rendered consequence anywhere in the test.

Output is a list, not a rewrite: for each suspect, either a replacement that measures the effect, or
a one-line note in the file saying why the wiring *is* the effect there. Both are acceptable answers;
silence is not.

### Pass D — the column API decision

Take §10's decision deliberately and write it down. Blocks Pass E, because sorting is the first thing
that needs somewhere to put per-column behaviour.

### Pass E — sorting

`Comparison<T>` on the descriptor. Header cells become focusable and invokable (§4.6). Three-state
cycle (§4.4). Stable ordering via `OrderBy`, unsorted order preserved (§4.3). Survives `Refresh`
(§4.5). Works before the template (§4.7).

**Guards:** §6.4, §6.5, §6.6, §6.7 — sort/re-sort/unsort round trip, sort survives refresh, two
tables do not sort each other, header reachable by keyboard alone.

### Pass F — widths, resize, persistence

Mutable widths, drag grips, `Persist` through `ISettingsStore` (§4.9).

**Now unblocked in a way it was not this morning:** §7.2 asked whether a runtime width change
propagates through the shared size scope. It could not have, because nothing was sharing. Re-ask it
against the fixed control before designing the resize — the answer may now be yes, which would make
a resize one number on the header instead of a sweep over realized rows.

### Pass G — editing and validation

`Commit` and `Validate` (§4.8). Error presentation shared with whatever §47 settles for `FieldRow`.

**Guards:** §6.1, §6.2, §6.3 — recycled rows carry no editor, an edit does not make its row jump, a
row's automation name is rebuilt after a commit.

### Pass H — automation depth

`ISelectionItemProvider` on rows, `IValueProvider` on editable cells — the real gap §2.1 identified
once grid semantics turned out to be unavailable to anybody.

---

## 12. Sequence

| | Pass | Size | Blocked on |
|---|---|---|---|
| ✅ | The two verifications (§7) | done 2026-08-13 | — |
| ✅ | assign→populate, guards, comments (§7.4) | `d653ca3` | — |
| ✅ | **A** — §27.7, the defect entry, the sabotage limit | `7d6df5e` | — |
| ✅ | **B** — `Ui.Cols` / `Ui.Rows` latent trap + guard | `7d6df5e` | — |
| ✅ | **C** — the audit; §46, and a shortcut bound to the wrong action | `df3dd6a` | — |
| ✅ | **D** — column API and sort cycle settled (§4.1, §4.4) | 2026-08-13 | — |
| ✅ | **E** — sorting; §27.8, §27.9, 8 sabotages | `16f0da0` | — |
| ✅ | star-column regression from the §27.7 fix; §27.10 | `0e9e960` | — |
| ✅ | **F** — resize grips, `TableKey`, `tables.json`; §27.11 | `7edf73d` | — |
| ✅ | **G** — editing and validation; man page §50, 4 traps guarded | `cf8ece9` | — |
| ✅ | **H** — `ISelectionItemProvider` (already free), `IValueProvider`; §50.6 | `cf8ece9` | — |

**Every pass in this plan is done.** §50.4 and §50.5 are the two findings worth
carrying: a recycling guard that could not fail, and a row name no screen reader
could ever reach. This file can be deleted.

A is blocking only for the citation. B and C are independent of everything and can go any time —
**C is the one with the most value per hour**, because it is the only pass that finds defects nobody
has thought of yet.
