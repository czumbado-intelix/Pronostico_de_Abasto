using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI;
using PronosticosAbasto.ViewModels;
using System.IO;
using System.Text.Json;
using PronosticosAbasto.Services;
using Windows.Foundation;

namespace PronosticosAbasto;

public sealed partial class MainPage : Page
{
    private const double NarrowLayoutThreshold = 900;
    private const double LoadControlsTwoRowBreakpoint = 1000;
    private const double LoadControlsFourRowBreakpoint = 760;
    private const double MetricCardsTwoColumnBreakpoint = 1000;
    private const double ExpandedSideNavOpenWidth = 236;
    private const double ExpandedSideNavOpenOffset = 0;
    private const double ExpandedSideNavClosedOffset = -ExpandedSideNavOpenWidth;
    private static readonly Duration SideNavAnimationDuration = new(TimeSpan.FromMilliseconds(220));
    private bool _isNarrowLayout;
    private LoadControlsLayoutMode? _lastLoadControlsLayoutMode;
    private bool? _lastTransferMetricsCompact;
    private bool? _lastExpedicionMetricsCompact;
    private bool _isSideNavOpen;
    private Storyboard? _sideNavStoryboard;
    private Storyboard? _moduleTransitionStoryboard;
    private TaskCompletionSource<bool>? _moduleTransitionCompletion;
    private int _moduleTransitionVersion;
    private Grid? _activeColumnDragHeaderGrid;
    private FrameworkElement? _activeColumnDragHeader;
    private string? _activeColumnDragTableKey;
    private Point _activeColumnDragStartPoint;
    private int _activeColumnDragSourceColumn = -1;
    private int _activeColumnDragTargetColumn = -1;
    private bool _activeColumnDragInsertAfter;
    private bool _isColumnHeaderPointerPressed;
    private bool _isColumnHeaderDragging;

    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Ctrl+F: foco en el buscador del modulo que se esta viendo, con lo escrito
    /// seleccionado para reemplazarlo de una.
    /// </summary>
    private void FocusSearch_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var box = ViewModel.ShowTransferView ? TransferSearchBox
            : ViewModel.ShowExpedicionesView ? ExpedicionSearchBox
            : ViewModel.ShowTransitView ? TransitSearchBox
            : ViewModel.ShowWarehouseComparisonView ? WarehouseComparisonSearchBox
            : null;

        if (box is null)
        {
            return;
        }

        box.FocusSearch();
        args.Handled = true;
    }

    private sealed class TableColumnLayoutState
    {
        public List<int> Order { get; } = [];
        public Dictionary<int, double> Widths { get; } = [];
        public Dictionary<int, double> FlexWeights { get; } = [];
        public HashSet<int> ManuallySizedColumns { get; } = [];
        public HashSet<int> InteractiveColumns { get; } = [];
        public List<WeakReference<Grid>> RegisteredGrids { get; } = [];
        public double AvailableColumnWidth { get; set; }
    }

    private sealed class TableColumnPreference
    {
        public int Version { get; set; }
        public List<int> Order { get; set; } = [];
        public Dictionary<int, double> Widths { get; set; } = [];
        public List<int> ManuallySizedColumns { get; set; } = [];
    }

    private sealed record TableColumnSpec(double Width, double FlexWeight = 0);

    private enum LoadControlsLayoutMode
    {
        SingleRow,
        TwoRows,
        FourRows,
    }

    private const double ColumnResizeMinWidth = 48;
    private const double ColumnResizeMaxWidth = 720;
    private const double ColumnAutoCompactMinWidth = 36;
    private const double ColumnHeaderDragThreshold = 8;
    private const double TableHorizontalPadding = 24;
    private const int TableColumnPreferenceVersion = 8;
    private const string TableColumnPreferencesFileName = "table-columns.json";
    private static readonly JsonSerializerOptions TableColumnPreferenceJsonOptions = new() { WriteIndented = true };
    private readonly Dictionary<string, TableColumnLayoutState> _tableColumnStates = [];
    private Dictionary<string, TableColumnPreference> _tableColumnPreferences = new(StringComparer.OrdinalIgnoreCase);
    private bool _tableColumnPreferencesLoaded;

    private static readonly IReadOnlyDictionary<string, TableColumnSpec[]> TableColumnSpecs =
        new Dictionary<string, TableColumnSpec[]>
        {
            ["TransferTable"] =
            [
                new(40),
                new(84),
                new(180, 0.5),
                new(76),
                new(82),
                new(82),
                new(92),
                new(78),
                new(96),
                new(0),
                new(0),
                new(0),
                new(0),
                new(220, 0.7),
                new(70),
                new(0),
            ],
            ["ExpedicionTable"] =
            [
                new(40),
                new(148, 0.12),
                new(96),
                new(190, 0.45),
                new(78),
                new(78),
                new(94),
                new(78),
                new(96),
                new(0),
                new(0),
                new(0),
                new(0),
                new(220, 0.55),
                new(0),
                new(70),
            ],
        };

    private static readonly DependencyProperty OriginalColumnProperty =
        DependencyProperty.RegisterAttached("OriginalColumn", typeof(int), typeof(MainPage), new PropertyMetadata(-1));

    private static readonly DependencyProperty PreparedTableElementProperty =
        DependencyProperty.RegisterAttached("PreparedTableElement", typeof(bool), typeof(MainPage), new PropertyMetadata(false));

    private static readonly DependencyProperty ResizeHandleProperty =
        DependencyProperty.RegisterAttached("ResizeHandle", typeof(bool), typeof(MainPage), new PropertyMetadata(false));

    private static readonly DependencyProperty ColumnDividerProperty =
        DependencyProperty.RegisterAttached("ColumnDivider", typeof(bool), typeof(MainPage), new PropertyMetadata(false));

    private static readonly DependencyProperty ColumnDropIndicatorProperty =
        DependencyProperty.RegisterAttached("ColumnDropIndicator", typeof(bool), typeof(MainPage), new PropertyMetadata(false));

    private static readonly DependencyProperty HeaderCellProperty =
        DependencyProperty.RegisterAttached("HeaderCell", typeof(bool), typeof(MainPage), new PropertyMetadata(false));

    private static readonly DependencyProperty HeaderDragSourceProperty =
        DependencyProperty.RegisterAttached("HeaderDragSource", typeof(bool), typeof(MainPage), new PropertyMetadata(false));

    private void TableHeaderGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Grid headerGrid || headerGrid.Tag is not string tableKey)
        {
            return;
        }

        if (!IsConfigurableColumnTable(tableKey))
        {
            return;
        }

        var state = GetOrCreateTableColumnState(tableKey, headerGrid);
        RegisterTableGrid(tableKey, headerGrid);
        PrepareHeaderGrid(tableKey, headerGrid, state);
        TryUpdateFluidTableViewport(tableKey);
        ApplyTableColumnLayout(tableKey);
    }

    private void TableRowGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Grid rowGrid || rowGrid.Tag is not string tableKey)
        {
            return;
        }

        if (!IsConfigurableColumnTable(tableKey))
        {
            return;
        }

        RegisterTableGrid(tableKey, rowGrid);
        ApplyTableColumnLayout(rowGrid, GetOrCreateTableColumnState(tableKey, rowGrid));
    }

    private TableColumnLayoutState GetOrCreateTableColumnState(string tableKey, Grid sourceGrid)
    {
        if (_tableColumnStates.TryGetValue(tableKey, out var existing))
        {
            return existing;
        }

        var state = new TableColumnLayoutState();
        for (var column = 0; column < sourceGrid.ColumnDefinitions.Count; column++)
        {
            var definition = sourceGrid.ColumnDefinitions[column];
            var spec = GetTableColumnSpec(tableKey, column);

            state.Order.Add(column);
            state.Widths[column] = spec?.Width ?? ReadColumnWidth(definition);

            if (spec is { FlexWeight: > 0 })
            {
                state.FlexWeights[column] = spec.FlexWeight;
            }
        }

        ApplyDefaultTableColumnOrder(tableKey, state);
        ApplySavedTableColumnState(tableKey, state);
        _tableColumnStates[tableKey] = state;
        return state;
    }

    private static void ApplyDefaultTableColumnOrder(string tableKey, TableColumnLayoutState state)
    {
        var preferredOrder = tableKey switch
        {
            "TransferTable" => new[] { 0, 1, 2, 3, 4, 5, 14, 6, 7, 8, 13, 9, 10, 11, 12, 15 },
            "ExpedicionTable" => new[] { 0, 1, 2, 3, 4, 5, 15, 6, 7, 8, 13, 9, 10, 11, 12, 14 },
            _ => null,
        };

        if (preferredOrder is null)
        {
            return;
        }

        var knownColumns = state.Order.ToHashSet();
        var nextOrder = new List<int>();
        foreach (var column in preferredOrder)
        {
            if (knownColumns.Contains(column))
            {
                nextOrder.Add(column);
            }
        }

        foreach (var column in state.Order)
        {
            if (!nextOrder.Contains(column))
            {
                nextOrder.Add(column);
            }
        }

        state.Order.Clear();
        state.Order.AddRange(nextOrder);
    }

    private static string TableColumnPreferencesPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PronosticosAbasto",
            TableColumnPreferencesFileName);

    private void EnsureTableColumnPreferencesLoaded()
    {
        if (_tableColumnPreferencesLoaded)
        {
            return;
        }

        _tableColumnPreferencesLoaded = true;

        try
        {
            var path = TableColumnPreferencesPath;
            if (!File.Exists(path))
            {
                return;
            }

            var preferences = JsonSerializer.Deserialize<Dictionary<string, TableColumnPreference>>(
                File.ReadAllText(path),
                TableColumnPreferenceJsonOptions);

            if (preferences is null)
            {
                return;
            }

            _tableColumnPreferences = new Dictionary<string, TableColumnPreference>(StringComparer.OrdinalIgnoreCase);
            var removedLegacyPreference = false;
            foreach (var (key, preference) in preferences)
            {
                if (preference.Version == TableColumnPreferenceVersion)
                {
                    _tableColumnPreferences[key] = preference;
                }
                else
                {
                    removedLegacyPreference = true;
                }
            }

            if (removedLegacyPreference)
            {
                TryWriteTableColumnPreferences();
            }
        }
        catch
        {
            _tableColumnPreferences = new Dictionary<string, TableColumnPreference>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void ApplySavedTableColumnState(string tableKey, TableColumnLayoutState state)
    {
        EnsureTableColumnPreferencesLoaded();
        if (!_tableColumnPreferences.TryGetValue(tableKey, out var preference))
        {
            return;
        }

        var knownColumns = state.Order.ToHashSet();
        var savedColumns = new HashSet<int>();
        var savedOrder = new List<int>();
        foreach (var column in preference.Order)
        {
            if (knownColumns.Contains(column) && savedColumns.Add(column))
            {
                savedOrder.Add(column);
            }
        }

        if (savedOrder.Count > 0)
        {
            foreach (var column in state.Order)
            {
                if (savedColumns.Add(column))
                {
                    savedOrder.Add(column);
                }
            }

            state.Order.Clear();
            state.Order.AddRange(savedOrder);
        }

        foreach (var (column, width) in preference.Widths)
        {
            if (knownColumns.Contains(column) && width >= 0)
            {
                state.Widths[column] = Math.Clamp(width, 0, ColumnResizeMaxWidth);
            }
        }

        state.ManuallySizedColumns.Clear();
        foreach (var column in preference.ManuallySizedColumns)
        {
            if (knownColumns.Contains(column) &&
                state.Widths.TryGetValue(column, out var width) &&
                width > 0)
            {
                state.ManuallySizedColumns.Add(column);
            }
        }
    }

    private void SaveTableColumnPreference(string tableKey)
    {
        EnsureTableColumnPreferencesLoaded();
        if (!_tableColumnStates.TryGetValue(tableKey, out var state))
        {
            return;
        }

        _tableColumnPreferences[tableKey] = new TableColumnPreference
        {
            Version = TableColumnPreferenceVersion,
            Order = state.Order.ToList(),
            Widths = state.Widths.ToDictionary(pair => pair.Key, pair => pair.Value),
            ManuallySizedColumns = state.ManuallySizedColumns.ToList(),
        };

        TryWriteTableColumnPreferences();
    }

    private void TryWriteTableColumnPreferences()
    {
        try
        {
            var path = TableColumnPreferencesPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(_tableColumnPreferences, TableColumnPreferenceJsonOptions));
        }
        catch
        {
            // La preferencia visual no debe bloquear el analisis ni la tabla.
        }
    }

    private static bool IsConfigurableColumnTable(string tableKey) =>
        TableColumnSpecs.ContainsKey(tableKey);

    private static TableColumnSpec? GetTableColumnSpec(string tableKey, int column)
    {
        if (TableColumnSpecs.TryGetValue(tableKey, out var specs) && column >= 0 && column < specs.Length)
        {
            return specs[column];
        }

        return null;
    }

    private static double ReadColumnWidth(ColumnDefinition column)
    {
        if (column.Width.IsStar)
        {
            return column.MinWidth > 0
                ? column.MinWidth
                : Math.Max(ColumnResizeMinWidth, column.Width.Value * 120);
        }

        if (column.Width.IsAbsolute)
        {
            return column.Width.Value;
        }

        if (column.ActualWidth > 0)
        {
            return column.ActualWidth;
        }

        if (column.MinWidth > 0)
        {
            return column.MinWidth;
        }

        return Math.Max(ColumnResizeMinWidth, column.Width.Value * 120);
    }

    /// <summary>
    /// Anota una cuadricula (encabezado o fila) para que reciba los cambios de
    /// ancho y orden de columnas de su tabla.
    /// </summary>
    /// <remarks>
    /// Se llama por cada fila que carga, y el ListView recicla contenedores al
    /// desplazarse, asi que la lista se limpia en la misma pasada en que se
    /// busca: antes las referencias muertas solo se podaban en
    /// <see cref="ApplyTableColumnLayout(string)"/>, al que
    /// <c>TableRowGrid_Loaded</c> no llama, y el recorrido lineal crecia sin
    /// techo mientras el operador desplazaba la tabla.
    /// </remarks>
    private void RegisterTableGrid(string tableKey, Grid grid)
    {
        var state = GetOrCreateTableColumnState(tableKey, grid);
        var alreadyRegistered = false;

        for (var index = state.RegisteredGrids.Count - 1; index >= 0; index--)
        {
            if (!state.RegisteredGrids[index].TryGetTarget(out var existing))
            {
                state.RegisteredGrids.RemoveAt(index);
                continue;
            }

            alreadyRegistered |= ReferenceEquals(existing, grid);
        }

        if (!alreadyRegistered)
        {
            state.RegisteredGrids.Add(new WeakReference<Grid>(grid));
        }
    }

    private void PrepareHeaderGrid(string tableKey, Grid headerGrid, TableColumnLayoutState state)
    {
        RemoveHeaderColumnHandles(headerGrid);
        RemoveColumnDropIndicators(headerGrid);
        state.InteractiveColumns.Clear();

        foreach (var child in headerGrid.Children.OfType<FrameworkElement>().ToList())
        {
            if (GetResizeHandle(child) ||
                GetColumnDivider(child) ||
                GetColumnDropIndicator(child) ||
                child.Visibility != Visibility.Visible)
            {
                continue;
            }

            var headerCell = EnsureDividedHeaderCell(headerGrid, child);
            if (headerCell is null)
            {
                continue;
            }

            var originalColumn = GetElementOriginalColumn(headerCell);
            if (originalColumn < 0 || !state.Widths.TryGetValue(originalColumn, out var width) || width <= 0)
            {
                continue;
            }

            state.InteractiveColumns.Add(originalColumn);
            if (TryGetHeaderDragSource(headerCell) is { } dragSource)
            {
                PrepareHeaderDragSource(dragSource, originalColumn);
            }
        }

        foreach (var originalColumn in state.InteractiveColumns.OrderBy(column => state.Order.IndexOf(column)))
        {
            var handle = new Thumb
            {
                Width = 14,
                MinHeight = 34,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = CreateColumnResizeHandleBrush(isActive: false),
                BorderBrush = CreateColumnResizeHandleBorderBrush(isActive: false),
                BorderThickness = new Thickness(0, 0, 1, 0),
                IsTabStop = false,
                Margin = new Thickness(0, -8, -6, -8),
                Tag = tableKey,
            };

            handle.SetValue(OriginalColumnProperty, originalColumn);
            handle.SetValue(ResizeHandleProperty, true);
            ToolTipService.SetToolTip(handle, "Arrastra para agrandar o hacer pequena esta columna");
            handle.PointerEntered += TableColumnResizeHandle_PointerEntered;
            handle.PointerExited += TableColumnResizeHandle_PointerExited;
            handle.DragStarted += TableColumnResizeHandle_DragStarted;
            handle.DragDelta += TableColumnResizeHandle_DragDelta;
            handle.DragCompleted += TableColumnResizeHandle_DragCompleted;
            Canvas.SetZIndex(handle, 5);
            headerGrid.Children.Add(handle);
        }

    }

    private static Grid? EnsureDividedHeaderCell(Grid headerGrid, FrameworkElement element)
    {
        if (GetHeaderCell(element) && element is Grid existingCell)
        {
            return existingCell;
        }

        if (element is not Button button)
        {
            return null;
        }

        var originalColumn = GetElementOriginalColumn(button);
        if (originalColumn < 0)
        {
            return null;
        }

        var displayColumn = Grid.GetColumn(button);
        var displayColumnSpan = Grid.GetColumnSpan(button);
        var displayRow = Grid.GetRow(button);
        var displayRowSpan = Grid.GetRowSpan(button);
        var flyout = button.Flyout;
        button.Flyout = null;

        var label = ExtractHeaderContentElement(button, firstElement: true);
        var filterIcon = ExtractHeaderContentElement(button, firstElement: false)
            ?? new FontIcon
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Glyph = "\uE71C",
                FontSize = 12,
                Foreground = button.Foreground,
            };

        var cell = new Grid
        {
            MinHeight = Math.Max(34, button.MinHeight),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Visibility = button.Visibility,
            Tag = button.Tag,
        };

        cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cell.SetValue(HeaderCellProperty, true);
        cell.SetValue(OriginalColumnProperty, originalColumn);

        var dragArea = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 255, 255, 255)),
            Padding = button.Padding,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = CreateHeaderDragContent(label, button.HorizontalAlignment),
        };

        dragArea.SetValue(HeaderDragSourceProperty, true);
        dragArea.SetValue(OriginalColumnProperty, originalColumn);
        AddHeaderInteractionHint(dragArea);
        Grid.SetColumn(dragArea, 0);
        cell.Children.Add(dragArea);

        if (flyout is not null)
        {
            var filterButton = new Button
            {
                Padding = new Thickness(2),
                MinWidth = 26,
                MinHeight = 26,
                Margin = new Thickness(0, 0, 16, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Content = filterIcon,
                Flyout = flyout,
            };

            ToolTipService.SetToolTip(filterButton, "Filtrar columna");
            Canvas.SetZIndex(filterButton, 6);
            Grid.SetColumn(filterButton, 1);
            cell.Children.Add(filterButton);
        }

        Grid.SetColumn(cell, displayColumn);
        Grid.SetColumnSpan(cell, displayColumnSpan);
        Grid.SetRow(cell, displayRow);
        Grid.SetRowSpan(cell, displayRowSpan);

        var insertIndex = headerGrid.Children.IndexOf(button);
        headerGrid.Children.Remove(button);
        headerGrid.Children.Insert(Math.Max(0, insertIndex), cell);
        return cell;
    }

    private static UIElement? ExtractHeaderContentElement(Button button, bool firstElement)
    {
        if (button.Content is not StackPanel panel)
        {
            if (firstElement && button.Content is UIElement directContent)
            {
                button.Content = null;
                return directContent;
            }

            return null;
        }

        if (panel.Children.Count == 0)
        {
            return null;
        }

        var index = firstElement ? 0 : Math.Min(1, panel.Children.Count - 1);
        var element = panel.Children[index];
        panel.Children.RemoveAt(index);
        return element;
    }

    private static UIElement CreateHeaderDragContent(UIElement? label, HorizontalAlignment originalAlignment)
    {
        var host = new Grid
        {
            HorizontalAlignment = originalAlignment switch
            {
                HorizontalAlignment.Center => HorizontalAlignment.Center,
                HorizontalAlignment.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left,
            },
            VerticalAlignment = VerticalAlignment.Center,
        };

        host.Children.Add(label ?? new TextBlock());
        return host;
    }

    private void PrepareHeaderDragSource(FrameworkElement dragSource, int originalColumn)
    {
        dragSource.SetValue(OriginalColumnProperty, originalColumn);

        if (GetPreparedTableElement(dragSource))
        {
            return;
        }

        dragSource.AddHandler(PointerPressedEvent, new PointerEventHandler(TableColumnHeader_PointerPressed), true);
        dragSource.AddHandler(PointerMovedEvent, new PointerEventHandler(TableColumnHeader_PointerMoved), true);
        dragSource.AddHandler(PointerReleasedEvent, new PointerEventHandler(TableColumnHeader_PointerReleased), true);
        dragSource.PointerCanceled += TableColumnHeader_PointerCanceled;
        dragSource.PointerCaptureLost += TableColumnHeader_PointerCaptureLost;
        dragSource.SetValue(PreparedTableElementProperty, true);
    }

    private static FrameworkElement? TryGetHeaderDragSource(Grid headerCell)
    {
        return headerCell.Children
            .OfType<FrameworkElement>()
            .FirstOrDefault(GetHeaderDragSource);
    }

    private static SolidColorBrush CreateColumnResizeHandleBrush(bool isActive) =>
        new(isActive
            ? Windows.UI.Color.FromArgb(40, 0, 168, 143)
            : Windows.UI.Color.FromArgb(18, 0, 168, 143));

    private static SolidColorBrush CreateColumnResizeHandleBorderBrush(bool isActive) =>
        new(isActive
            ? Windows.UI.Color.FromArgb(220, 0, 168, 143)
            : Windows.UI.Color.FromArgb(110, 0, 168, 143));

    private static SolidColorBrush CreateColumnDividerBrush(bool isHeader) =>
        new(isHeader
            ? Windows.UI.Color.FromArgb(120, 148, 163, 184)
            : Windows.UI.Color.FromArgb(58, 148, 163, 184));

    private static void AddHeaderInteractionHint(FrameworkElement header)
    {
        const string hint = "Arrastra este encabezado para mover la columna. Usa la linea derecha para cambiar el ancho.";
        var current = ToolTipService.GetToolTip(header);
        if (current is null)
        {
            ToolTipService.SetToolTip(header, hint);
        }
        else if (current is string currentText &&
                 !currentText.Contains("Arrastra este encabezado", StringComparison.OrdinalIgnoreCase))
        {
            ToolTipService.SetToolTip(header, $"{currentText}\n{hint}");
        }
    }

    private static void RemoveHeaderColumnHandles(Grid headerGrid)
    {
        for (var index = headerGrid.Children.Count - 1; index >= 0; index--)
        {
            if (headerGrid.Children[index] is DependencyObject child && GetResizeHandle(child))
            {
                headerGrid.Children.RemoveAt(index);
            }
        }
    }

    private static void MoveColumn(TableColumnLayoutState state, int sourceColumn, int targetColumn, bool insertAfterTarget)
    {
        var sourceIndex = state.Order.IndexOf(sourceColumn);
        var targetIndex = state.Order.IndexOf(targetColumn);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        state.Order.RemoveAt(sourceIndex);
        var insertIndex = targetIndex + (insertAfterTarget ? 1 : 0);
        if (sourceIndex < insertIndex)
        {
            insertIndex--;
        }

        state.Order.Insert(Math.Clamp(insertIndex, 0, state.Order.Count), sourceColumn);
    }

    private void TableColumnHeader_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement dragSource ||
            !GetHeaderDragSource(dragSource) ||
            TryFindTableHeader(dragSource) is not { } headerGrid ||
            headerGrid.Tag is not string tableKey ||
            !_tableColumnStates.TryGetValue(tableKey, out var state))
        {
            return;
        }

        var startPoint = e.GetCurrentPoint(headerGrid).Position;
        var sourceColumn = GetElementOriginalColumn(dragSource);
        if (sourceColumn < 0 || !state.InteractiveColumns.Contains(sourceColumn))
        {
            return;
        }

        ResetColumnHeaderDrag(commitMove: false);
        _activeColumnDragHeaderGrid = headerGrid;
        _activeColumnDragHeader = dragSource;
        _activeColumnDragTableKey = tableKey;
        _activeColumnDragSourceColumn = sourceColumn;
        _activeColumnDragTargetColumn = sourceColumn;
        _activeColumnDragInsertAfter = false;
        _activeColumnDragStartPoint = startPoint;
        _isColumnHeaderPointerPressed = true;
        _isColumnHeaderDragging = false;
        dragSource.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void TableColumnHeader_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isColumnHeaderPointerPressed ||
            _activeColumnDragHeaderGrid is not { } headerGrid ||
            _activeColumnDragHeader is not { } header ||
            _activeColumnDragTableKey is not string tableKey ||
            !_tableColumnStates.TryGetValue(tableKey, out var state))
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(headerGrid).Position;
        var horizontalChange = currentPoint.X - _activeColumnDragStartPoint.X;
        var verticalChange = currentPoint.Y - _activeColumnDragStartPoint.Y;
        if (!_isColumnHeaderDragging &&
            Math.Abs(horizontalChange) < ColumnHeaderDragThreshold &&
            Math.Abs(verticalChange) < ColumnHeaderDragThreshold)
        {
            return;
        }

        if (!_isColumnHeaderDragging)
        {
            _isColumnHeaderDragging = true;
            header.Opacity = 0.58;
            Canvas.SetZIndex(header, 18);
        }

        var sourceSlot = state.Order.IndexOf(_activeColumnDragSourceColumn);
        if (sourceSlot >= 0 &&
            TryGetColumnMoveTarget(
                headerGrid,
                state,
                _activeColumnDragSourceColumn,
                currentPoint.X - GetColumnSlotCenter(headerGrid, sourceSlot),
                out var targetColumn,
                out var insertAfterTarget))
        {
            _activeColumnDragTargetColumn = targetColumn;
            _activeColumnDragInsertAfter = insertAfterTarget;
            ShowColumnDropIndicator(headerGrid, state, targetColumn, insertAfterTarget);
        }

        e.Handled = true;
    }

    private void TableColumnHeader_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isColumnHeaderDragging)
        {
            ResetColumnHeaderDrag(commitMove: true);
            e.Handled = true;
            return;
        }

        ResetColumnHeaderDrag(commitMove: false);
        e.Handled = true;
    }

    private void TableColumnHeader_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        ResetColumnHeaderDrag(commitMove: false);
    }

    private void TableColumnHeader_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_isColumnHeaderPointerPressed || _isColumnHeaderDragging)
        {
            ResetColumnHeaderDrag(commitMove: false);
        }
    }

    private void ResetColumnHeaderDrag(bool commitMove)
    {
        var headerGrid = _activeColumnDragHeaderGrid;
        var header = _activeColumnDragHeader;
        var tableKey = _activeColumnDragTableKey;

        if (commitMove &&
            _isColumnHeaderDragging &&
            headerGrid is not null &&
            tableKey is not null &&
            _tableColumnStates.TryGetValue(tableKey, out var state) &&
            _activeColumnDragSourceColumn >= 0 &&
            _activeColumnDragTargetColumn >= 0 &&
            _activeColumnDragSourceColumn != _activeColumnDragTargetColumn)
        {
            MoveColumn(state, _activeColumnDragSourceColumn, _activeColumnDragTargetColumn, _activeColumnDragInsertAfter);
            ApplyTableColumnLayout(tableKey);
            SaveTableColumnPreference(tableKey);
        }

        if (header is not null)
        {
            header.Opacity = 1;
            Canvas.SetZIndex(header, 0);
        }

        if (headerGrid is not null)
        {
            RemoveColumnDropIndicators(headerGrid);
        }

        _activeColumnDragHeaderGrid = null;
        _activeColumnDragHeader = null;
        _activeColumnDragTableKey = null;
        _activeColumnDragSourceColumn = -1;
        _activeColumnDragTargetColumn = -1;
        _activeColumnDragInsertAfter = false;
        _isColumnHeaderPointerPressed = false;
        _isColumnHeaderDragging = false;
        header?.ReleasePointerCaptures();
    }

    private static void ShowColumnDropIndicator(
        Grid headerGrid,
        TableColumnLayoutState state,
        int targetColumn,
        bool insertAfterTarget)
    {
        RemoveColumnDropIndicators(headerGrid);
        var targetSlot = state.Order.IndexOf(targetColumn);
        if (targetSlot < 0 || targetSlot >= headerGrid.ColumnDefinitions.Count)
        {
            return;
        }

        var indicator = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = insertAfterTarget ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 168, 143)),
            IsHitTestVisible = false,
            Margin = insertAfterTarget
                ? new Thickness(0, -7, -6, -7)
                : new Thickness(-6, -7, 0, -7),
        };

        indicator.SetValue(ColumnDropIndicatorProperty, true);
        Canvas.SetZIndex(indicator, 30);
        Grid.SetColumn(indicator, targetSlot);
        headerGrid.Children.Add(indicator);
    }

    private static void RemoveColumnDropIndicators(Grid headerGrid)
    {
        for (var index = headerGrid.Children.Count - 1; index >= 0; index--)
        {
            if (headerGrid.Children[index] is DependencyObject child && GetColumnDropIndicator(child))
            {
                headerGrid.Children.RemoveAt(index);
            }
        }
    }

    private static bool TryGetColumnMoveTarget(
        Grid headerGrid,
        TableColumnLayoutState state,
        int sourceColumn,
        double horizontalChange,
        out int targetColumn,
        out bool insertAfterTarget)
    {
        targetColumn = -1;
        insertAfterTarget = false;

        var sourceSlot = state.Order.IndexOf(sourceColumn);
        if (sourceSlot < 0)
        {
            return false;
        }

        var visibleInteractiveSlots = Enumerable
            .Range(0, Math.Min(headerGrid.ColumnDefinitions.Count, state.Order.Count))
            .Where(slot =>
                state.InteractiveColumns.Contains(state.Order[slot]) &&
                GetColumnSlotWidth(headerGrid, slot) > 0.5)
            .ToArray();

        if (visibleInteractiveSlots.Length <= 1 || !visibleInteractiveSlots.Contains(sourceSlot))
        {
            return false;
        }

        var sourceCenter = GetColumnSlotCenter(headerGrid, sourceSlot);
        var dropX = sourceCenter + horizontalChange;
        var firstSlot = visibleInteractiveSlots[0];
        var lastSlot = visibleInteractiveSlots[^1];
        var firstCenter = GetColumnSlotCenter(headerGrid, firstSlot);
        var lastCenter = GetColumnSlotCenter(headerGrid, lastSlot);

        if (dropX <= firstCenter)
        {
            targetColumn = state.Order[firstSlot];
            insertAfterTarget = false;
            return targetColumn != sourceColumn;
        }

        if (dropX >= lastCenter)
        {
            targetColumn = state.Order[lastSlot];
            insertAfterTarget = true;
            return targetColumn != sourceColumn;
        }

        var targetSlot = visibleInteractiveSlots
            .OrderBy(slot => Math.Abs(GetColumnSlotCenter(headerGrid, slot) - dropX))
            .First();

        targetColumn = state.Order[targetSlot];
        insertAfterTarget = dropX > GetColumnSlotCenter(headerGrid, targetSlot);
        return targetColumn != sourceColumn;
    }

    private static double GetColumnSlotCenter(Grid grid, int slot) =>
        GetColumnSlotLeft(grid, slot) + GetColumnSlotWidth(grid, slot) / 2;

    private static double GetColumnSlotLeft(Grid grid, int slot)
    {
        var left = 0d;
        var spacing = grid.ColumnSpacing;
        for (var index = 0; index < slot && index < grid.ColumnDefinitions.Count; index++)
        {
            left += GetColumnSlotWidth(grid, index) + spacing;
        }

        return left;
    }

    private static double GetColumnSlotWidth(Grid grid, int slot)
    {
        if (slot < 0 || slot >= grid.ColumnDefinitions.Count)
        {
            return 0;
        }

        var definition = grid.ColumnDefinitions[slot];
        if (definition.ActualWidth > 0)
        {
            return definition.ActualWidth;
        }

        return definition.Width.IsAbsolute ? definition.Width.Value : 0;
    }

    private void TableColumnResizeHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        SetResizeHandleVisualState(sender, isActive: true);
    }

    private void TableColumnResizeHandle_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        SetResizeHandleVisualState(sender, isActive: false);
    }

    private void TableColumnResizeHandle_DragStarted(object sender, DragStartedEventArgs e)
    {
        SetResizeHandleVisualState(sender, isActive: true);
    }

    private void TableColumnResizeHandle_DragDelta(object sender, DragDeltaEventArgs args)
    {
        if (sender is not FrameworkElement handle ||
            handle.Tag is not string tableKey ||
            !_tableColumnStates.TryGetValue(tableKey, out var state))
        {
            return;
        }

        var originalColumn = GetElementOriginalColumn(handle);
        if (originalColumn < 0 || !state.Widths.TryGetValue(originalColumn, out var currentWidth))
        {
            return;
        }

        state.Widths[originalColumn] = Math.Clamp(currentWidth + args.HorizontalChange, ColumnResizeMinWidth, ColumnResizeMaxWidth);
        state.ManuallySizedColumns.Add(originalColumn);
        ApplyTableColumnLayout(tableKey);
    }

    private void TableColumnResizeHandle_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tableKey })
        {
            SaveTableColumnPreference(tableKey);
        }

        SetResizeHandleVisualState(sender, isActive: false);
    }

    private static void SetResizeHandleVisualState(object sender, bool isActive)
    {
        if (sender is not Thumb handle)
        {
            return;
        }

        handle.Background = CreateColumnResizeHandleBrush(isActive);
        handle.BorderBrush = CreateColumnResizeHandleBorderBrush(isActive);
    }

    private void ApplyTableColumnLayout(string tableKey)
    {
        if (!_tableColumnStates.TryGetValue(tableKey, out var state))
        {
            return;
        }

        for (var index = state.RegisteredGrids.Count - 1; index >= 0; index--)
        {
            if (state.RegisteredGrids[index].TryGetTarget(out var grid))
            {
                ApplyTableColumnLayout(grid, state);
            }
            else
            {
                state.RegisteredGrids.RemoveAt(index);
            }
        }
    }

    private static void ApplyTableColumnLayout(Grid grid, TableColumnLayoutState state)
    {
        if (grid.ColumnDefinitions.Count == 0 || state.Order.Count == 0)
        {
            return;
        }

        RemoveColumnDividers(grid);
        var slotCount = Math.Min(grid.ColumnDefinitions.Count, state.Order.Count);
        var baseWidths = new double[slotCount];
        var baseTotal = 0d;
        var flexTotal = 0d;

        for (var slot = 0; slot < slotCount; slot++)
        {
            var originalColumn = state.Order[slot];
            var width = state.Widths.GetValueOrDefault(originalColumn, ColumnResizeMinWidth);
            baseWidths[slot] = width;
            baseTotal += width;

            if (width > 0 &&
                !state.ManuallySizedColumns.Contains(originalColumn) &&
                state.FlexWeights.TryGetValue(originalColumn, out var flexWeight))
            {
                flexTotal += flexWeight;
            }
        }

        var extraWidth = state.AvailableColumnWidth > 0
            ? Math.Max(0, state.AvailableColumnWidth - baseTotal)
            : 0;

        if (state.AvailableColumnWidth > 0 && baseTotal > state.AvailableColumnWidth)
        {
            CompactColumnWidths(baseWidths, slotCount, state.AvailableColumnWidth);
            extraWidth = 0;
        }

        for (var slot = 0; slot < slotCount; slot++)
        {
            var originalColumn = state.Order[slot];
            var width = baseWidths[slot];

            if (extraWidth > 0 &&
                flexTotal > 0 &&
                width > 0 &&
                !state.ManuallySizedColumns.Contains(originalColumn) &&
                state.FlexWeights.TryGetValue(originalColumn, out var flexWeight))
            {
                width += extraWidth * (flexWeight / flexTotal);
            }

            var definition = grid.ColumnDefinitions[slot];
            if (!definition.Width.IsAbsolute || Math.Abs(definition.Width.Value - width) > 0.5)
            {
                definition.Width = new GridLength(width);
            }
        }

        foreach (var child in grid.Children.OfType<FrameworkElement>())
        {
            var originalColumn = GetElementOriginalColumn(child);
            if (originalColumn < 0)
            {
                continue;
            }

            var slot = state.Order.IndexOf(originalColumn);
            if (slot >= 0)
            {
                Grid.SetColumn(child, slot);
                if (GetResizeHandle(child) && child is FrameworkElement handle)
                {
                    handle.Margin = IsLastVisibleColumnSlot(state, slot)
                        ? new Thickness(0, -8, 0, -8)
                        : new Thickness(0, -8, -6, -8);
                }
            }
        }

        AddColumnDividers(grid, slotCount);
    }

    private static void CompactColumnWidths(
        double[] widths,
        int slotCount,
        double availableWidth)
    {
        var visibleSlots = Enumerable
            .Range(0, slotCount)
            .Where(slot => widths[slot] > 0.5)
            .ToArray();
        if (visibleSlots.Length == 0)
        {
            return;
        }

        var totalWidth = visibleSlots.Sum(slot => widths[slot]);
        if (totalWidth <= availableWidth)
        {
            return;
        }

        var minimumWidths = visibleSlots.ToDictionary(
            slot => slot,
            slot => Math.Min(widths[slot], ColumnAutoCompactMinWidth));
        var minimumTotal = minimumWidths.Values.Sum();
        if (minimumTotal >= availableWidth)
        {
            var scale = Math.Max(0.1, availableWidth / totalWidth);
            foreach (var slot in visibleSlots)
            {
                widths[slot] = Math.Max(1, widths[slot] * scale);
            }

            return;
        }

        var reductionNeeded = totalWidth - availableWidth;
        var reducibleTotal = visibleSlots.Sum(slot => widths[slot] - minimumWidths[slot]);
        if (reducibleTotal <= 0)
        {
            return;
        }

        foreach (var slot in visibleSlots)
        {
            var reducibleWidth = widths[slot] - minimumWidths[slot];
            widths[slot] -= reductionNeeded * (reducibleWidth / reducibleTotal);
        }
    }

    private static bool IsLastVisibleColumnSlot(TableColumnLayoutState state, int slot)
    {
        for (var index = state.Order.Count - 1; index >= 0; index--)
        {
            var originalColumn = state.Order[index];
            if (state.Widths.GetValueOrDefault(originalColumn, 0) > 0.5)
            {
                return index == slot;
            }
        }

        return false;
    }

    private static void AddColumnDividers(Grid grid, int slotCount)
    {
        var visibleSlots = Enumerable
            .Range(0, slotCount)
            .Where(slot => grid.ColumnDefinitions[slot].Width.Value > 0.5)
            .ToArray();

        if (visibleSlots.Length <= 1)
        {
            return;
        }

        var isHeader = grid.Children.OfType<DependencyObject>().Any(GetResizeHandle);
        var dividerSlots = isHeader
            ? visibleSlots
            : visibleSlots.Take(visibleSlots.Length - 1);
        var lastVisibleSlot = visibleSlots[^1];
        foreach (var slot in dividerSlots)
        {
            var divider = new Border
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = CreateColumnDividerBrush(isHeader),
                IsHitTestVisible = false,
                Margin = slot == lastVisibleSlot
                    ? new Thickness(0, -6, 0, -6)
                    : new Thickness(0, -6, -3, -6),
            };

            divider.SetValue(ColumnDividerProperty, true);
            Grid.SetColumn(divider, slot);
            grid.Children.Add(divider);
        }
    }

    private static void RemoveColumnDividers(Grid grid)
    {
        for (var index = grid.Children.Count - 1; index >= 0; index--)
        {
            if (grid.Children[index] is DependencyObject child && GetColumnDivider(child))
            {
                grid.Children.RemoveAt(index);
            }
        }
    }

    private void TransferTableViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateFluidTableViewport("TransferTable", TransferTableHost, TransferTableScrollViewer, e.NewSize.Width);
    }

    private void ExpedicionTableViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateFluidTableViewport("ExpedicionTable", ExpedicionTableHost, ExpedicionTableScrollViewer, e.NewSize.Width);
    }

    private void UpdateFluidTableViewport(string tableKey, FrameworkElement tableHost, ScrollViewer scrollViewer, double viewportWidth)
    {
        if (viewportWidth <= 0 || double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth))
        {
            return;
        }

        if (!_tableColumnStates.TryGetValue(tableKey, out var state))
        {
            if (TableColumnSpecs.TryGetValue(tableKey, out var specs))
            {
                var minimumHostWidth = GetMinimumTableHostWidth(specs, GetTableColumnSpacing(tableKey));
                SetTableHostWidth(tableHost, scrollViewer, viewportWidth, minimumHostWidth);
            }

            return;
        }

        var minimumWidth = GetMinimumTableHostWidth(state, GetTableColumnSpacing(tableKey));
        var targetWidth = SetTableHostWidth(tableHost, scrollViewer, viewportWidth, minimumWidth);
        var spacingTotal = GetTableSpacingTotal(state.Order.Count, GetTableColumnSpacing(tableKey));
        state.AvailableColumnWidth = Math.Max(0, targetWidth - TableHorizontalPadding - spacingTotal);
        ApplyTableColumnLayout(tableKey);
    }

    private bool TryUpdateFluidTableViewport(string tableKey)
    {
        return tableKey switch
        {
            "TransferTable" => TryUpdateFluidTableViewport(tableKey, TransferTableHost, TransferTableScrollViewer),
            "ExpedicionTable" => TryUpdateFluidTableViewport(tableKey, ExpedicionTableHost, ExpedicionTableScrollViewer),
            _ => false,
        };
    }

    private bool TryUpdateFluidTableViewport(string tableKey, FrameworkElement tableHost, ScrollViewer scrollViewer)
    {
        if (scrollViewer.ActualWidth <= 0)
        {
            return false;
        }

        UpdateFluidTableViewport(tableKey, tableHost, scrollViewer, scrollViewer.ActualWidth);
        return true;
    }

    private static double SetTableHostWidth(FrameworkElement tableHost, ScrollViewer scrollViewer, double viewportWidth, double minimumWidth)
    {
        _ = minimumWidth;
        var targetWidth = viewportWidth;
        if (double.IsNaN(tableHost.Width) || Math.Abs(tableHost.Width - targetWidth) > 0.5)
        {
            tableHost.Width = targetWidth;
        }

        scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
        scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        return targetWidth;
    }

    private static double GetMinimumTableHostWidth(TableColumnLayoutState state, double columnSpacing)
    {
        var columnTotal = state.Order.Sum(column => state.Widths.GetValueOrDefault(column, 0));
        return columnTotal + TableHorizontalPadding + GetTableSpacingTotal(state.Order.Count, columnSpacing);
    }

    private static double GetMinimumTableHostWidth(TableColumnSpec[] specs, double columnSpacing)
    {
        var columnTotal = specs.Sum(spec => spec.Width);
        return columnTotal + TableHorizontalPadding + GetTableSpacingTotal(specs.Length, columnSpacing);
    }

    private static double GetTableSpacingTotal(int columnCount, double columnSpacing) =>
        Math.Max(0, columnCount - 1) * columnSpacing;

    private static double GetTableColumnSpacing(string tableKey) =>
        tableKey is "TransferTable" or "ExpedicionTable" ? 6 : 0;

    private static int GetElementOriginalColumn(DependencyObject element)
    {
        var originalColumn = (int)element.GetValue(OriginalColumnProperty);
        if (originalColumn >= 0)
        {
            return originalColumn;
        }

        if (element is not FrameworkElement frameworkElement)
        {
            return -1;
        }

        originalColumn = Grid.GetColumn(frameworkElement);
        element.SetValue(OriginalColumnProperty, originalColumn);
        return originalColumn;
    }

    private static bool GetPreparedTableElement(DependencyObject element) =>
        (bool)element.GetValue(PreparedTableElementProperty);

    private static bool GetResizeHandle(DependencyObject element) =>
        (bool)element.GetValue(ResizeHandleProperty);

    private static bool GetColumnDivider(DependencyObject element) =>
        (bool)element.GetValue(ColumnDividerProperty);

    private static bool GetColumnDropIndicator(DependencyObject element) =>
        (bool)element.GetValue(ColumnDropIndicatorProperty);

    private static bool GetHeaderCell(DependencyObject element) =>
        (bool)element.GetValue(HeaderCellProperty);

    private static bool GetHeaderDragSource(DependencyObject element) =>
        (bool)element.GetValue(HeaderDragSourceProperty);

    private static Grid? TryFindTableHeader(DependencyObject source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is Grid { Tag: string })
            {
                return (Grid)current;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Responsive layout: side-by-side table + detail on wide windows, detail
    /// stacked below the table when the content area gets narrow.
    /// </summary>
    private void MainContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < NarrowLayoutThreshold;
        if (narrow == _isNarrowLayout && e.PreviousSize.Width != 0)
        {
            return;
        }

        _isNarrowLayout = narrow;

        if (narrow)
        {
            MainCol1.Width = new GridLength(0);
            MainRow1.Height = new GridLength(360);
            Grid.SetColumn(DetailColumnPanel, 0);
            Grid.SetRow(DetailColumnPanel, 1);
        }
        else
        {
            MainCol1.Width = new GridLength(1.1, GridUnitType.Star);
            MainRow1.Height = new GridLength(0);
            Grid.SetColumn(DetailColumnPanel, 1);
            Grid.SetRow(DetailColumnPanel, 0);
        }
    }

    private void LoadControlsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid || e.NewSize.Width <= 0)
        {
            return;
        }

        var mode = e.NewSize.Width < LoadControlsFourRowBreakpoint
            ? LoadControlsLayoutMode.FourRows
            : e.NewSize.Width < LoadControlsTwoRowBreakpoint
                ? LoadControlsLayoutMode.TwoRows
                : LoadControlsLayoutMode.SingleRow;

        if (_lastLoadControlsLayoutMode == mode)
        {
            return;
        }

        _lastLoadControlsLayoutMode = mode;

        if (mode == LoadControlsLayoutMode.FourRows)
        {
            ApplyLoadControlsFourRowLayout(grid);
        }
        else if (mode == LoadControlsLayoutMode.TwoRows)
        {
            ApplyLoadControlsTwoRowLayout(grid);
        }
        else
        {
            ApplyLoadControlsSingleRowLayout(grid);
        }
    }

    private void ApplyLoadControlsSingleRowLayout(Grid grid)
    {
        SetGridColumns(
            grid,
            GridLength.Auto,
            GridLength.Auto,
            GridLength.Auto,
            GridLength.Auto,
            GridLength.Auto,
            GridLength.Auto);
        SetGridRows(grid, GridLength.Auto, new GridLength(0), new GridLength(0), new GridLength(0));

        SetGridPosition(LoadInventoryButton, 0, 0);
        SetGridPosition(LoadForecastButton, 0, 1);
        SetGridPosition(AnalysisPeriodSelector, 0, 2);
        SetGridPosition(HorizonSelector, 0, 3);
        SetGridPosition(MoreLoadActionsButton, 0, 4);
        SetGridPosition(LoadProgressRing, 0, 5);
    }

    private void ApplyLoadControlsTwoRowLayout(Grid grid)
    {
        SetGridColumns(
            grid,
            GridLength.Auto,
            GridLength.Auto,
            GridLength.Auto,
            GridLength.Auto,
            new GridLength(0),
            new GridLength(0));
        SetGridRows(grid, GridLength.Auto, GridLength.Auto, new GridLength(0), new GridLength(0));

        SetGridPosition(LoadInventoryButton, 0, 0);
        SetGridPosition(LoadForecastButton, 0, 1);
        SetGridPosition(MoreLoadActionsButton, 0, 2);
        SetGridPosition(LoadProgressRing, 0, 3);
        SetGridPosition(AnalysisPeriodSelector, 1, 0, 2);
        SetGridPosition(HorizonSelector, 1, 2, 2);
    }

    private void ApplyLoadControlsFourRowLayout(Grid grid)
    {
        SetGridColumns(
            grid,
            GridLength.Auto,
            GridLength.Auto,
            new GridLength(0),
            new GridLength(0),
            new GridLength(0),
            new GridLength(0));
        SetGridRows(grid, GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto);

        SetGridPosition(LoadInventoryButton, 0, 0);
        SetGridPosition(LoadForecastButton, 0, 1);
        SetGridPosition(AnalysisPeriodSelector, 1, 0, 2);
        SetGridPosition(HorizonSelector, 2, 0, 2);
        SetGridPosition(MoreLoadActionsButton, 3, 0);
        SetGridPosition(LoadProgressRing, 3, 1);
    }

    private void MetricCardsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Grid grid && e.NewSize.Width > 0)
        {
            var compact = e.NewSize.Width < MetricCardsTwoColumnBreakpoint;
            if (ReferenceEquals(grid, TransferMetricCardsGrid))
            {
                if (_lastTransferMetricsCompact == compact)
                {
                    return;
                }

                _lastTransferMetricsCompact = compact;
            }
            else if (ReferenceEquals(grid, ExpedicionMetricCardsGrid))
            {
                if (_lastExpedicionMetricsCompact == compact)
                {
                    return;
                }

                _lastExpedicionMetricsCompact = compact;
            }

            ApplyMetricCardsLayout(grid, compact);
        }
    }

    private static void ApplyMetricCardsLayout(Grid grid, bool compact)
    {
        SetGridColumns(
            grid,
            new GridLength(1, GridUnitType.Star),
            new GridLength(1, GridUnitType.Star),
            compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star),
            compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star));
        SetGridRows(grid, GridLength.Auto, compact ? GridLength.Auto : new GridLength(0));

        var cards = grid.Children.OfType<FrameworkElement>().Take(4).ToArray();
        for (var index = 0; index < cards.Length; index++)
        {
            var row = compact && index >= 2 ? 1 : 0;
            var column = compact ? index % 2 : index;
            SetGridPosition(cards[index], row, column);
            cards[index].MinWidth = 180;
            cards[index].HorizontalAlignment = HorizontalAlignment.Stretch;
            cards[index].VerticalAlignment = VerticalAlignment.Stretch;
        }
    }

    private static void SetGridColumns(Grid grid, params GridLength[] widths)
    {
        while (grid.ColumnDefinitions.Count < widths.Length)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var index = 0; index < grid.ColumnDefinitions.Count; index++)
        {
            var targetWidth = index < widths.Length ? widths[index] : new GridLength(0);
            if (grid.ColumnDefinitions[index].Width != targetWidth)
            {
                grid.ColumnDefinitions[index].Width = targetWidth;
            }
        }
    }

    private static void SetGridRows(Grid grid, params GridLength[] heights)
    {
        while (grid.RowDefinitions.Count < heights.Length)
        {
            grid.RowDefinitions.Add(new RowDefinition());
        }

        for (var index = 0; index < grid.RowDefinitions.Count; index++)
        {
            var targetHeight = index < heights.Length ? heights[index] : new GridLength(0);
            if (grid.RowDefinitions[index].Height != targetHeight)
            {
                grid.RowDefinitions[index].Height = targetHeight;
            }
        }
    }

    private static void SetGridPosition(FrameworkElement element, int row, int column, int columnSpan = 1)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            _ = SelectSectionWithTransitionAsync(tag);
        }
    }

    private void CollapsedSideNav_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ShowExpandedSideNav();
    }

    private void ExpandedSideNav_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ShowExpandedSideNav();
    }

    private void ExpandedSideNav_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        HideExpandedSideNav();
    }

    private void SideNavDimOverlay_Tapped(object sender, TappedRoutedEventArgs e)
    {
        HideExpandedSideNav();
        e.Handled = true;
    }

    private void ShowExpandedSideNav()
    {
        if (_isSideNavOpen && ExpandedSideNav.Visibility == Visibility.Visible)
        {
            return;
        }

        _isSideNavOpen = true;
        ExpandedSideNav.Visibility = Visibility.Visible;
        SideNavDimOverlay.Visibility = Visibility.Visible;
        AnimateSideNav(ExpandedSideNavOpenOffset, 1, 1, Colors.White);
    }

    private void HideExpandedSideNav()
    {
        if (!_isSideNavOpen && ExpandedSideNav.Visibility != Visibility.Visible)
        {
            return;
        }

        _isSideNavOpen = false;
        AnimateSideNav(ExpandedSideNavClosedOffset, 0, 0, Colors.White, collapseWhenCompleted: true);
    }

    private void CloseSideNavStoryboard_Completed(object? sender, object e)
    {
        if (_isSideNavOpen)
        {
            return;
        }

        ExpandedSideNav.Visibility = Visibility.Collapsed;
        SideNavDimOverlay.Visibility = Visibility.Collapsed;
    }

    private void AnimateSideNav(
        double targetOffset,
        double targetContentOpacity,
        double targetOverlayOpacity,
        Windows.UI.Color targetBackground,
        bool collapseWhenCompleted = false)
    {
        _sideNavStoryboard?.Stop();

        var storyboard = new Storyboard();
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var slideAnimation = new DoubleAnimation
        {
            From = ExpandedSideNavTransform.TranslateX,
            To = targetOffset,
            Duration = SideNavAnimationDuration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(slideAnimation, ExpandedSideNavTransform);
        Storyboard.SetTargetProperty(slideAnimation, nameof(CompositeTransform.TranslateX));
        storyboard.Children.Add(slideAnimation);

        var contentAnimation = new DoubleAnimation
        {
            From = ExpandedSideNavContent.Opacity,
            To = targetContentOpacity,
            Duration = SideNavAnimationDuration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(contentAnimation, ExpandedSideNavContent);
        Storyboard.SetTargetProperty(contentAnimation, nameof(UIElement.Opacity));
        storyboard.Children.Add(contentAnimation);

        var overlayAnimation = new DoubleAnimation
        {
            From = SideNavDimOverlay.Opacity,
            To = targetOverlayOpacity,
            Duration = SideNavAnimationDuration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(overlayAnimation, SideNavDimOverlay);
        Storyboard.SetTargetProperty(overlayAnimation, nameof(UIElement.Opacity));
        storyboard.Children.Add(overlayAnimation);

        var backgroundAnimation = new ColorAnimation
        {
            From = ExpandedSideNavBackgroundBrush.Color,
            To = targetBackground,
            Duration = SideNavAnimationDuration,
        };
        Storyboard.SetTarget(backgroundAnimation, ExpandedSideNavBackgroundBrush);
        Storyboard.SetTargetProperty(backgroundAnimation, nameof(SolidColorBrush.Color));
        storyboard.Children.Add(backgroundAnimation);

        storyboard.Completed += (_, _) =>
        {
            ExpandedSideNavTransform.TranslateX = targetOffset;
            ExpandedSideNavContent.Opacity = targetContentOpacity;
            SideNavDimOverlay.Opacity = targetOverlayOpacity;
            ExpandedSideNavBackgroundBrush.Color = targetBackground;

            if (collapseWhenCompleted && !_isSideNavOpen)
            {
                ExpandedSideNav.Visibility = Visibility.Collapsed;
                SideNavDimOverlay.Visibility = Visibility.Collapsed;
            }
        };

        _sideNavStoryboard = storyboard;
        storyboard.Begin();
    }

    private void SideNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            _ = SelectSectionWithTransitionAsync(tag);
            HideExpandedSideNav();
        }
    }

    private async Task SelectSectionWithTransitionAsync(string? tag)
    {
        var section = NormalizeSectionTag(tag);
        if (section is null)
        {
            return;
        }

        var version = ++_moduleTransitionVersion;
        RootGrid.IsHitTestVisible = false;

        try
        {
            var isActiveSection = IsActiveSection(section);
            _moduleTransitionCompletion?.TrySetResult(false);
            _moduleTransitionStoryboard?.Stop();

            if (!isActiveSection)
            {
                ViewModel.SelectSection(section);
                RootGrid.Opacity = 0;
                ModuleSurfaceTransform.TranslateX = 18;
            }
            else
            {
                if (section == "Transito")
                {
                    ViewModel.RefreshTransitPresentation();
                }

                RootGrid.Opacity = 0.86;
                ModuleSurfaceTransform.TranslateX = 8;
            }

            await AnimateModuleTransitionAsync(targetOpacity: 1, targetOffset: 0, durationMilliseconds: 210);
            if (version != _moduleTransitionVersion)
            {
                return;
            }

            RootGrid.Opacity = 1;
            ModuleSurfaceTransform.TranslateX = 0;
        }
        finally
        {
            if (version == _moduleTransitionVersion)
            {
                RootGrid.IsHitTestVisible = true;
            }
        }
    }

    private static string? NormalizeSectionTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        return string.Equals(tag, "Tabla", StringComparison.OrdinalIgnoreCase)
            ? "Traslados"
            : tag;
    }

    private bool IsActiveSection(string section) =>
        section switch
        {
            "Traslados" => ViewModel.ShowTransferView,
            "Expediciones" => ViewModel.ShowExpedicionesView,
            "Transito" => ViewModel.ShowTransitView,
            "Comparativa" => ViewModel.ShowWarehouseComparisonView,
            "Simulacion" => ViewModel.ShowSimulacionView,
            _ => false,
        };

    private Task AnimateModuleTransitionAsync(double targetOpacity, double targetOffset, int durationMilliseconds)
    {
        _moduleTransitionCompletion?.TrySetResult(false);
        _moduleTransitionStoryboard?.Stop();

        var storyboard = new Storyboard();
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(durationMilliseconds));
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var opacityAnimation = new DoubleAnimation
        {
            From = RootGrid.Opacity,
            To = targetOpacity,
            Duration = duration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(opacityAnimation, RootGrid);
        Storyboard.SetTargetProperty(opacityAnimation, nameof(UIElement.Opacity));
        storyboard.Children.Add(opacityAnimation);

        var slideAnimation = new DoubleAnimation
        {
            From = ModuleSurfaceTransform.TranslateX,
            To = targetOffset,
            Duration = duration,
            EasingFunction = easing,
        };
        Storyboard.SetTarget(slideAnimation, ModuleSurfaceTransform);
        Storyboard.SetTargetProperty(slideAnimation, nameof(CompositeTransform.TranslateX));
        storyboard.Children.Add(slideAnimation);

        storyboard.Completed += (_, _) =>
        {
            RootGrid.Opacity = targetOpacity;
            ModuleSurfaceTransform.TranslateX = targetOffset;
            if (ReferenceEquals(_moduleTransitionCompletion, completion))
            {
                _moduleTransitionCompletion = null;
                _moduleTransitionStoryboard = null;
            }

            completion.TrySetResult(true);
        };

        _moduleTransitionCompletion = completion;
        _moduleTransitionStoryboard = storyboard;
        storyboard.Begin();
        return completion.Task;
    }

    private void TransferTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            ViewModel.SetTransferTab(tag);
        }
    }

    private void ExpedicionTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            ViewModel.SetExpedicionTab(tag);
        }
    }

    private void TransitStatusTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            ViewModel.SetTransitStatusTab(tag);
        }
    }

    private void TransferRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TransferRowViewModel row })
        {
            ViewModel.SelectedTransferRow = row;
            e.Handled = true;
        }
    }

    private void CloseTransferPalletPane_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTransferRow = null;
    }

    private void TransferPalletOverlayBackground_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.SelectedTransferRow = null;
    }

    private void TransferPalletPane_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void SelectAllTransferPallets_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TransferRowViewModel row })
        {
            row.SelectAllPallets();
        }
    }

    private void ExpedicionRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExpedicionRowViewModel row })
        {
            ViewModel.SelectedExpedicionRow = row;
            e.Handled = true;
        }
    }

    private void SelectAllExpedicionesOrders_Click(object sender, RoutedEventArgs e)
    {
        // Actua sobre las filas visibles: si quedo una busqueda escrita sin
        // aplicar (menos de 220 ms), se aplica antes; si no, marcaria todas las
        // de la pestana en vez de las buscadas.
        ViewModel.SubmitSearchCommand.Execute(null);

        foreach (var row in ViewModel.ExpedicionRows.Where(row => row.CanOrder && row.SelectedPallets.Count > 0))
        {
            row.IsOrdered = true;
        }
    }

    private void CloseExpedicionPalletPane_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedExpedicionRow = null;
    }

    private void ExpedicionPalletOverlayBackground_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.SelectedExpedicionRow = null;
    }

    private void ExpedicionPalletPane_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void SelectCompany_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string company })
        {
            ViewModel.SelectCompany(company);
        }
    }

    private async void Help_Click(object sender, RoutedEventArgs e)
    {
        var body = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text =
                "Flujo de abasto:\n\n" +
                "1. Elige la empresa (EPA o Cofersa) en la barra superior.\n" +
                "2. Carga Inventario y Forecast; el analisis corre automaticamente.\n" +
                "3. Traslados: que traer de bodega externa para cubrir lo que no alcanza el principal.\n" +
                "4. Expediciones: lo mismo pero segun el reporte de expediciones.\n" +
                "5. Comparativa e histograma: inventario OLO contra SERVICA, semanas que alcanza y distribucion de cobertura.\n" +
                "6. Simulacion: proyeccion de demanda y carga de traslado por horizonte y buffer.\n\n" +
                "Ajustes: umbrales, zonas externas, alistado satelital y tamanos de tarima por empresa.",
        };

        var dialog = new ContentDialog
        {
            Title = "Ayuda",
            Content = body,
            CloseButtonText = "Cerrar",
            XamlRoot = XamlRoot,
            RequestedTheme = App.CurrentTheme,
        };

        await dialog.ShowAsync();
    }

    private async void ConfirmTransitPallet_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TransitRowViewModel row })
        {
            await ViewModel.ConfirmTransitPalletAsync(row);
        }
    }

    private async void ConfirmTransitArticle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TransitRowViewModel row })
        {
            await ViewModel.ConfirmTransitArticleAsync(row);
        }
    }

    /// <summary>Copies the right-clicked list row to the clipboard as tab-separated text.</summary>
    private void CopyRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: IClipboardRow row })
        {
            SafeClipboard.TrySetText(row.ToClipboardText());
        }
    }

    private void CopyExpeditionsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExpedicionRowViewModel row })
        {
            SafeClipboard.TrySetText(row.ExpeditionListMultilineText);
        }
    }

    private void CopyExpeditionText_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.Tag is string text && !string.IsNullOrWhiteSpace(text))
        {
            SafeClipboard.TrySetText(text);
        }

        HideContainingFlyout(element);
    }

    private static void HideContainingFlyout(FrameworkElement sourceElement)
    {
        if (sourceElement.XamlRoot is { } xamlRoot)
        {
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
            {
                if (popup.IsOpen && popup.Child is not null && VisualSubtreeContains(popup.Child, sourceElement))
                {
                    popup.IsOpen = false;
                    return;
                }
            }
        }

        DependencyObject? current = sourceElement;
        while (current is not null)
        {
            if (current is Popup popup)
            {
                popup.IsOpen = false;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private static bool VisualSubtreeContains(DependencyObject root, DependencyObject target)
    {
        if (ReferenceEquals(root, target))
        {
            return true;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            if (VisualSubtreeContains(VisualTreeHelper.GetChild(root, index), target))
            {
                return true;
            }
        }

        return false;
    }
}
