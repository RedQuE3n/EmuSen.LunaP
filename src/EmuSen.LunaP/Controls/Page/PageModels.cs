using System.Collections.Generic;
using Avalonia.Media;

namespace EmuSen.LunaP.Controls
{
    /// <summary>The top of an item's page: its best art in a poster slot shaped for video, its cover, its name and a line of facts.</summary>
    /// <param name="Title">The item's name, on one line.</param>
    /// <param name="Facts">A line of facts under the name; null for none.</param>
    /// <param name="Art">The hero picture, the poster a clip will turn from; null draws the surface.</param>
    /// <param name="Cover">The cover; null draws a generated card with the title and the mark.</param>
    /// <param name="CoverAspect">The generated card's width over its height, with no cover; a cover is drawn whole at its own shape.</param>
    /// <param name="Mark">A short name drawn at the top left of a generated cover, such as a console's.</param>
    public sealed record PageHero(string Title, string? Facts = null, IImage? Art = null, IImage? Cover = null, double CoverAspect = 0.75, string? Mark = null);

    /// <summary>A mark drawn before an action's words as the toolkit's own geometry, so it never falls back to another typeface.</summary>
    public enum PageMark
    {
        /// <summary>No mark: the words alone.</summary>
        None,
        /// <summary>An outlined heart, such as a favourite not yet chosen.</summary>
        Heart,
        /// <summary>A filled heart, such as a favourite.</summary>
        FilledHeart,
    }

    /// <summary>One of the page's actions under its name: a press, or a value Left and Right step, such as a slot to resume.</summary>
    /// <param name="Key">What the host switches on, never shown.</param>
    /// <param name="Text">The words on the button.</param>
    public sealed record PageAction(string Key, string Text)
    {
        /// <summary>The pad button drawn in the action's button and named in the help bar; null for none.</summary>
        public PadGlyphButton? PadButton { get; init; }

        /// <summary>Whether it is the page's main action, filled with the accent and first focused.</summary>
        public bool IsMain { get; init; }

        /// <summary>The value Left and Right step, drawn between arrows after the words; null for a plain press.</summary>
        public string? Value { get; init; }

        /// <summary>The values a desktop's split button lists, in order; the chosen one is Value.</summary>
        public IReadOnlyList<string>? Choices { get; init; }

        /// <summary>Whether the action can be pressed now.</summary>
        public bool IsEnabled { get; init; } = true;

        /// <summary>A mark drawn before the words; None by default.</summary>
        public PageMark Mark { get; init; }
    }

    /// <summary>What a row of a page offers beside its label.</summary>
    public enum PageRowKind
    {
        /// <summary>Does something when chosen.</summary>
        Action,
        /// <summary>Opens a deeper page, marked with a chevron.</summary>
        Opens,
        /// <summary>Asks a question first, marked with an ellipsis.</summary>
        Asks,
        /// <summary>A value Left and Right step, between arrows.</summary>
        Steps,
        /// <summary>Text typed in place on the desktop, through the keyboard on the big screen.</summary>
        Text,
    }

    /// <summary>One row of a page: a label at the left and its status, one line right-aligned, as a stepped value, a count or a choice.</summary>
    /// <param name="Key">What the host switches on, never shown.</param>
    /// <param name="Label">The words at the left.</param>
    public sealed record PageRow(string Key, string Label)
    {
        /// <summary>The row's status: the stepped value, or a line such as a count; null for none.</summary>
        public string? Status { get; init; }

        /// <summary>What the row offers. Action by default.</summary>
        public PageRowKind Kind { get; init; }

        /// <summary>A sentence the desktop draws under the row; null for none.</summary>
        public string? Sentence { get; init; }

        /// <summary>A stepped row's values for the desktop's choice, in order; the chosen one is Status.</summary>
        public IReadOnlyList<string>? Choices { get; init; }

        /// <summary>Whether the row can be chosen now.</summary>
        public bool IsEnabled { get; init; } = true;
    }

    /// <summary>One tile of a page: a picture with a label under it and a status line under that.</summary>
    /// <param name="Key">What the host switches on, never shown.</param>
    /// <param name="Label">The first line under the picture.</param>
    public sealed record PageTile(string Key, string Label)
    {
        /// <summary>The second line under the picture, muted; null for none.</summary>
        public string? Status { get; init; }

        /// <summary>The picture, drawn in its own shape on the raised surface; null for none.</summary>
        public IImage? Picture { get; init; }

        /// <summary>Words drawn on the tile where there is no picture, such as a generated cover's title.</summary>
        public string? Caption { get; init; }

        /// <summary>A short name at the top left of a generated tile, such as a console's.</summary>
        public string? Mark { get; init; }

        /// <summary>An empty place, drawn as a dashed outline with its caption.</summary>
        public bool IsEmpty { get; init; }

        /// <summary>A dot in the accent at the top right, such as the slot the page resumes.</summary>
        public bool IsMarked { get; init; }

        /// <summary>The desktop's buttons on the selected or hovered tile, as keys and words.</summary>
        public IReadOnlyList<(string Key, string Text)>? Buttons { get; init; }
    }

    /// <summary>A button's entry in the page's help bar, and the key the host is told when it is pressed.</summary>
    /// <param name="Button">The pad button.</param>
    /// <param name="Text">The words beside its glyph.</param>
    /// <param name="Key">What the host switches on; null for an entry that only names what the page does itself, such as South.</param>
    public sealed record PageHint(PadGlyphButton Button, string Text, string? Key = null);

    /// <summary>What is focused on a page: an action under the name, or the content's row or tile, by key.</summary>
    /// <param name="InActions">Whether the focus is on the actions.</param>
    /// <param name="Key">The focused action's, row's or tile's key; null where nothing is focused.</param>
    public readonly record struct PageFocus(bool InActions, string? Key);

    /// <summary>What a page's section shows: rows, tiles, or text beside a picture.</summary>
    public abstract record PageContent
    {
        private PageContent() { }

        /// <summary>Rows, with an optional sentence above them and buttons under them on the desktop.</summary>
        /// <param name="Items">The rows in order.</param>
        /// <param name="Sentence">Words above the rows, such as a question's; null for none.</param>
        /// <param name="Buttons">The desktop's buttons under the rows, such as Save, as keys and words; the big screen names them in the help bar.</param>
        public sealed record Rows(IReadOnlyList<PageRow> Items, string? Sentence = null, IReadOnlyList<(string Key, string Text)>? Buttons = null) : PageContent;

        /// <summary>Tiles of one size in design units, a strip on the big screen and a wrapping grid on the desktop.</summary>
        /// <param name="Items">The tiles in order.</param>
        /// <param name="Width">A tile's width on the big screen.</param>
        /// <param name="Height">A tile's height on the big screen.</param>
        public sealed record Tiles(IReadOnlyList<PageTile> Items, double Width, double Height) : PageContent;

        /// <summary>Words beside a picture, such as a game's description and a screenshot.</summary>
        /// <param name="Words">The text, wrapped.</param>
        /// <param name="Picture">The picture beside it; null for none.</param>
        /// <param name="Caption">Words drawn where the picture is missing; null for none.</param>
        public sealed record Text(string Words, IImage? Picture = null, string? Caption = null) : PageContent;
    }
}
