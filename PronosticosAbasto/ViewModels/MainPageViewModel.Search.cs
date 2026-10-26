using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PronosticosAbasto.Core.Tables;

namespace PronosticosAbasto.ViewModels;

/// <summary>
/// Buscador de cada modulo.
/// </summary>
/// <remarks>
/// <para>Tres cosas hacen que buscar sea rapido con 16k-34k filas:</para>
/// <list type="number">
/// <item>Se filtra cuando el operador deja de escribir (<see cref="SearchDebouncer"/>), no en cada tecla.</item>
/// <item>El texto buscable de cada fila se normaliza una sola vez (<see cref="SearchIndex{TRow}"/>).</item>
/// <item>Escribir en el buscador ya no reconstruye las listas de valores de los
/// filtros tipo Excel: esas listas dependen de la pestana y de los datos, no de
/// lo que se busca, y rehacerlas en cada tecla (13 columnas, miles de valores
/// cada una) era lo mas caro de todo.</item>
/// </list>
/// <para>
/// Las reglas de la consulta (palabras en cualquier orden, sin tildes, listas de
/// codigos pegadas) estan en <see cref="SearchQuery"/>.
/// </para>
/// </remarks>
public partial class MainPageViewModel
{
    // Se indexan identificadores, no textos formateados: los textos de pallets
    // y zonas traen fechas, cantidades y "Sin ..." que harian que "2026" o "200"
    // encontraran casi todo. Son campos inmutables de cada fila, asi que la cache
    // por fila no se desactualiza; la excepcion es el comentario de Comparativa,
    // que se invalida a mano al editarlo o borrarlo.
    private readonly SearchIndex<TransferRowViewModel> _transferSearchIndex = new(row =>
        [row.Article, row.Description, .. PalletIdentifiers(row.PalletOptions)]);

    private readonly SearchIndex<ExpedicionRowViewModel> _expedicionSearchIndex = new(row =>
        [row.ExpeditionNumber, row.Article, row.Description, .. PalletIdentifiers(row.PalletOptions)]);

    private readonly SearchIndex<TransitRowViewModel> _transitSearchIndex = new(row =>
        [row.Article, row.Description, row.Pallet, row.Location, row.StorageZone, row.Origin, row.StatusLabel]);

    // Solo el comentario visible: el automatico queda escondido cuando hay uno
    // manual y no deberia seguir encontrando la fila.
    private readonly SearchIndex<WarehouseComparisonRowViewModel> _warehouseComparisonSearchIndex = new(row =>
        [row.Article, row.Description, row.CommentText]);

    private SearchDebouncer _transferSearch = null!;
    private SearchDebouncer _expedicionSearch = null!;
    private SearchDebouncer _transitSearch = null!;
    private SearchDebouncer _warehouseComparisonSearch = null!;

    [ObservableProperty]
    public partial string WarehouseComparisonSearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SearchResultSummary TransferSearchSummary { get; set; } = SearchResultSummary.None;

    [ObservableProperty]
    public partial SearchResultSummary ExpedicionSearchSummary { get; set; } = SearchResultSummary.None;

    [ObservableProperty]
    public partial SearchResultSummary TransitSearchSummary { get; set; } = SearchResultSummary.None;

    [ObservableProperty]
    public partial SearchResultSummary WarehouseComparisonSearchSummary { get; set; } = SearchResultSummary.None;

    /// <summary>Se llama desde el constructor, en el hilo de UI (los temporizadores lo necesitan).</summary>
    private void InitializeSearch()
    {
        _transferSearch = new SearchDebouncer(() => FilterTransferRows(refreshOptions: false));
        _expedicionSearch = new SearchDebouncer(() => FilterExpedicionRows(refreshOptions: false));
        _transitSearch = new SearchDebouncer(FilterTransitRows);
        _warehouseComparisonSearch = new SearchDebouncer(() => FilterWarehouseComparisonRows(refreshOptions: false));
    }

    partial void OnTransferSearchTextChanged(string value) => _transferSearch.Schedule(value);

    partial void OnExpedicionSearchTextChanged(string value) => _expedicionSearch.Schedule(value);

    partial void OnTransitSearchTextChanged(string value) => _transitSearch.Schedule(value);

    partial void OnWarehouseComparisonSearchTextChanged(string value)
    {
        // "Limpiar filtros" se habilita tambien por la busqueda.
        OnPropertyChanged(nameof(HasWarehouseComparisonFilters));
        ClearWarehouseComparisonFiltersCommand.NotifyCanExecuteChanged();
        _warehouseComparisonSearch.Schedule(value);
    }

    /// <summary>Enter en cualquier buscador: aplica ya lo que este esperando.</summary>
    [RelayCommand]
    private void SubmitSearch()
    {
        _transferSearch.Flush();
        _expedicionSearch.Flush();
        _transitSearch.Flush();
        _warehouseComparisonSearch.Flush();
    }

    /// <summary>
    /// Enter, o un comando que actua sobre las filas visibles (confirmar
    /// visibles, exportar): si quedo una busqueda escrita sin aplicar, se aplica
    /// antes. Si no, "Confirmar visible" a los 200 ms de pegar un pallet
    /// confirmaria todos los pendientes en vez de ese.
    /// </summary>
    private void FlushPendingSearches() => SubmitSearch();

    /// <summary>Al vaciarse los datos de un modulo, su resumen de busqueda ya no describe nada.</summary>
    private static void ResetSearchState(SearchDebouncer debouncer, Action<SearchResultSummary> setSummary)
    {
        debouncer.Cancel();
        setSummary(SearchResultSummary.None);
    }

    private static IEnumerable<string?> PalletIdentifiers(IEnumerable<PalletSelectionViewModel> pallets) =>
        pallets
            .SelectMany(pallet => new[] { pallet.Pallet, pallet.StorageZone, pallet.Location })
            .Where(value => !string.Equals(value, "Sin ubicacion", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Filtra por el buscador y deja el resumen de lo que no aparecio.
    /// </summary>
    /// <remarks>
    /// El resumen se calcula sobre las filas de la pestana actual, antes de los
    /// filtros de columna: si el operador pego 20 codigos y 3 no estan, lo que
    /// importa es que no estan en esta pestana, no que un filtro de columna los
    /// oculte.
    /// </remarks>
    private static IEnumerable<TRow> ApplySearch<TRow>(
        SearchIndex<TRow> index,
        IReadOnlyList<TRow> tabRows,
        string? searchText,
        out SearchResultSummary summary)
        where TRow : class
    {
        var result = index.Search(tabRows, SearchQuery.Parse(searchText));
        summary = SearchResultSummary.From(result.AppliedQuery.AlternativeCount, result.Unmatched);
        return result.Rows;
    }
}
