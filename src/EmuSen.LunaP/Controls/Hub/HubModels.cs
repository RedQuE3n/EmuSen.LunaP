using Avalonia.Media;

namespace EmuSen.LunaP.Controls
{
    /// <summary>The way back to what was left: a paused picture, its name and a line of facts, in a poster slot shaped for video.</summary>
    /// <param name="Title">The name, on at most two lines.</param>
    /// <param name="Facts">A line of facts under the name, such as how long ago it was left; null for none.</param>
    /// <param name="Picture">The paused picture; null draws the raised surface.</param>
    public sealed record HubContinue(string Title, string? Facts = null, IImage? Picture = null)
    {
        /// <summary>The words above the name, drawn in upper case, such as Continue Playing.</summary>
        public string Caption { get; init; } = "Continue playing";

        /// <summary>The desktop's button under the facts, such as Resume.</summary>
        public string Button { get; init; } = "Resume";

        /// <summary>The colour of its world in degrees, such as its console's, drawn as a small planet before the facts at night; null for none.</summary>
        public double? Hue { get; init; }

        /// <summary>A clip the poster slot will play once a video surface exists; null for none.</summary>
        public object? Video { get; init; }
    }

    /// <summary>A place on a hub: a destination's name and one live line about it.</summary>
    /// <param name="Key">What the host switches on, never shown.</param>
    /// <param name="Label">The destination's name.</param>
    public sealed record HubPlace(string Key, string Label)
    {
        /// <summary>One line about the place, cut with an ellipsis where it is long; null for none.</summary>
        public string? Status { get; init; }

        /// <summary>Whether the line needs the person, drawn in the attention colour after a warning sign.</summary>
        public bool NeedsAttention { get; init; }
    }

    /// <summary>The rows of a hub, top to bottom.</summary>
    public enum HubRow
    {
        /// <summary>The way back to what was left.</summary>
        Continue,
        /// <summary>The shelf of covers.</summary>
        Shelf,
        /// <summary>The row of places.</summary>
        Places,
    }

    /// <summary>What is focused on a hub: its row and, on the shelf or the places, the item's key.</summary>
    /// <param name="Row">The focused row.</param>
    /// <param name="Key">The focused cover's or place's key; null on Continue.</param>
    public readonly record struct HubFocus(HubRow Row, string? Key);
}
