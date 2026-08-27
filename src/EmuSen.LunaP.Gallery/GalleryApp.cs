using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;

namespace EmuSen.LunaP.Gallery
{
    // The application the gallery runs as - see docs/LunaP.md §86.13.
    //
    // THE THEME INCLUDE IS LOAD-BEARING AND IS NOT OPTIONAL, and it is here rather than inside
    // LunaApp.Configure for a stated reason (§11, §17): Configure applies the theme VARIANT and any
    // saved custom theme, but an application building its own AppBuilder has to be able to opt out
    // of the base theme, so the include is left to the consumer. Without it, templated controls have
    // no template - they lay out as nothing and render as nothing.
    //
    // This file is therefore also the smallest honest example of what a consumer must write, which
    // is a second reason for the gallery to be a real application rather than a class nobody can
    // start: the bootstrap is part of what a reader is trying to discover.
    // PUBLIC SO IT CAN BE GUARDED, which is the only reason - nothing outside this project
    // constructs one. `GalleryBootstrapTests` calls Initialize and asserts the include below both
    // exists and RESOLVES, because a sabotage found this file entirely untested: pointing the URI
    // at a file that does not exist turned nothing red anywhere in 1,041 tests (§86.13).
    //
    // That matters more here than it would in an ordinary sample, because this file is now the
    // smallest honest example of what a consumer must write, and §11 and §17 record this toolkit
    // shipping a window with no theme once already.
    public sealed class GalleryApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new StyleInclude(null as Uri)
            {
                Source = new Uri("avares://EmuSen.LunaP/Theme/LunaTheme.axaml"),
            });
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new GalleryWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
