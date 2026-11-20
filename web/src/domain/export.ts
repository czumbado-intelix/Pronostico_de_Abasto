import writeXlsxFile, { type Column } from 'write-excel-file/browser'
import type { ArticleAnalysisResult, ExpeditionLineResult } from './types'

interface ExportRow {
  Articulo: string
  Descripcion: string
  Demanda: number
  Principal: number
  'En externas': number
  'Necesidad calculada': number
  'A traer': number
  'Pendiente transito': number
  'Pendiente final': number
  'Sobrante por tarima': number
  Pallets: string
  Zonas: string
}

export async function exportTransferRows(
  rows: Array<ArticleAnalysisResult | ExpeditionLineResult>,
  fileName = 'requisicion-traslados.xlsx',
): Promise<void> {
  const sheetRows: ExportRow[] = rows.map((row) => ({
    Articulo: row.article,
    Descripcion: row.description,
    Demanda: isArticle(row) ? row.forecastQuantity : row.demandQuantity,
    Principal: row.principalInventoryQuantity,
    'En externas': row.satelliteInventoryQuantity,
    'Necesidad calculada': row.calculatedTransferNeedQuantity,
    'A traer': row.transferSuggestionQuantity,
    'Pendiente transito': row.pendingTransitQuantity,
    'Pendiente final': row.remainingShortageAfterTransfer,
    'Sobrante por tarima': row.palletRoundUpSurplusQuantity,
    Pallets: row.suggestedPallets.map((pallet) => pallet.pallet).join('; '),
    Zonas: row.satelliteZones
      .map((zone) => `${zone.storageZone} (${formatNumber(zone.quantity)})`)
      .join('; '),
  }))

  await writeXlsxFile(sheetRows, {
    sheet: 'Requisicion',
    columns: exportColumns,
  }).toFile(fileName)
}

function isArticle(row: ArticleAnalysisResult | ExpeditionLineResult): row is ArticleAnalysisResult {
  return 'forecastQuantity' in row
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('es-CR', { maximumFractionDigits: 2 }).format(value)
}

const exportColumns: Column<ExportRow>[] = [
  column('Articulo', 16),
  column('Descripcion', 42),
  column('Demanda', 12),
  column('Principal', 12),
  column('En externas', 12),
  column('Necesidad calculada', 18),
  column('A traer', 12),
  column('Pendiente transito', 18),
  column('Pendiente final', 16),
  column('Sobrante por tarima', 18),
  column('Pallets', 36),
  column('Zonas', 52),
]

function column<Key extends keyof ExportRow>(key: Key, width: number): Column<ExportRow> {
  return {
    header: {
      value: String(key),
      fontWeight: 'bold',
      textColor: '#ffffff',
      backgroundColor: '#00a88f',
    },
    width,
    cell: (row) => ({
      value: row[key],
      align: typeof row[key] === 'number' ? 'right' : 'left',
    }),
  }
}
