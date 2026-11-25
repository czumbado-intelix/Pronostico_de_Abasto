import writeXlsxFile, { type Cell, type Row } from 'write-excel-file/browser'
import { buildArticlePalletKey, normalizeArticleKey, resolveStorageOrigin } from './normalization'
import type {
  ArticleAnalysisResult,
  CatalogArticle,
  CoverageStatus,
  ExpeditionLineResult,
  PalletInventoryDetail,
  TarimaSizeEntry,
  TransferRecommendationState,
  WarehouseComparisonHistogramBucket,
  WarehouseComparisonRow,
  WeeklyAnalysisResult,
} from './types'

type TransferLikeRow = ArticleAnalysisResult | ExpeditionLineResult

export interface TransferWorkbookExportRow {
  row: TransferLikeRow
  pallets: PalletInventoryDetail[]
  ordered: boolean
}

export async function exportAnalysisWorkbook(args: {
  company: string
  result: WeeklyAnalysisResult
  preparedArticles?: CatalogArticle[]
  tarimaSizes?: TarimaSizeEntry[]
  fileName?: string
}): Promise<void> {
  const preparedSet = new Set((args.preparedArticles ?? []).map((item) => normalizeArticleKey(item.article).toUpperCase()))
  const tarimaMap = new Map(
    (args.tarimaSizes ?? []).map((item) => [normalizeArticleKey(item.article).toUpperCase(), item.sizeCode]),
  )
  const horizonLabel = `${args.result.selectedPeriod} a ${args.result.horizonEndPeriod}`

  await writeXlsxFile([
    {
      sheet: 'Resumen',
      data: [
        [headerCell('Empresa'), text(args.company)],
        [headerCell('Semana inicio'), text(args.result.selectedPeriod)],
        [headerCell('Semana fin'), text(args.result.horizonEndPeriod)],
        [headerCell('Horizonte solicitado'), text(`${args.result.requestedPeriods} periodos`)],
        [headerCell('Horizonte analizado'), text(`${args.result.analyzedPeriods} periodos`)],
        [headerCell('Horizonte incompleto'), text(args.result.isIncompleteHorizon ? 'Si' : 'No')],
        [headerCell('Rojos principales'), number(args.result.criticalCount)],
        [headerCell('Amarillos principales'), number(args.result.warningCount)],
        [headerCell('Verdes principales'), number(args.result.healthyCount)],
        [headerCell('Rojos satelitales'), number(args.result.satelliteCriticalCount)],
        [headerCell('Amarillos satelitales'), number(args.result.satelliteWarningCount)],
        [headerCell('Verdes satelitales'), number(args.result.satelliteHealthyCount)],
      ],
    },
    {
      sheet: 'Resultado',
      data: [
        headerRow([
          'Articulo',
          'Alistado satelital',
          'Tamano tarima',
          'Inventario principal',
          'Inventario satelital',
          'Inventario total',
          `Forecast ${horizonLabel}`,
          'Diferencia principal',
          'Estado principal',
          'Diferencia satelital',
          'Estado satelital',
          'Traslado sugerido',
          'Faltante restante',
          'Estado de traslado',
        ]),
        ...args.result.articles.map((row) => [
          text(row.article),
          text(preparedSet.has(normalizeArticleKey(row.article).toUpperCase()) ? 'Si' : 'No'),
          text(tarimaMap.get(normalizeArticleKey(row.article).toUpperCase()) ?? ''),
          number(row.principalInventoryQuantity),
          number(row.satelliteInventoryQuantity),
          number(row.totalInventoryQuantity),
          number(row.forecastQuantity),
          number(row.difference),
          text(toSpanishStatus(row.status)),
          number(row.satelliteDifference),
          text(toSpanishStatus(row.satelliteStatus)),
          number(row.transferSuggestionQuantity),
          number(row.remainingShortageAfterTransfer),
          text(toSpanishTransferState(row.transferRecommendationState)),
        ]),
      ],
    },
    {
      sheet: 'Detalle por zonas',
      data: [
        headerRow(['Articulo', 'Zona de almacenaje', 'Cantidad', 'Tipo de zona']),
        ...args.result.articles.flatMap((article) =>
          article.zones.map((zone) => [
            text(article.article),
            text(zone.storageZone),
            number(zone.quantity),
            text(zone.isSatellite ? 'Satelital' : 'Principal'),
          ]),
        ),
      ],
    },
  ]).toFile(args.fileName ?? 'analisis-pronostico.xlsx')
}

export async function exportTransferWorkbook(args: {
  company: string
  periodLabel: string
  rows: TransferWorkbookExportRow[]
  preparedArticles?: CatalogArticle[]
  tarimaSizes?: TarimaSizeEntry[]
  pendingPalletKeys?: Set<string>
  fileName?: string
}): Promise<void> {
  const preparedSet = new Set((args.preparedArticles ?? []).map((item) => normalizeArticleKey(item.article).toUpperCase()))
  const tarimaMap = new Map(
    (args.tarimaSizes ?? []).map((item) => [normalizeArticleKey(item.article).toUpperCase(), item.sizeCode]),
  )
  const regular = args.rows.filter((item) => !preparedSet.has(normalizeArticleKey(item.row.article).toUpperCase()))
  const prepared = args.rows.filter((item) => preparedSet.has(normalizeArticleKey(item.row.article).toUpperCase()))

  const sheets = [
    {
      sheet: 'Requisicion',
      data: [
        ...metadataRows(args.company, args.periodLabel),
        headerRow([
          'Articulo',
          'Descripcion',
          'Alistado satelital',
          'Tamano tarima',
          'Disponible externas',
          'Transito pendiente',
          'Necesidad calculada',
          'Cantidad a traer',
          'Sobrante tarima',
          'Cantidad palets',
          'Palets FIFO',
          'Ubicaciones',
          'Zonas externas',
          'Pendiente',
          'Estado transito',
          'Pedido',
        ]),
        ...regular
          .filter((item) => item.pallets.length > 0)
          .map((item) => transferSummaryRow(item, preparedSet, tarimaMap, args.pendingPalletKeys)),
      ],
    },
    {
      sheet: 'Alistado satelital',
      data: [
        ...metadataRows(args.company, args.periodLabel),
        headerRow([
          'Articulo',
          'Descripcion',
          'Tamano tarima',
          'Disponible externas',
          'Transito pendiente',
          'Cantidad calculada',
          'Cantidad palets',
          'Palets FIFO',
          'Ubicaciones',
          'Zonas externas',
          'Pendiente',
          'Estado transito',
        ]),
        ...prepared.map((item) => preparedSummaryRow(item, tarimaMap, args.pendingPalletKeys)),
      ],
    },
    buildZoneDetailSheet(args.rows),
    ...buildOriginSheets(regular, tarimaMap, args.pendingPalletKeys),
  ]

  await writeXlsxFile(sheets).toFile(args.fileName ?? 'requisicion-traslados.xlsx')
}

export async function exportExpedicionesWorkbook(args: {
  company: string
  rows: TransferWorkbookExportRow[]
  tarimaSizes?: TarimaSizeEntry[]
  fileName?: string
}): Promise<void> {
  const tarimaMap = new Map(
    (args.tarimaSizes ?? []).map((item) => [normalizeArticleKey(item.article).toUpperCase(), item.sizeCode]),
  )

  await writeXlsxFile([
    {
      sheet: 'Expediciones',
      data: [
        headerRow(['Articulo', 'Descripcion', 'Pallet', 'Solicitud', 'Ubicacion', 'Zona', 'Tamano tarima']),
        ...args.rows.flatMap((item) =>
          item.pallets.map((pallet) => [
            text(item.row.article),
            text(item.row.description),
            text(pallet.pallet),
            number(pallet.quantity),
            text(pallet.location || 'Sin ubicacion'),
            text(pallet.storageZone),
            text(tarimaMap.get(normalizeArticleKey(item.row.article).toUpperCase()) ?? ''),
          ]),
        ),
      ],
    },
  ]).toFile(args.fileName ?? 'expediciones-marcadas.xlsx')
}

export async function exportTransferRows(
  rows: Array<ArticleAnalysisResult | ExpeditionLineResult>,
  fileName = 'requisicion-traslados.xlsx',
): Promise<void> {
  await exportTransferWorkbook({
    company: '',
    periodLabel: '',
    rows: rows.map((row) => ({ row, pallets: row.suggestedPallets, ordered: false })),
    fileName,
  })
}

export async function exportWarehouseComparisonWorkbook(args: {
  company: string
  periodLabel: string
  rows: WarehouseComparisonRow[]
  histogram: WarehouseComparisonHistogramBucket[]
  comments: Record<string, string>
  fileName?: string
}): Promise<void> {
  await writeXlsxFile([
    {
      sheet: 'Comparativa',
      data: [
        ...metadataRows(args.company, args.periodLabel),
        headerRow(['Articulo', 'Descripcion', 'Inv OLO', 'Inv Servica', 'Semanas OLO', 'Comentarios']),
        ...args.rows.map((row) => [
          text(row.article),
          text(row.description),
          number(row.oloQuantity),
          number(row.servicaQuantity),
          row.coveragePeriods === undefined ? text('—') : number(row.coveragePeriods),
          text(comparisonCommentForArticle(args.comments, row.article) || row.automaticComment),
        ]),
      ],
    },
    {
      sheet: 'Histograma',
      data: [
        [titleCell('Reporte de Distribucion de Articulos por Periodo'), text(''), text('')],
        [],
        headerRow(['Periodo', 'Cantidad de articulos', '% del total']),
        ...args.histogram.map((bucket) => [
          text(bucket.label),
          number(bucket.articleCount),
          number(bucket.percentage / 100),
        ]),
        [
          headerCell('Total'),
          number(args.histogram.reduce((sum, bucket) => sum + bucket.articleCount, 0)),
          number(args.histogram.reduce((sum, bucket) => sum + bucket.percentage, 0) / 100),
        ],
      ],
    },
  ]).toFile(args.fileName ?? 'comparativa-histograma.xlsx')
}

function transferSummaryRow(
  item: TransferWorkbookExportRow,
  preparedSet: Set<string>,
  tarimaMap: Map<string, string>,
  pendingPalletKeys?: Set<string>,
): Row {
  const row = item.row
  const transferQuantity = item.pallets.reduce((sum, pallet) => sum + pallet.quantity, 0)
  return [
    text(row.article),
    text(row.description),
    text(preparedSet.has(normalizeArticleKey(row.article).toUpperCase()) ? 'Si' : 'No'),
    text(tarimaMap.get(normalizeArticleKey(row.article).toUpperCase()) ?? ''),
    number(row.satelliteInventoryQuantity),
    number(row.pendingTransitQuantity),
    number(row.calculatedTransferNeedQuantity),
    number(transferQuantity),
    number(Math.max(0, transferQuantity - row.calculatedTransferNeedQuantity)),
    number(item.pallets.length),
    text(describePallets(item.pallets)),
    text(describeLocations(item.pallets)),
    text(describeZones(row)),
    number(Math.max(0, row.calculatedTransferNeedQuantity - transferQuantity)),
    text(transitState(row.article, item.pallets, pendingPalletKeys)),
    text(item.ordered ? 'Si' : 'No'),
  ]
}

function preparedSummaryRow(
  item: TransferWorkbookExportRow,
  tarimaMap: Map<string, string>,
  pendingPalletKeys?: Set<string>,
): Row {
  const row = item.row
  const transferQuantity = item.pallets.reduce((sum, pallet) => sum + pallet.quantity, 0)
  return [
    text(row.article),
    text(row.description),
    text(tarimaMap.get(normalizeArticleKey(row.article).toUpperCase()) ?? ''),
    number(row.satelliteInventoryQuantity),
    number(row.pendingTransitQuantity),
    number(row.transferSuggestionQuantity),
    number(item.pallets.length),
    text(describePallets(item.pallets)),
    text(describeLocations(item.pallets)),
    text(describeZones(row)),
    number(Math.max(0, row.calculatedTransferNeedQuantity - transferQuantity)),
    text(transitState(row.article, item.pallets, pendingPalletKeys)),
  ]
}

function buildZoneDetailSheet(rows: TransferWorkbookExportRow[]): { sheet: string; data: Row[] } {
  return {
    sheet: 'Detalle por zonas',
    data: [
      headerRow(['Articulo', 'Descripcion', 'Zona', 'Cantidad', 'Palets seleccionados']),
      ...rows.flatMap((item) => {
        const zones = item.row.satelliteZones.filter((zone) => zone.quantity > 0)
        const selectedPallets = item.pallets.map((pallet) => pallet.pallet).filter(Boolean).join(', ')
        if (zones.length === 0) {
          return [[
            text(item.row.article),
            text(item.row.description),
            text('Sin zona'),
            number(0),
            text(selectedPallets),
          ]]
        }
        return zones.map((zone) => [
          text(item.row.article),
          text(item.row.description),
          text(zone.storageZone),
          number(zone.quantity),
          text(selectedPallets),
        ])
      }),
    ],
  }
}

function buildOriginSheets(
  rows: TransferWorkbookExportRow[],
  tarimaMap: Map<string, string>,
  pendingPalletKeys?: Set<string>,
): Array<{ sheet: string; data: Row[] }> {
  const origins = ['SERVICA', 'COCO', 'OTRAS']
  return origins.map((origin) => ({
    sheet: origin,
    data: [
      headerRow([
        'Articulo',
        'Descripcion',
        'Pallet',
        'Cantidad',
        'Ubicacion',
        'Zona',
        'Fecha validacion',
        'Tamano tarima',
        'Cantidad palets',
        'Estado transito',
      ]),
      ...rows.flatMap((item) =>
        sortOriginPallets(item.pallets)
          .filter((pallet) => resolveStorageOrigin(pallet.storageZone) === origin)
          .map((pallet) => [
            text(item.row.article),
            text(item.row.description),
            text(pallet.pallet),
            number(pallet.quantity),
            text(pallet.location || 'Sin ubicacion'),
            text(pallet.storageZone),
            text(pallet.validationDate ?? 'Sin fecha'),
            text(tarimaMap.get(normalizeArticleKey(item.row.article).toUpperCase()) ?? ''),
            number(1),
            text(transitState(item.row.article, [pallet], pendingPalletKeys)),
          ]),
      ),
    ],
  }))
}

function metadataRows(company: string, periodLabel: string): Row[] {
  if (!company && !periodLabel) {
    return []
  }
  return [
    [headerCell('Empresa'), text(company)],
    [headerCell('Periodo'), text(periodLabel)],
    [],
  ]
}

function headerRow(labels: string[]): Row {
  return labels.map(headerCell)
}

function headerCell(value: string): Cell {
  return {
    value,
    fontWeight: 'bold',
    textColor: '#ffffff',
    backgroundColor: '#00a88f',
  }
}

function titleCell(value: string): Cell {
  return {
    value,
    fontWeight: 'bold',
    textColor: '#ffffff',
    backgroundColor: '#00a88f',
  }
}

function text(value: string): Cell {
  return { value, type: String }
}

function number(value: number): Cell {
  return { value, type: Number, align: 'right' }
}

function describePallets(pallets: PalletInventoryDetail[]): string {
  return pallets
    .map(
      (pallet) =>
        `${pallet.validationDate ?? 'Sin fecha'} | ${pallet.pallet} | ${formatNumber(pallet.quantity)} | ${
          pallet.location || 'Sin ubicacion'
        } | ${pallet.storageZone} | ${resolveStorageOrigin(pallet.storageZone)}`,
    )
    .join('; ')
}

function describeLocations(pallets: PalletInventoryDetail[]): string {
  return [
    ...new Set(
      pallets
        .map((pallet) => pallet.location.trim() || 'Sin ubicacion')
        .filter(Boolean),
    ),
  ]
    .sort((a, b) => a.localeCompare(b))
    .join('; ')
}

function describeZones(row: TransferLikeRow): string {
  return row.satelliteZones
    .filter((zone) => zone.quantity > 0)
    .sort((a, b) => b.quantity - a.quantity)
    .map((zone) => `${zone.storageZone} (${formatNumber(zone.quantity)})`)
    .join('; ')
}

function transitState(article: string, pallets: PalletInventoryDetail[], pendingPalletKeys?: Set<string>): string {
  if (pallets.length === 0) {
    return 'Sin palets'
  }

  if (!pendingPalletKeys || pendingPalletKeys.size === 0) {
    return 'Nuevo'
  }

  const pendingCount = pallets.filter((pallet) =>
    pendingPalletKeys.has(buildArticlePalletKey(article, pallet.pallet)),
  ).length

  if (pendingCount === 0) {
    return 'Nuevo'
  }
  if (pendingCount === pallets.length) {
    return 'Pendiente'
  }
  return `Parcial (${formatNumber(pendingCount)}/${formatNumber(pallets.length)})`
}

function comparisonCommentForArticle(comments: Record<string, string>, article: string): string {
  return comments[article]?.trim() ?? comments[normalizeArticleKey(article)]?.trim() ?? ''
}

function sortOriginPallets(pallets: PalletInventoryDetail[]): PalletInventoryDetail[] {
  return [...pallets].sort((a, b) =>
    a.location.localeCompare(b.location) ||
    a.storageZone.localeCompare(b.storageZone) ||
    (a.validationDate ?? '').localeCompare(b.validationDate ?? '') ||
    a.pallet.localeCompare(b.pallet),
  )
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('es-CR', { maximumFractionDigits: 2 }).format(value)
}

function toSpanishStatus(status: CoverageStatus): string {
  return status === 'Critical' ? 'Rojo' : status === 'Warning' ? 'Amarillo' : 'Verde'
}

function toSpanishTransferState(state: TransferRecommendationState): string {
  return {
    Suggested: 'Sugerido',
    Partial: 'Parcial',
    Unavailable: 'Sin satelital',
    NotRequired: 'No requerido',
  }[state]
}
