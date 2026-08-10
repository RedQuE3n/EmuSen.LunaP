# EmuSen.LunaP.Testing

The headless test harness [LunaP](https://github.com/RedQuE3n/EmuSen.LunaP)
tests itself with. Render capture, layout assertions and visual-tree queries for
Avalonia windows, with no display and no GPU.

It is a **separate package on purpose**: `EmuSen.LunaP` references Avalonia and
nothing else, and a harness needs xunit and `Avalonia.Headless`. Reference this
from your test project only, and nothing your application ships gains a
dependency.

    dotnet add package EmuSen.LunaP.Testing

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

## What it gives you

- **`UiTest.Run(body)`** — dispatches onto the one headless UI thread the
  session owns. Window and control construction is only valid there.
- **`UiTest.AssertLaidOut(window, name)`** — the assertion that earns its keep.
  A window that failed to lay out, or whose controls have no template, renders
  as one flat colour; counting distinct colours catches that where walking the
  logical tree does not.
- **`UiTest.AssertStable(name, build)`** — renders twice and asserts the two are
  identical. A window showing a clock, a pid or a frame counter fails by design,
  which is what makes it a usable baseline target.
- **`UiTest.Capture(window)`** → RGBA bytes, with `Hash` and `DistinctColours`.
- **`VisualQuery`** — `FindPart<T>()`, `FindParts<T>()`, `CountParts<T>()`,
  `FindNamed<T>(name)`. For windows built in code there is no XAML namescope for
  `GetControl` to search, so these go through the visual tree.

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

## Licence

GPL-3.0-or-later.
