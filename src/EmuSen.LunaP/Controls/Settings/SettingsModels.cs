using System;
using Avalonia.Controls;
using System.Collections.Generic;
using Avalonia.Media;

namespace EmuSen.LunaP.Controls
{
    /// <summary>How a line of a settings pane is coloured.</summary>
    public enum SettingsTone
    {
        /// <summary>The body text.</summary>
        Text,
        /// <summary>A quieter line, such as how a row is used.</summary>
        Muted,
        /// <summary>Its mark in the success colour, as something found.</summary>
        Success,
        /// <summary>Its mark in the attention colour, as something missing.</summary>
        Attention,
    }

    /// <summary>One line of a settings pane, wrapped to the pane's width.</summary>
    /// <param name="Text">The words the line says, wrapped to the pane's width.</param>
    /// <param name="Tone">How it is coloured.</param>
    public sealed record SettingsLine(string Text, SettingsTone Tone = SettingsTone.Text)
    {
        /// <summary>A short mark drawn before the words in the tone's colour, such as a tick; null for none.</summary>
        public string? Mark { get; init; }

        /// <summary>The line's size in units, for a sample of type; null for the pane's body size.</summary>
        public double? Size { get; init; }
    }

    /// <summary>A picture at the top of a settings pane.</summary>
    public abstract record SettingsPreview;

    /// <summary>A picture fitted in a 4:3 box, or the generated stand-in when there is none, with its caption over it.</summary>
    /// <param name="Picture">The picture, such as a frame of the game the setting changes; null for the stand-in.</param>
    /// <param name="Caption">The words over it; empty for none.</param>
    public sealed record SettingsPicture(IImage? Picture, string Caption) : SettingsPreview;

    /// <summary>A fraction drawn as a bar, such as a volume.</summary>
    /// <param name="Fraction">From 0 to 1.</param>
    public sealed record SettingsMeter(double Fraction) : SettingsPreview;

    /// <summary>The palette's two variants side by side, each named on its own surface.</summary>
    /// <param name="DarkName">The dark variant's name.</param>
    /// <param name="LightName">The light variant's name.</param>
    public sealed record SettingsVariants(string DarkName, string LightName) : SettingsPreview;

    /// <summary>What a settings screen's pane shows for the focused row: a preview where the row has a picture, then its lines.</summary>
    /// <param name="Lines">The lines, in order.</param>
    public sealed record SettingsPane(IReadOnlyList<SettingsLine> Lines)
    {
        /// <summary>The picture over the lines; null for none.</summary>
        public SettingsPreview? Preview { get; init; }
    }

    /// <summary>A page of content in place of a settings screen's rows: the host's control, what it has focused, an optional strip of tabs over it and a sentence under it - see docs/LunaP.md §213.7.</summary>
    /// <param name="Focus">The key of what the content has focused, asked of the pane and the help bar as a row's key is.</param>
    /// <param name="Body">The host's control, drawn in the box the layout gives it.</param>
    public sealed record SettingsContent(string Focus, Control Body)
    {
        /// <summary>The words of a strip over the body; empty for none.</summary>
        public IReadOnlyList<string> Tabs { get; init; } = Array.Empty<string>();

        /// <summary>The tab marked chosen.</summary>
        public int Chosen { get; init; }

        /// <summary>A sentence under the body; null for none.</summary>
        public string? Caption { get; init; }
    }

}
