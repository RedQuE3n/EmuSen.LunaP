using System.Collections.Generic;
using EmuSen.LunaP.Theme;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // THE COUNTS THE README PRINTS, READ BACK OFF THE REAL ALLOW-LISTS - see docs/LunaP.md §80.6.
    //
    // These were correct when the audit checked them, and that is exactly why they are pinned now:
    // a count in prose is right until the day something is added, and nothing about adding an
    // element name makes anybody open the README. It has already happened downstream - EmuSen's
    // `man theme` shipped a vocabulary two versions old, which is the same drift arriving in a
    // document that is harder to correct.
    //
    // §21.5's rule about a vocabulary change is what makes this cheap to hold: the lists are
    // generated from the allow-lists, so a deliberate addition turns this red once, in the same
    // commit, with the README line to fix named in the message.
    public class ReadmeClaimTests
    {
        [Fact]
        public void The_vocabulary_is_the_size_the_readme_says()
        {
            Assert.Equal(22, CssTheme.ElementNames.Count);
            Assert.Equal(20, CssTheme.TokenNames.Count);
        }

        [Fact]
        public void The_readme_lists_every_property_name_in_order()
        {
            // Spelled out rather than counted: the README prints all six, so a rename is a
            // documentation defect even when the count survives it.
            Assert.Equal(
                new List<string>
                {
                    "background", "background-color", "color",
                    "font-family", "font-size", "font-weight",
                },
                CssTheme.PropertyNames);
        }
    }
}
