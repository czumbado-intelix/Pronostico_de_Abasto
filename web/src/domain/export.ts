import writeXlsxFile, { type Cell, type Row } from 'write-excel-file/browser'
import { normalizeArticleKey, resolveStorageOrigin } from './normalization'
import type {
  ArticleAnalysisResult,
  CatalogArticle,
  ExpeditionLineResult,
  PalletInventoryDetail,
  TarimaSizeEntry,
  WarehouseComparisonHistogramBucket,
  WarehouseComparisonRow,
} from './types'

type TransferLikeRow = ArticleAnalysisResult | ExpeditionLineResult

export interface TransferWorkbookExportRow {
  row: TransferLikeRow
  pallets: PalletInventoryDetail[]
  ordered: boolean
}

export async function exportTransferWorkbook(args: {
  company: string
  periodLabel: string
  rows: TransferWorkbookExportRow[]
  preparedArticles?: CatalogArticle[]
  tarimaSizes?: TarimaSizeEntry[]
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
          'Pedido',
        ]),
        ...regular
          .filter((item) => item.pallets.length > 0)
          .map((item) => transferSummaryRow(item, preparedSet, tarimaMap)),
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
        ]),
        ...prepared.map((item) => preparedSummaryRow(item, tarimaMap)),
      ],
    },
    ...buildOriginSheets(regular, tarimaMap),
  ]

  await writeXlsxFile(sheets).toFile(args.fileName ?? 'requisicion-traslados.xlsx')
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
        headerRow(['Articulo', 'Descripcion', 'Inv OLO', 'Inv Servica', 'Forecast', 'Cobertura', 'Comentarios']),
        ...args.rows.map((row) => [
          text(row.article),
          text(row.description),
          number(row.oloQuantity),
          number(row.servicaQuantity),
          number(row.forecastQuantity),
          row.coveragePeriods === undefined ? text('-') : number(row.coveragePeriods),
          text(args.comments[row.article] || row.automaticComment),
        ]),
      ],
    },
    {
      sheet: 'Histograma',
      data: [
        headerRow(['Periodo', 'Cantidad de articulos', '% del total']),
        ...args.histogram.map((bucket) => [
          text(bucket.label),
          number(bucket.articleCount),
          number(bucket.percentage / 100),
        ]),
      ],
    },
  ]).toFile(args.fileName ?? 'comparativa-histograma.xlsx')
}

function transferSummaryRow(
  item: TransferWorkbookExportRow,
  preparedSet: Set<string>,
  tarimaMap: Map<string, string>,
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
    text(item.ordered ? 'Si' : 'No'),
  ]
}

function preparedSummaryRow(item: TransferWorkbookExportRow, tarimaMap: Map<string, string>): Row {
  const row = item.row
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
    number(row.remainingShortageAfterTransfer),
  ]
}

function buildOriginSheets(
  rows: TransferWorkbookExportRow[],
  tarimaMap: Map<string, string>,
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
      ]),
      ...rows.flatMap((item) =>
        item.pallets
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

function formatNumber(value: number): string {
  return new Intl.NumberFormat('es-CR', { maximumFractionDigits: 2 }).format(value)
}
