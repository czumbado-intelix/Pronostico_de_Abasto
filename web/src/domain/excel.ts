import { strFromU8, unzipSync } from 'fflate'
import { normalizeHeader, parseNumber } from './normalization'
import type {
  CatalogArticle,
  ExpedicionesWorkbook,
  ExpeditionLine,
  ForecastEntry,
  ForecastGranularity,
  ForecastWorkbook,
  InventoryPosition,
  InventoryWorkbook,
  TarimaSizeEntry,
} from './types'

type SheetRows = unknown[][]
type WorkbookSheets = SheetRows[]

const articleHeaders = ['Articulo', 'Articulos', 'Prod', 'Producto', 'Item code', 'Item', 'Codigo', 'Cod articulo']
const descriptionHeaders = ['Descripcion', 'Description', 'Descripción']

const inventoryZoneHeaders = ['Zona de almacenaje', 'Zona Almacenaje']
const inventoryQuantityHeaders = ['Cantidad unidades']

const expeditionArticleHeaders = ['Articulo', 'Item code', 'Codigo', 'Prod']
const expeditionQuantityHeaders = [
  'Cantidad pedida presentacion minima',
  'Cantidad pedida presentación mínima',
  'Cantidad a expedir',
  'Cantidad',
  'Pedidas',
  'Unidades',
  'Expedido',
]
const preparedHeaders = [
  'Cantidad preparada presentacion minima',
  'Cantidad preparada presentación mínima',
  'Cantidad preparada',
  'Preparadas',
  'Preparada',
  'Preparado',
]
const expeditionHeaders = [
  'Expedicion',
  'Expedición',
  'Numero expedicion',
  'Numero de expedicion',
  'No expedicion',
]
const situationHeaders = ['Situacion', 'Situación']
const companyHeaders = ['Id Compania', 'Id Compañía', 'Compania', 'Compañía']

export async function readWorkbook(file: File): Promise<WorkbookSheets> {
  const archive = unzipSync(new Uint8Array(await file.arrayBuffer()))
  const sharedStrings = parseSharedStringsXml(readArchiveText(archive, 'xl/sharedStrings.xml'))
  const sheets = parseWorkbookSheetPaths(
    readArchiveText(archive, 'xl/workbook.xml'),
    readArchiveText(archive, 'xl/_rels/workbook.xml.rels'),
  )

  const workbook = sheets
    .map((sheetPath) => readArchiveText(archive, sheetPath))
    .filter(Boolean)
    .map((sheetXml) => parseSheetXml(sheetXml, sharedStrings))

  if (workbook.length === 0) {
    throw new Error('El archivo de Excel no contiene hojas legibles.')
  }

  return workbook
}

function readArchiveText(archive: Record<string, Uint8Array>, path: string): string {
  const content = archive[path]
  return content ? strFromU8(content) : ''
}

function parseWorkbookSheetPaths(workbookXml: string, relsXml: string): string[] {
  const workbookDocument = parseXmlDocument(workbookXml)
  const relsDocument = parseXmlDocument(relsXml)
  const relationshipTargets = new Map<string, string>()

  for (const relationship of elementsByLocalName(relsDocument, 'Relationship')) {
    const id = relationship.getAttribute('Id') ?? ''
    const target = relationship.getAttribute('Target') ?? ''
    if (id && target) {
      relationshipTargets.set(id, normalizeWorkbookTarget(target))
    }
  }

  return elementsByLocalName(workbookDocument, 'sheet')
    .map((sheet) => sheet.getAttribute('r:id') ?? sheet.getAttribute('id') ?? '')
    .map((relationshipId) => relationshipTargets.get(relationshipId) ?? '')
    .filter(Boolean)
}

function normalizeWorkbookTarget(target: string): string {
  const normalized = target.replace(/\\/g, '/').replace(/^\//, '')
  return normalized.startsWith('xl/') ? normalized : `xl/${normalized}`
}

function parseSharedStringsXml(xml: string): string[] {
  if (!xml) {
    return []
  }

  const document = parseXmlDocument(xml)
  return elementsByLocalName(document, 'si').map((item) => readTextNodes(item))
}

function parseSheetXml(xml: string, sharedStrings: string[]): SheetRows {
  const document = parseXmlDocument(xml)
  const rows: SheetRows = []

  for (const rowElement of elementsByLocalName(document, 'row')) {
    const rowNumber = parsePositiveInteger(rowElement.getAttribute('r')) ?? rows.length + 1
    const row: unknown[] = []
    let nextColumn = 0

    for (const cellElement of childElementsByLocalName(rowElement, 'c')) {
      const referenceColumn = parseCellColumnIndex(cellElement.getAttribute('r') ?? '')
      const columnIndex = referenceColumn ?? nextColumn
      const value = readCellValue(cellElement, sharedStrings)
      row[columnIndex] = value
      nextColumn = columnIndex + 1
    }

    while (rows.length < rowNumber - 1) {
      rows.push([])
    }
    rows[rowNumber - 1] = row
  }

  return rows
}

function readCellValue(cellElement: Element, sharedStrings: string[]): unknown {
  const type = cellElement.getAttribute('t') ?? ''
  if (type === 'inlineStr') {
    return readTextNodes(cellElement)
  }

  const value = firstChildText(cellElement, 'v')
  if (!value) {
    return ''
  }

  if (type === 's') {
    return sharedStrings[Number(value)] ?? ''
  }
  if (type === 'b') {
    return value === '1'
  }
  if (type === 'd') {
    const date = new Date(value)
    return Number.isNaN(date.valueOf()) ? value : date
  }

  const numeric = Number(value)
  return Number.isFinite(numeric) ? numeric : value
}

function readTextNodes(element: Element): string {
  const values = elementsByLocalName(element, 't')
    .map((item) => item.textContent ?? '')
    .filter(Boolean)
  return values.length > 0 ? values.join('') : (element.textContent ?? '')
}

function firstChildText(element: Element, localName: string): string {
  return childElementsByLocalName(element, localName)[0]?.textContent?.trim() ?? ''
}

function parseXmlDocument(xml: string): Document {
  const document = new DOMParser().parseFromString(xml, 'application/xml')
  const error = elementsByLocalName(document, 'parsererror')[0]
  if (error) {
    throw new Error(`No se pudo leer la estructura interna del Excel: ${error.textContent?.trim() ?? 'XML invalido'}`)
  }
  return document
}

function elementsByLocalName(root: Document | Element, localName: string): Element[] {
  return [...root.querySelectorAll('*')].filter((element) => element.localName === localName)
}

function childElementsByLocalName(root: Element, localName: string): Element[] {
  return [...root.children].filter((element) => element.localName === localName)
}

function parseCellColumnIndex(reference: string): number | undefined {
  const match = reference.match(/^([A-Za-z]+)/)
  if (!match) {
    return undefined
  }

  let index = 0
  for (const letter of match[1].toUpperCase()) {
    index = index * 26 + letter.charCodeAt(0) - 64
  }
  return index - 1
}

function parsePositiveInteger(value: string | null): number | undefined {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined
}

export async function parseInventoryFile(file: File): Promise<InventoryWorkbook> {
  return parseInventoryWorkbook(await readWorkbook(file))
}

export async function parseForecastFile(file: File): Promise<ForecastWorkbook> {
  return parseForecastWorkbook(await readWorkbook(file))
}

export async function parseExpedicionesFile(file: File): Promise<ExpedicionesWorkbook> {
  return parseExpedicionesWorkbook(await readWorkbook(file))
}

export async function parsePreparedArticlesFile(file: File): Promise<CatalogArticle[]> {
  return parsePreparedArticlesWorkbook(await readWorkbook(file))
}

export async function parseTarimaSizesFile(file: File): Promise<TarimaSizeEntry[]> {
  return parseTarimaSizesWorkbook(await readWorkbook(file))
}

export function parseInventoryWorkbook(workbook: WorkbookSheets): InventoryWorkbook {
  for (const rows of workbook) {
    if (rows.length === 0) {
      continue
    }

    const header = rows[0]
    const articleColumn = findColumn(header, articleHeaders)
    const zoneColumn = findColumn(header, inventoryZoneHeaders)
    const quantityColumn = findColumn(header, inventoryQuantityHeaders)
    if (articleColumn < 0 || zoneColumn < 0 || quantityColumn < 0) {
      continue
    }

    const descriptionColumn = findColumn(header, descriptionHeaders)
    const palletColumn = findColumn(header, ['Pallet', 'Paleta', 'Palet'])
    const locationColumn = findColumn(header, ['Ubicacion', 'Ubicación'])
    const validationDateColumn = findColumn(header, [
      'Fecha de validacion',
      'Fecha validacion',
      'Fecha de validación',
      'Fecha Validación',
    ])
    const palletTypeColumn = findColumn(header, [
      'Tipo pallet',
      'Tipo de pallet',
      'Tipo paleta',
      'Tipo de paleta',
    ])

    const positions: InventoryPosition[] = []
    const descriptions: Record<string, string> = {}

    for (const row of rows.slice(1)) {
      const article = cellText(row[articleColumn]).trim()
      if (!article) {
        continue
      }

      const palletType = cellText(row[palletTypeColumn]).trim()
      if (normalizeHeader(palletType).includes('merma')) {
        continue
      }

      const quantity = parseNumber(row[quantityColumn])
      if (quantity === undefined) {
        continue
      }

      const description = cellText(row[descriptionColumn]).trim()
      if (description && !descriptions[article]) {
        descriptions[article] = description
      }

      positions.push({
        article,
        storageZone: cellText(row[zoneColumn]).trim() || 'Sin zona',
        quantity,
        description,
        pallet: cellText(row[palletColumn]).trim(),
        validationDate: parseDateCell(row[validationDateColumn]),
        palletType,
        location: cellText(row[locationColumn]).trim(),
      })
    }

    return { positions, descriptions }
  }

  throw new Error('No se encontro una hoja de inventario con Articulo, Zona Almacenaje y Cantidad Unidades.')
}

export function parseForecastWorkbook(workbook: WorkbookSheets): ForecastWorkbook {
  const rows = workbook[0]
  if (!rows) {
    throw new Error('El archivo de forecast no contiene hojas.')
  }

  const headerInfo = findForecastHeader(rows)
  const header = rows[headerInfo.headerRow]
  const descriptionColumn = findColumn(header, descriptionHeaders)

  const periodColumns = header
    .map((value, index) => ({ index, parsed: parsePeriodHeader(value) }))
    .filter((item) => item.index !== headerInfo.articleColumn && item.parsed)
    .map((item) => ({ column: item.index, period: item.parsed!.period, isMonthly: item.parsed!.isMonthly }))

  if (periodColumns.length === 0) {
    throw new Error('No se encontraron columnas de semana o mes validas en el forecast.')
  }

  const descriptions: Record<string, string> = {}
  const entries: ForecastEntry[] = []

  for (const row of rows.slice(headerInfo.headerRow + 1)) {
    const article = cellText(row[headerInfo.articleColumn]).trim()
    if (!article) {
      continue
    }

    const description = cellText(row[descriptionColumn]).trim()
    if (description && !descriptions[article]) {
      descriptions[article] = description
    }

    for (const periodColumn of periodColumns) {
      const quantity = parseNumber(row[periodColumn.column])
      if (quantity === undefined || quantity === 0) {
        continue
      }
      entries.push({ article, period: periodColumn.period, quantity })
    }
  }

  const consolidated = consolidateForecastEntries(entries)
  const availablePeriods = [...new Set(periodColumns.map((column) => column.period))].sort()
  const granularity: ForecastGranularity =
    periodColumns.every((column) => column.isMonthly) ||
    (availablePeriods.length >= 2 && availablePeriods.every((period) => period.endsWith('-01')))
      ? 'Monthly'
      : 'Weekly'

  return {
    entries: consolidated,
    availablePeriods,
    granularity,
    descriptions,
  }
}

export function parseExpedicionesWorkbook(workbook: WorkbookSheets): ExpedicionesWorkbook {
  for (const rows of workbook) {
    if (!hasDetailHeader(rows)) {
      continue
    }

    return { lines: parseExpeditionRows(rows) }
  }

  throw new Error('No se encontro una hoja de expediciones con las columnas requeridas: Articulo y Cantidad.')
}

export function parsePreparedArticlesWorkbook(workbook: WorkbookSheets): CatalogArticle[] {
  for (const rows of workbook) {
    const header = rows[0]
    if (!header) {
      continue
    }

    const articleColumn = findColumn(header, articleHeaders)
    if (articleColumn < 0) {
      continue
    }

    const descriptionColumn = findColumn(header, descriptionHeaders)
    const byArticle = new Map<string, CatalogArticle>()
    for (const row of rows.slice(1)) {
      const article = cellText(row[articleColumn]).trim()
      if (!article) {
        continue
      }

      const description = cellText(row[descriptionColumn]).trim()
      const existing = byArticle.get(article.toUpperCase())
      if (!existing || (!existing.description && description)) {
        byArticle.set(article.toUpperCase(), { article, description })
      }
    }

    return [...byArticle.values()].sort((a, b) => a.article.localeCompare(b.article))
  }

  throw new Error('No se encontro una hoja con la columna requerida: Articulo.')
}

export function parseTarimaSizesWorkbook(workbook: WorkbookSheets): TarimaSizeEntry[] {
  for (const rows of workbook) {
    const header = rows[0]
    if (!header) {
      continue
    }

    const articleColumn = findColumn(header, articleHeaders)
    const sizeColumn = findColumn(header, [
      'Tamano tarima',
      'Tamaño tarima',
      'Tamano de tarima',
      'Tamaño de tarima',
    ])
    if (articleColumn < 0 || sizeColumn < 0) {
      continue
    }

    const descriptionColumn = findColumn(header, descriptionHeaders)
    const byArticle = new Map<string, TarimaSizeEntry>()
    for (const [index, row] of rows.slice(1).entries()) {
      const article = cellText(row[articleColumn]).trim()
      if (!article) {
        continue
      }

      const sizeCode = normalizeTarimaSize(cellText(row[sizeColumn]))
      if (!sizeCode) {
        throw new Error(
          `El tamano de tarima de la fila ${index + 2} no es valido. Use S/1/Sencilla, D/2/Doble o T/3/Triple.`,
        )
      }

      byArticle.set(article.toUpperCase(), {
        article,
        description: cellText(row[descriptionColumn]).trim(),
        sizeCode,
      })
    }

    return [...byArticle.values()].sort((a, b) => a.article.localeCompare(b.article))
  }

  throw new Error('No se encontro una hoja con las columnas requeridas: Articulo y Tamano Tarima.')
}

function parseExpeditionRows(rows: SheetRows): ExpeditionLine[] {
  const lines: ExpeditionLine[] = []
  let detailColumns: DetailColumns | undefined
  let currentExpedition = ''
  let currentSituation = ''
  let pendingSummaryValues = false
  let summaryExpeditionColumns: number[] = []
  let summarySituationColumns: number[] = []

  rows.forEach((row) => {
    if (pendingSummaryValues) {
      currentExpedition = readFirstFieldValue(row, summaryExpeditionColumns, expeditionHeaders)
      currentSituation = readFirstFieldValue(row, summarySituationColumns, situationHeaders)
      pendingSummaryValues = false
      return
    }

    const foundDetail = findDetailColumns(row)
    if (foundDetail) {
      detailColumns = foundDetail
      return
    }

    const summary = findSummaryColumns(row)
    if (summary) {
      summaryExpeditionColumns = summary.expeditionColumns
      summarySituationColumns = summary.situationColumns
      pendingSummaryValues = true
      return
    }

    if (!detailColumns) {
      return
    }

    const article = cellText(row[detailColumns.articleColumn]).trim()
    if (!article) {
      return
    }

    const requestedQuantity = parseNumber(row[detailColumns.quantityColumn])
    if (requestedQuantity === undefined) {
      return
    }

    const preparedQuantity =
      detailColumns.preparedColumn >= 0
        ? (parseNumber(row[detailColumns.preparedColumn]) ?? 0)
        : 0
    const missingQuantity =
      detailColumns.preparedColumn >= 0
        ? Math.max(0, requestedQuantity - preparedQuantity)
        : requestedQuantity
    if (missingQuantity <= 0) {
      return
    }

    const lineSituation = readFirstFieldValue(row, detailColumns.situationColumns, situationHeaders)
    if (isCancelled(currentSituation) || isCancelled(lineSituation)) {
      return
    }

    const lineExpedition = readFirstFieldValue(row, detailColumns.expeditionColumns, expeditionHeaders)
    lines.push({
      article,
      description:
        detailColumns.descriptionColumn >= 0 ? cellText(row[detailColumns.descriptionColumn]).trim() : '',
      quantity: missingQuantity,
      expeditionNumber: lineExpedition || currentExpedition,
      requestedQuantity,
      preparedQuantity,
      situation: lineSituation || currentSituation,
    })
  })

  return lines
}

function consolidateForecastEntries(entries: ForecastEntry[]): ForecastEntry[] {
  const byKey = new Map<string, ForecastEntry>()
  for (const entry of entries) {
    const key = `${entry.article.trim().toUpperCase()}|${entry.period}`
    const existing = byKey.get(key)
    if (existing) {
      existing.quantity += entry.quantity
    } else {
      byKey.set(key, { ...entry, article: entry.article.trim() })
    }
  }
  return [...byKey.values()]
}

function findForecastHeader(rows: SheetRows): { headerRow: number; articleColumn: number } {
  const maxHeaderScanRows = Math.min(rows.length, 12)
  for (let rowIndex = 0; rowIndex < maxHeaderScanRows; rowIndex += 1) {
    const column = findColumn(rows[rowIndex], articleHeaders)
    if (column >= 0) {
      return { headerRow: rowIndex, articleColumn: column }
    }
  }

  throw new Error("No se encontro la columna de articulo ('Articulo', 'Prod' o 'Item code') en el forecast.")
}

function hasDetailHeader(rows: SheetRows): boolean {
  return rows.slice(0, 20).some((row) => findDetailColumns(row))
}

interface DetailColumns {
  articleColumn: number
  descriptionColumn: number
  quantityColumn: number
  preparedColumn: number
  expeditionColumns: number[]
  situationColumns: number[]
}

function findDetailColumns(row: unknown[]): DetailColumns | undefined {
  const articleColumn = findColumn(row, expeditionArticleHeaders)
  const quantityColumn = findColumn(row, expeditionQuantityHeaders)
  if (articleColumn < 0 || quantityColumn < 0) {
    return undefined
  }

  return {
    articleColumn,
    descriptionColumn: findColumn(row, descriptionHeaders),
    quantityColumn,
    preparedColumn: findColumn(row, preparedHeaders),
    expeditionColumns: findColumns(row, expeditionHeaders),
    situationColumns: findColumns(row, situationHeaders),
  }
}

function findSummaryColumns(row: unknown[]):
  | { expeditionColumns: number[]; situationColumns: number[] }
  | undefined {
  let expeditionColumns = findColumns(row, expeditionHeaders)
  let situationColumns = findColumns(row, situationHeaders)
  if (expeditionColumns.length === 0 || situationColumns.length === 0) {
    return undefined
  }

  const companyColumn = findColumn(row, companyHeaders)
  if (companyColumn >= 0) {
    expeditionColumns = orderColumnsAfterAnchorFirst(expeditionColumns, companyColumn)
    situationColumns = orderColumnsAfterAnchorFirst(situationColumns, companyColumn)
  }

  return { expeditionColumns, situationColumns }
}

function orderColumnsAfterAnchorFirst(columns: number[], anchorColumn: number): number[] {
  return [
    ...columns.filter((column) => column >= anchorColumn),
    ...columns.filter((column) => column < anchorColumn),
  ]
}

function findColumn(row: unknown[], candidates: string[]): number {
  const normalized = new Set(candidates.map(normalizeHeader))
  return row.findIndex((value) => normalized.has(normalizeHeader(value)))
}

function findColumns(row: unknown[], candidates: string[]): number[] {
  const result: number[] = []
  for (const candidate of candidates) {
    const normalizedCandidate = normalizeHeader(candidate)
    row.forEach((value, index) => {
      if (normalizeHeader(value) === normalizedCandidate && !result.includes(index)) {
        result.push(index)
      }
    })
  }
  return result
}

function readFirstFieldValue(row: unknown[], columns: number[], headerLabels: string[]): string {
  for (const column of columns) {
    const value = cellText(row[column]).trim()
    if (value && !matchesAnyHeader(value, headerLabels)) {
      return value
    }
  }
  return ''
}

function matchesAnyHeader(value: string, headerLabels: string[]): boolean {
  const normalized = normalizeHeader(value)
  return normalized.length > 0 && headerLabels.some((header) => normalizeHeader(header) === normalized)
}

function isCancelled(value: string): boolean {
  return normalizeHeader(value) === 'anul'
}

function cellText(value: unknown): string {
  if (value === undefined || value === null) {
    return ''
  }
  if (value instanceof Date) {
    return toIsoDate(value)
  }
  if (typeof value === 'number' && Number.isInteger(value)) {
    return value.toString()
  }
  return String(value).trim()
}

function parseDateCell(value: unknown): string | undefined {
  if (value instanceof Date) {
    return toIsoDate(value)
  }
  if (typeof value === 'number') {
    const parsed = excelSerialToDate(value)
    if (parsed) {
      return toIsoDate(parsed)
    }
  }

  const text = cellText(value)
  if (!text) {
    return undefined
  }

  const date = new Date(text)
  return Number.isNaN(date.valueOf()) ? undefined : toIsoDate(date)
}

function parsePeriodHeader(value: unknown): { period: string; isMonthly: boolean } | undefined {
  if (value instanceof Date) {
    return { period: toIsoDate(value), isMonthly: false }
  }

  if (typeof value === 'number') {
    const parsed = excelSerialToDate(value)
    if (parsed) {
      return {
        period: toIsoDate(parsed),
        isMonthly: false,
      }
    }
  }

  const text = cellText(value)
  if (!text) {
    return undefined
  }

  const exact = parseExactDate(text)
  if (exact) {
    return { period: exact, isMonthly: false }
  }

  const month = parseMonthHeader(text)
  if (month) {
    return { period: month, isMonthly: true }
  }

  return undefined
}

function parseExactDate(text: string): string | undefined {
  const trimmed = text.trim()
  const iso = trimmed.match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/)
  if (iso) {
    return `${iso[1]}-${iso[2].padStart(2, '0')}-${iso[3].padStart(2, '0')}`
  }

  const dotted = trimmed.match(/^(\d{1,2})\.(\d{1,2})\.(\d{2}|\d{4})$/)
  if (dotted) {
    const year = dotted[3].length === 2 ? `20${dotted[3]}` : dotted[3]
    return `${year}-${dotted[2].padStart(2, '0')}-${dotted[1].padStart(2, '0')}`
  }

  return undefined
}

function parseMonthHeader(text: string): string | undefined {
  const normalized = text.trim().replace('.', '').replace('-', ' ')
  const match = normalized.match(/^([A-Za-zÁÉÍÓÚáéíóúÑñ]+)\s+(\d{2}|\d{4})$/)
  if (!match) {
    return undefined
  }

  const monthIndex = monthNames[normalizeHeader(match[1])]
  if (!monthIndex) {
    return undefined
  }

  const year = match[2].length === 2 ? `20${match[2]}` : match[2]
  return `${year}-${monthIndex.toString().padStart(2, '0')}-01`
}

function normalizeTarimaSize(value: string): string {
  const token = normalizeHeader(value)
  if (['s', '1', 'sencilla', 'simple'].includes(token)) {
    return 'S'
  }
  if (['d', '2', 'doble'].includes(token)) {
    return 'D'
  }
  if (['t', '3', 'triple'].includes(token)) {
    return 'T'
  }
  return ''
}

const monthNames: Record<string, number> = {
  jan: 1,
  january: 1,
  ene: 1,
  enero: 1,
  feb: 2,
  february: 2,
  febrero: 2,
  mar: 3,
  march: 3,
  marzo: 3,
  apr: 4,
  april: 4,
  abr: 4,
  abril: 4,
  may: 5,
  mayo: 5,
  jun: 6,
  june: 6,
  junio: 6,
  jul: 7,
  july: 7,
  julio: 7,
  aug: 8,
  august: 8,
  ago: 8,
  agosto: 8,
  sep: 9,
  sept: 9,
  september: 9,
  septiembre: 9,
  oct: 10,
  october: 10,
  octubre: 10,
  nov: 11,
  november: 11,
  noviembre: 11,
  dec: 12,
  december: 12,
  dic: 12,
  diciembre: 12,
}

function toIsoDate(date: Date): string {
  return `${date.getFullYear().toString().padStart(4, '0')}-${(date.getMonth() + 1)
    .toString()
    .padStart(2, '0')}-${date.getDate().toString().padStart(2, '0')}`
}

function excelSerialToDate(value: number): Date | undefined {
  if (!Number.isFinite(value) || value < 20000 || value > 80000) {
    return undefined
  }
  const epoch = Date.UTC(1899, 11, 30)
  return new Date(epoch + value * 24 * 60 * 60 * 1000)
}
