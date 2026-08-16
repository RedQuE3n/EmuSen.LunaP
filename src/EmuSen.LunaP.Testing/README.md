# EmuSen.LunaP.Testing

The headless test harness [LunaP](https://github.com/RedQuE3n/EmuSen.LunaP)
tests itself with. Render capture, layout assertions and visual-tree queries for
Avalonia windows, with no display and no GPU.

It is a **separate package on purpose**: `EmuSen.LunaP` references Avalonia and
nothing else, and a harness needs xunit and `Avalonia.Headless`. Reference this
from your test project only, and nothing your application ships gains a
dependency.

    dotnet add package EmuSen.LunaP.Testing

| | |
|---|---|
| Target framework | `net10.0` |
| Depends on | `EmuSen.LunaP`, `Avalonia.Headless`, `Avalonia.Skia`, `Avalonia.Markup.Xaml.Loader`, `xunit.assert` |
| Version | ships from the same tag, at the same number, as `EmuSen.LunaP` |
| Licence | MIT, as of 0.6.0 |

**The version pairing is deliberate.** The harness asserts about the toolkit's
own controls, so it tracks the toolkit's number rather than keeping its own — a
consumer pairing 0.8.0 of one with 0.2.0 of the other has a question nobody
wants to answer.

**`xunit.assert`, not `xunit`.** The harness calls `Assert` and nothing else,
and pulling the full metapackage would put `xunit.core`'s `Fact`/`Theory` into
every consumer that references this, including one running its tests on a
different runner.

## Using it

```csharp
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => LunaHeadless.BuildApp();
}

[Fact]
public Task The_settings_window_lays_out() => UiTest.Run(() =>
{
    var window = new SettingsWindow();
    window.Show();
    UiTest.AssertLaidOut(window, "settings");
});
```

`LunaHeadless.BuildApp()` builds the application with a real Skia render pass
and LunaP's theme already included. That include is not cosmetic: without the
real theme, templated controls have no template, render as nothing, and **every
assertion over them silently passes**.

`BuildApp(extra)` takes a hook for a suite with its own styles or services to
add. It runs after LunaP's own setup, so it can override what LunaP configured.

## What it gives you

**Dispatching.**

- **`UiTest.Run(body)`** — dispatches onto the one headless UI thread the
  session owns. Window and control construction is only valid there.
- **`UiTest.Session`** — the underlying `HeadlessUnitTestSession`, for a suite
  that needs to dispatch something `Run` does not cover. Read the warning below
  before you use it.

**Assertions.**

- **`UiTest.AssertLaidOut(window, name, minColours = 8)`** — the assertion that
  earns its keep. A window that failed to lay out, or whose controls have no
  template, renders as one flat colour; counting distinct colours catches that
  where walking the logical tree does not. Returns the frame it captured.
- **`UiTest.AssertStable(name, build)`** — renders twice and asserts the two are
  identical. A window showing a clock, a pid or a frame counter fails by design,
  which is what makes it a usable baseline target.
- **`UiTest.AssertMatchesBaseline(name, window)`** and the frame overload —
  compares against a stored baseline, and does nothing unless
  `EMUSEN_UI_BASELINE` names a directory.

**Capture.**

- **`UiTest.Capture(window)`** and **`Capture(bitmap)`** → a `RenderedFrame`:
  `Rgba`, `Width`, `Height`, a `Hash` (FNV-1a over every pixel) and
  `DistinctColours(stopAt)`, which ignores alpha and can stop counting early.
- **`UiTest.Redraw(window)`** — forces a genuine second render pass and captures
  that, rather than the window's first draw.
- **`UiTest.Dump(name, bitmap)`** — writes a PNG, if `EMUSEN_UI_DUMP` names a
  directory.

**Queries.**

- **`VisualQuery`** — `FindPart<T>()`, `FindParts<T>()`, `CountParts<T>()`,
  `FindNamed<T>(name)`. For windows built in code there is no XAML namescope for
  `GetControl` to search, so these go through the visual tree.

**The session itself.**

- **`UiSession.Current`** — the one headless session, started on first use.
- **`UiSession.TestAssembly`** — your test assembly, found by looking for the
  one carrying `[AvaloniaTestApplication]`.
- **`UiSession.Use(assembly)`** — names it explicitly, for a layout where the
  search cannot pick one. Discards any session already started.
- **`UiSession.DisablesParallelization(assembly)`** — public so a suite can
  assert its own configuration.

Set `EMUSEN_UI_DUMP` to a directory to get a PNG of every capture in the run.
Pixel-exact baselines are opt-in behind `EMUSEN_UI_BASELINE`, because they are
an artefact of one machine's font rendering.

## Test parallelisation must be off

`[assembly: CollectionBehavior(DisableTestParallelization = true)]` is required,
and **the harness refuses to start without it** rather than letting you find out
later.

Every test shares one headless application, and several things around it are
process-global — the settings store, the diagnostics hook, and the applied
theme's resource dictionary. xunit parallelises across test *classes*, so one
class's constructor can replace another class's state mid-assertion. It presents
as a suite that is green on your machine and red on CI, which is exactly how it
was found.

If your suite serialises another way — a single shared collection — say so
explicitly with `UiSession.ParallelismIsHandled = true`.

## An `async` lambda handed to `Session.Dispatch` is a test that cannot fail

Worth knowing before you reach past `UiTest.Run` for the session directly.

```csharp
// WRONG - this test can never fail
public Task Foo() => UiTest.Session.Dispatch(async () => { …; Assert.Equal(x, y); }, default);
```

`HeadlessUnitTestSession.Dispatch` has an overload taking
`Func<TResult>`, and an `async () => { … }` lambda binds to **that** one with
`TResult` inferred as `Task`. The call returns `Task<Task>`, so handing it back
as the `[Fact]`'s `Task` awaits only the **outer** one — which completes at the
body's first `await`. Everything after that runs detached on a thread nobody is
watching, and its failure is swallowed.

`Unwrap()` is the whole fix, spelled once per test class:

```csharp
private static Task Run(Func<Task> body) => UiTest.Session.Dispatch(body, default).Unwrap();
```

This was found by sabotaging two guards and watching them stay green. It leaves
no runtime trace — it is a compile-time overload choice — so there is nothing to
reflect over; LunaP's own suite catches it with a textual scan for
`Dispatch(async`. `docs/LunaP.md` §77.5.

## Limitations

- **`AssertLaidOut` is portable; `AssertStable` and the baseline comparison are
  not.** A `.frame` baseline is one machine's font rendering, which is why they
  are behind an environment variable and why LunaP does not commit its own.
- **No screen reader is involved.** The toolkit's accessibility guards measure
  Avalonia's automation tree, which is what a platform bridge reads — not Orca,
  NVDA or VoiceOver reading it aloud. This harness cannot close that gap.
- **The process-global statics are structural.** The refusal above is a guard
  against the hazard, not a fix for it; the statics belong to the toolkit's
  design (`docs/LunaP.md` §21.3).
- **`Assert` throws xunit exceptions.** A suite on a different runner can still
  reference this package, but a failure arrives as an xunit assertion type.

## Licence

MIT, as of 0.6.0, in step with the toolkit. Versions 0.2.0 through 0.5.0 were
published GPL-3.0-or-later and stay that way on nuget.org — see §25.

This package links `xunit.assert`, which is Apache-2.0 — the one dependency in
either package that is not MIT. It is referenced from a test project, so nothing
your application ships carries it.
