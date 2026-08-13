using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EmuSen.LunaP.Fluent
{
    // Every name here is the XAML attribute it sets, so the two ways of building a window stay one vocabulary - see docs/LunaP.md §9.
    /// <summary>Fluent setters named after the XAML attributes they set, so both ways of building a window share one vocabulary.</summary>
    public static class LayoutExtensions
    {
        /// <summary>Sets the same margin on all four sides.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="uniform">The margin for every side.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Margin<T>(this T control, double uniform) where T : Control
        {
            control.Margin = new Thickness(uniform);
            return control;
        }

        /// <summary>Sets left/right and top/bottom margins.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="horizontal">The left and right margin.</param>
        /// <param name="vertical">The top and bottom margin.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Margin<T>(this T control, double horizontal, double vertical) where T : Control
        {
            control.Margin = new Thickness(horizontal, vertical);
            return control;
        }

        /// <summary>Sets each margin separately.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="left">The left margin.</param>
        /// <param name="top">The top margin.</param>
        /// <param name="right">The right margin.</param>
        /// <param name="bottom">The bottom margin.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Margin<T>(this T control, double left, double top, double right, double bottom) where T : Control
        {
            control.Margin = new Thickness(left, top, right, bottom);
            return control;
        }

        /// <summary>Sets the gap a stack panel leaves between its children.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="panel">The stack panel to set it on.</param>
        /// <param name="spacing">The gap between children. Not added before the first or after the last.</param>
        /// <returns>The same panel, so calls can be chained. Nothing is copied.</returns>
        public static T Spacing<T>(this T panel, double spacing) where T : StackPanel
        {
            panel.Spacing = spacing;
            return panel;
        }

        /// <summary>Fixes the control's width.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="width">The width. This is exact, not a minimum: the control will not grow to fill more.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Width<T>(this T control, double width) where T : Control
        {
            control.Width = width;
            return control;
        }

        /// <summary>Fixes the control's height.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="height">The height. This is exact, not a minimum.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Height<T>(this T control, double height) where T : Control
        {
            control.Height = height;
            return control;
        }

        /// <summary>Caps the control's height, leaving it free to be shorter.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="height">The tallest it may become.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T MaxHeight<T>(this T control, double height) where T : Control
        {
            control.MaxHeight = height;
            return control;
        }

        /// <summary>Sets a floor under both dimensions, leaving the control free to grow.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="width">The narrowest it may become.</param>
        /// <param name="height">The shortest it may become.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T MinSize<T>(this T control, double width, double height) where T : Control
        {
            control.MinWidth = width;
            control.MinHeight = height;
            return control;
        }

        /// <summary>Stretches the control across its parent horizontally.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Grow<T>(this T control) where T : Control
        {
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            return control;
        }

        /// <summary>Aligns the control to the left of the space it is given.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Left<T>(this T control) where T : Control
        {
            control.HorizontalAlignment = HorizontalAlignment.Left;
            return control;
        }

        /// <summary>Aligns the control to the right of the space it is given.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Right<T>(this T control) where T : Control
        {
            control.HorizontalAlignment = HorizontalAlignment.Right;
            return control;
        }

        // Vertical centring, which is what almost every use of it in the frontends means.
        /// <summary>Centres the control VERTICALLY, which is what almost every use of it means. For horizontal centring set HorizontalAlignment directly.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Center<T>(this T control) where T : Control
        {
            control.VerticalAlignment = VerticalAlignment.Center;
            return control;
        }

        /// <summary>Sets which edge of a DockPanel the control takes.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="side">The edge to dock against. Has no effect unless the parent is a DockPanel.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Dock<T>(this T control, Avalonia.Controls.Dock side) where T : Control
        {
            DockPanel.SetDock(control, side);
            return control;
        }

        /// <summary>Places the control in a grid column.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="column">The zero-based column.</param>
        /// <param name="span">How many columns to cover.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T AtColumn<T>(this T control, int column, int span = 1) where T : Control
        {
            Grid.SetColumn(control, column);
            Grid.SetColumnSpan(control, span);
            return control;
        }

        /// <summary>Places the control in a grid row.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="row">The zero-based row.</param>
        /// <param name="span">How many rows to cover.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T AtRow<T>(this T control, int row, int span = 1) where T : Control
        {
            Grid.SetRow(control, row);
            Grid.SetRowSpan(control, span);
            return control;
        }

        // Must be set before the control joins a visual tree, which is what building it fluently already guarantees.
        /// <summary>Names the control so a test or a lookup can find it.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="name">The name. Avalonia refuses to change a name once the control has joined a visual tree, which building it fluently already avoids.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Name<T>(this T control, string name) where T : Control
        {
            control.Name = name;
            return control;
        }

        /// <summary>Shows or hides the control. A hidden control takes no space, rather than being left blank.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to set it on.</param>
        /// <param name="visible">True to show it.</param>
        /// <returns>The same control, so calls can be chained. Nothing is copied.</returns>
        public static T Visible<T>(this T control, bool visible) where T : Control
        {
            control.IsVisible = visible;
            return control;
        }

        /// <summary>Sets the text bold.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="text">The text block to set it on.</param>
        /// <returns>The same text block, so calls can be chained. Nothing is copied.</returns>
        public static T Bold<T>(this T text) where T : TextBlock
        {
            text.FontWeight = Avalonia.Media.FontWeight.Bold;
            return text;
        }

        /// <summary>Sets the text size, overriding whatever it inherits.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="text">The text block to set it on.</param>
        /// <param name="size">The point size.</param>
        /// <returns>The same text block, so calls can be chained. Nothing is copied.</returns>
        public static T FontSize<T>(this T text, double size) where T : TextBlock
        {
            text.FontSize = size;
            return text;
        }

        /// <summary>Lets the text wrap onto more lines instead of being clipped.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="text">The text block to set it on.</param>
        /// <returns>The same text block, so calls can be chained. Nothing is copied.</returns>
        public static T Wrap<T>(this T text) where T : TextBlock
        {
            text.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            return text;
        }
    }
}
