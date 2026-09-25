using PronosticosAbasto.Core.Tables;

namespace PronosticosAbasto.ViewModels;

/// <summary>
/// Declaracion de las cuatro tablas filtrables de la app.
/// </summary>
/// <remarks>
/// <para>
/// Cada columna se declara una sola vez: su texto visible, su filtro y, si
/// corresponde, la clave numerica con la que ordena. Antes esto estaba repartido
/// en tres metodos por tabla —<c>Refresh*FilterOptions</c>, la cadena de
/// <c>Matches</c> dentro de <c>Filter*Rows</c> y un <c>switch</c> de claves en
/// <c>Apply*TextSort</c>— o sea doce metodos donde agregar una columna obligaba
/// a tocar tres lugares y olvidarse de uno no rompia la compilacion.
/// </para>
/// <para>
/// El texto visible es tambien lo que ve el filtro, asi que la lista de valores
/// del panel tipo Excel y lo que se compara siempre coinciden por construccion.
/// </para>
/// </remarks>
public partial class MainPageViewModel
{
    private TableView<ArticleResultRowViewModel> BuildTableColumns() =>
        new(
            new TableColumn<ArticleResultRowViewModel>("Article", row => row.ArticleDescriptionText, TableArticuloFilter),
            new TableColumn<ArticleResultRowViewModel>("Forecast", row => row.ForecastText, TableForecastFilter, row => row.ForecastQuantity),
            new TableColumn<ArticleResultRowViewModel>("Principal", row => row.PrincipalInventoryText, TablePrincipalFilter, row => row.PrincipalInventoryQuantity),
            new TableColumn<ArticleResultRowViewModel>("Difference", row => row.DifferenceText, TableDifferenceFilter, row => row.Difference),
            new TableColumn<ArticleResultRowViewModel>("Status", row => row.StatusLabel, TableStatusFilter),
            new TableColumn<ArticleResultRowViewModel>("Satellite", row => row.SatelliteInventoryText, TableSatelliteFilter, row => row.SatelliteInventoryQuantity));

    private TableView<TransferRowViewModel> BuildTransferColumns() =>
        new(
            new TableColumn<TransferRowViewModel>("Article", row => row.ArticleDescriptionText, TrasladosArticuloFilter),
            new TableColumn<TransferRowViewModel>("Description", row => row.Description, TrasladosDescripcionFilter),
            new TableColumn<TransferRowViewModel>("Coverage", row => row.CoverageText, TrasladosCoberturaFilter, row => row.CoverageWeeksValue),
            new TableColumn<TransferRowViewModel>("Principal", row => row.PrincipalInventoryText, TrasladosPrincipalFilter, row => row.PrincipalInventoryQuantity),
            new TableColumn<TransferRowViewModel>("StockExt", row => row.SatelliteAvailableText, TrasladosStockExtFilter, row => row.SatelliteAvailableQuantity),
            new TableColumn<TransferRowViewModel>("Transito", row => row.PendingTransitText, TrasladosTransitoFilter, row => row.PendingTransitQuantity),
            new TableColumn<TransferRowViewModel>("Traer", row => row.TransferSuggestionText, TrasladosTraerFilter, row => row.TransferSuggestionQuantity),
            new TableColumn<TransferRowViewModel>("PalletCount", row => row.SuggestedPalletCountText, TrasladosPalletCountFilter, row => row.SuggestedPalletCount),
            new TableColumn<TransferRowViewModel>("Pallets", row => row.SuggestedPalletNumbersText, TrasladosPaletsFilter),
            new TableColumn<TransferRowViewModel>("Tarima", row => row.TarimaSizeText, TrasladosTarimaFilter),
            new TableColumn<TransferRowViewModel>("Alistado", row => row.SatellitePreparedText, TrasladosAlistadoFilter),
            new TableColumn<TransferRowViewModel>("Zonas", row => row.SatelliteZonesText, TrasladosZonasFilter),
            new TableColumn<TransferRowViewModel>("Status", row => row.StatusLabel, TrasladosEstadoFilter));

    private TableView<ExpedicionRowViewModel> BuildExpedicionColumns() =>
        new(
            new TableColumn<ExpedicionRowViewModel>("Expedicion", row => row.ExpeditionNumber, ExpExpedicionFilter),
            new TableColumn<ExpedicionRowViewModel>("Article", row => row.ArticleDescriptionText, ExpArticuloFilter),
            new TableColumn<ExpedicionRowViewModel>("Description", row => row.Description, ExpDescripcionFilter),
            new TableColumn<ExpedicionRowViewModel>("Cantidad", row => row.DemandText, ExpCantidadFilter, row => row.DemandQuantity),
            new TableColumn<ExpedicionRowViewModel>("Principal", row => row.PrincipalInventoryText, ExpPrincipalFilter, row => row.PrincipalQuantity),
            new TableColumn<ExpedicionRowViewModel>("Externas", row => row.SatelliteAvailableText, ExpExternasFilter, row => row.SatelliteQuantity),
            new TableColumn<ExpedicionRowViewModel>("Transito", row => row.PendingTransitText, ExpTransitoFilter, row => row.PendingTransitQuantity),
            new TableColumn<ExpedicionRowViewModel>("Traer", row => row.TransferSuggestionText, ExpTraerFilter, row => row.TransferQuantity),
            new TableColumn<ExpedicionRowViewModel>("PalletCount", row => row.SuggestedPalletCountText, ExpPalletCountFilter, row => row.SuggestedPalletCount),
            new TableColumn<ExpedicionRowViewModel>("Pallets", row => row.SuggestedPalletNumbersText, ExpPaletsFilter),
            new TableColumn<ExpedicionRowViewModel>("Tarima", row => row.TarimaSizeText, ExpTarimaFilter),
            new TableColumn<ExpedicionRowViewModel>("Alistado", row => row.SatellitePreparedText, ExpAlistadoFilter),
            new TableColumn<ExpedicionRowViewModel>("Zonas", row => row.SatelliteZonesText, ExpZonasFilter),
            new TableColumn<ExpedicionRowViewModel>("Pendiente", row => row.RemainingShortageText, ExpPendienteFilter, row => row.RemainingShortageQuantity));

    private TableView<WarehouseComparisonRowViewModel> BuildWarehouseComparisonColumns() =>
        new(
            new TableColumn<WarehouseComparisonRowViewModel>("Article", row => row.ArticleDescriptionText, ComparisonArticuloFilter),
            new TableColumn<WarehouseComparisonRowViewModel>("Description", row => row.Description, ComparisonDescripcionFilter),
            new TableColumn<WarehouseComparisonRowViewModel>("Olo", row => row.OloQuantityText, ComparisonOloFilter, row => row.OloQuantity),
            new TableColumn<WarehouseComparisonRowViewModel>("Servica", row => row.ServicaQuantityText, ComparisonServicaFilter, row => row.ServicaQuantity),
            new TableColumn<WarehouseComparisonRowViewModel>("Coverage", row => row.CoverageText, ComparisonCoverageFilter, row => row.CoveragePeriods),
            new TableColumn<WarehouseComparisonRowViewModel>("Comments", row => row.CommentText, ComparisonComentariosFilter));
}
