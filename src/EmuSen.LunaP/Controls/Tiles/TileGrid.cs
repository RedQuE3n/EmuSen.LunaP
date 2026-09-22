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
    // The part of a tile grid that does not depend on what the tiles show - see docs/LunaP.md §88.2.
    //
    // It exists for the same reason LunaTable does: a style selector cannot name a generic type, and
    // `luna|TileGrid` is an exact match, so the theme writes `:is(luna|TileGrid)` against this base
    // and reaches every TileGrid<T> there will ever be. A test or a gallery counting grids without
    // knowing the model type counts this.
    /// <summary>The non-generic base of TileGrid&lt;T&gt;: the tile size and spacing, so a style selector can name every tile grid at once.</summary>
    public abstract class TileGrid : TemplatedControl
    {
        public static readonly StyledProperty<double> TileWidthProperty =
            AvaloniaProperty.Register<TileGrid, double>(nameof(TileWidth), 160);

        public static readonly StyledProperty<double> TileHeightProperty =
            AvaloniaProperty.Register<TileGrid, double>(nameof(TileHeight), 200);

        public static readonly StyledProperty<double> SpacingProperty =
            AvaloniaProperty.Register<TileGrid, double>(nameof(Spacing), 20);

        // One row of tiles either side of the viewport, realised before it scrolls into view so a
        // wheel notch never shows a blank row being filled. More would cost a row of BindTile calls
        // per step for nothing a person can see.
        internal const int BufferRows = 1;

        private protected ScrollViewer? Scroller;
        private protected TilePanel? Surface;
        private double _availableHeight = double.NaN;

        /// <summary>The width of every tile, in pixels. 160 by default.</summary>
        public double TileWidth
        {
            get => GetValue(TileWidthProperty);
            set => SetValue(TileWidthProperty, value);
        }

        /// <summary>The height of every tile, in pixels. 200 by default.</summary>
        public double TileHeight
        {
            get => GetValue(TileHeightProperty);
            set => SetValue(TileHeightProperty, value);
        }

        // The least gap between tiles, across and down, and at the edges. Space left over across a
        // row is shared out evenly between the gaps (TileLayout says why).
        /// <summary>The smallest gap between tiles and at the grid's edges, in pixels. 20 by default. Horizontal space left over is shared evenly between the gaps.</summary>
        public double Spacing
        {
            get => GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        /// <summary>How many tiles a row holds at the current width. One until the grid has been laid out.</summary>
        public int Columns { get; private set; } = 1;

        internal abstract int Count { get; }

        internal TileLayout LastLayout { get; private set; } = TileLayout.For(double.NaN, 160, 200, 20);

        internal TileLayout LayoutFor(double width)
        {
            LastLayout = TileLayout.For(width, TileWidth, TileHeight, Spacing);
            Columns = LastLayout.Columns;
            return LastLayout;
        }

        internal abstract void RealiseFor(TileLayout layout);

        // The height tiles are realised for. The scroll viewer's own viewport once it has one; until
        // the first arrange the height this grid was offered, so the first measure already realises
        // the rows in view rather than none of them. With neither - a grid in an unbounded parent -
        // it is the whole extent, which is correct and slow, and §88.2 records it as the one
        // configuration that defeats virtualisation.
        internal double ViewportHeight
        {
            get
            {
                if (Scroller is { Viewport.Height: > 0 } viewer) return viewer.Viewport.Height;
                if (double.IsFinite(_availableHeight) && _availableHeight > 0) return _availableHeight;
                return double.PositiveInfinity;
            }
        }

        internal double ScrollTop => Scroller?.Offset.Y ?? 0;

        protected override Size MeasureOverride(Size availableSize)
        {
            _availableHeight = availableSize.Height;
            return base.MeasureOverride(availableSize);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TileWidthProperty || change.Property == TileHeightProperty || change.Property == SpacingProperty)
            {
                Surface?.InvalidateMeasure();
            }
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
            Surface ??= new TilePanel(this);

            if (Scroller is not null)
            {
                Scroller.Content = Surface;

                // Every kind of change - offset, viewport, extent - moves which rows are in view.
                Scroller.ScrollChanged += OnScrollChanged;
            }

            OnPartsAttached();
        }

        private protected abstract void OnPartsAttached();

        private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => Surface?.InvalidateMeasure();
    }

    // A virtualised grid of fixed-size tiles, OpenEmu's cover grid - see docs/LunaP.md §88.2.
    //
    // WHY NOT A WrapPanel IN A ListBox, which is the obvious build and the one measured against: a
    // WrapPanel is not a virtualising panel, so an ItemsControl over it realises a container for
    // every item. Five thousand covers is five thousand controls laid out on every resize. Avalonia
    // ships no virtualising wrap panel, and its VirtualizingPanel base is built around a single
    // stacking axis. §88.2 has the alternatives and what each costs.
    //
    // So this computes the grid itself. Every tile is the same size, which is what makes that cheap:
    // the row of item N is N / Columns, the extent is a multiplication, and the rows in view are two
    // divisions of the scroll offset. The panel reports the full height so the scroll bar is honest,
    // and holds containers only for the rows in view plus one either side, reusing them as the view
    // moves. The tile inside a container is created once by CreateTile and re-bound by BindTile.
    //
    // THE SAME CONTRACT AS LunaList, on purpose. Label, Key, Refresh, Select and Chose mean here what
    // they mean there, including that Refresh and Select never raise Chose (§22.9, §78), so a host
    // that has learnt one has learnt both.
    /// <summary>A virtualised grid of fixed-size tiles over a list of models, with single selection, keyboard navigation and activation.</summary>
    public partial class TileGrid<T> : TileGrid where T : class
    {
        private readonly Dictionary<int, TileGridItem> _realised = new();
        private readonly List<TileGridItem> _pool = new();
        private IReadOnlyList<T> _items = Array.Empty<T>();
        private int _selectedIndex = -1;
        private bool _scrollPending;

        public TileGrid()
        {
            Focusable = true;
            BindTile = (tile, item) =>
            {
                if (tile is TextBlock text) text.Text = Label(item);
            };
            LayoutUpdated += (_, _) => ApplyPendingScroll();
            AddHandler(DoubleTappedEvent, OnDoubleTapped);
        }

        // Called once per container, and the control it returns is that container's for life: a
        // scrolled-away tile is re-bound to a new item, never rebuilt, which is what keeps a fast
        // scroll through thousands of covers from allocating a control per step.
        /// <summary>Builds the content control for one tile. Called once per container, never per item, since containers are reused as the grid scrolls. Returns a TextBlock unless replaced.</summary>
        public Func<Control> CreateTile { get; set; } = () => new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // THE ONE PLACE A TILE LEARNS ITS ITEM, and it must set everything the tile shows: a container
        // is reused, so a field BindTile leaves alone keeps whatever the previous item put there.
        /// <summary>Puts an item into a tile control made by CreateTile. Called when a container is realised for an item it was not already showing, and for every realised tile on Refresh; it must set everything the tile shows, because the control is reused for other items.</summary>
        public Action<Control, T> BindTile { get; set; }

        /// <summary>How a model becomes a tile's name for automation, and the text of the default tile. Should be cheap and free of side effects.</summary>
        public Func<T, string> Label { get; set; } = item => item?.ToString() ?? "";

        /// <summary>How a tile is matched to a model across a Refresh, so a selection survives a rebuild. The item itself unless replaced, which is reference identity for a class.</summary>
        public Func<T, object?> Key { get; set; } = item => item;

        // A selection change made by a person: a press on a tile, an arrow key, a reader's Select.
        /// <summary>Raised when the user changes the selection, by pointer, keyboard or assistive technology. Not raised by Select or Refresh.</summary>
        public event Action<T>? Chose;

        // THE EVENT LunaList REFUSED, and the reason it refused does not hold here. LunaList is a
        // ListBox, so DoubleTapped and KeyDown on its rows are Avalonia's own and already mean
        // "open this" - a LunaP wrapper would be a third spelling of them (§78). A tile grid is not
        // an ItemsControl: its containers are recycled, so a host that hooked DoubleTapped on one
        // would be hooking whatever item that container shows next, and a host handling KeyDown on
        // the grid would have to repeat the grid's own idea of which tile is selected. The grid
        // is the only thing that knows which item a gesture landed on, so it has to say.
        /// <summary>Raised when the user opens a tile: a double-tap on it, or Enter or Space while the grid has focus and a tile is selected.</summary>
        public event Action<T>? Activated;

        /// <summary>The selected model, or null when nothing is selected.</summary>
        public T? Selected => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

        internal override int Count => _items.Count;

        /// <summary>Replaces every tile, keeping the selection if Key still matches something. Does not raise Chose.</summary>
        /// <param name="items">The new models, in display order. Copied, so later changes to the list are not seen until the next Refresh. Safe to call before the control has a template.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="items"/> is null.</exception>
        public void Refresh(IReadOnlyList<T> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            object? wasSelected = Selected is { } previous ? Key(previous) : null;

            _items = items.ToArray();

            // -1 when the item is gone rather than its neighbour: the tile it named no longer
            // exists, and choosing a neighbour would be a guess made on the user's behalf.
            _selectedIndex = wasSelected is null ? -1 : _items.FindIndex(item => Equals(Key(item), wasSelected));

            // Indices have all moved, so every container goes back to the pool and is re-bound when
            // it is realised again - that is the "again for every realised tile" half of BindTile.
            foreach (TileGridItem container in _realised.Values) Release(container);
            _realised.Clear();
            foreach (TileGridItem container in _pool) container.HasBinding = false;

            Surface?.InvalidateMeasure();
        }

        // Sets the selection without raising Chose - the caller already knows what it chose - and
        // scrolls it into view, which a restored selection always wants.
        /// <summary>Selects a model and scrolls it into view, without raising Chose.</summary>
        /// <param name="item">The model to select, matched by Key. Null clears the selection. Safe to call before the control has a template.</param>
        public void Select(T? item)
        {
            _selectedIndex = item is null ? -1 : _items.FindIndex(candidate => Equals(Key(candidate), Key(item)));
            MarkSelection();
            RequestScroll();
        }

        // A user's choice, which is the only path that raises Chose.
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

        internal override void RealiseFor(TileLayout layout)
        {
            if (Surface is null) return;

            int first = 0;
            int last = -1;
            int rows = layout.Rows(_items.Count);

            if (rows > 0)
            {
                double top = ScrollTop;
                double height = ViewportHeight;
                int firstRow = Math.Max(0, (int)Math.Floor((top - layout.Spacing) / layout.RowPitch) - BufferRows);
                int lastRow = double.IsFinite(height)
                    ? Math.Min(rows - 1, (int)Math.Floor((top + height) / layout.RowPitch) + BufferRows)
                    : rows - 1;

                first = firstRow * layout.Columns;
                last = Math.Min(_items.Count - 1, (lastRow + 1) * layout.Columns - 1);
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

        // A pooled container that last showed this very item is preferred, so scrolling back and
        // forth over the same rows costs no BindTile at all. Otherwise any pooled one, and only
        // then a new container with a new tile from CreateTile.
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

        // Hidden rather than removed: a container leaving the panel would be detached, restyled
        // and re-templated when it came back, which is the cost recycling exists to avoid.
        private void Release(TileGridItem container)
        {
            container.IsVisible = false;
            container.Index = -1;
            container.SetSelected(false);
            _pool.Add(container);
        }

        private protected override void OnPartsAttached()
        {
            // A new panel after a re-template starts empty, so nothing realised before is on it.
            _realised.Clear();
            _pool.Clear();
            Surface?.Children.Clear();
            Surface?.InvalidateMeasure();
            if (_selectedIndex >= 0) _scrollPending = true;
        }

        // Scrolling needs an extent that already includes the items, which only exists after a
        // layout pass - a Select straight after a Refresh would otherwise scroll within the old list
        // and be clamped short. So the request waits for LayoutUpdated unless layout is current.
        private void RequestScroll()
        {
            if (_selectedIndex < 0) return;

            _scrollPending = true;
            if (Surface is { IsMeasureValid: true, IsArrangeValid: true } && Scroller is { Viewport.Height: > 0 }) ApplyPendingScroll();
        }

        private void ApplyPendingScroll()
        {
            if (!_scrollPending || Scroller is not { Viewport.Height: > 0 } viewer) return;
            if (Surface is not { IsMeasureValid: true }) return;

            _scrollPending = false;
            if (_selectedIndex < 0 || _selectedIndex >= _items.Count) return;

            TileLayout layout = LastLayout;
            Rect slot = layout.Slot(_selectedIndex);
            double top = viewer.Offset.Y;
            double height = viewer.Viewport.Height;
            double target = top;

            if (slot.Top - layout.Spacing < top) target = slot.Top - layout.Spacing;
            else if (slot.Bottom + layout.Spacing > top + height) target = slot.Bottom + layout.Spacing - height;

            target = Math.Clamp(target, 0, Math.Max(0, layout.Extent(_items.Count) - height));
            if (Math.Abs(target - top) > 0.5) viewer.Offset = new Vector(viewer.Offset.X, target);
        }

        // A List, named by whatever the host put in AutomationProperties.Name, reporting the
        // selected tile's container. A selection scrolled out of view has no container and reports
        // none - the same answer LunaTable gives for a row that is not realised.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new SingleSelectionPeer(this, AutomationControlType.List, SelectionPeers);

        private IReadOnlyList<AutomationPeer> SelectionPeers() =>
            _realised.TryGetValue(_selectedIndex, out TileGridItem? container)
                ? new[] { ControlAutomationPeer.CreatePeerForElement(container) }
                : Array.Empty<AutomationPeer>();
    }
}
