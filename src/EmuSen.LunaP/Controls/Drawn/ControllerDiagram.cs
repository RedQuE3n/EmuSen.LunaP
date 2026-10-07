using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>Which controller a ControllerDiagram draws.</summary>
    public enum ControllerLayout
    {
        /// <summary>A modern pad with two sticks, four face buttons, two bumpers and two triggers, as the RetroPad names them.</summary>
        Gamepad,
        /// <summary>A flat oblong pad with a cross, Select, Start, B and A.</summary>
        Nes,
        /// <summary>A pad with round grips, a cross, Select, Start, four face buttons and two shoulders.</summary>
        Snes,
        /// <summary>A three-grip pad with a cross, a stick, Start, A, B, four C buttons, L, R and Z.</summary>
        Nintendo64,
        /// <summary>A handheld whose controls are its own face: a cross, A, B, Select and Start.</summary>
        GameBoy,
        /// <summary>A wide two-lobed pad with a cross on a round plate, Start, and A, B and C in a rising row: the Genesis's three-button pad.</summary>
        Genesis,
        /// <summary>The same pad with X, Y and Z in a second row above A, B and C, and Mode on its right shoulder: the Genesis's six-button pad.</summary>
        GenesisSixButton,
    }

    /// <summary>The side of a ControllerDiagram a region's label stands on.</summary>
    public enum DiagramSide
    {
        /// <summary>In the column left of the drawing.</summary>
        Left,
        /// <summary>In the column right of the drawing.</summary>
        Right,
        /// <summary>In the row above the drawing.</summary>
        Top,
        /// <summary>In the row below the drawing.</summary>
        Bottom,
    }

    /// <summary>What one region of a diagram is bound to: a keyboard key and a pad button, as text the host chose.</summary>
    /// <param name="Key">The key's name, or null when none is bound.</param>
    /// <param name="Pad">The pad button's name, or null when none is bound.</param>
    public readonly record struct DiagramBinding(string? Key, string? Pad);

    /// <summary>One button, direction or trigger of a drawn controller.</summary>
    public sealed class DiagramRegion
    {
        internal DiagramRegion(RegionArt art) => Art = art;

        internal RegionArt Art { get; }

        /// <summary>The name a host binds it by, such as "A", "Up", "CLeft" or "StickUp".</summary>
        public string Id => Art.Id;

        /// <summary>What its label and a screen reader call it, such as "C Left".</summary>
        public string Name => Art.Name;

        /// <summary>Where its label stands.</summary>
        public DiagramSide Side => Art.Side;

        /// <summary>The stick it is a direction of, or null for a button.</summary>
        public string? Stick => Art.Stick;

        /// <summary>Whether it is a trigger that shows how far it is pulled.</summary>
        public bool IsTrigger => Art.IsTrigger;
    }

    /// <summary>The region a diagram's event is about.</summary>
    public sealed class DiagramRegionEventArgs : EventArgs
    {
        /// <summary>Makes the arguments for one region.</summary>
        /// <param name="region">The region's id.</param>
        public DiagramRegionEventArgs(string region) => Region = region;

        /// <summary>The region's id.</summary>
        public string Region { get; }
    }

    /// <summary>A large drawing of a controller whose buttons are named regions: each labelled with its bindings, lit while pressed, and chosen by a click or by the pad.</summary>
    public class ControllerDiagram : Control, ICustomHitTest
    {
        public static readonly StyledProperty<ControllerLayout> LayoutProperty = AvaloniaProperty.Register<ControllerDiagram, ControllerLayout>(nameof(Layout), ControllerLayout.Snes);
        public static readonly StyledProperty<string?> SelectedRegionProperty = AvaloniaProperty.Register<ControllerDiagram, string?>(nameof(SelectedRegion));
        public static readonly StyledProperty<bool> IsInteractiveProperty = AvaloniaProperty.Register<ControllerDiagram, bool>(nameof(IsInteractive), true);
        public static readonly StyledProperty<bool> ShowsKeysProperty = AvaloniaProperty.Register<ControllerDiagram, bool>(nameof(ShowsKeys), true);
        public static readonly StyledProperty<bool> ShowsLabelsProperty = AvaloniaProperty.Register<ControllerDiagram, bool>(nameof(ShowsLabels), true);
        public static readonly StyledProperty<double> StickRingProperty = AvaloniaProperty.Register<ControllerDiagram, double>(nameof(StickRing));
        public static readonly StyledProperty<bool> CompactLabelsProperty = AvaloniaProperty.Register<ControllerDiagram, bool>(nameof(CompactLabels));

        private DiagramArt _art = DiagramArt.For(ControllerLayout.Snes);
        private List<DiagramRegion> _regions = new();
        private readonly Dictionary<string, DiagramLabel> _labels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DiagramBinding> _bindings = new(StringComparer.Ordinal);
        private readonly HashSet<string> _pressed = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _captions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector> _sticks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _triggers = new(StringComparer.Ordinal);
        private readonly List<(Point From, Point? Pass, Point Elbow, Point To, string Region)> _leaders = new();
        private readonly HashSet<DiagramSide> _split = new();
        private Matrix _toControl = Matrix.Identity;
        private double _scale = 1;
        private double _unit = 1;

        static ControllerDiagram()
        {
            AffectsRender<ControllerDiagram>(SelectedRegionProperty, StickRingProperty);
            AffectsMeasure<ControllerDiagram>(ShowsKeysProperty, ShowsLabelsProperty, CompactLabelsProperty);
            AffectsMeasure<ControllerDiagram>(TextElement.FontSizeProperty, TextElement.FontFamilyProperty);
            FocusableProperty.OverrideDefaultValue<ControllerDiagram>(false);
        }

        /// <summary>Makes a diagram of the Super NES pad, with no bindings and nothing pressed.</summary>
        public ControllerDiagram()
        {
            ClipToBounds = true;
            Rebuild();
            AddHandler(KeyDownEvent, OnArrowKey, RoutingStrategies.Bubble);
        }

        /// <summary>Which controller is drawn. Snes by default.</summary>
        public ControllerLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

        /// <summary>The region whose label has the focus, drawn with a ring, or null. Null by default.</summary>
        public string? SelectedRegion { get => GetValue(SelectedRegionProperty); set => SetValue(SelectedRegionProperty, value); }

        /// <summary>Whether the labels take the focus and clicks; off, the diagram only shows. True by default.</summary>
        public bool IsInteractive { get => GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }

        /// <summary>Whether a label shows a key above its pad button; off, it shows the pad button alone. True by default.</summary>
        public bool ShowsKeys { get => GetValue(ShowsKeysProperty); set => SetValue(ShowsKeysProperty, value); }

        /// <summary>Whether each region has its label and line beside the drawing; off, the drawing alone fills the control. True by default.</summary>
        public bool ShowsLabels { get => GetValue(ShowsLabelsProperty); set => SetValue(ShowsLabelsProperty, value); }

        /// <summary>A ring drawn over each stick at this share of its travel, such as a dead zone, or none at 0. 0 by default.</summary>
        public double StickRing { get => GetValue(StickRingProperty); set => SetValue(StickRingProperty, value); }

        /// <summary>Whether labels are set closer, with less padding, tighter lines and shorter leaders, so their words stay large where the height is short, as on a big-screen menu. False by default.</summary>
        public bool CompactLabels { get => GetValue(CompactLabelsProperty); set => SetValue(CompactLabelsProperty, value); }

        /// <summary>The size the labels' key and pad lines are drawn at, in the diagram's own units, after the labels are fitted to the space; 0 before a layout.</summary>
        public double LabelTextSize => ShowsLabels && _labels.Values.FirstOrDefault() is { } label && IsMeasureValid ? label.LineSize : 0;

        // The gap a row of labels keeps for its leaders, and the space between two labels, in label units (§196.10).
        private (double Gap, double Space) Spacing => CompactLabels ? (26, 6) : (44, 8);

        /// <summary>The side columns whose labels stand in two staggered columns, as a column of more than four does where that lets its labels grow and the drawing keeps its size; empty before a layout.</summary>
        public IReadOnlyCollection<DiagramSide> SplitSides => _split;

        // A side column of more labels than this may take a second column (§198.11).
        private const int SplitAbove = 4;

        /// <summary>Prints other words on a region, such as a connected pad's own letter, in place of the drawing's.</summary>
        /// <param name="region">The region's id.</param>
        /// <param name="caption">The words, or null to print the drawing's own.</param>
        public void SetCaption(string region, string? caption)
        {
            if (caption is null) _captions.Remove(region); else _captions[region] = caption;
            InvalidateVisual();
        }

        /// <summary>Every region of the drawn controller, in drawing order.</summary>
        public IReadOnlyList<DiagramRegion> Regions => _regions;

        /// <summary>The ids of the drawn controller's sticks.</summary>
        public IReadOnlyList<string> Sticks => _art.Sticks.Select(s => s.Id).ToList();

        /// <summary>The ids of the regions drawn as pressed.</summary>
        public IReadOnlyCollection<string> Pressed => _pressed;

        /// <summary>Raised when a region is chosen: clicked on the drawing, or its label clicked or pressed.</summary>
        public event EventHandler<DiagramRegionEventArgs>? RegionInvoked;

        /// <summary>Sets the text a region's label shows for its bindings.</summary>
        /// <param name="region">The region's id.</param>
        /// <param name="key">The key bound to it, or null for none.</param>
        /// <param name="pad">The pad button bound to it, or null for none.</param>
        public void SetBinding(string region, string? key, string? pad)
        {
            _bindings[region] = new DiagramBinding(key, pad);
            if (_labels.TryGetValue(region, out DiagramLabel? label)) label.Binding = _bindings[region];
        }

        /// <summary>The text a region's label shows for its bindings.</summary>
        /// <param name="region">The region's id.</param>
        /// <returns>The binding, both halves null when none was set.</returns>
        public DiagramBinding BindingOf(string region) => _bindings.TryGetValue(region, out DiagramBinding b) ? b : default;

        /// <summary>Draws a region pressed or not.</summary>
        /// <param name="region">The region's id.</param>
        /// <param name="pressed">Whether it is held.</param>
        public void SetPressed(string region, bool pressed)
        {
            if (pressed ? !_pressed.Add(region) : !_pressed.Remove(region)) return;
            if (_labels.TryGetValue(region, out DiagramLabel? label)) label.IsLit = pressed;
            InvalidateVisual();
        }

        /// <summary>Whether a region is drawn as pressed.</summary>
        /// <param name="region">The region's id.</param>
        /// <returns>True while it is drawn pressed.</returns>
        public bool IsPressed(string region) => _pressed.Contains(region);

        /// <summary>Moves a stick's knob, each axis from -1 to 1 with right and down positive.</summary>
        /// <param name="stick">The stick's id.</param>
        /// <param name="x">Across, -1 left to 1 right.</param>
        /// <param name="y">Down, -1 up to 1 down.</param>
        public void SetStick(string stick, double x, double y)
        {
            var at = new Vector(Math.Clamp(x, -1, 1), Math.Clamp(y, -1, 1));
            if (_sticks.TryGetValue(stick, out Vector was) && was == at) return;
            _sticks[stick] = at;
            InvalidateVisual();
        }

        /// <summary>Where a stick's knob is drawn, each axis from -1 to 1.</summary>
        /// <param name="stick">The stick's id.</param>
        /// <returns>The knob's position; zero at rest.</returns>
        public Vector StickPosition(string stick) => _sticks.TryGetValue(stick, out Vector v) ? v : default;

        /// <summary>Where a stick's knob is drawn, as a point in the diagram's own coordinates.</summary>
        /// <param name="stick">The stick's id.</param>
        /// <returns>The knob's centre, or null for a stick the drawing lacks.</returns>
        public Point? StickKnobCentre(string stick) =>
            _art.Sticks.FirstOrDefault(s => s.Id == stick) is { } art ? KnobAt(art).Transform(_toControl) : null;

        /// <summary>Sets how far a trigger is pulled, from 0 to 1.</summary>
        /// <param name="region">The trigger's id.</param>
        /// <param name="value">How far it is pulled.</param>
        public void SetTrigger(string region, double value)
        {
            value = Math.Clamp(value, 0, 1);
            if (_triggers.TryGetValue(region, out double was) && was == value) return;
            _triggers[region] = value;
            InvalidateVisual();
        }

        /// <summary>How far a trigger is drawn pulled, from 0 to 1.</summary>
        /// <param name="region">The trigger's id.</param>
        /// <returns>The value last set; 0 by default.</returns>
        public double TriggerValue(string region) => _triggers.TryGetValue(region, out double v) ? v : 0;

        /// <summary>The label standing for a region: a button that takes the focus and a click, or null for an id the drawing lacks.</summary>
        /// <param name="region">The region's id.</param>
        /// <returns>The region's label, or null.</returns>
        public Button? LabelOf(string region) => _labels.TryGetValue(region, out DiagramLabel? l) ? l : null;

        /// <summary>The region a label of this diagram stands for, or null for anything that is not one of its labels.</summary>
        /// <param name="element">An element, such as the one with the focus.</param>
        /// <returns>The region's id.</returns>
        public string? RegionOf(object? element) => element is DiagramLabel label && ReferenceEquals(label.Owner, this) ? label.Region.Id : null;

        /// <summary>The region drawn under a point, or null for none.</summary>
        /// <param name="point">A point in the diagram's own coordinates.</param>
        /// <returns>The region's id.</returns>
        public string? RegionAt(Point point)
        {
            if (!_toControl.TryInvert(out Matrix inverse)) return null;
            Point p = point.Transform(inverse);
            foreach (StickArt stick in _art.Sticks)
                if (stick.ClickRegion is { } click && ((Vector)(p - KnobAt(stick))).Length <= stick.KnobRadius) return click;
            foreach (bool behind in new[] { false, true })
                for (int i = _regions.Count - 1; i >= 0; i--)
                {
                    RegionArt art = _regions[i].Art;
                    if (art.Behind == behind && _art.Hits(art, p)) return art.Id;
                }
            return null;
        }

        /// <summary>Whether a point lies on the drawn controller's shell or any of its parts, rather than on the space around it.</summary>
        /// <param name="point">A point in the diagram's own coordinates.</param>
        /// <returns>True over the drawing.</returns>
        public bool IsOnDrawing(Point point) =>
            _toControl.TryInvert(out Matrix inverse) && point.Transform(inverse) is var p
            && _art.Body.Any(b => b.Fill is not null && b.Shape.Bounds.Contains(p) && b.Shape.FillContains(p));

        /// <summary>The line joining a region's label to it: from the label's edge, by the point where it enters the drawing's box, to the point where it meets the region, in the diagram's own coordinates.</summary>
        /// <param name="region">The region's id.</param>
        /// <returns>The line's three points, or null for an id the drawing lacks or before the diagram is arranged.</returns>
        public (Point From, Point Via, Point To)? LeaderOf(string region) =>
            _leaders.FirstOrDefault(l => l.Region == region) is { Region: not null } leader ? (leader.From, leader.Elbow, leader.To) : null;

        /// <summary>Every point of the line joining a region's label to it, in order from the label, in the diagram's own coordinates: a label in the outer of two columns has one more, where its line has passed between the inner column's labels.</summary>
        /// <param name="region">The region's id.</param>
        /// <returns>The line's points, or empty for an id the drawing lacks or before the diagram is arranged.</returns>
        public IReadOnlyList<Point> LeaderPath(string region) =>
            _leaders.FirstOrDefault(l => l.Region == region) is { Region: not null } leader
                ? (leader.Pass is { } pass ? [leader.From, pass, leader.Elbow, leader.To] : [leader.From, leader.Elbow, leader.To])
                : [];

        /// <summary>The box a region is drawn in, in the diagram's own coordinates.</summary>
        /// <param name="region">The region's id.</param>
        /// <returns>The box; empty for an id the drawing lacks.</returns>
        public Rect RegionRect(string region) =>
            _regions.FirstOrDefault(r => r.Id == region) is { } found ? found.Art.Bounds.TransformToAABB(_toControl) : default;

        /// <summary>A point inside a region, nearest the middle of its box, in the diagram's own coordinates; what a click on the region would hit.</summary>
        /// <param name="region">The region's id.</param>
        /// <returns>The point, or null for an id the drawing lacks.</returns>
        public Point? PointIn(string region)
        {
            return _regions.FirstOrDefault(r => r.Id == region) is { } found ? found.Art.Centre.Transform(_toControl) : null;
        }

        /// <summary>The nearest region a way from another on the drawing, or null when none lies that way.</summary>
        /// <param name="region">The region to move from.</param>
        /// <param name="direction">Up, Down, Left or Right.</param>
        /// <returns>The region's id.</returns>
        public string? Neighbour(string region, NavigationDirection direction)
        {
            if (_regions.FirstOrDefault(r => r.Id == region) is not { } from) return null;
            Vector way = direction switch
            {
                NavigationDirection.Up => new Vector(0, -1),
                NavigationDirection.Down => new Vector(0, 1),
                NavigationDirection.Left => new Vector(-1, 0),
                NavigationDirection.Right => new Vector(1, 0),
                _ => default,
            };
            if (way == default) return null;
            Point origin = from.Art.Centre;
            string? best = null;
            double bestScore = double.MaxValue;
            foreach (DiagramRegion other in _regions)
            {
                if (ReferenceEquals(other, from)) continue;
                Vector d = other.Art.Centre - origin;
                double along = d.X * way.X + d.Y * way.Y;
                if (along <= 6) continue;
                double across = Math.Abs(d.X * way.Y - d.Y * way.X);
                double score = Math.Sqrt(along * along + across * across) + (across > along * 1.5 ? 5 : 1) * across;
                if (score < bestScore) { bestScore = score; best = other.Id; }
            }
            return best;
        }

        /// <summary>Moves the selection, and the focus with it, to the nearest region a way from the selected one.</summary>
        /// <param name="direction">Up, Down, Left or Right.</param>
        /// <returns>False when nothing lies that way, so the host can move the focus out of the diagram.</returns>
        public bool MoveSelection(NavigationDirection direction)
        {
            if (SelectedRegion is not { } at || Neighbour(at, direction) is not { } next) return false;
            Select(next, NavigationMethod.Directional);
            return true;
        }

        /// <summary>Selects a region and gives its label the focus.</summary>
        /// <param name="region">The region's id.</param>
        /// <param name="method">How the focus arrived, for the focus ring.</param>
        public void Select(string region, NavigationMethod method = NavigationMethod.Unspecified)
        {
            SelectedRegion = region;
            if (IsInteractive && _labels.TryGetValue(region, out DiagramLabel? label)) label.Focus(method);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == LayoutProperty) Rebuild();
            else if (change.Property == SelectedRegionProperty)
            {
                foreach (DiagramLabel label in _labels.Values) label.IsSelectedRegion = label.Region.Id == SelectedRegion;
            }
            else if (change.Property == IsInteractiveProperty)
            {
                foreach (DiagramLabel label in _labels.Values) label.Focusable = IsInteractive;
            }
            else if (change.Property == ShowsLabelsProperty)
            {
                foreach (DiagramLabel label in _labels.Values) label.IsVisible = ShowsLabels;
            }
            else if (change.Property == ShowsKeysProperty || change.Property == CompactLabelsProperty)
            {
                foreach (DiagramLabel label in _labels.Values) { label.InvalidateMeasure(); label.InvalidateVisual(); }
            }
        }

        private void Rebuild()
        {
            _art = DiagramArt.For(Layout);
            _regions = _art.Regions.Select(r => new DiagramRegion(r)).ToList();
            foreach (DiagramLabel old in _labels.Values)
            {
                VisualChildren.Remove(old);
                LogicalChildren.Remove(old);
            }
            _labels.Clear();
            _pressed.Clear();
            foreach (DiagramRegion region in _regions)
            {
                var label = new DiagramLabel(this, region) { Binding = BindingOf(region.Id), Focusable = IsInteractive, IsVisible = ShowsLabels };
                _labels[region.Id] = label;
                VisualChildren.Add(label);
                LogicalChildren.Add(label);
            }
            if (SelectedRegion is { } selected && !_labels.ContainsKey(selected)) SelectedRegion = null;
            InvalidateMeasure();
            InvalidateVisual();
        }

        internal void Invoke(DiagramLabel label)
        {
            SelectedRegion = label.Region.Id;
            RegionInvoked?.Invoke(this, new DiagramRegionEventArgs(label.Region.Id));
        }

        internal void Selected(DiagramLabel label) => SelectedRegion = label.Region.Id;

        private void OnArrowKey(object? sender, KeyEventArgs e)
        {
            if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not DiagramLabel) return;
            NavigationDirection? way = e.Key switch
            {
                Key.Up => NavigationDirection.Up,
                Key.Down => NavigationDirection.Down,
                Key.Left => NavigationDirection.Left,
                Key.Right => NavigationDirection.Right,
                _ => null,
            };
            if (way is { } w && MoveSelection(w)) e.Handled = true;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.Handled || !IsInteractive || RegionAt(e.GetPosition(this)) is not { } region) return;
            Select(region, NavigationMethod.Pointer);
            RegionInvoked?.Invoke(this, new DiagramRegionEventArgs(region));
            e.Handled = true;
        }

        bool ICustomHitTest.HitTest(Point point) => RegionAt(point) is not null;

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group, () => Describe(Layout));

        /// <summary>What a screen reader hears for a diagram.</summary>
        /// <param name="layout">The controller drawn.</param>
        /// <returns>A short name, such as "Nintendo 64 controller".</returns>
        public static string Describe(ControllerLayout layout) => layout switch
        {
            ControllerLayout.Nes => "NES controller",
            ControllerLayout.Snes => "Super NES controller",
            ControllerLayout.Nintendo64 => "Nintendo 64 controller",
            ControllerLayout.GameBoy => "Game Boy",
            ControllerLayout.Genesis => "Genesis controller",
            ControllerLayout.GenesisSixButton => "Genesis six-button controller",
            _ => "Gamepad",
        };

        // --- Layout: the drawing fitted between four bands of labels - see docs/LunaP.md §198.2 ---

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 960 : availableSize.Width;
            double h = double.IsInfinity(availableSize.Height) ? w * 0.62 : availableSize.Height;
            // Labels grow with the space, and never fall far below the text around them, as on a big-screen sheet whose text is scaled up.
            double text = GetValue(TextElement.FontSizeProperty) / 14 * 0.85;
            double start = Math.Clamp(Math.Max(Math.Min(w / 1100, h / 640), Math.Min(text, Math.Min(w / 900, h / (CompactLabels ? 340 : 420)))), 0.45, 3);
            _split.Clear();
            Fit(start, w, h);

            // A tall column that shrank the labels takes a second column when that lets them grow and the width it takes was free: the drawing stays bound by its height (§198.11).
            DiagramSide[] tall = new[] { DiagramSide.Left, DiagramSide.Right }.Where(s => _regions.Count(r => r.Side == s) > SplitAbove).ToArray();
            if (ShowsLabels && tall.Length > 0 && _unit < start - 1e-9 && BoundByHeight(w, h))
            {
                double single = _unit;
                _split.UnionWith(tall);
                Fit(start, w, h);
                if (_unit <= single + 1e-9 || !BoundByHeight(w, h))
                {
                    _split.Clear();
                    _unit = single;
                    MeasureLabels();
                }
            }
            return new Size(w, h);
        }

        // A column taller than the space, or a row wider than it, shrinks every label, so none spills past another or the edge.
        private void Fit(double start, double w, double h)
        {
            _unit = start;
            MeasureLabels();
            for (int pass = 0; pass < 3 && ShowsLabels; pass++)
            {
                double chipH = ChipHeight, gap = Spacing.Gap * _unit, space = Spacing.Space * _unit;
                int rows = (_regions.Any(r => r.Side == DiagramSide.Top) ? 1 : 0) + (_regions.Any(r => r.Side == DiagramSide.Bottom) ? 1 : 0);
                double tallest = new[] { DiagramSide.Left, DiagramSide.Right }.Max(side => ColumnHeight(side, chipH, space));
                double need = tallest + rows * (chipH + gap);
                double across = new[] { DiagramSide.Top, DiagramSide.Bottom }.Max(side => _regions.Where(r => r.Side == side).Sum(r => _labels[r.Id].DesiredSize.Width + space));
                double over = Math.Max(need / h, across / w);
                if (over <= 1 || _unit <= 0.45) break;
                _unit = Math.Max(0.45, _unit / over);
                MeasureLabels();
            }
        }

        private double ChipHeight => _labels.Values.Select(l => l.DesiredSize.Height).DefaultIfEmpty(0).Max();

        // Two columns keep twice the space between a column's labels, so an outer label's line passes between two inner ones well clear of both.
        private double ColumnSpace(DiagramSide side, double space) => _split.Contains(side) ? space * 2 : space;

        // The height a column's labels take: one under another, or in two columns, each label half a step below the one before.
        private double ColumnHeight(DiagramSide side, double chipH, double space)
        {
            int n = _regions.Count(r => r.Side == side);
            double s = ColumnSpace(side, space);
            return n == 0 ? 0 : _split.Contains(side) ? (n - 1) * (chipH + s) / 2 + chipH + s : n * (chipH + s);
        }

        // A side column's labels in order down it, and which of them stand in the outer of two columns: every second one.
        private List<(DiagramRegion Region, Point Anchor)> Column(DiagramSide side) =>
            _regions.Where(r => r.Side == side).Select(r => (r, r.Art.Edge.Transform(_toControl))).OrderBy(p => p.Item2.Y).ToList();

        private static bool IsOuter(int index) => index % 2 == 1;

        // The width a band's labels take across: the widest label, or the two columns' widest with the space between them.
        private double BandWidth(DiagramSide side, double space)
        {
            List<DiagramRegion> here = _regions.Where(r => r.Side == side).OrderBy(r => r.Art.Edge.Y).ToList();
            double Widest(IEnumerable<DiagramRegion> of) => of.Select(r => _labels[r.Id].DesiredSize.Width).DefaultIfEmpty(0).Max();
            if (!_split.Contains(side)) return Widest(here);
            return Widest(here.Where((_, i) => !IsOuter(i))) + space + Widest(here.Where((_, i) => IsOuter(i)));
        }

        private bool Has(DiagramSide side) => ShowsLabels && _regions.Any(r => r.Side == side);

        // The box the drawing is fitted in between the bands of labels, as arranged at this unit.
        private Rect DrawingBox(double w, double h)
        {
            double u = _unit, chipH = ChipHeight;
            double gap = Spacing.Gap * u, space = Spacing.Space * u, pad = 10 * u;
            double left = Has(DiagramSide.Left) ? BandWidth(DiagramSide.Left, space) + gap : pad, right = Has(DiagramSide.Right) ? BandWidth(DiagramSide.Right, space) + gap : pad;
            double top = Has(DiagramSide.Top) ? chipH + gap : pad, bottom = Has(DiagramSide.Bottom) ? chipH + gap : pad;
            return new Rect(left, top, Math.Max(1, w - left - right), Math.Max(1, h - top - bottom));
        }

        // Whether the drawing's height, not its width, sets its size: so the space either side of it is free.
        private bool BoundByHeight(double w, double h)
        {
            Rect box = DrawingBox(w, h);
            return box.Height / _art.Design.Height < box.Width / _art.Design.Width;
        }

        private void MeasureLabels()
        {
            foreach (DiagramLabel label in _labels.Values)
            {
                label.Scale = _unit;
                label.Measure(Size.Infinity);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double u = _unit, w = finalSize.Width, h = finalSize.Height;
            double chipH = ChipHeight;
            double gap = Spacing.Gap * u, space = Spacing.Space * u;
            Rect box = DrawingBox(w, h);
            _scale = Math.Min(box.Width / _art.Design.Width, box.Height / _art.Design.Height);
            var art = new Rect(box.X + (box.Width - _art.Design.Width * _scale) / 2, box.Y + (box.Height - _art.Design.Height * _scale) / 2,
                _art.Design.Width * _scale, _art.Design.Height * _scale);
            _toControl = Matrix.CreateScale(_scale, _scale) * Matrix.CreateTranslation(art.X, art.Y);

            _leaders.Clear();
            var slots = new List<(DiagramLabel Label, Rect Slot)>();
            double near = gap * 0.55;
            double rowTop = Math.Max(0, art.Top - near - chipH), rowBottom = Math.Min(h - chipH, art.Bottom + near);
            double columnFrom = Has(DiagramSide.Top) ? rowTop + chipH + space : 0;
            double columnTo = Has(DiagramSide.Bottom) ? rowBottom - space : h;

            foreach (DiagramSide side in Enum.GetValues<DiagramSide>())
            {
                if (!Has(side)) continue;
                bool column = side is DiagramSide.Left or DiagramSide.Right, split = _split.Contains(side);
                List<(DiagramRegion Region, Point Anchor)> sorted = column ? Column(side)
                    : _regions.Where(r => r.Side == side).Select(r => (r, r.Art.Edge.Transform(_toControl))).OrderBy(p => p.Item2.X).ToList();
                double from = column ? columnFrom : 0, to = column ? columnTo : w;
                // A column stands beside the drawing, clear of the lines a row above or below runs down to it, unless it needs the height (§198.11).
                if (column)
                {
                    double spare = art.Height - (ColumnHeight(side, chipH, space) - ColumnSpace(side, space));
                    if (Has(DiagramSide.Top)) from = Math.Max(from, art.Top + Math.Min(0, spare / 2));
                    if (Has(DiagramSide.Bottom)) to = Math.Min(to, art.Bottom - Math.Min(0, spare / 2));
                }
                double[] at;
                double innerWidth = 0;
                if (split)
                {
                    // Two columns staggered: each label starts at least half a step below the one before, so the two in one column keep the column's space between them.
                    double s = ColumnSpace(side, space), step = (chipH + s) / 2;
                    double[] half = sorted.Select(_ => step - s).ToArray();
                    at = Spread(sorted.Select(p => p.Anchor.Y - chipH / 2).ToArray(), half, s, from, to - (chipH - (step - s)));
                    innerWidth = sorted.Where((_, i) => !IsOuter(i)).Select(p => _labels[p.Region.Id].DesiredSize.Width).DefaultIfEmpty(0).Max();
                }
                else
                {
                    double[] sizes = sorted.Select(p => column ? chipH : _labels[p.Region.Id].DesiredSize.Width).ToArray();
                    at = Spread(sorted.Select((p, i) => (column ? p.Anchor.Y : p.Anchor.X) - sizes[i] / 2).ToArray(), sizes, space, from, to);
                }

                for (int i = 0; i < sorted.Count; i++)
                {
                    (DiagramRegion region, _) = sorted[i];
                    DiagramLabel label = _labels[region.Id];
                    double cw = label.DesiredSize.Width;
                    bool outer = split && IsOuter(i);
                    double beyond = outer ? innerWidth + space : 0;
                    Rect slot = side switch
                    {
                        DiagramSide.Left => new Rect(Math.Max(0, art.Left - near - beyond - cw), at[i], cw, chipH),
                        DiagramSide.Right => new Rect(Math.Min(w - cw, art.Right + near + beyond), at[i], cw, chipH),
                        DiagramSide.Top => new Rect(at[i], rowTop, cw, chipH),
                        _ => new Rect(at[i], rowBottom, cw, chipH),
                    };
                    slots.Add((label, slot));
                    Point start = side switch
                    {
                        DiagramSide.Left => new Point(slot.Right, slot.Center.Y),
                        DiagramSide.Right => new Point(slot.Left, slot.Center.Y),
                        DiagramSide.Top => new Point(slot.Center.X, slot.Bottom),
                        _ => new Point(slot.Center.X, slot.Top),
                    };
                    // An outer label's line runs level between two inner labels, and turns toward its button only in the gap beside the drawing.
                    Point? pass = outer ? new Point(side == DiagramSide.Left ? art.Left - near / 2 : art.Right + near / 2, start.Y) : null;
                    Point elbow = region.Art.Edge.Transform(_toControl);
                    Point meets = region.Art.Anchor.Transform(_toControl);
                    _leaders.Add((start, pass, elbow, meets, region.Id));
                }
            }

            // The drawing and its labels centred together, so a drawing narrower than its box does not leave its labels to one side.
            Rect all = slots.Aggregate(art, (r, s) => r.Union(s.Slot));
            var shift = new Vector(Math.Round((w - all.Width) / 2 - all.X), Math.Round((h - all.Height) / 2 - all.Y));
            _toControl *= Matrix.CreateTranslation(shift);
            foreach ((DiagramLabel label, Rect slot) in slots) label.Arrange(slot.Translate(shift));
            for (int i = 0; i < _leaders.Count; i++)
            {
                var l = _leaders[i];
                _leaders[i] = (l.From + shift, l.Pass is { } p ? p + shift : null, l.Elbow + shift, l.To + shift, l.Region);
            }
            return finalSize;
        }

        /// <summary>Places a row of labels, each as near its wish as it can be: labels that would overlap are pushed apart as a group centred on their wishes, and kept within a span.</summary>
        /// <param name="wish">Where each label would start, in order along the row.</param>
        /// <param name="size">Each label's length along the row.</param>
        /// <param name="space">The gap kept between neighbours.</param>
        /// <param name="from">Where the span starts.</param>
        /// <param name="to">Where the span ends.</param>
        /// <returns>Where each label starts.</returns>
        public static double[] Spread(double[] wish, double[] size, double space, double from, double to)
        {
            int n = wish.Length;
            var offset = new double[n + 1];
            var clusters = new List<(int First, int Last, double Start)>();
            double Len(int first, int last) => offset[last + 1] - offset[first] - space;
            double Ideal(int first, int last)
            {
                double sum = 0;
                for (int k = first; k <= last; k++) sum += wish[k] - (offset[k] - offset[first]);
                return Math.Max(from, Math.Min(to - Len(first, last), sum / (last - first + 1)));
            }
            for (int i = 0; i < n; i++) offset[i + 1] = offset[i] + size[i] + space;
            for (int i = 0; i < n; i++)
            {
                clusters.Add((i, i, Ideal(i, i)));
                while (clusters.Count >= 2 && clusters[^2].Start + Len(clusters[^2].First, clusters[^2].Last) + space > clusters[^1].Start)
                {
                    int first = clusters[^2].First, last = clusters[^1].Last;
                    clusters.RemoveRange(clusters.Count - 2, 2);
                    clusters.Add((first, last, Ideal(first, last)));
                }
            }
            var at = new double[n];
            foreach ((int first, int last, double start) in clusters)
                for (int k = first; k <= last; k++) at[k] = start + offset[k] - offset[first];
            return at;
        }

        // --- Drawing ---


        private Point KnobAt(StickArt stick)
        {
            Vector at = StickPosition(stick.Id);
            return stick.Centre + at * stick.Travel;
        }

        internal Color Themed(string key, Color fallback) =>
            this.TryFindResource(key, ActualThemeVariant, out object? found) && found is Color c ? c : fallback;

        public override void Render(DrawingContext context)
        {
            Color accent = Themed("LunaAccentColor", LunaPalette.Accent.Color);
            Color muted = Themed("LunaMutedColor", LunaPalette.Muted.Color);
            FontFamily font = GetValue(TextElement.FontFamilyProperty);

            using (context.PushTransform(_toControl))
            {
                foreach (RegionArt region in _art.Regions.Where(r => r.Behind)) DrawRegion(context, region, accent, font);
                foreach (ArtPart part in _art.Body) DrawPart(context, part);
                foreach (StickArt stick in _art.Sticks)
                    if (stick.Gate is { } gate) context.DrawGeometry(new ImmutableSolidColorBrush(stick.GateFill), null, gate);
                foreach (RegionArt region in _art.Regions.Where(r => !r.Behind)) DrawRegion(context, region, accent, font);
                foreach (ArtPart part in _art.Marks) DrawPart(context, part);
                foreach (ArtText word in _art.Words) DrawText(context, word.Text, word.Centre, word.Size, word.Colour, word.Angle, word.Bold, font);
                foreach (StickArt stick in _art.Sticks) DrawKnob(context, stick, accent);
            }

            var line = new ImmutablePen(new ImmutableSolidColorBrush(muted), Math.Max(1, 1.5 * _unit), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var lit = new ImmutablePen(new ImmutableSolidColorBrush(accent), Math.Max(1.5, 2.5 * _unit), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            foreach ((Point from, Point? pass, Point elbow, Point to, string region) in _leaders)
            {
                bool strong = region == SelectedRegion || _pressed.Contains(region);
                IPen pen = strong ? lit : line;
                if (pass is { } p)
                {
                    context.DrawLine(pen, from, p);
                    context.DrawLine(pen, p, elbow);
                }
                else context.DrawLine(pen, from, elbow);
                context.DrawLine(pen, elbow, to);
                double dot = (strong ? 4 : 3) * _unit;
                context.DrawEllipse(pen.Brush, null, to, dot, dot);
            }
        }

        private static void DrawPart(DrawingContext context, ArtPart part) =>
            context.DrawGeometry(part.Fill is { } f ? new ImmutableSolidColorBrush(f) : null,
                part.Stroke is { } s ? new ImmutablePen(new ImmutableSolidColorBrush(s), part.StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round) : null,
                part.Shape);

        private void DrawRegion(DrawingContext context, RegionArt region, Color accent, FontFamily font)
        {
            bool pressed = _pressed.Contains(region.Id), selected = region.Id == SelectedRegion;
            var accentBrush = new ImmutableSolidColorBrush(accent);
            IDisposable? clip = region.Clip is { } c ? context.PushGeometryClip(c) : null;
            try
            {
                if (selected && region.Clip is null)
                    context.DrawGeometry(null, new ImmutablePen(new ImmutableSolidColorBrush(accent), 14, lineJoin: PenLineJoin.Round), region.Shape);
                if (pressed && region.Clip is null)
                    context.DrawGeometry(null, new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(90, accent.R, accent.G, accent.B)), 26, lineJoin: PenLineJoin.Round), region.Shape);

                IBrush? fill = pressed ? accentBrush : region.Fill is { } f ? new ImmutableSolidColorBrush(f) : null;
                IPen? stroke = region.Stroke is { } s && region.StrokeWidth > 0
                    ? new ImmutablePen(new ImmutableSolidColorBrush(pressed ? DiagramArt.Darken(accent, 0.3) : s), region.StrokeWidth, lineJoin: PenLineJoin.Round) : null;
                if (fill is not null || stroke is not null) context.DrawGeometry(fill, stroke, region.Shape);
                if (selected && !pressed && region.Clip is not null)
                    context.DrawGeometry(new ImmutableSolidColorBrush(Color.FromArgb(130, accent.R, accent.G, accent.B)), null, region.Shape);

                if (region.IsTrigger && TriggerValue(region.Id) is > 0 and var pulled && !pressed)
                {
                    Rect b = region.Bounds;
                    using (context.PushGeometryClip(region.Shape))
                        context.DrawRectangle(accentBrush, null, new Rect(b.X, b.Bottom - b.Height * pulled, b.Width, b.Height * pulled));
                }
            }
            finally { clip?.Dispose(); }

            if (region.Mark is { } mark) context.DrawGeometry(new ImmutableSolidColorBrush(pressed ? Colors.White : region.MarkColour), null, mark);
            if ((_captions.TryGetValue(region.Id, out string? own) ? own : region.Caption) is { } caption)
                DrawText(context, caption, region.Bounds.Center, region.CaptionSize, region.CaptionColour, region.CaptionAngle, true, font);
        }

        private void DrawKnob(DrawingContext context, StickArt stick, Color accent)
        {
            Point at = KnobAt(stick);
            bool clicked = stick.ClickRegion is { } click && _pressed.Contains(click);
            bool selected = stick.ClickRegion is { } chosen && chosen == SelectedRegion;
            if (at != stick.Centre)
                context.DrawLine(new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(200, accent.R, accent.G, accent.B)), 6, lineCap: PenLineCap.Round), stick.Centre, at);
            if (selected) context.DrawEllipse(null, new ImmutablePen(new ImmutableSolidColorBrush(accent), 14), at, stick.KnobRadius, stick.KnobRadius);
            context.DrawEllipse(new ImmutableSolidColorBrush(clicked ? accent : stick.KnobFill), new ImmutablePen(new ImmutableSolidColorBrush(stick.KnobStroke), 3), at, stick.KnobRadius, stick.KnobRadius);
            context.DrawEllipse(null, new ImmutablePen(new ImmutableSolidColorBrush(DiagramArt.Darken(stick.KnobFill, 0.18)), 3), at, stick.KnobRadius * 0.62, stick.KnobRadius * 0.62);
            if (StickRing > 0)
                context.DrawEllipse(null, new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(220, accent.R, accent.G, accent.B)), 3, new ImmutableDashStyle(new double[] { 2, 2 }, 0)),
                    stick.Centre, stick.Travel * StickRing, stick.Travel * StickRing);
            if (at != stick.Centre) context.DrawEllipse(new ImmutableSolidColorBrush(accent), null, at, 7, 7);
        }

        internal static void DrawText(DrawingContext context, string text, Point centre, double size, Color colour, double angle, bool bold, FontFamily font)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(font, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), size, new ImmutableSolidColorBrush(colour));
            var origin = new Point(centre.X - formatted.Width / 2, centre.Y - formatted.Height / 2);
            if (angle == 0)
            {
                context.DrawText(formatted, origin);
                return;
            }
            using (context.PushTransform(Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(angle * Math.PI / 180) * Matrix.CreateTranslation(centre.X, centre.Y)))
                context.DrawText(formatted, origin);
        }
    }

    // One region's label: its name, key and pad button, drawn by itself and joined to the drawing by a line - see docs/LunaP.md §198.3.
    internal sealed class DiagramLabel : Button
    {
        internal static readonly StyledProperty<bool> IsLitProperty = AvaloniaProperty.Register<DiagramLabel, bool>(nameof(IsLit));
        internal static readonly StyledProperty<bool> IsSelectedRegionProperty = AvaloniaProperty.Register<DiagramLabel, bool>(nameof(IsSelectedRegion));
        internal static readonly StyledProperty<DiagramBinding> BindingProperty = AvaloniaProperty.Register<DiagramLabel, DiagramBinding>(nameof(Binding));

        private readonly ControllerDiagram _owner;
        private double _scale = 1;

        internal ControllerDiagram Owner => _owner;

        static DiagramLabel()
        {
            AffectsRender<DiagramLabel>(IsLitProperty, IsSelectedRegionProperty, IsFocusedProperty, IsPointerOverProperty);
            AffectsMeasure<DiagramLabel>(BindingProperty);
        }

        internal DiagramLabel(ControllerDiagram owner, DiagramRegion region)
        {
            _owner = owner;
            Region = region;
            AutomationProperties.SetName(this, region.Name);
            ClickMode = ClickMode.Release;
            FocusAdorner = null;
            Click += (_, _) => _owner.Invoke(this);
            Cursor = new Cursor(StandardCursorType.Hand);
            UpdateHelp();
        }

        protected override Type StyleKeyOverride => typeof(DiagramLabel);

        public DiagramRegion Region { get; }

        public bool IsLit { get => GetValue(IsLitProperty); set => SetValue(IsLitProperty, value); }

        public bool IsSelectedRegion { get => GetValue(IsSelectedRegionProperty); set => SetValue(IsSelectedRegionProperty, value); }

        public DiagramBinding Binding { get => GetValue(BindingProperty); set => SetValue(BindingProperty, value); }

        // What a half of a binding that is not bound shows.
        public const string Unbound = "—";

        internal double Scale
        {
            get => _scale;
            set { if (_scale != value) { _scale = value; InvalidateMeasure(); } }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BindingProperty) UpdateHelp();
        }

        private void UpdateHelp() =>
            AutomationProperties.SetHelpText(this, $"Key {Binding.Key ?? "none"}, pad {Binding.Pad ?? "none"}");


        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e);
            _owner.Selected(this);
        }

        private FormattedText Text(string text, double size, bool bold, Color colour) =>
            new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), size, new ImmutableSolidColorBrush(colour));

        private (double Name, double Line, double Pad) Sizes => _owner.CompactLabels ? (15 * _scale, 14 * _scale, 5 * _scale) : (15 * _scale, 13 * _scale, 8 * _scale);

        // A line's height for its words' size: set closer in a compact label (§196.10).
        private double Row(double lineSize) => lineSize * (_owner.CompactLabels ? 1.2 : 1.45);

        internal double LineSize => Sizes.Line;

        protected override Size MeasureOverride(Size availableSize)
        {
            (double nameSize, double lineSize, double pad) = Sizes;
            double badge = Math.Max(Text(Region.Name, nameSize, true, Colors.White).Width + 14 * _scale, 34 * _scale);
            double lines = Math.Max(_owner.ShowsKeys ? Text(Binding.Key ?? Unbound, lineSize, false, Colors.White).Width : 0, Text(Binding.Pad ?? Unbound, lineSize, false, Colors.White).Width);
            // A compact label's words start 20 units past the badge as ever, and keep 8 units clear of its right edge though its padding is less.
            double width = _owner.CompactLabels ? pad + badge + 28 * _scale + Math.Max(lines, 60 * _scale) + 8 * _scale
                : pad + badge + 8 * _scale + 16 * _scale + Math.Max(lines, 64 * _scale) + pad;
            double height = pad * 2 + Row(lineSize) * 2;
            return new Size(Math.Ceiling(width), Math.Ceiling(height));
        }

        public override void Render(DrawingContext context)
        {
            Color accent = _owner.Themed("LunaAccentColor", LunaPalette.Accent.Color);
            Color text = _owner.Themed("LunaTextColor", LunaPalette.Text.Color);
            Color muted = _owner.Themed("LunaMutedColor", LunaPalette.Muted.Color);
            Color surface = _owner.Themed("LunaInputSurfaceColor", LunaPalette.InputSurface.Color);
            Color border = _owner.Themed("LunaBorderColor", LunaPalette.Border.Color);
            (double nameSize, double lineSize, double pad) = Sizes;
            var box = new Rect(Bounds.Size).Deflate(1);
            double radius = 7 * _scale;

            bool strong = IsSelectedRegion || IsFocused;
            Color back = IsLit ? Color.FromArgb(255, (byte)((surface.R + accent.R) / 2), (byte)((surface.G + accent.G) / 2), (byte)((surface.B + accent.B) / 2)) : surface;
            context.DrawRectangle(new ImmutableSolidColorBrush(back),
                new ImmutablePen(new ImmutableSolidColorBrush(strong ? accent : IsPointerOver ? text : border), strong ? Math.Max(2, 2.5 * _scale) : Math.Max(1, _scale)),
                box, radius, radius);

            Color badgeFill = Region.Art.Fill is { } f && Region.Stick is null ? f : Color.FromArgb(255, 80, 80, 90);
            if (IsLit) badgeFill = accent;
            FormattedText name = Text(Region.Name, nameSize, true, Colors.White);
            double badgeW = Math.Max(name.Width + 14 * _scale, 34 * _scale), badgeH = box.Height - 2 * pad;
            var badge = new Rect(box.X + pad, box.Y + pad, badgeW, badgeH);
            context.DrawRectangle(new ImmutableSolidColorBrush(badgeFill), null, badge, 5 * _scale, 5 * _scale);
            context.DrawText(name, new Point(badge.Center.X - name.Width / 2, badge.Center.Y - name.Height / 2));

            double x = badge.Right + 8 * _scale, row = Row(lineSize);
            double y0 = box.Y + pad, y1 = _owner.ShowsKeys ? y0 + row : box.Center.Y - row / 2;
            if (_owner.ShowsKeys) KeyIcon(context, new Rect(x, y0 + (row - 12 * _scale) / 2, 15 * _scale, 12 * _scale), muted);
            BadgeGlyphDrawing.Draw(context, new Rect(x - 1 * _scale, y1 + (row - 17 * _scale) / 2, 17 * _scale, 17 * _scale), ControllerShape.Gamepad, muted);
            FormattedText key = Text(Binding.Key ?? Unbound, lineSize, false, Binding.Key is null ? muted : text);
            FormattedText padText = Text(Binding.Pad ?? Unbound, lineSize, false, Binding.Pad is null ? muted : text);
            if (_owner.ShowsKeys) context.DrawText(key, new Point(x + 20 * _scale, y0 + (row - key.Height) / 2));
            context.DrawText(padText, new Point(x + 20 * _scale, y1 + (row - padText.Height) / 2));
        }

        private void KeyIcon(DrawingContext context, Rect r, Color colour)
        {
            var pen = new ImmutablePen(new ImmutableSolidColorBrush(colour), Math.Max(1, 1.3 * _scale));
            context.DrawRectangle(null, pen, r, 2 * _scale, 2 * _scale);
            var brush = new ImmutableSolidColorBrush(colour);
            for (int i = 0; i < 3; i++) context.DrawRectangle(brush, null, new Rect(r.X + r.Width * (0.2 + 0.25 * i), r.Y + r.Height * 0.28, r.Width * 0.12, r.Height * 0.14));
            context.DrawRectangle(brush, null, new Rect(r.X + r.Width * 0.25, r.Y + r.Height * 0.62, r.Width * 0.5, r.Height * 0.13));
        }
    }
}
