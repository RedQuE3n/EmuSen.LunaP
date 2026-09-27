using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace EmuSen.LunaP.Controls
{
    // Stock controls drawn as big-screen menu rows while keeping their own behaviour: a Button is clicked, a ComboBox steps and drops down, a ListBox selects - see docs/LunaP.md §181.3.
    /// <summary>Gives a Button, a ComboBox or a ListBox's rows the look of a MenuRow without changing what they do, and carries the row's value and kind on the control.</summary>
    public static class MenuRows
    {
        /// <summary>The row's label where it is not the control's content, such as a ListBox row's or a ComboBox's.</summary>
        public static readonly AttachedProperty<string?> LabelProperty = AvaloniaProperty.RegisterAttached<Control, string?>("Label", typeof(MenuRows));

        /// <summary>The row's value, shown at its right.</summary>
        public static readonly AttachedProperty<string?> ValueProperty = AvaloniaProperty.RegisterAttached<Control, string?>("Value", typeof(MenuRows));

        /// <summary>What the row offers beside its label.</summary>
        public static readonly AttachedProperty<MenuRowKind> KindProperty = AvaloniaProperty.RegisterAttached<Control, MenuRowKind>("Kind", typeof(MenuRows));

        /// <summary>Whether a switch row is on.</summary>
        public static readonly AttachedProperty<bool> IsOnProperty = AvaloniaProperty.RegisterAttached<Control, bool>("IsOn", typeof(MenuRows));

        /// <summary>Reads a control's row label.</summary>
        /// <param name="control">The control.</param>
        /// <returns>The label, or null to use the content.</returns>
        public static string? GetLabel(Control control) => control.GetValue(LabelProperty);

        /// <summary>Sets a control's row label.</summary>
        /// <param name="control">The control.</param>
        /// <param name="value">The label, or null to use the content.</param>
        public static void SetLabel(Control control, string? value) => control.SetValue(LabelProperty, value);

        /// <summary>Reads a control's row value.</summary>
        /// <param name="control">The control.</param>
        /// <returns>The value, or null for none.</returns>
        public static string? GetValue(Control control) => control.GetValue(ValueProperty);

        /// <summary>Sets a control's row value.</summary>
        /// <param name="control">The control.</param>
        /// <param name="value">The value, or null for none.</param>
        public static void SetValue(Control control, string? value) => control.SetValue(ValueProperty, value);

        /// <summary>Reads a control's row kind.</summary>
        /// <param name="control">The control.</param>
        /// <returns>The kind, Action unless set.</returns>
        public static MenuRowKind GetKind(Control control) => control.GetValue(KindProperty);

        /// <summary>Sets a control's row kind.</summary>
        /// <param name="control">The control.</param>
        /// <param name="value">What the row offers beside its label.</param>
        public static void SetKind(Control control, MenuRowKind value) => control.SetValue(KindProperty, value);

        /// <summary>Reads whether a control's switch row is on.</summary>
        /// <param name="control">The control.</param>
        /// <returns>The state, off unless set.</returns>
        public static bool GetIsOn(Control control) => control.GetValue(IsOnProperty);

        /// <summary>Sets whether a control's switch row is on.</summary>
        /// <param name="control">The control.</param>
        /// <param name="value">True for a switch drawn on.</param>
        public static void SetIsOn(Control control, bool value) => control.SetValue(IsOnProperty, value);

        /// <summary>Draws a button as a row: its content is the label, and the bar follows the keyboard focus.</summary>
        /// <typeparam name="T">The button's type.</typeparam>
        /// <param name="button">The button; it is still clicked, focused and named as before.</param>
        /// <param name="kind">What the row offers beside its label.</param>
        /// <param name="value">A value shown at its right, or null.</param>
        /// <returns>The same button.</returns>
        public static T Apply<T>(T button, MenuRowKind kind = MenuRowKind.Action, string? value = null) where T : Button
        {
            SetKind(button, kind);
            SetValue(button, value);
            button.Template = new FuncControlTemplate<Button>((b, _) => Row(b, Text(b, ContentControl.ContentProperty), b.GetObservable(InputElement.IsFocusedProperty)));
            Plain(button);
            return button;
        }

        /// <summary>Draws a button as a menu's outlined push button, such as Apply or Cancel under the rows, in the menu's typeface; filled while it has the focus.</summary>
        /// <typeparam name="T">The button's type.</typeparam>
        /// <param name="button">The button; it is still clicked, focused and named as before.</param>
        /// <returns>The same button.</returns>
        public static T ApplyButton<T>(T button) where T : Button
        {
            button.Template = new FuncControlTemplate<Button>((b, _) =>
            {
                var text = new FontText { Wrap = false, LineSpacing = 1.15, LetterCase = Media.LetterCase.Upper, TextAlignment = TextAlignment.Center, TextVerticalAlignment = VerticalAlignment.Center };
                text.Bind(FontText.TextProperty, Text(b, ContentControl.ContentProperty));
                text.Bind(FontText.FontPathProperty, b.GetObservable(MenuPanel.FontPathProperty));
                text.Bind(FontText.FontSizeProperty, b.GetObservable(MenuPanel.ScaleProperty, u => 34 * u));
                text.Bind(FontText.ForegroundProperty, b.GetObservable(InputElement.IsFocusedProperty, f => (IBrush)new SolidColorBrush(f ? PushTextFocused : PushText)));
                var frame = new Border { Child = text, BorderBrush = new SolidColorBrush(PushOutline) };
                frame.Bind(Border.BorderThicknessProperty, b.GetObservable(MenuPanel.ScaleProperty, u => new Thickness(Math.Max(1, Math.Round(u)))));
                frame.Bind(Border.CornerRadiusProperty, b.GetObservable(MenuPanel.ScaleProperty, u => new CornerRadius(3 * u)));
                frame.Bind(Decorator.PaddingProperty, b.GetObservable(MenuPanel.ScaleProperty, u => new Thickness(12 * u, 2 * u)));
                frame.Bind(Border.BackgroundProperty, b.GetObservable(InputElement.IsFocusedProperty, f => (IBrush?)(f ? new SolidColorBrush(PushFill) : null)));
                return frame;
            });
            Plain(button);
            button.HorizontalAlignment = HorizontalAlignment.Center;
            return button;
        }

        // A push button's colours: a grey outline and text, filled near black with light text while focused, as a row's bar is.
        private static readonly Color PushOutline = Color.FromRgb(0x55, 0x55, 0x5A), PushText = Color.FromRgb(0x9C, 0x9C, 0xA0), PushTextFocused = Color.FromRgb(0xEE, 0xEE, 0xF0), PushFill = Color.FromRgb(0x05, 0x05, 0x07);

        /// <summary>Draws a dropdown as an option row, its selection between arrows; Left and Right still step it and opening it still drops the list down.</summary>
        /// <param name="choice">The dropdown.</param>
        /// <param name="label">The row's label.</param>
        /// <returns>The same dropdown.</returns>
        public static ComboBox Apply(ComboBox choice, string label)
        {
            SetKind(choice, MenuRowKind.Option);
            SetLabel(choice, label);
            choice.Template = new FuncControlTemplate<ComboBox>((c, scope) =>
            {
                MenuRow row = Row(c, null, c.GetObservable(InputElement.IsFocusedProperty));
                row.Bind(MenuRow.ValueProperty, Text(c, SelectingItemsControl.SelectedItemProperty));
                var items = new ItemsPresenter { Name = "PART_ItemsPresenter" };
                var popup = new Popup
                {
                    Name = "PART_Popup",
                    Placement = PlacementMode.Bottom,
                    PlacementTarget = row,
                    IsLightDismissEnabled = true,
                    Child = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1E)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x37)),
                        BorderThickness = new Thickness(1),
                        Child = new ScrollViewer { Content = items, MaxHeight = 360 },
                    },
                };
                popup.Bind(Popup.IsOpenProperty, c.GetObservable(ComboBox.IsDropDownOpenProperty));
                popup.IsOpen = c.IsDropDownOpen;
                popup.Closed += (_, _) => c.IsDropDownOpen = false;
                popup.Bind(Layoutable.MinWidthProperty, row.GetObservable(Visual.BoundsProperty, b => b.Width));
                items.RegisterInNameScope(scope);
                popup.RegisterInNameScope(scope);
                return new Panel { Children = { row, popup } };
            });
            Plain(choice);
            return choice;
        }

        /// <summary>Draws a list's rows as menu rows; each row's label, value and kind are read from its container, set when the container is prepared.</summary>
        /// <param name="list">The list; its selection is the highlighted row.</param>
        /// <returns>The same list.</returns>
        public static T Apply<T>(T list) where T : ListBox
        {
            list.ItemContainerTheme = ItemTheme;
            list.Background = Brushes.Transparent;
            list.BorderThickness = default;
            list.Padding = default;
            ScrollViewer.SetVerticalScrollBarVisibility(list, Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden);
            return list;
        }

        /// <summary>Takes the row look off a list, giving its rows back their theme's own.</summary>
        /// <param name="list">A list given the look by Apply.</param>
        public static void Remove(ListBox list)
        {
            list.ClearValue(ItemsControl.ItemContainerThemeProperty);
            list.ClearValue(TemplatedControl.BackgroundProperty);
            list.ClearValue(TemplatedControl.BorderThicknessProperty);
            list.ClearValue(TemplatedControl.PaddingProperty);
            list.ClearValue(ScrollViewer.VerticalScrollBarVisibilityProperty);
        }

        private static ControlTheme? _itemTheme;

        // One theme for every list's rows: the template is a MenuRow reading the container's attached label, value and kind, highlighted while selected.
        private static ControlTheme ItemTheme => _itemTheme ??= new ControlTheme(typeof(ListBoxItem))
        {
            Setters =
            {
                new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<ListBoxItem>((item, _) => Row(item, Text(item, ContentControl.ContentProperty), item.GetObservable(ListBoxItem.IsSelectedProperty)))),
                new Setter(TemplatedControl.PaddingProperty, default(Thickness)),
                new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(Control.FocusAdornerProperty, null),
            },
        };

        // The row a template draws: the label from the attached one, else the content; the value, kind and switch from the control.
        private static MenuRow Row(Control owner, IObservable<string?>? content, IObservable<bool> highlighted)
        {
            var row = new MenuRow();
            if (content is not null) row.Bind(MenuRow.LabelProperty, content);
            row.Bind(MenuRow.LabelOverrideProperty, owner.GetObservable(LabelProperty));
            if (content is not null) row.Bind(MenuRow.ValueProperty, owner.GetObservable(ValueProperty));
            row.Bind(MenuRow.KindProperty, owner.GetObservable(KindProperty));
            row.Bind(MenuRow.IsOnProperty, owner.GetObservable(IsOnProperty));
            row.Bind(MenuRow.IsHighlightedProperty, highlighted);
            return row;
        }

        // A property's value as the text a row shows.
        private static IObservable<string?> Text(AvaloniaObject owner, AvaloniaProperty property) => owner.GetObservable(property, v => v?.ToString());

        // No chrome of the control's own: the row draws everything, and the focus is the bar rather than a rectangle.
        private static void Plain(TemplatedControl control)
        {
            control.Background = Brushes.Transparent;
            control.BorderThickness = default;
            control.Padding = default;
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            control.FocusAdorner = null;
        }
    }
}
