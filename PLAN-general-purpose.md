# Working plan — making LunaP general-purpose

**This is a working document, not part of the record.** `docs/LunaP.md` is the design record and
stays the only one. This file was written to be deleted once the arcs below were built.

**It is committed as of 0.9.0 — see `docs/LunaP.md` §83.2.** Not because anything cites it directly,
but because `PLAN-table.md` had to be committed and elaborates §48 of this file: keeping the one that
is cited while discarding the document it refines would leave the survivor pointing at nothing, which
is the failure being fixed rather than a smaller version of it. Every arc below is built; it is kept
as the reasoning that preceded them, not as a plan anybody should still be working from.

Written 2026-08-13, on branch `settings-root`, against LunaP 0.7.1.

---

## 0. Read this before quoting anything below

**§21's warning applies to this file with force:**

> a doc that says "the remaining option is X" is recording what was considered, not what is
> possible. **Re-derive before building on it.**

That is not a formality here. **Two claims in this plan were falsified within a day of it being
written**, both by spending a few minutes checking rather than by anything going wrong:

| This plan said | What is true |
|---|---|
| §6/§48: *"Both dependency routes are closed"* | `Avalonia.Controls.DataGrid` **12.1.2 is MIT**, maintained, public repo, no licence gate, depends on Avalonia 12.1.0. It is closed on §1 and on Avalonia's own performance advice — **not on licence** |
| §5: TreeDataGrid may be **AGPL-3**, warranting a §27.1 correction | **There was never an AGPL release.** Issue #307 announced an intent in Sept 2024 that was never carried out; §27.1's conclusion stands unchanged. See §5 below |

`PLAN-table.md` produced a third: a propagation measurement taken under code that a later fix
invalidated, load-bearing for a design, and only caught because it was re-taken.

**The numbering in this file is stale and has been corrected.** The man page now runs to §47, and
two of the arcs planned here are already written into it:

| Planned as | Became |
|---|---|
| §46, the scope decision | **man page §47** — and its proposed standard was *rejected*, see §47.5 there |
| §48, the table | **man page §27.8–§27.11** — sorting, resize, remembered layout, and a regression |

The three arcs that remain are renumbered below to the next free man-page numbers: **§48** inputs,
**§49** the graphics door, **§50** window services, **§51** deferred.

---

## 1. The question this plan answers

> *If a developer picked up LunaP to build an application — an office app or a video game — can she
> meet the need?*

Short answer: **for applications shaped like the ones that built her, yes and better than most.
For an office app or a game proper, not today, and the gaps are three.**

---

## 2. What was verified in the code (not recalled, not assumed)

These were read or grepped during the session and are the factual basis for everything below.

| Finding | Where |
|---|---|
| 24 public control types; toolkit is ~7,300 lines of C# | `src/EmuSen.LunaP/` |
| **`<FluentTheme />` is included by LunaP's own theme** — stock Avalonia controls work and are themed, but in *Fluent's* palette, not `LunaPalette` | `Theme/LunaTheme.axaml:9` |
| **No text box, checkbox, radio, slider, numeric, or date control in LunaP.** Input surface is: `LunaSwitch`, `Dropdown`, `FilterBar`, `PathPickerRow`, `ActionButton`, `ActionToggle`, `ActionMenuItem` | `Controls/` |
| **No graphics interop of any kind.** Zero hits for `NativeControlHost`, `OpenGl`, `Vulkan`, `CompositionCustomVisual` | `src/` |
| `RgbaImageView` is the only graphics door: `byte[]` → `Marshal.Copy` → `WriteableBitmap` → `InvalidateVisual()` | `Controls/RgbaImageView.cs:57-82` |
| Nearest-neighbour **is** already handled (hardcoded, not configurable) | `Theme/Controls/RgbaImageView.axaml:12` |
| **No fullscreen anywhere.** `WindowState` is referenced only to pause a timer on minimise | `Windowing/PollingWindow.cs:71` |
| Restored geometry **is** validated against connected screens — the multi-monitor bug is already handled | `Windowing/ToolWindow.cs:77`, `WindowPlacementStore.cs:57` |

Three small defects/nits found in passing:

1. **`Stretch` summary describes the wrong enum member.** `Controls/RgbaImageView.cs:28` says
   *"Defaults to preserving the aspect ratio"* but the default is `Stretch.None`. In Avalonia's
   vocabulary "preserves the aspect ratio" is `Uniform`; `None` does not scale at all. Intent and
   value are both right for pixel-accuracy — the sentence names a different member than the one it
   is on. Under the house rule ("a comment that has drifted is worse than no comment, because it is
   believed") this is worth a one-line fix.
2. **`SetFrame` forces two copies.** It takes `byte[]` only, so a consumer whose pixels are in
   native memory must marshal to a managed array first, then `Marshal.Copy` again into the
   framebuffer. At 1080p that is ~8.3 MB per copy; at 60fps, ~500 MB/s of avoidable memcpy. A
   `ReadOnlySpan<byte>` and an `nint` overload remove one copy for almost no code —
   `ILockedFramebuffer.Address` is already in hand at line 77.
3. **No integer scaling.** With `Stretch="Uniform"` at a non-integer factor, hardcoded
   nearest-neighbour gives uneven pixel rows (a 160×144 frame at 4.17× yields rows 4 and 5 tall),
   which shimmers on scroll. Every emulator frontend eventually grows an integer-scale option.

---

## 3. The verdict, per application class

### Office / productivity: yes, with a visible seam and one real hole

The shell is complete — §26 built the whole `QMainWindow` vocabulary, and settings persistence,
window placement, dialogs, pickers and a genuine accessibility pass come with it.

- **The seam.** Forms fall back to stock Avalonia controls, which work but render in FluentTheme's
  palette. An office app comes out ~70% LunaP, ~30% Fluent, and the join shows in accents, borders
  and focus rings. §21.2 already caught this from the other side (three sites reaching for
  `SystemChromeLowColor`, "a key LunaP's theme cannot reach"). A settings-heavy app is *mostly*
  form controls, so the seam is most of the screen.
- ~~**The hole.** `LunaTable<T>` is read-only and unsorted (§27.5).~~ **Half closed since this was
  written.** An office app is approximately "a list you can sort, filter and edit": filtering
  existed, **sorting now does** (§27.8), and the columns can be dragged and remembered (§27.11).
  Editing is the remaining piece and is Pass G of `PLAN-table.md`.
- Also absent: printing, undo/redo.

### Video game: she is the frontend, not the game

Launcher, config UI, editor panels, emulator frontend — excellent, and that last one is what she
was built for. The game window itself — no. CPU blit only, no GPU surface, no fullscreen.

---

## 4. The structural finding, which is the real answer

**LunaP is an *instrument panel* toolkit** — meters, consoles, framebuffer views, tool windows,
live settings — and she is unusually strong at it for her size, precisely because §26.1's method
only ever built what real consumers had already hand-rolled badly.

But **all five of those consumers are the same kind of application.** The evidence-driven method
cannot answer "can she serve anyone's app," because it can only report on classes that fed it. It
will never surface "you need column sorting" or "you need field validation," because no emulator
tool needed one. *The absence of evidence here is an artifact of a homogeneous consumer set, not a
finding.*

So going general-purpose is a **scope decision, not a measurement** — and it needs saying out loud
once, so it is a chosen direction rather than a drift.

---

## 5. TreeDataGrid — settled, and the open question is closed

### The refusal, on API shape first and licence second

**Do not take the dependency**, and the argument that ends it is not the licence. TreeDataGrid's
entire public API *is* its source object — a consumer writes
`new FlatTreeDataGridSource<T>(items) { Columns = { new TextColumn<T, string>(…) } }` — so LunaP
would either re-export those types from its own signatures, putting a third-party name in a toolkit
whose tin says "Avalonia and nothing else", or wrap **71 public types** to expose about seven.

That ordering matters: **a licence-only refusal invites the next author to take the MIT community
fork the moment they find one**, and the shape argument does not care whether a fork is MIT.

### The open question is closed: there was never an AGPL release

Both checks in the earlier draft were run, 2026-08-13.

1. `licence.md` on the archived repo's `master` is **MIT**, and has **exactly one commit in its
   entire history** — `2150ddb0`, 2022-03-01, "Add licence." GitHub reports the repository's licence
   as MIT. Archived, last push 2025-10-13.
2. [Issue #307](https://github.com/AvaloniaUI/Avalonia.Controls.TreeDataGrid/issues/307) is dated
   **2024-09-14** and says *"We plan to implement these licensing changes later this year."* It is an
   announcement of intent, and it was never carried out in public.

What shipped instead, from the packages themselves:

| | 11.1.0 / 11.1.1 | 11.2.0 → 12.2.1 |
|---|---|---|
| `<repository>` | url + a commit that **resolves** | url **removed**; commit **absent** from the public repo |
| `AvaloniaUI.Licensing` | absent | present, 3.0.2 → 3.1.2 |
| licence strings in the assembly | **0** | **36**, including runtime enforcement |
| obfuscation | no | yes, per the shipped `themes/README.md` |

**So §27.1 needs no correction subsection.** Its conclusion — *"not a copyleft problem … a commercial
one"* — is correct, and this plan's hypothesis (source AGPL, binary EULA) is refuted.

**One evidence row in §27.1 is inert, though**, and that is worth knowing: *"no `<license>` element
and no `licenseUrl`"* is equally true of 11.1.0 and 11.1.1, which are indisputably MIT-era.
AvaloniaUI never put licence metadata in this nuspec in any version. The four other rows carry the
whole argument.

The gate is enforced **at run time**: the 12.2.1 assembly carries
*"No AvaloniaUI license key found. Please ensure the `<AvaloniaUILicenseKey />` item is defined in
your executable project"* — the **consumer's** executable project, not LunaP's — plus expiry,
per-application binding, and an online ticket download.

### The third path is better than this plan assumed, and still refused

Not "vendor an archived tree." `TreeDataGrid.Avalonia` **12.0.0** is a live package on nuget.org:
`<license type="expression">MIT</license>`, authors *"Fidarit Mullayanov, Steven Kirk"*, targets
Avalonia 12.0.1, ships net8.0 and net10.0, 17,699 downloads, **zero licence strings in the DLL**,
repository active. It carried the MIT line forward past upstream's freeze on its own.

Refused on **§1** — it is not Avalonia, it is one person's fork — on **failure mode 3 below**, since
the bus factor is one against a thing that has already demonstrated this exact failure once, and on
**§27.2's measurement**. The reason this plan originally gave — *"a permanent maintenance liability
against an upstream that is archived"* — is **no longer true** and should not be used.

The last clean upstream version is **11.1.1** (2025-01-30, commit `0cb3b3a5`), not 11.1.0.

---

## 6. The plan

### ~~Record the scope decision~~ (planned here as §46) → **done, man page §47**

Taken 2026-08-13, and it came out **smaller than this plan expected.**

This plan assumed going general-purpose meant retiring "build what was counted" wholesale. §47
concluded instead that §21's rule was only ever answering *what is missing* — it was never asked
when a control that already earned its place is **finished** — so four of the five arcs here are
**completions of things that already earned their place** and need no scope change at all. Only the
graphics door is a genuine widening, and §47.4 names it without taking it.

**The standard this plan proposed was rejected in §47.5, before it was ever adopted:**

> ~~The gallery must contain a plausible office-app screen and a plausible game shell, built only
> from LunaP.~~

Two reasons. *"Built only from LunaP"* is **false by design** — the inputs arc below re-skins
`TextBox` and `CheckBox` rather than wrapping them, so an office application calls `new TextBox()`
by intention, and the standard fails on its first day for a reason that is not a failure. And
*"plausible"* is not assertable; §7's gallery rule works because reflection checks it.

**What replaced it** is the palette sweep already planned as the inputs arc's guard: every control
in a live headless application must resolve its brushes to `LunaPalette` rather than to Fluent's
defaults. Mechanical, falsifiable, and it fails the day somebody adds a control and forgets to style
it.

### ~~The table grows up~~ (planned here as §48) → **done, man page §27.8–§27.11**

Built 2026-08-13. Sorting (§27.8), draggable widths and a remembered layout (§27.11), plus a
regression the first fix introduced and a correction for it (§27.10).

**The model layer was rejected as this plan proposed**, and the SQL-backed variant of the same idea
with it: .NET already has the query language, so `Refresh(IEnumerable<T>)` *is* the `SELECT … WHERE`
seam and the control owns only `ORDER BY`, because the gesture that triggers it lives on the header.
A SQLite backing would also have put Apache-2.0 and 39 MB of native binaries into a toolkit whose
every dependency is MIT.

**CORRECTION — this plan said "Both dependency routes are closed" and that is wrong.**
`Avalonia.Controls.DataGrid` **12.1.2** is MIT by expression, has a public repository whose commit
resolves, carries **zero** licence strings in its assembly, and depends on Avalonia 12.1.0 — this
toolkit's exact version. It is refused on §1 and on Avalonia's own performance advice, **not on
licence.** Saying both routes were closed made the home-grown table look forced when it was chosen.

**Tree: still deferred**, and §47.3 now gives the reason a test rather than a preference — hierarchy
introduces expansion state, a parent/child model and a path to address a row by, which is a new noun
for a consumer to learn and therefore a new kind rather than a completion.

### ~~§48 — Inputs, and closing the FluentTheme seam~~ → **done, man page §48**

Built 2026-08-13. 46 resource overrides in `Theme/FluentBridge.axaml`, one style in
`Theme/Controls/FormControls.axaml` (`ProgressBar` names no resource for either half of itself), two
new palette tokens, and `FormControlTests` as the guard §47.5 asked for — nine controls swept, and
it passes.

**Validation is the piece that was NOT built** and it is the one this plan called "the one genuinely
new thing". `DataValidationErrors` still has no LunaP answer; §48.5 names it.

**Three corrections came out of it, and they are why §48.3 exists.** Of four contrast figures written
beside the new tokens, one (6.98:1, white on the light accent) was measured by nothing — it is
6.31:1 — and two more were rounding slips. The bridge also claimed the two slider tracks are told
apart at 3:1 when they measure 1.13:1 dark and 1.88:1 light; the standard invoked was the wrong one
and no theme meets it, Fluent's own pair being 1.02:1. **The colours did not change; the claims did.**
The `LunaOnAccent` floor is now pinned by a test that was made to fail on purpose.

**The method entry is §48.2**, and it is the one to carry forward: two rounds of probing remembered
resource key names missed a panel three controls were painting, and enumerating the real tree —
2,052 entries — found it at once. That is the rule now in `CLAUDE.md`.

### ~~Original plan for the inputs arc, kept for the record~~

Highest value. An office app is mostly form controls.

- **Re-skin, do not re-implement.** §29.1 already set the precedent: `ActionControls`, `LunaList`
  and `Widgets` use `StyleKeyOverride` and borrow FluentTheme's templates wholesale. Do the same
  for `TextBox`, `CheckBox`, `RadioButton`, `Slider`, `NumericUpDown`, `CalendarDatePicker`. Styles,
  not templates — far less code, and it inherits Fluent's keyboard and accessibility behaviour.
- **Keep `<FluentTheme />`.** Third-party Avalonia controls assume it is present; TreeDataGrid
  [breaks entirely without it](https://github.com/AvaloniaUI/Avalonia.Controls.TreeDataGrid/issues/246).
  Removing it to own the whole look would make LunaP incompatible with the ecosystem she is trying
  to join.
- **Validation is the one genuinely new thing.** Avalonia has `DataValidationErrors`; LunaP has no
  error state anywhere. A `FieldRow` that can show an invalid state with a message is the
  LunaP-shaped answer, and it is the piece an office app cannot fake.
- **Guard:** extend the `LunaPaletteTests` idea — sweep every input control in a live headless app
  and assert its resolved brushes trace to `LunaPalette`, not Fluent's defaults. Turns "the seam is
  closed" into an assertion that fails the day someone adds a control and forgets the style.

*Largest arc, but volume rather than difficulty.*

### §49 — The graphics door: two doors and one hazard

**Correction made during the session.** `NativeControlHost` was first recommended as the primary
door. That was wrong. It has an **airspace problem** — native content always renders *above*
Avalonia's layer, so no LunaP control can sit on top of it: no pause overlay, no HUD, no menu
dropping over the game area, no tooltip. Pointer events also do not reliably reach it
([#8104](https://github.com/AvaloniaUI/Avalonia/issues/8104),
[#18244](https://github.com/AvaloniaUI/Avalonia/issues/18244), the latter open against 11.2.1).
Render transforms do not apply to it and it cannot be transparent.

- **Primary: `GraphicsSurface` on `OpenGlControlBase`.** A normal control — composites into the
  tree, takes focus, receives input, LunaP chrome sits on top. The consumer brings Silk.NET or
  OpenTK; LunaP exposes the context in the render callback and names no graphics library, so §1
  holds. The seam is a handle, not a dependency — the same shape as `ISettingsStore` in §19.1.
- **Secondary: `NativeControlHost`** as the escape hatch for "I own the whole surface, composite
  nothing over me" — fullscreen games, video players. Ships with the airspace and pointer-event
  limits written up in the §12.3 style: version, reproduction, issue numbers. The difference
  between a consumer reading it beforehand and discovering it at 2am.
- Also here: the `SetFrame` span/pointer overloads and integer scaling from §2 above.

**Verify before starting:** whether `Avalonia.OpenGL` ships inside the main Avalonia package or is
a separate `PackageReference`. If separate, it is a decision needing a `§` and a licence check.

### §50 — Window services ← *do fullscreen early, before §49*

Fullscreen is small and it is most of what a game needs from a window. Pull it forward.

**The trap worth a guard:** fullscreen must not poison `WindowPlacementStore`. Enter fullscreen,
save placement, exit — if the saved rect is the fullscreen one, the window never returns to a
usable size. Test: save, fullscreen, save again, restore, assert the pre-fullscreen rect survived.
Same shape as the `IsOnAScreen` rule already at `ToolWindow.cs:77`.

Then, in rough value order: cursor hide/idle, keep-awake, `Topmost`, clipboard, file drag-and-drop,
window icon, single-instance, custom title bar. Ordinary work, no research problems.

**Name as hazards rather than build:** cursor confinement and raw/relative mouse motion — Avalonia
has no API, so they stay the developer's problem, honestly labelled. Same shelf as gamepad input
(already moved out to `EmuSen.Endymion` in §15) and audio.

### §51 — Deferred, and said out loud

Printing, undo/redo, tree views, MDI, embedded browser. Each gets a line in a "what this does not
do" section with the reason. §26.12 is the model: a shell that is 80% of `QMainWindow` invites the
assumption it is all of it, and the same will be true of a toolkit that is 80% general-purpose.

---

## 7. Sequence

| | Arc | Unblocks | Size | State |
|---|---|---|---|---|
| 1 | Scope decision | everything after it | hours | ✅ man page §47 |
| 2 | The table | office apps | medium | ✅ man page §27.8–§27.11 |
| 3 | §48 inputs + seam | office apps | large | ✅ man page §48 — validation still open |
| 4 | fullscreen | games | small | ✅ **man page §75** |
| 4b | cursor hide/idle | games | small | ✅ **man page §76** |
| 5 | graphics door | games | medium | needs a named first consumer (§47.4) |
| 6 | remaining window services | both | small, incremental | ✅ **man page §77** — settled: one built, six refused |

**THE ARC NUMBERS IN THIS FILE ARE STALE A SECOND TIME.** §0 renumbered the remaining arcs to
§48–§51; the man page has since run past all four, and every one of those numbers is taken (§48 form
controls, §49 validation, §50 table editing, §51 the version). Fullscreen went in as **§75**. The
next free number is **§76** — re-derive it rather than trusting this line, which is the third time
this file has needed that warning.

**§77 falsified a sixth claim from this file.** Its own sentence *"Then, in rough value order:
cursor hide/idle, keep-awake, Topmost, clipboard, file drag-and-drop, window icon, single-instance,
custom title bar. **Ordinary work, no research problems**"* is wrong in both directions: four of
those are already done by Avalonia (`Topmost`, `Icon`, `Clipboard`, the client-area hints) and three
are research problems (keep-awake needs three platform implementations with no Avalonia surface;
single-instance needs cross-platform IPC; a custom title bar is a new noun). Only file drop was
built. The list was written from memory and never checked against `Avalonia.Controls` 12.1.0;
checking it took twenty minutes.

**What §75 found, and it is the reason to re-derive rather than quote:** the guard this plan
proposed for the placement trap — *"save, fullscreen, save again, restore, assert the pre-fullscreen
rect survived"* — **passes against code with no full-screen handling at all.** `Avalonia.Headless`
stores `WindowState` and never acts on it, so the poisoned geometry the test looks for is never
produced. The rule had to be split out and made pure to be assertable at all (§75.4). That is a
fourth falsified claim from these working documents.

**The ordering changed.** This plan put the table fifth; it went second, because the TreeDataGrid
licence question had to be settled before anything else could be reasoned about and settling it left
the table as the obvious next thing.

**§49 moved behind a gate rather than down the list.** Man page §47.4 names it as the one arc here
that is not a completion of something that already earned its place, and says what taking it
requires — first among them a named repository that will use it, rather than a hypothetical game.

---

## 8. Four failure modes to design against

1. **Churn kills toolkits.** Microsoft's UI history — WinForms, WPF, Silverlight, UWP,
   Xamarin→MAUI — is a record of migrations costing users more than the improvements returned.
   §26.13's standard holds for every arc: *nothing breaks, everything is additive, a consumer who
   upgrades and changes nothing has the same application.*
2. **Generalizing must not close doors.** GTK4's widening made application-specific theming
   *harder* and removed low-level APIs. LunaP's CSS theming (§12.2) and palette-pinning test are
   differentiators; a general-purpose push that regresses them trades an asset for a commodity.
3. **Never take a dependency whose licence can change underneath you.** §27.1 caught one
   mid-flight. ~~§5 above shows it then changed again.~~ **It had not** — the AGPL relicence was
   announced and never carried out, and §5 now records that. The lesson survives the correction
   intact and is arguably sharper for it: what actually happened is that the *source stopped being
   published at all* between 11.1.1 and 11.2.0, with no announcement and nothing in the package to
   say so beyond a `<repository>` element quietly losing its url. A licence that changes is visible.
   A source tree that stops existing is not. Both packages MIT, every dependency MIT, all six arcs.
4. **An "everything" toolkit is a toolkit with no opinion.** The one to hold hardest. LunaP's
   opinion — dark-first, instrument panels, meters, consoles, framebuffers, a real accessibility
   pass, a headless render harness — is why she is worth picking up. General-purpose should mean
   *she does not block an office app*, not *she becomes FluentTheme with extra steps*. Every arc
   adds a capability; none should sand off a preference.

---

## 9. Where this stands, 2026-08-13

Nine commits on `settings-root`, none pushed, and **no publishing until this arc is finished** —
which is what made the breaking-change latitude in §47 of `PLAN-table.md` free to use.

| | |
|---|---|
| `d653ca3` | shared size groups never registered; the guard that could not notice |
| `7d6df5e` | §27.7, and the same trap closed in `Ui.Cols` / `Ui.Rows` |
| `df3dd6a` | §46, the audit — and a shortcut bound to entirely the wrong action |
| `16f0da0` | sorting (§27.8, §27.9) |
| `0e9e960` | the star-column regression the alignment fix introduced (§27.10) |
| `7edf73d` | resize grips and a remembered layout (§27.11) |
| `1845f25` | §47, what §21's rule governs |

**Still open in `PLAN-table.md`:** Pass G, cell editing and validation; Pass H,
`ISelectionItemProvider` and `IValueProvider`. Both are completions under §47.3 and need no further
decision.

**`CHANGELOG.md` and the version are untouched on purpose.** Nothing here has shipped, the public
surface has grown by `LunaColumn<T>`, one `Column` overload, `TableKey`, `SaveNow`, `TableLayout` and
`TableLayoutStore` — additive, nothing removed — and the version and its changelog entry are a
person's call at release time (§42.4).

---

## Sources

- [Qt Bridges: public beta for C#](https://www.qt.io/blog/qt-bridges-public-beta-for-csharp)
- [Avalonia native interop docs](https://docs.avaloniaui.net/docs/app-development/native-interop)
- [NativeControlHost pointer events #8104](https://github.com/AvaloniaUI/Avalonia/issues/8104)
- [NativeControlHost receives no events #18244](https://github.com/AvaloniaUI/Avalonia/issues/18244)
- [OpenGlControlBase with a non-cooperative renderer #11926](https://github.com/AvaloniaUI/Avalonia/discussions/11926)
- [DataGrid — Achilles' heel? #16235](https://github.com/AvaloniaUI/Avalonia/discussions/16235)
- [TreeDataGrid requires FluentTheme #246](https://github.com/AvaloniaUI/Avalonia.Controls.TreeDataGrid/issues/246)
- [TreeDataGrid upcoming license change #307](https://github.com/AvaloniaUI/Avalonia.Controls.TreeDataGrid/issues/307)
- [Avalonia Accelerate licensing changes](https://avaloniaui.net/blog/building-a-sustainable-future-for-avalonia)
- [Avalonia app performance docs](https://docs.avaloniaui.net/troubleshooting/app-performance-issues)
