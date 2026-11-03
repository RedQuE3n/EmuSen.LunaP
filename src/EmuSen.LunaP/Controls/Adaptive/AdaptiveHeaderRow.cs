using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>A header's row as a control of its own: the picture, the name and subtitle, the facts with a warning in Attention, and on the desktop the row's action - see docs/LunaP.md §215.</summary>
    public sealed class AdaptiveHeaderRow : Border
    {
        /// <summary>The name of the facts' line, for tests and audits.</summary>
        public const string FactsName = "AdaptiveHeaderFacts";

        /// <summary>The name of the row's action button on the desktop.</summary>
        public const string ActionName = "AdaptiveHeaderAction";

        /// <summary>The mark a fact that needs the person begins with.</summary>
        public const string Warning = "⚠";

        private AdaptiveHeader? _header;
        private AdaptiveForm _form;
        private LunaAction? _action;

        /// <summary>The header drawn; null draws nothing.</summary>
        public AdaptiveHeader? Header
        {
            get => _header;
            set { _header = value; Build(); }
        }

        /// <summary>The form the row is drawn in: Desktop or BigScreen.</summary>
        public AdaptiveForm Form
        {
            get => _form;
            set { _form = value; Build(); }
        }

        /// <summary>The chosen row's one action, drawn at the right on the desktop only; null for none.</summary>
        public LunaAction? Action
        {
            get => _action;
            set { _action = value; Build(); }
        }

        /// <summary>The facts as one line, each fact its own text: one beginning with ⚠ in LunaAttention, the rest and the separators in LunaMuted.</summary>
        /// <param name="facts">The facts joined by " · ".</param>
        /// <param name="size">The font size.</param>
        /// <returns>A horizontal panel of one text per fact and separator, named FactsName.</returns>
        public static StackPanel FactsLine(string facts, double size)
        {
            var line = new StackPanel { Name = FactsName, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            string[] parts = facts.Split(" · ");
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) line.Children.Add(Fact(" · ", size, "LunaMuted"));
                line.Children.Add(Fact(parts[i], size, parts[i].StartsWith(Warning, StringComparison.Ordinal) ? "LunaAttention" : "LunaMuted"));
            }
            AutomationProperties.SetName(line, facts);
            return line;
        }

        private static TextBlock Fact(string text, double size, string tone)
        {
            var fact = new TextBlock { Text = text, FontFamily = Media.LunaFonts.Body, FontSize = size, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap, Classes = { tone == "LunaMuted" ? "muted" : "attention" } };
            fact.Bind(TextBlock.ForegroundProperty, fact.GetResourceObservable(tone));
            return fact;
        }

        private void Build()
        {
            AdaptiveHeader? h = _header;
            if (h is null)
            {
                Child = null;
                return;
            }
            bool big = _form == AdaptiveForm.BigScreen;
            LunaTypeScale type = LunaTokens.Type(big ? LunaForm.BigScreen : LunaForm.Desktop);
            // The big screen's header row is 48 high, its title centred in it, as the design's grid draws it (docs/LunaP.md §211.3).
            var row = new DockPanel { LastChildFill = true, Margin = big ? new Thickness(16, 12, 16, 0) : new Thickness(8, 6, 8, 2), MinHeight = big ? 48 : 0 };
            if (h.Picture is { } picture)
            {
                var image = new Image { Source = picture, Height = big ? 64 : 32, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, big ? 16 : 8, 0), VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(image, Dock.Left);
                row.Children.Add(image);
            }
            if (!big && _action is { } action)
            {
                var button = new ActionButton(action) { Name = ActionName, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                button.Bind(Button.BackgroundProperty, button.GetResourceObservable("LunaAccent"));
                button.Bind(Button.ForegroundProperty, button.GetResourceObservable("LunaOnAccent"));
                DockPanel.SetDock(button, Dock.Right);
                row.Children.Add(button);
            }
            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock { Text = h.Title, FontFamily = Media.LunaFonts.Condensed, FontSize = type.Title, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            if (h.Facts is { Length: > 0 } facts)
            {
                StackPanel line = FactsLine(facts, type.Caption);
                line.Margin = new Thickness(12, 0, 0, 0);
                if (big)
                {
                    DockPanel.SetDock(line, Dock.Right);
                    row.Children.Add(line);
                    words.Children.Add(title);
                }
                else
                {
                    // The desktop sets the facts after the name, on its baseline's line (docs/LunaP.md §215).
                    var named = new DockPanel { LastChildFill = true };
                    DockPanel.SetDock(title, Dock.Left);
                    named.Children.Add(title);
                    named.Children.Add(line);
                    words.Children.Add(named);
                }
            }
            else words.Children.Add(title);
            if (h.Subtitle is { Length: > 0 } sub)
            {
                var subtitle = new TextBlock { Text = sub, FontFamily = Media.LunaFonts.Body, FontSize = type.Caption, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
                subtitle.Bind(TextBlock.ForegroundProperty, subtitle.GetResourceObservable("LunaMuted"));
                words.Children.Add(subtitle);
            }
            row.Children.Add(words);
            AutomationProperties.SetName(this, h.Title);
            Child = row;
        }
    }
}
