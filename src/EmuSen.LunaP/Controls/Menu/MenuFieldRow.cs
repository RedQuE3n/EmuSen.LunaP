using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // A big-screen menu row whose value is a control of its own, such as a star rating or a date, placed at the row's right and highlighted with it - see docs/LunaP.md §182.3.
    /// <summary>A menu row that hosts a control in place of a value: the label at the left, the control at the right, the rule beneath, and the full-width bar while the focus is anywhere in the row.</summary>
    public class MenuFieldRow : Control
    {
        public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<MenuFieldRow, string?>(nameof(Label));
        public static readonly StyledProperty<Control?> FieldProperty = AvaloniaProperty.Register<MenuFieldRow, Control?>(nameof(Field));

        private readonly MenuRow _row = new() { IsHitTestVisible = false };

        static MenuFieldRow()
        {
            AffectsMeasure<MenuFieldRow>(FieldProperty);
        }

        /// <summary>An empty row; set Label and Field.</summary>
        public MenuFieldRow()
        {
            VisualChildren.Add(_row);
            LogicalChildren.Add(_row);
        }

        /// <summary>The words at the row's left, cased as a MenuRow cases them.</summary>
        public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

        /// <summary>The control drawn at the row's right at its own desired size, centred on the bar; it keeps its own input and focus.</summary>
        public Control? Field { get => GetValue(FieldProperty); set => SetValue(FieldProperty, value); }

        /// <summary>The row drawn beneath the field, whose colours and sizes a host may set as on any MenuRow.</summary>
        public MenuRow Row => _row;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == LabelProperty) _row.Label = Label;
            else if (change.Property == IsKeyboardFocusWithinProperty) _row.IsHighlighted = IsKeyboardFocusWithin;
            else if (change.Property == FieldProperty)
            {
                if (change.OldValue is Control old)
                {
                    VisualChildren.Remove(old);
                    LogicalChildren.Remove(old);
                }
                if (change.NewValue is Control added)
                {
                    VisualChildren.Add(added);
                    LogicalChildren.Add(added);
                }
            }
        }

        private double Unit => MenuPanel.GetScale(this) is var u && double.IsFinite(u) && u > 0 ? u : 1;

        protected override Size MeasureOverride(Size availableSize)
        {
            _row.Measure(availableSize);
            Field?.Measure(Size.Infinity);
            return new Size(Field?.DesiredSize.Width ?? 0, _row.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Size field = Field?.DesiredSize ?? default;
            _row.SetValue(MenuRow.TrailingWidthProperty, field.Width);
            _row.Arrange(new Rect(finalSize));
            Rect bar = _row.Layout(finalSize).Bar;
            Field?.Arrange(new Rect(Math.Max(0, finalSize.Width - 8 * Unit - field.Width), Math.Max(0, (bar.Height - field.Height) / 2), field.Width, field.Height));
            return finalSize;
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.ListItem, () => Label ?? "");
    }
}
