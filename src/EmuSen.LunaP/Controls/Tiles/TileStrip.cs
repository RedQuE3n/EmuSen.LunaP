using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // The part of a tile strip that does not depend on what the tiles show, so a style selector can name it - see docs/LunaP.md §95.1.
    /// <summary>The non-generic base of TileStrip&lt;T&gt;: the tile size and spacing, so a style selector can name every tile strip at once.</summary>
    public abstract class TileStrip : TemplatedControl
    {
        public static readonly StyledProperty<double> TileWidthProperty =
            AvaloniaProperty.Register<TileStrip, double>(nameof(TileWidth), 160);

        public static readonly StyledProperty<double> TileHeightProperty =
            AvaloniaProperty.Register<TileStrip, double>(nameof(TileHeight), 120);

        public static readonly StyledProperty<double> SpacingProperty =
            AvaloniaProperty.Register<TileStrip, double>(nameof(Spacing), 12);

        // Tiles realised beyond each edge of the view, so a step never shows a blank tile being filled - see §95.1.
        internal const int BufferTiles = 2;

        private protected ScrollViewer? Scroller;
        private protected StripPanel? Surface;
        private double _availableWidth = double.NaN;

        /// <summary>The width of every tile, in pixels. 160 by default.</summary>
        public double TileWidth
        {
            get => GetValue(TileWidthProperty);
            set => SetValue(TileWidthProperty, value);
        }

        /// <summary>The height of every tile, in pixels. 120 by default.</summary>
        public double TileHeight
        {
            get => GetValue(TileHeightProperty);
            set => SetValue(TileHeightProperty, value);
        }

        /// <summary>The gap between tiles and at the strip's edges, in pixels. 12 by default.</summary>
        public double Spacing
        {
            get => GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        internal abstract int Count { get; }

        internal double Pitch => Math.Max(1, TileWidth) + Math.Max(0, Spacing);

        internal Rect Slot(int index) => new(Math.Max(0, Spacing) + index * Pitch, Math.Max(0, Spacing), Math.Max(1, TileWidth), Math.Max(1, TileHeight));

        internal double Extent(int count) => count <= 0 ? 0 : Math.Max(0, Spacing) + count * Pitch;

        internal abstract void Realise();

        // The scroll viewer's own viewport once it has one, else the width offered, else everything - see §95.1.
        internal double ViewportWidth
        {
            get
            {
                if (Scroller is { Viewport.Width: > 0 } viewer) return viewer.Viewport.Width;
                if (double.IsFinite(_availableWidth) && _availableWidth > 0) return _availableWidth;
                return double.PositiveInfinity;
            }
        }

        internal double ScrollLeft => Scroller?.Offset.X ?? 0;

        // Whole tiles a viewport of this width shows, never below one, so a page always moves.
        internal int TilesInView => double.IsFinite(ViewportWidth) ? Math.Max(1, (int)Math.Floor((ViewportWidth - Math.Max(0, Spacing)) / Pitch)) : 1;

        protected override Size MeasureOverride(Size availableSize)
        {
            _availableWidth = availableSize.Width;
            return base.MeasureOverride(availableSize);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TileWidthProperty || change.Property == TileHeightProperty || change.Property == SpacingProperty) Surface?.InvalidateMeasure();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (Scroller is not null)
            {
                Scroller.ScrollChanged -= OnScrollChanged;
                Scroller.Content = null;
            }

            Scroller = e.NameScope.Find<ScrollViewer>("PART_Scroll");
            Surface ??= new StripPanel(this);

            if (Scroller is not null)
            {
                Scroller.Content = Surface;
                Scroller.ScrollChanged += OnScrollChanged;
            }

            OnPartsAttached();
        }

        private protected abstract void OnPartsAttached();

        private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => Surface?.InvalidateMeasure();
    }

    // One row of fixed-size tiles scrolling sideways, virtualised, with TileGrid's contract - see docs/LunaP.md §95.
    /// <summary>A virtualised horizontal strip of fixed-size tiles over a list of models, with single selection, keyboard and pointer navigation, and activation.</summary>
    public partial class TileStrip<T> : TileStrip where T : class
    {
        private readonly Dictionary<int, TileGridItem> _realised = new();
        private readonly List<TileGridItem> _pool = new();
        private IReadOnlyList<T> _items = Array.Empty<T>();
        private int _selectedIndex = -1;
        private bool _scrollPending;

        public TileStrip()
        {
            Focusable = true;
            BindTile = (tile, item) =>
            {
                if (tile is TextBlock text) text.Text = Label(item);
            };
            LayoutUpdated += (_, _) => ApplyPendingScroll();
            AddHandler(DoubleTappedEvent, OnDoubleTapped);
        }

        /// <summary>Builds the content control for one tile. Called once per container, never per item, since containers are reused as the strip scrolls. Returns a TextBlock unless replaced.</summary>
        public Func<Control> CreateTile { get; set; } = () => new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        /// <summary>Puts an item into a tile control made by CreateTile. Called when a container is realised for an item it was not already showing, and for every realised tile on Refresh; it must set everything the tile shows, because the control is reused for other items.</summary>
        public Action<Control, T> BindTile { get; set; }

        /// <summary>How a model becomes a tile's name for automation, and the text of the default tile. Should be cheap and free of side effects.</summary>
        public Func<T, string> Label { get; set; } = item => item?.ToString() ?? "";

        /// <summary>How a tile is matched to a model across a Refresh, so a selection survives a rebuild. The item itself unless replaced, which is reference identity for a class.</summary>
        public Func<T, object?> Key { get; set; } = item => item;

        /// <summary>Raised when the user changes the selection, by pointer, keyboard, Move or assistive technology. Not raised by Select or Refresh.</summary>
        public event Action<T>? Chose;

        /// <summary>Raised when the user opens a tile: a double-tap on it, or Enter or Space while the strip has focus and a tile is selected.</summary>
        public event Action<T>? Activated;

        /// <summary>The selected model, or null when nothing is selected.</summary>
        public T? Selected => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

        /// <summary>The selected model's position in the list, or -1 when nothing is selected.</summary>
        public int SelectedIndex => Selected is null ? -1 : _selectedIndex;

        internal override int Count => _items.Count;

        /// <summary>Replaces every tile, keeping the selection if Key still matches something. Does not raise Chose.</summary>
        /// <param name="items">The new models, in display order, left to right. Copied, so later changes to the list are not seen until the next Refresh. Safe to call before the control has a template.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="items"/> is null.</exception>
        public void Refresh(IReadOnlyList<T> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            object? wasSelected = Selected is { } previous ? Key(previous) : null;
            _items = items.ToArray();
            _selectedIndex = wasSelected is null ? -1 : _items.FindIndex(item => Equals(Key(item), wasSelected));

            foreach (TileGridItem container in _realised.Values) Release(container);
            _realised.Clear();
            foreach (TileGridItem container in _pool) container.HasBinding = false;

            Surface?.InvalidateMeasure();
        }

        /// <summary>Selects a model and scrolls it into view, without raising Chose.</summary>
        /// <param name="item">The model to select, matched by Key. Null clears the selection. Safe to call before the control has a template.</param>
        public void Select(T? item)
        {
            _selectedIndex = item is null ? -1 : _items.FindIndex(candidate => Equals(Key(candidate), Key(item)));
            MarkSelection();
            RequestScroll();
        }

        // What a key or a pad's button does, for a host that maps its own input onto the strip - see §95.2.
        /// <summary>Moves the selection by a number of tiles, clamped to the ends, as the user would, raising Chose if it moved. With nothing selected it selects the first tile, or the last for a negative step.</summary>
        /// <param name="by">Tiles to move: negative is left, positive is right. Safe to call before the control has a template.</param>
        /// <returns>Whether the selection changed.</returns>
        public bool Move(int by)
        {
            if (_items.Count == 0) return false;
            int from = _selectedIndex;
            int to = from < 0 ? (by < 0 ? _items.Count - 1 : 0) : (int)Math.Clamp((long)from + by, 0, _items.Count - 1);
            ChooseByUser(to);
            return to != from;
        }

        private void ChooseByUser(int index)
        {
            if (index < 0 || index >= _items.Count || index == _selectedIndex) return;

            _selectedIndex = index;
            MarkSelection();
            RequestScroll();
            Chose?.Invoke(_items[index]);
        }

        private void MarkSelection()
        {
            foreach (TileGridItem container in _realised.Values) container.SetSelected(container.Index == _selectedIndex);
        }

        internal override void Realise()
        {
            if (Surface is null) return;

            int first = 0, last = -1;
            if (_items.Count > 0)
            {
                double left = ScrollLeft, width = ViewportWidth;
                first = Math.Max(0, (int)Math.Floor((left - Math.Max(0, Spacing)) / Pitch) - BufferTiles);
                last = double.IsFinite(width) ? Math.Min(_items.Count - 1, (int)Math.Floor((left + width) / Pitch) + BufferTiles) : _items.Count - 1;
            }

            foreach (int index in _realised.Keys.Where(i => i < first || i > last).ToList())
            {
                Release(_realised[index]);
                _realised.Remove(index);
            }

            for (int index = first; index <= last; index++)
            {
                if (_realised.ContainsKey(index)) continue;

                T item = _items[index];
                TileGridItem container = Take(item);
                container.Index = index;
                container.SetSelected(index == _selectedIndex);

                if (!container.HasBinding || !ReferenceEquals(container.BoundItem, item))
                {
                    container.BoundItem = item;
                    container.HasBinding = true;
                    if (container.Content is Control tile) BindTile(tile, item);
                }

                container.IsVisible = true;
                _realised[index] = container;
            }
        }

        // A pooled container that last showed this item first, so scrolling back costs no BindTile - TileGrid's rule (§88.2).
        private TileGridItem Take(T item)
        {
            int match = _pool.FindIndex(c => c.HasBinding && ReferenceEquals(c.BoundItem, item));
            int at = match >= 0 ? match : _pool.Count - 1;

            if (at >= 0)
            {
                TileGridItem pooled = _pool[at];
                _pool.RemoveAt(at);
                return pooled;
            }

            var container = new TileGridItem { Content = CreateTile() };
            container.Speak = () => container.BoundItem is T bound ? Label(bound) : null;
            container.IsChosen = () => container.Index >= 0 && container.Index == _selectedIndex;
            container.Choose = () => ChooseByUser(container.Index);
            container.Grid = () => this;
            Surface!.Children.Add(container);
            return container;
        }

        private void Release(TileGridItem container)
        {
            container.IsVisible = false;
            container.Index = -1;
            container.SetSelected(false);
            _pool.Add(container);
        }

        private protected override void OnPartsAttached()
        {
            _realised.Clear();
            _pool.Clear();
            Surface?.Children.Clear();
            Surface?.InvalidateMeasure();
            if (_selectedIndex >= 0) _scrollPending = true;
        }

        // Waits for a layout pass that has the items' extent, as TileGrid's does (§88.2).
        private void RequestScroll()
        {
            if (_selectedIndex < 0) return;

            _scrollPending = true;
            if (Surface is { IsMeasureValid: true, IsArrangeValid: true } && Scroller is { Viewport.Width: > 0 }) ApplyPendingScroll();
        }

        // The selected tile is centred where the extent allows, so its neighbours on both sides stay in view - see §95.1.
        private void ApplyPendingScroll()
        {
            if (!_scrollPending || Scroller is not { Viewport.Width: > 0 } viewer) return;
            if (Surface is not { IsMeasureValid: true }) return;

            _scrollPending = false;
            if (_selectedIndex < 0 || _selectedIndex >= _items.Count) return;

            Rect slot = Slot(_selectedIndex);
            double width = viewer.Viewport.Width;
            double target = Math.Clamp(slot.Center.X - width / 2, 0, Math.Max(0, Extent(_items.Count) - width));
            if (Math.Abs(target - viewer.Offset.X) > 0.5) viewer.Offset = new Vector(target, viewer.Offset.Y);
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new SingleSelectionPeer(this, AutomationControlType.List, SelectionPeers);

        private IReadOnlyList<AutomationPeer> SelectionPeers() =>
            _realised.TryGetValue(_selectedIndex, out TileGridItem? container)
                ? new[] { ControlAutomationPeer.CreatePeerForElement(container) }
                : Array.Empty<AutomationPeer>();
    }
}
