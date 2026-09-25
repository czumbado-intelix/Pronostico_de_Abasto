using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PronosticosAbasto.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace PronosticosAbasto;

public sealed partial class MainPage : Page
{
    private const double NarrowLayoutThreshold = 900;
    private const double NavigationHoverOpenEdge = 84;
    private const double NavigationHoverCloseEdge = 316;
    private bool _isNarrowLayout;

    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
    }

    private sealed class TableColumnLayoutState
    {
        public List<int> Order { get; } = [];
        public Dictionary<int, double> Widths { get; } = [];
        public HashSet<int> InteractiveColumns { get; } = [];
        public List<WeakReference<Grid>> RegisteredGrids { get; } = [];
    }

    private const double ColumnResizeMinWidth = 48;
    private const double ColumnResizeMaxWidth = 720;
    private readonly Dictionary<string, TableColumnLayoutState> _tableColumnStates = [];

    private static readonly DependencyProperty OriginalColumnProperty =
        DependencyProperty.RegisterAttached("OriginalColumn", typeof(int), typeof(MainPage), new PropertyMetadata(-1));

    private static readonly DependencyProperty PreparedTableElementProperty =
        DependencyProperty.RegisterAttached("PreparedTableElement", typeof(bool), typeof(MainPage), new PropertyMetadata(false));

    private static readonly DependencyProperty ResizeHandleProperty =
        DependencyProperty.RegisterAttached("ResizeHandle", typeof(bool), typeof(MainPage), new PropertyMetadata(false));

    private void TableHeaderGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Grid headerGrid || headerGrid.Tag is not string tableKey)
        {
            return;
        }

        var state = GetOrCreateTableColumnState(tableKey, headerGrid);
        RegisterTableGrid(tableKey, headerGrid);
        PrepareHeaderGrid(tableKey, headerGrid, state);
        ApplyTableColumnLayout(tableKey);
    }

    private void TableRowGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Grid rowGrid || rowGrid.Tag is not string tableKey)
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
            state.Order.Add(column);
            state.Widths[column] = ReadColumnWidth(sourceGrid.ColumnDefinitions[column]);
        }

        _tableColumnStates[tableKey] = state;
        return state;
    }

    private static double ReadColumnWidth(ColumnDefinition column)
    {
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

    private void RegisterTableGrid(string tableKey, Grid grid)
    {
        var state = GetOrCreateTableColumnState(tableKey, grid);
        if (!state.RegisteredGrids.Any(reference => reference.TryGetTarget(out var existing) && ReferenceEquals(existing, grid)))
        {
            state.RegisteredGrids.Add(new WeakReference<Grid>(grid));
        }
    }

    private void PrepareHeaderGrid(string tableKey, Grid headerGrid, TableColumnLayoutState state)
    {
        RemoveResizeHandles(headerGrid);
        state.InteractiveColumns.Clear();

        foreach (var child in headerGrid.Children.OfType<FrameworkElement>().ToList())
        {
            if (GetResizeHandle(child) || child.Visibility != Visibility.Visible)
            {
                continue;
            }

            var originalColumn = GetElementOriginalColumn(child);
            if (originalColumn < 0 || !state.Widths.TryGetValue(originalColumn, out var width) || width <= 0)
            {
                continue;
            }

            state.InteractiveColumns.Add(originalColumn);
            child.CanDrag = true;
            child.AllowDrop = true;

            if (!GetPreparedTableElement(child))
            {
                child.DragStarting += TableColumn_DragStarting;
                child.DragOver += TableColumn_DragOver;
                child.Drop += TableColumn_Drop;
                child.SetValue(PreparedTableElementProperty, true);
            }
        }

        foreach (var originalColumn in state.InteractiveColumns.OrderBy(column => state.Order.IndexOf(column)))
        {
            var handle = new Thumb
            {
                Width = 10,
                MinHeight = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)),
                Tag = tableKey,
            };

            handle.SetValue(OriginalColumnProperty, originalColumn);
            handle.SetValue(ResizeHandleProperty, true);
            ToolTipService.SetToolTip(handle, "Ajustar ancho");
            handle.DragDelta += TableColumnResizeHandle_DragDelta;
            headerGrid.Children.Add(handle);
        }
    }

    private static void RemoveResizeHandles(Grid headerGrid)
    {
        for (var index = headerGrid.Children.Count - 1; index >= 0; index--)
        {
            if (headerGrid.Children[index] is DependencyObject child && GetResizeHandle(child))
            {
                headerGrid.Children.RemoveAt(index);
            }
        }
    }

    private void TableColumn_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is not FrameworkElement element ||
            TryFindTableHeader(element) is not { } headerGrid ||
            headerGrid.Tag is not string tableKey)
        {
            return;
        }

        var originalColumn = GetElementOriginalColumn(element);
        if (originalColumn < 0 ||
            !_tableColumnStates.TryGetValue(tableKey, out var state) ||
            !state.InteractiveColumns.Contains(originalColumn))
        {
            return;
        }

        args.Data.SetText($"{tableKey}:{originalColumn}");
        args.AllowedOperations = DataPackageOperation.Move;
    }

    private void TableColumn_DragOver(object sender, DragEventArgs args)
    {
        args.AcceptedOperation = DataPackageOperation.Move;
        args.Handled = true;
    }

    private async void TableColumn_Drop(object sender, DragEventArgs args)
    {
        if (sender is not FrameworkElement target ||
            TryFindTableHeader(target) is not { } headerGrid ||
            headerGrid.Tag is not string tableKey ||
            !_tableColumnStates.TryGetValue(tableKey, out var state))
        {
            return;
        }

        var targetColumn = GetElementOriginalColumn(target);
        if (targetColumn < 0 || !state.InteractiveColumns.Contains(targetColumn))
        {
            return;
        }

        var payload = await args.DataView.GetTextAsync();
        var parts = payload.Split(':', 2);
        if (parts.Length != 2 ||
            !string.Equals(parts[0], tableKey, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out var sourceColumn) ||
            sourceColumn == targetColumn ||
            !state.InteractiveColumns.Contains(sourceColumn))
        {
            return;
        }

        MoveColumnBefore(state, sourceColumn, targetColumn);
        ApplyTableColumnLayout(tableKey);
        args.Handled = true;
    }

    private static void MoveColumnBefore(TableColumnLayoutState state, int sourceColumn, int targetColumn)
    {
        var sourceIndex = state.Order.IndexOf(sourceColumn);
        var targetIndex = state.Order.IndexOf(targetColumn);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        state.Order.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        state.Order.Insert(Math.Max(0, targetIndex), sourceColumn);
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
        ApplyTableColumnLayout(tableKey);
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

        for (var slot = 0; slot < grid.ColumnDefinitions.Count && slot < state.Order.Count; slot++)
        {
            var originalColumn = state.Order[slot];
            var width = state.Widths.GetValueOrDefault(originalColumn, ColumnResizeMinWidth);
            grid.ColumnDefinitions[slot].Width = new GridLength(width);
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
            }
        }
    }

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

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string tag })
        {
            ViewModel.SelectSection(tag);
        }
    }

    private void Page_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this).Position;
        if (point.X <= NavigationHoverOpenEdge)
        {
            ExpandedSideNav.Visibility = Visibility.Visible;
        }
        else if (ExpandedSideNav.Visibility == Visibility.Visible && point.X >= NavigationHoverCloseEdge)
        {
            ExpandedSideNav.Visibility = Visibility.Collapsed;
        }
    }

    private void Page_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ExpandedSideNav.Visibility = Visibility.Collapsed;
    }

    private void SideNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            ViewModel.SelectSection(tag);
            ExpandedSideNav.Visibility = Visibility.Collapsed;
        }
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e) => App.ToggleTheme();

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

    private void TransferRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TransferRowViewModel row })
        {
            ViewModel.SelectedTransferRow = row;
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
            var data = new DataPackage();
            data.SetText(row.ToClipboardText());
            Clipboard.SetContent(data);
        }
    }
}
