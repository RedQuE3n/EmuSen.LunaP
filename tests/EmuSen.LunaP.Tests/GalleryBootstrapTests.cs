using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using EmuSen.LunaP.Gallery;
using EmuSen.LunaP.Testing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // THE ONE FILE A CONSUMER COPIES, AND IT WAS UNTESTED - see docs/LunaP.md §86.13.
    //
    // Pass 4 made the gallery a runnable application, and its `GalleryApp` is now the smallest
    // honest example of the bootstrap a consumer has to write: LunaApp.Configure owns the builder
    // chain, but the base theme include is deliberately left to the host so an application can opt
    // out (§11, §17).
    //
    // A sabotage pointed that include at a file that does not exist and **turned nothing red across
    // 1,041 tests**, because every UI test in this suite runs under LunaHeadless.BuildApp, which
    // supplies the theme itself and never constructs GalleryApp at all. That is the same structural
    // gap CLAUDE.md warns about for consumers - "anything the application must do at startup is
    // invisible to every window test" - reproduced inside this repository.
    //
    // WITHOUT THE INCLUDE, templated controls have no template: they lay out as nothing, render as
    // nothing, and every assertion over them silently passes. This toolkit has shipped that once.
    public class GalleryBootstrapTests
    {
        [Fact]
        public Task The_gallery_app_adds_the_toolkits_base_theme() => UiTest.Run(() =>
        {
            var app = new GalleryApp();
            app.Initialize();

            StyleInclude[] includes = app.Styles.OfType<StyleInclude>().ToArray();

            Assert.True(includes.Length > 0,
                "GalleryApp.Initialize added no StyleInclude at all. Without the toolkit's base "
                + "theme every templated control renders as nothing. See docs/LunaP.md §11 and §17.");

            Assert.Contains(includes,
                i => i.Source is { } uri && uri.ToString().Contains("LunaTheme.axaml", StringComparison.Ordinal));
        });

        [Fact]
        public Task The_theme_it_names_actually_resolves() => UiTest.Run(() =>
        {
            // THE HALF THAT CATCHES A TYPO, and the reason the test above is not enough on its own:
            // a Source pointing at `NoSuchFile.axaml` still contains the word it was asked about
            // and still adds a StyleInclude. Reading `Loaded` is what forces Avalonia to resolve
            // the avares:// URI, and it throws when there is nothing there.
            var app = new GalleryApp();
            app.Initialize();

            foreach (StyleInclude include in app.Styles.OfType<StyleInclude>())
            {
                IStyle loaded = include.Loaded;
                Assert.NotNull(loaded);
            }
        });
    }
}
