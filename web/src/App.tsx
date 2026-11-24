import {
  AlertTriangle,
  ArrowDownUp,
  BarChart3,
  Check,
  ClipboardList,
  Copy,
  Database,
  Download,
  Eraser,
  FileSpreadsheet,
  History,
  PackageCheck,
  Play,
  RotateCcw,
  Search,
  Settings,
  Truck,
  Upload,
  X,
} from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  analyzeExpediciones,
  analyzeWarehouseComparison,
  analyzeWeeklyForecast,
  buildWarehouseComparisonHistogram,
  outstandingPalletKeys,
  pendingTransitQuantities,
  simulateScenario,
} from './domain/analysis'
import { companies, defaultSettings } from './domain/defaults'
import {
  exportTransferWorkbook,
  exportWarehouseComparisonWorkbook,
  type TransferWorkbookExportRow,
} from './domain/export'
import {
  parseExpedicionesFile,
  parseForecastFile,
  parseInventoryFile,
  parsePreparedArticlesFile,
  parseTarimaSizesFile,
} from './domain/excel'
import { normalizeArticleKey, normalizeSearchText, resolveStorageOrigin } from './domain/normalization'
import {
  applyTableControls,
  type TableColumn,
  type TableFilters,
  type TableSort,
} from './domain/tableControls'
import type {
  AppSettings,
  ArticleAnalysisResult,
  CatalogArticle,
  Company,
  ExpeditionLineResult,
  FileSessionInfo,
  FileSessionKind,
  LoadedFiles,
  OrderStateEntry,
  PalletInventoryDetail,
  TarimaSizeEntry,
  TransitItem,
  WarehouseComparisonRow,
} from './domain/types'
import {
  addTransit,
  backupUrl,
  loadComparisonComments,
  loadFileSessions,
  loadOrderState,
  loadSettings,
  loadTransit,
  markTransitReceived,
  saveComparisonComments,
  saveFileSession,
  saveOrderState,
  saveSettings,
  restoreFileSession,
  type FileSessionState,
} from './services/api'
import './index.css'

type ViewKey = 'traslados' | 'expediciones' | 'transito' | 'comparativa' | 'simulacion' | 'ajustes'
type TransferTab = 'Todos' | 'ParaTraer' | 'Criticos' | 'Pendientes' | 'AltoVolumen' | 'AlistadoSatelital'
type ExpeditionTab = 'Todos' | 'ParaTraer' | 'Cubiertas' | 'SinSatelite' | 'AlistadoSatelital'
type TransitTab = 'Pendientes' | 'Recibidos' | 'Todos'
type PalletScope = 'transfer' | 'expedition'
type AnalysisRow = ArticleAnalysisResult | ExpeditionLineResult
type TableKey = 'transfer' | 'expedition' | 'comparison' | 'transit'

const formatNumber = new Intl.NumberFormat('es-CR', { maximumFractionDigits: 2 })

function App() {
  const [company, setCompany] = useState<Company>('EPA')
  const [settingsByCompany, setSettingsByCompany] = useState<Record<Company, AppSettings>>({
    EPA: defaultSettings('EPA'),
    Cofersa: defaultSettings('Cofersa'),
  })
  const [filesByCompany, setFilesByCompany] = useState<Record<Company, LoadedFiles>>({
    EPA: {},
    Cofersa: {},
  })
  const [fileSessionsByCompany, setFileSessionsByCompany] = useState<Record<Company, FileSessionInfo[]>>({
    EPA: [],
    Cofersa: [],
  })
  const [fileStorageByCompany, setFileStorageByCompany] = useState<Record<Company, 'd1' | 'r2'>>({
    EPA: 'd1',
    Cofersa: 'd1',
  })
  const [periodByCompany, setPeriodByCompany] = useState<Record<Company, string>>({
    EPA: '',
    Cofersa: '',
  })
  const [comparisonPeriodByCompany, setComparisonPeriodByCompany] = useState<Record<Company, string>>({
    EPA: '',
    Cofersa: '',
  })
  const [transit, setTransit] = useState<TransitItem[]>([])
  const [view, setView] = useState<ViewKey>('traslados')
  const [selectedTransferTab, setSelectedTransferTab] = useState<TransferTab>('Todos')
  const [selectedExpeditionTab, setSelectedExpeditionTab] = useState<ExpeditionTab>('Todos')
  const [transitTab, setTransitTab] = useState<TransitTab>('Pendientes')
  const [comparisonMode, setComparisonMode] = useState<'Tabla' | 'Histograma'>('Tabla')
  const [horizonPeriods, setHorizonPeriods] = useState(4)
  const [scenarioMonths, setScenarioMonths] = useState(3)
  const [scenarioBuffer, setScenarioBuffer] = useState(15)
  const [searchText, setSearchText] = useState('')
  const [busy, setBusy] = useState('')
  const [notice, setNotice] = useState('')
  const [orderedRows, setOrderedRows] = useState<Record<string, boolean>>({})
  const [selectedPalletKeys, setSelectedPalletKeys] = useState<Record<string, string[]>>({})
  const [comparisonComments, setComparisonComments] = useState<Record<string, string>>({})
  const [columnFilters, setColumnFilters] = useState<Partial<Record<TableKey, TableFilters>>>({})
  const [tableSorts, setTableSorts] = useState<Partial<Record<TableKey, TableSort>>>({})
  const [palletPane, setPalletPane] = useState<{ scope: PalletScope; key: string; row: AnalysisRow }>()

  const files = filesByCompany[company] ?? {}
  const fileSessions = fileSessionsByCompany[company] ?? []
  const fileStorage = fileStorageByCompany[company] ?? 'd1'
  const settings = settingsByCompany[company]
  const selectedPeriod = periodByCompany[company] || files.forecast?.availablePeriods[0] || ''
  const comparisonInventory = files.comparisonInventory ?? files.inventory
  const comparisonForecast = files.comparisonForecast ?? files.forecast
  const selectedComparisonPeriod =
    comparisonPeriodByCompany[company] || comparisonForecast?.availablePeriods[0] || ''
  const activeInventoryFile = view === 'comparativa' ? files.comparisonInventory : files.inventory
  const activeForecastFile = view === 'comparativa' ? files.comparisonForecast : files.forecast
  const activePeriods = view === 'comparativa'
    ? comparisonForecast?.availablePeriods ?? []
    : files.forecast?.availablePeriods ?? []
  const activePeriod = view === 'comparativa' ? selectedComparisonPeriod : selectedPeriod

  useEffect(() => {
    let cancelled = false
    async function load() {
      const [loadedSettings, loadedTransit, loadedComments, loadedFileState] = await Promise.all([
        loadSettings(company),
        loadTransit(company),
        loadComparisonComments(company),
        loadFileSessions(company),
      ])
      if (!cancelled) {
        setSettingsByCompany((current) => ({ ...current, [company]: loadedSettings }))
        setTransit(loadedTransit)
        setComparisonComments(loadedComments)
        applyFileSessionState(company, loadedFileState)
        setPeriodByCompany((current) => ({
          ...current,
          [company]: resolveAvailablePeriod(current[company], loadedFileState.files.forecast?.availablePeriods),
        }))
        setComparisonPeriodByCompany((current) => ({
          ...current,
          [company]: resolveAvailablePeriod(current[company], loadedFileState.files.comparisonForecast?.availablePeriods),
        }))
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [company])

  useEffect(() => {
    let cancelled = false
    async function load() {
      if (!selectedPeriod) {
        setOrderedRows({})
        setSelectedPalletKeys({})
        return
      }
      const state = await loadOrderState(company, selectedPeriod)
      if (!cancelled) {
        applyOrderState(state)
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [company, selectedPeriod])

  const pendingQuantities = useMemo(() => pendingTransitQuantities(transit), [transit])
  const unavailablePalletKeys = useMemo(() => outstandingPalletKeys(transit), [transit])
  const descriptions = useMemo(
    () => ({
      ...(files.inventory?.descriptions ?? {}),
      ...(files.forecast?.descriptions ?? {}),
    }),
    [files.forecast?.descriptions, files.inventory?.descriptions],
  )
  const comparisonDescriptions = useMemo(
    () => ({
      ...(comparisonInventory?.descriptions ?? {}),
      ...(comparisonForecast?.descriptions ?? {}),
    }),
    [comparisonForecast?.descriptions, comparisonInventory?.descriptions],
  )
  const preparedSet = useMemo(() => buildArticleSet(settings.preparedArticles), [settings.preparedArticles])
  const tarimaMap = useMemo(() => buildTarimaMap(settings.tarimaSizes), [settings.tarimaSizes])

  const weeklyResult = useMemo(() => {
    if (!files.inventory || !files.forecast || !selectedPeriod) {
      return undefined
    }

    return analyzeWeeklyForecast({
      inventory: files.inventory.positions,
      forecast: files.forecast.entries,
      selectedPeriod,
      horizonPeriods,
      thresholds: settings.thresholds,
      satelliteZones: settings.satelliteZones,
      ignoredStorageZones: [],
      pendingTransitQuantities: pendingQuantities,
      unavailablePalletKeys,
      availablePeriods: files.forecast.availablePeriods,
      descriptions,
    })
  }, [
    descriptions,
    files.forecast,
    files.inventory,
    horizonPeriods,
    pendingQuantities,
    selectedPeriod,
    settings.satelliteZones,
    settings.thresholds,
    unavailablePalletKeys,
  ])

  const expedicionesResult = useMemo(() => {
    if (!files.inventory || !files.expediciones) {
      return undefined
    }

    return analyzeExpediciones({
      inventory: files.inventory.positions,
      lines: files.expediciones.lines,
      satelliteZones: settings.satelliteZones,
      ignoredStorageZones: [],
      pendingTransitQuantities: pendingQuantities,
      unavailablePalletKeys,
    })
  }, [
    files.expediciones,
    files.inventory,
    pendingQuantities,
    settings.satelliteZones,
    unavailablePalletKeys,
  ])

  const comparisonResult = useMemo(() => {
    if (!comparisonInventory || !comparisonForecast || !selectedComparisonPeriod) {
      return undefined
    }

    return analyzeWarehouseComparison({
      inventory: comparisonInventory.positions,
      forecast: comparisonForecast.entries,
      selectedPeriod: selectedComparisonPeriod,
      availablePeriods: comparisonForecast.availablePeriods,
      ignoredStorageZones: [],
      descriptions: comparisonDescriptions,
      granularity: comparisonForecast.granularity,
    })
  }, [comparisonDescriptions, comparisonForecast, comparisonInventory, selectedComparisonPeriod])

  const comparisonColumns = useMemo<TableColumn<WarehouseComparisonRow>[]>(
    () => [
      { key: 'article', label: 'Articulo', text: (row) => row.article },
      { key: 'description', label: 'Descripcion', text: (row) => row.description },
      { key: 'olo', label: 'Inv OLO', text: (row) => formatNumber.format(row.oloQuantity), number: (row) => row.oloQuantity },
      { key: 'servica', label: 'Inv Servica', text: (row) => formatNumber.format(row.servicaQuantity), number: (row) => row.servicaQuantity },
      { key: 'forecast', label: 'Forecast', text: (row) => formatNumber.format(row.forecastQuantity), number: (row) => row.forecastQuantity },
      { key: 'coverage', label: 'Cobertura', text: (row) => row.coveragePeriods === undefined ? '-' : `${formatNumber.format(row.coveragePeriods)} per.`, number: (row) => row.coveragePeriods },
      { key: 'comments', label: 'Comentarios', text: (row) => comparisonComments[row.article] ?? row.automaticComment },
    ],
    [comparisonComments],
  )

  const comparisonSearchRows = useMemo(
    () =>
      filterComparisonRows(
        (comparisonResult?.rows ?? []).filter((row) => row.servicaQuantity > 0),
        searchText,
        comparisonComments,
      ),
    [comparisonComments, comparisonResult?.rows, searchText],
  )

  const comparisonRows = useMemo(
    () => applyTableControls(comparisonSearchRows, comparisonColumns, columnFilters.comparison, tableSorts.comparison),
    [columnFilters.comparison, comparisonColumns, comparisonSearchRows, tableSorts.comparison],
  )
  const histogram = useMemo(
    () =>
      buildWarehouseComparisonHistogram(
        comparisonRows,
        comparisonResult?.periodCount ?? 0,
        comparisonForecast?.granularity === 'Monthly' ? 'mes' : 'sem',
      ),
    [comparisonForecast?.granularity, comparisonResult?.periodCount, comparisonRows],
  )

  const scenario = useMemo(() => {
    return simulateCombinedScenario(filesByCompany, periodByCompany, settingsByCompany, scenarioMonths, scenarioBuffer)
  }, [filesByCompany, periodByCompany, scenarioBuffer, scenarioMonths, settingsByCompany])

  const transferBaseRows = useMemo(
    () =>
      (weeklyResult?.articles ?? []).filter(
        (row) =>
          row.transferSuggestionQuantity > 0 ||
          row.remainingShortageAfterTransfer > 0 ||
          row.pendingTransitQuantity > 0,
      ),
    [weeklyResult?.articles],
  )

  const transferColumns: TableColumn<ArticleAnalysisResult>[] = [
    { key: 'article', label: 'Articulo', text: (row) => row.article },
    { key: 'description', label: 'Descripcion', text: (row) => row.description },
    { key: 'coverage', label: 'Cobertura', text: (row) => coverageText(row.coveragePeriods, weeklyResult?.analyzedPeriods ?? 0), number: (row) => row.coveragePeriods },
    { key: 'forecast', label: 'Forecast', text: (row) => formatNumber.format(row.forecastQuantity), number: (row) => row.forecastQuantity },
    { key: 'principal', label: 'Principal', text: (row) => formatNumber.format(row.principalInventoryQuantity), number: (row) => row.principalInventoryQuantity },
    { key: 'external', label: 'Stock ext.', text: (row) => formatNumber.format(row.satelliteInventoryQuantity), number: (row) => row.satelliteInventoryQuantity },
    { key: 'transit', label: 'Transito', text: (row) => formatNumber.format(row.pendingTransitQuantity), number: (row) => row.pendingTransitQuantity },
    { key: 'transfer', label: 'A traer', text: (row) => formatNumber.format(selectedQuantity(row)), number: (row) => selectedQuantity(row) },
    { key: 'pallets', label: 'Palets', text: (row) => selectedPallets(row).map((pallet) => pallet.pallet).join(' '), number: (row) => selectedPallets(row).length },
    { key: 'tarima', label: 'Tarima', text: (row) => tarimaFor(row.article) },
    { key: 'prepared', label: 'Alistado', text: (row) => (isPrepared(row, preparedSet) ? 'Si' : 'No') },
    { key: 'zones', label: 'Zonas', text: formatZones },
  ]

  const transferSearchRows = useMemo(
    () =>
      filterTransferRows(
        transferBaseRows,
        selectedTransferTab,
        searchText,
        preparedSet,
      ),
    [preparedSet, searchText, selectedTransferTab, transferBaseRows],
  )

  const transferRows = applyTableControls(transferSearchRows, transferColumns, columnFilters.transfer, tableSorts.transfer)

  const expeditionBaseRows = useMemo(
    () =>
      (expedicionesResult?.lines ?? []).filter(
        (row) =>
          row.transferSuggestionQuantity > 0 ||
          row.remainingShortageAfterTransfer > 0 ||
          row.pendingTransitQuantity > 0,
      ),
    [expedicionesResult?.lines],
  )

  const expeditionColumns: TableColumn<ExpeditionLineResult>[] = [
    { key: 'expedition', label: 'Expedicion', text: (row) => row.expeditionNumber },
    { key: 'article', label: 'Articulo', text: (row) => row.article },
    { key: 'description', label: 'Descripcion', text: (row) => row.description },
    { key: 'demand', label: 'Solicitud', text: (row) => formatNumber.format(row.demandQuantity), number: (row) => row.demandQuantity },
    { key: 'principal', label: 'Principal', text: (row) => formatNumber.format(row.principalInventoryQuantity), number: (row) => row.principalInventoryQuantity },
    { key: 'external', label: 'Externas', text: (row) => formatNumber.format(row.satelliteInventoryQuantity), number: (row) => row.satelliteInventoryQuantity },
    { key: 'transit', label: 'Transito', text: (row) => formatNumber.format(row.pendingTransitQuantity), number: (row) => row.pendingTransitQuantity },
    { key: 'transfer', label: 'A traer', text: (row) => formatNumber.format(selectedQuantity(row)), number: (row) => selectedQuantity(row) },
    { key: 'pallets', label: 'Palets', text: (row) => selectedPallets(row).map((pallet) => pallet.pallet).join(' '), number: (row) => selectedPallets(row).length },
    { key: 'tarima', label: 'Tarima', text: (row) => tarimaFor(row.article) },
    { key: 'status', label: 'Estado', text: (row) => expeditionStatus(row, isPrepared(row, preparedSet)) },
    { key: 'pending', label: 'Pendiente', text: (row) => formatNumber.format(Math.max(0, row.calculatedTransferNeedQuantity - selectedQuantity(row))), number: (row) => Math.max(0, row.calculatedTransferNeedQuantity - selectedQuantity(row)) },
  ]

  const expeditionSearchRows = useMemo(
    () =>
      filterExpeditionRows(
        expeditionBaseRows,
        selectedExpeditionTab,
        searchText,
        preparedSet,
      ),
    [expeditionBaseRows, preparedSet, searchText, selectedExpeditionTab],
  )

  const expeditionRows = applyTableControls(expeditionSearchRows, expeditionColumns, columnFilters.expedition, tableSorts.expedition)

  const orderedTransferRows = transferBaseRows.filter(
    (row) => orderedRows[rowKey('transfer', row)] && canOrder(row, preparedSet) && selectedPallets(row).length > 0,
  )
  const orderedExpeditionRows = expeditionBaseRows.filter(
    (row) => orderedRows[rowKey('expedition', row)] && canOrder(row, preparedSet) && selectedPallets(row).length > 0,
  )

  const searchSummary = useMemo(() => {
    if (view === 'traslados') {
      return summarizeSearch(transferBaseRows, searchText, (row) => rowSearchHaystack(row))
    }
    if (view === 'expediciones') {
      return summarizeSearch(expeditionBaseRows, searchText, (row) => rowSearchHaystack(row))
    }
    if (view === 'comparativa') {
      return summarizeSearch(
        (comparisonResult?.rows ?? []).filter((row) => row.servicaQuantity > 0),
        searchText,
        (row) => `${row.article} ${row.description} ${comparisonComments[row.article] ?? row.automaticComment}`,
      )
    }
    if (view === 'transito') {
      return summarizeSearch(transit, searchText, transitSearchHaystack)
    }
    return { badge: '', detail: '', unmatched: [] }
  }, [comparisonComments, comparisonResult?.rows, expeditionBaseRows, searchText, transferBaseRows, transit, view])

  async function handleInventory(file: File) {
    await runBusy('Leyendo inventario...', async () => {
      const inventory = await parseInventoryFile(file)
      await persistFileSession('inventory', inventory, file.name)
      setNotice(`Inventario cargado en la nube: ${inventory.positions.length.toLocaleString('es-CR')} filas.`)
    })
  }

  async function handleForecast(file: File) {
    await runBusy('Leyendo forecast...', async () => {
      const forecast = await parseForecastFile(file)
      await persistFileSession('forecast', forecast, file.name)
      setPeriodByCompany((current) => ({ ...current, [company]: forecast.availablePeriods[0] ?? '' }))
      setNotice(
        `Forecast cargado en la nube: ${forecast.entries.length.toLocaleString('es-CR')} combinaciones articulo-periodo.`,
      )
    })
  }

  async function handleComparisonInventory(file: File) {
    await runBusy('Leyendo inventario de comparativa...', async () => {
      const comparisonInventory = await parseInventoryFile(file)
      await persistFileSession('comparisonInventory', comparisonInventory, file.name)
      setNotice(`Inventario de comparativa cargado en la nube: ${comparisonInventory.positions.length.toLocaleString('es-CR')} filas.`)
    })
  }

  async function handleComparisonForecast(file: File) {
    await runBusy('Leyendo forecast de comparativa...', async () => {
      const comparisonForecast = await parseForecastFile(file)
      await persistFileSession('comparisonForecast', comparisonForecast, file.name)
      setComparisonPeriodByCompany((current) => ({ ...current, [company]: comparisonForecast.availablePeriods[0] ?? '' }))
      setNotice(
        `Forecast de comparativa cargado en la nube: ${comparisonForecast.entries.length.toLocaleString('es-CR')} combinaciones articulo-periodo.`,
      )
    })
  }

  async function handleExpediciones(file: File) {
    await runBusy('Leyendo expediciones...', async () => {
      const expediciones = await parseExpedicionesFile(file)
      await persistFileSession('expediciones', expediciones, file.name)
      setNotice(`Expediciones cargadas en la nube: ${expediciones.lines.length.toLocaleString('es-CR')} lineas faltantes.`)
    })
  }

  async function handlePreparedArticles(file: File) {
    await runBusy('Leyendo alistado satelital...', async () => {
      const preparedArticles = await parsePreparedArticlesFile(file)
      await persistSettings({ ...settings, preparedArticles })
      setNotice(`Alistado satelital cargado: ${preparedArticles.length.toLocaleString('es-CR')} articulos.`)
    })
  }

  async function handleTarimaSizes(file: File) {
    await runBusy('Leyendo tamanos de tarima...', async () => {
      const tarimaSizes = await parseTarimaSizesFile(file)
      await persistSettings({ ...settings, tarimaSizes })
      setNotice(`Tamanos de tarima cargados: ${tarimaSizes.length.toLocaleString('es-CR')} articulos.`)
    })
  }

  async function runBusy(label: string, action: () => Promise<void>) {
    setBusy(label)
    setNotice('')
    try {
      await action()
    } catch (error) {
      setNotice(error instanceof Error ? error.message : 'No se pudo procesar el archivo.')
    } finally {
      setBusy('')
    }
  }

  async function persistFileSession<K extends FileSessionKind>(
    kind: K,
    payload: NonNullable<LoadedFiles[K]>,
    sourceName: string,
  ) {
    updateCompanyFiles((current) => ({ ...current, [kind]: payload }))
    const savedState = await saveFileSession(company, kind, payload, sourceName)
    applyFileSessionState(company, savedState)
  }

  function applyFileSessionState(targetCompany: Company, state: FileSessionState) {
    setFilesByCompany((current) => ({ ...current, [targetCompany]: state.files }))
    setFileSessionsByCompany((current) => ({ ...current, [targetCompany]: state.sessions }))
    setFileStorageByCompany((current) => ({ ...current, [targetCompany]: state.storage.objectStore }))
  }

  function updateCompanyFiles(updater: (files: LoadedFiles) => LoadedFiles) {
    setFilesByCompany((current) => ({
      ...current,
      [company]: updater(current[company] ?? {}),
    }))
  }

  function applyOrderState(state: Record<string, OrderStateEntry>) {
    setOrderedRows(
      Object.fromEntries(
        Object.entries(state)
          .filter(([, entry]) => entry.ordered)
          .map(([key]) => [key, true]),
      ),
    )
    setSelectedPalletKeys(
      Object.fromEntries(
        Object.entries(state)
          .filter(([, entry]) => entry.selectedPalletKeys.length > 0)
          .map(([key, entry]) => [key, entry.selectedPalletKeys]),
      ),
    )
  }

  function persistOrderState(
    nextOrderedRows: Record<string, boolean>,
    nextSelectedPalletKeys: Record<string, string[]>,
  ) {
    if (!selectedPeriod) {
      return
    }
    void saveOrderState(company, selectedPeriod, buildOrderState(nextOrderedRows, nextSelectedPalletKeys))
  }

  function selectedPallets(row: AnalysisRow): PalletInventoryDetail[] {
    const key = rowKey(isExpedition(row) ? 'expedition' : 'transfer', row)
    const available = availablePallets(row)
    const selectedKeys = selectedPalletKeys[key]
    if (!selectedKeys) {
      return row.suggestedPallets
    }
    const selected = new Set(selectedKeys)
    return available.filter((pallet) => selected.has(palletIdentity(pallet)))
  }

  function selectedQuantity(row: AnalysisRow): number {
    return selectedPallets(row).reduce((sum, pallet) => sum + pallet.quantity, 0)
  }

  function setPalletSelection(key: string, pallets: PalletInventoryDetail[]) {
    setSelectedPalletKeys((current) => {
      const next = {
        ...current,
        [key]: pallets.map(palletIdentity),
      }
      persistOrderState(orderedRows, next)
      return next
    })
  }

  function selectFifoForQuantity(row: AnalysisRow, key: string, quantity: number) {
    let remaining = Math.max(0, quantity)
    const pallets: PalletInventoryDetail[] = []
    for (const pallet of [...availablePallets(row)].sort(comparePallets)) {
      if (remaining <= 0) {
        break
      }
      if (pallet.quantity > 0) {
        pallets.push(pallet)
        remaining -= pallet.quantity
      }
    }
    setPalletSelection(key, pallets)
  }

  function toggleOrdered(scope: PalletScope, row: AnalysisRow, value: boolean) {
    const key = rowKey(scope, row)
    setOrderedRows((current) => {
      const next = { ...current, [key]: value }
      persistOrderState(next, selectedPalletKeys)
      return next
    })
  }

  async function sendMarkedToTransit(scope: PalletScope) {
    const sourceRows = scope === 'transfer' ? orderedTransferRows : orderedExpeditionRows
    await sendRowsToTransit(scope, sourceRows)
  }

  async function sendSingleToTransit(scope: PalletScope, row: AnalysisRow) {
    await sendRowsToTransit(scope, [row])
  }

  async function sendRowsToTransit(scope: PalletScope, rows: AnalysisRow[]) {
    const items = rows.flatMap((row) =>
      selectedPallets(row).map((pallet) => buildTransitItem(company, selectedPeriod, row, pallet, tarimaFor(row.article))),
    )
    if (items.length === 0) {
      setNotice('No hay pallets seleccionados para enviar a transito.')
      return
    }

    const saved = await addTransit(company, items)
    setTransit(saved)
    const sentKeys = new Set(rows.map((row) => rowKey(scope, row)))
    setOrderedRows((current) => {
      const nextOrdered = Object.fromEntries(Object.entries(current).filter(([key]) => !sentKeys.has(key)))
      setSelectedPalletKeys((selected) => {
        const nextSelected = Object.fromEntries(Object.entries(selected).filter(([key]) => !sentKeys.has(key)))
        persistOrderState(nextOrdered, nextSelected)
        return nextSelected
      })
      return nextOrdered
    })
    setNotice(`${items.length.toLocaleString('es-CR')} pallet(s) agregados a Transito.`)
  }

  async function receiveTransit(id: string) {
    const saved = await markTransitReceived(company, [id])
    setTransit(saved)
    setNotice('Pallet marcado como recibido.')
  }

  async function receiveVisibleTransit(items: TransitItem[]) {
    const ids = items.filter((item) => !item.receivedAt && !item.reconciledAt).map((item) => item.id)
    if (ids.length === 0) {
      setNotice('No hay pallets pendientes visibles.')
      return
    }
    const saved = await markTransitReceived(company, ids)
    setTransit(saved)
    setNotice(`${ids.length.toLocaleString('es-CR')} pallet(s) recibidos.`)
  }

  async function persistSettings(next: AppSettings) {
    const normalized = {
      ...next,
      company,
      excludedZones: [],
      preparedArticles: normalizeCatalogArticles(next.preparedArticles),
      tarimaSizes: normalizeTarimaSizes(next.tarimaSizes),
    }
    setSettingsByCompany((current) => ({ ...current, [company]: normalized }))
    const saved = await saveSettings(normalized)
    setSettingsByCompany((current) => ({ ...current, [company]: saved }))
    setNotice('Ajustes guardados.')
  }

  async function exportTransfer(scope: PalletScope, visibleRows: AnalysisRow[]) {
    const selectedRows = scope === 'transfer' ? orderedTransferRows : orderedExpeditionRows
    const sourceRows = selectedRows.length > 0 ? selectedRows : visibleRows.filter((row) => selectedPallets(row).length > 0)
    if (sourceRows.length === 0) {
      setNotice('No hay filas con pallets para exportar.')
      return
    }

    await exportTransferWorkbook({
      company,
      periodLabel: selectedPeriod,
      preparedArticles: settings.preparedArticles,
      tarimaSizes: settings.tarimaSizes,
      pendingPalletKeys: unavailablePalletKeys,
      rows: sourceRows.map((row): TransferWorkbookExportRow => ({
        row,
        pallets: selectedPallets(row),
        ordered: Boolean(orderedRows[rowKey(scope, row)]),
      })),
      fileName: scope === 'transfer' ? `${company}-traslados-${todayKey()}.xlsx` : `${company}-expediciones-${todayKey()}.xlsx`,
    })
    setNotice('Archivo Excel generado.')
  }

  async function exportComparison() {
    if (!comparisonResult || comparisonRows.length === 0) {
      setNotice('No hay comparativa para exportar.')
      return
    }
    await exportWarehouseComparisonWorkbook({
      company,
      periodLabel: `${comparisonResult.selectedPeriod} a ${comparisonResult.horizonEndPeriod}`,
      rows: comparisonRows,
      histogram,
      comments: comparisonComments,
      fileName: `${company}-comparativa-${todayKey()}.xlsx`,
    })
    setNotice('Comparativa exportada.')
  }

  function updateComparisonComment(article: string, comment: string) {
    setComparisonComments((current) => {
      const next = { ...current, [article]: comment }
      if (!comment.trim()) {
        delete next[article]
      }
      void saveComparisonComments(company, next)
      return next
    })
  }

  function clearComparisonComments() {
    setComparisonComments({})
    void saveComparisonComments(company, {})
    setNotice('Comentarios de comparativa limpiados.')
  }

  async function restoreFileVersion(versionId: string) {
    await runBusy('Restaurando carga...', async () => {
      const restored = await restoreFileSession(company, versionId)
      applyFileSessionState(company, restored)
      setPeriodByCompany((current) => ({
        ...current,
        [company]: resolveAvailablePeriod(current[company], restored.files.forecast?.availablePeriods),
      }))
      setComparisonPeriodByCompany((current) => ({
        ...current,
        [company]: resolveAvailablePeriod(current[company], restored.files.comparisonForecast?.availablePeriods),
      }))
      setNotice('Carga restaurada desde el historial.')
    })
  }

  async function copyText(text: string, label: string) {
    try {
      await navigator.clipboard.writeText(text)
      setNotice(`${label} copiado al portapapeles.`)
    } catch {
      setNotice('No se pudo copiar al portapapeles.')
    }
  }

  function setColumnFilter(table: TableKey, column: string, value: string) {
    setColumnFilters((current) => ({
      ...current,
      [table]: {
        ...(current[table] ?? {}),
        [column]: value,
      },
    }))
  }

  function toggleTableSort(table: TableKey, column: string) {
    setTableSorts((current) => {
      const currentSort = current[table]
      const direction = currentSort?.column === column && currentSort.direction === 'asc' ? 'desc' : 'asc'
      return {
        ...current,
        [table]: { column, direction },
      }
    })
  }

  function clearTableControls(table: TableKey) {
    setColumnFilters((current) => ({ ...current, [table]: {} }))
    setTableSorts((current) => {
      const next = { ...current }
      delete next[table]
      return next
    })
  }

  function tarimaFor(article: string): string {
    return tarimaMap.get(normalizeArticleKey(article).toUpperCase()) ?? ''
  }

  const currentSubtitle = sectionSubtitle(view)
  return (
    <main className="app-shell">
      <aside className="sidebar" aria-label="Modulos">
        <div className="brand-block">
          <img src="/brand-logo.png" alt="" />
          <div>
            <strong>Pronostico de Abasto</strong>
            <span>LOGISTICA & SUPPLY</span>
          </div>
        </div>
        <nav>
          <NavButton active={view === 'traslados'} icon={<Truck size={19} />} onClick={() => setView('traslados')}>
            Traslados
          </NavButton>
          <NavButton
            active={view === 'expediciones'}
            icon={<ClipboardList size={19} />}
            onClick={() => setView('expediciones')}
          >
            Expediciones
          </NavButton>
          <NavButton active={view === 'transito'} icon={<PackageCheck size={19} />} onClick={() => setView('transito')}>
            Transito
          </NavButton>
          <NavButton active={view === 'comparativa'} icon={<BarChart3 size={19} />} onClick={() => setView('comparativa')}>
            Comparativa
          </NavButton>
          <NavButton active={view === 'simulacion'} icon={<Play size={19} />} onClick={() => setView('simulacion')}>
            Simulacion
          </NavButton>
        </nav>
        <div className="sidebar-footer">
          <NavButton active={view === 'ajustes'} icon={<Settings size={19} />} onClick={() => setView('ajustes')}>
            Ajustes
          </NavButton>
          <div className="user-card">
            <span>US</span>
            <div>
              <strong>Sistemas</strong>
              <small>{company}</small>
            </div>
          </div>
        </div>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <div className="title-row">
              <h1>{sectionTitle(view)}</h1>
              {view === 'traslados' && <span className="badge">REABASTECIMIENTO OPERATIVO</span>}
            </div>
            <p>{currentSubtitle}</p>
          </div>
          <div className="topbar-actions">
            <div className="company-switch" aria-label="Empresa">
              {companies.map((item) => (
                <button
                  className={company === item ? 'active' : ''}
                  key={item}
                  type="button"
                  onClick={() => setCompany(item)}
                >
                  {item}
                </button>
              ))}
            </div>
            <div className="search-box">
              <Search size={18} />
              <input
                value={searchText}
                onChange={(event) => setSearchText(event.target.value)}
                onPaste={(event) => {
                  const pasted = event.clipboardData.getData('text')
                  const flattened = flattenPastedSearch(pasted)
                  if (flattened && flattened !== pasted.trim()) {
                    event.preventDefault()
                    setSearchText(insertAtSelection(event.currentTarget, flattened))
                  }
                }}
                placeholder="Buscar articulo, pallet, zona..."
              />
              {searchSummary.badge && (
                <button
                  className={searchSummary.unmatched.length > 0 ? 'search-badge warn' : 'search-badge'}
                  title={searchSummary.detail}
                  type="button"
                  onClick={() => {
                    if (searchSummary.unmatched.length > 0) {
                      void copyText(searchSummary.unmatched.join('\n'), 'Codigos sin resultado')
                    }
                  }}
                >
                  {searchSummary.badge}
                </button>
              )}
            </div>
          </div>
        </header>

        <section className="file-strip" aria-label="Carga de archivos">
          <FileLoader
            icon={<FileSpreadsheet size={18} />}
            title={view === 'comparativa' ? 'Inventario comparativa' : 'Inventario'}
            loaded={Boolean(activeInventoryFile)}
            onFile={view === 'comparativa' ? handleComparisonInventory : handleInventory}
          />
          <FileLoader
            icon={<Upload size={18} />}
            title={view === 'comparativa' ? 'Forecast comparativa' : 'Forecast'}
            loaded={Boolean(activeForecastFile)}
            onFile={view === 'comparativa' ? handleComparisonForecast : handleForecast}
          />
          {view !== 'comparativa' && (
            <FileLoader icon={<ClipboardList size={18} />} title="Expediciones" loaded={Boolean(files.expediciones)} onFile={handleExpediciones} />
          )}
          <div className="period-controls">
            <label>
              Periodo
              <select
                value={activePeriod}
                onChange={(event) =>
                  view === 'comparativa'
                    ? setComparisonPeriodByCompany((current) => ({ ...current, [company]: event.target.value }))
                    : setPeriodByCompany((current) => ({ ...current, [company]: event.target.value }))
                }
              >
                {activePeriods.map((period) => (
                  <option key={period} value={period}>
                    {period}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Horizonte
              <input
                type="number"
                min={1}
                max={26}
                value={horizonPeriods}
                onChange={(event) => setHorizonPeriods(Number(event.target.value))}
              />
            </label>
          </div>
        </section>

        <FileSessionHistory
          backupHref={backupUrl(company)}
          sessions={fileSessions}
          storage={fileStorage}
          onRestore={(versionId) => void restoreFileVersion(versionId)}
        />

        {(busy || notice) && (
          <div className={busy ? 'notice is-busy' : 'notice'}>
            {busy ? <Upload size={18} /> : <Check size={18} />}
            <span>{busy || notice}</span>
          </div>
        )}

        {view === 'traslados' && (
          <TransferView
            result={weeklyResult}
            rows={transferRows}
            tab={selectedTransferTab}
            onTab={setSelectedTransferTab}
            selectedCount={orderedTransferRows.length}
            preparedSet={preparedSet}
            tarimaFor={tarimaFor}
            orderedRows={orderedRows}
            selectedQuantity={selectedQuantity}
            selectedPallets={selectedPallets}
            columns={transferColumns}
            filters={columnFilters.transfer ?? {}}
            sort={tableSorts.transfer}
            onFilter={(column, value) => setColumnFilter('transfer', column, value)}
            onSort={(column) => toggleTableSort('transfer', column)}
            onClearTable={() => clearTableControls('transfer')}
            onCopy={(row) => void copyText(copyTransferRow(row, selectedQuantity(row), selectedPallets(row), tarimaFor(row.article), isPrepared(row, preparedSet)), 'Fila de traslado')}
            onToggleOrdered={(row, value) => toggleOrdered('transfer', row, value)}
            onOpenPallets={(row) => setPalletPane({ scope: 'transfer', key: rowKey('transfer', row), row })}
            onSend={(row) => void sendSingleToTransit('transfer', row)}
            onSendMarked={() => void sendMarkedToTransit('transfer')}
            onExport={() => void exportTransfer('transfer', transferRows)}
          />
        )}
        {view === 'expediciones' && (
          <ExpedicionesView
            result={expedicionesResult}
            rows={expeditionRows}
            tab={selectedExpeditionTab}
            onTab={setSelectedExpeditionTab}
            selectedCount={orderedExpeditionRows.length}
            preparedSet={preparedSet}
            tarimaFor={tarimaFor}
            orderedRows={orderedRows}
            selectedQuantity={selectedQuantity}
            selectedPallets={selectedPallets}
            columns={expeditionColumns}
            filters={columnFilters.expedition ?? {}}
            sort={tableSorts.expedition}
            onFilter={(column, value) => setColumnFilter('expedition', column, value)}
            onSort={(column) => toggleTableSort('expedition', column)}
            onClearTable={() => clearTableControls('expedition')}
            onCopy={(row) => void copyText(copyExpeditionRow(row, selectedQuantity(row), selectedPallets(row), tarimaFor(row.article), isPrepared(row, preparedSet)), 'Fila de expedicion')}
            onCopyExpeditions={(row) => void copyText(splitExpeditionNumbers(row.expeditionNumber).join('\n'), 'Expediciones')}
            onToggleOrdered={(row, value) => toggleOrdered('expedition', row, value)}
            onOpenPallets={(row) => setPalletPane({ scope: 'expedition', key: rowKey('expedition', row), row })}
            onSend={(row) => void sendSingleToTransit('expedition', row)}
            onSendMarked={() => void sendMarkedToTransit('expedition')}
            onExport={() => void exportTransfer('expedition', expeditionRows)}
          />
        )}
        {view === 'transito' && (
          <TransitView
            items={transit}
            tab={transitTab}
            onTab={setTransitTab}
            filters={columnFilters.transit ?? {}}
            sort={tableSorts.transit}
            onFilter={(column, value) => setColumnFilter('transit', column, value)}
            onSort={(column) => toggleTableSort('transit', column)}
            onClearTable={() => clearTableControls('transit')}
            onCopy={(row) => void copyText(copyTransitRow(row), 'Fila de transito')}
            onReceive={receiveTransit}
            onReceiveVisible={receiveVisibleTransit}
          />
        )}
        {view === 'comparativa' && (
          <ComparisonView
            result={comparisonResult}
            rows={comparisonRows}
            mode={comparisonMode}
            onMode={setComparisonMode}
            histogram={histogram}
            comments={comparisonComments}
            columns={comparisonColumns}
            filters={columnFilters.comparison ?? {}}
            sort={tableSorts.comparison}
            onFilter={(column, value) => setColumnFilter('comparison', column, value)}
            onSort={(column) => toggleTableSort('comparison', column)}
            onClearTable={() => clearTableControls('comparison')}
            onClearComments={clearComparisonComments}
            onCopy={(row) => void copyText(copyComparisonRow(row, comparisonComments[row.article] ?? row.automaticComment), 'Fila de comparativa')}
            onComment={updateComparisonComment}
            onExport={() => void exportComparison()}
          />
        )}
        {view === 'simulacion' && (
          <SimulationView
            result={scenario}
            months={scenarioMonths}
            buffer={scenarioBuffer}
            onMonths={setScenarioMonths}
            onBuffer={setScenarioBuffer}
            onReset={() => {
              setScenarioMonths(3)
              setScenarioBuffer(15)
            }}
          />
        )}
        {view === 'ajustes' && (
          <SettingsView
            key={settingsStateKey(settings)}
            settings={settings}
            onSave={(next) => void persistSettings(next)}
            onPreparedFile={handlePreparedArticles}
            onTarimaFile={handleTarimaSizes}
          />
        )}
      </section>

      {palletPane && (
        <PalletPane
          row={palletPane.row}
          rowKeyValue={palletPane.key}
          selected={selectedPallets(palletPane.row)}
          onClose={() => setPalletPane(undefined)}
          onSetSelection={(pallets) => setPalletSelection(palletPane.key, pallets)}
          onSelectFifo={(quantity) => selectFifoForQuantity(palletPane.row, palletPane.key, quantity)}
        />
      )}
    </main>
  )
}

function NavButton(props: {
  active: boolean
  icon: ReactNode
  children: ReactNode
  onClick: () => void
}) {
  return (
    <button className={props.active ? 'nav-button active' : 'nav-button'} type="button" onClick={props.onClick}>
      {props.icon}
      <span>{props.children}</span>
    </button>
  )
}

function FileLoader(props: {
  title: string
  loaded: boolean
  icon: ReactNode
  onFile: (file: File) => void | Promise<void>
}) {
  return (
    <label className={props.loaded ? 'file-loader loaded' : 'file-loader'}>
      <span>{props.icon}</span>
      <strong>{props.title}</strong>
      <small>{props.loaded ? 'Cargado' : 'Seleccionar Excel'}</small>
      <input
        type="file"
        accept=".xlsx,.xls"
        onChange={(event) => {
          const file = event.currentTarget.files?.[0]
          if (file) {
            void props.onFile(file)
          }
          event.currentTarget.value = ''
        }}
      />
    </label>
  )
}

function FileSessionHistory(props: {
  sessions: FileSessionInfo[]
  storage: 'd1' | 'r2'
  backupHref: string
  onRestore: (versionId: string) => void
}) {
  const visibleSessions = props.sessions.slice(0, 8)
  return (
    <section className="file-history" aria-label="Historial de cargas">
      <div className="file-history-head">
        <div>
          <span>
            <Database size={15} />
            Almacenamiento {props.storage === 'r2' ? 'R2' : 'D1'}
          </span>
          <strong>Ultimas cargas</strong>
        </div>
        <a className="secondary-button" href={props.backupHref}>
          <Download size={15} />
          Respaldo
        </a>
      </div>
      {visibleSessions.length > 0 ? (
        <div className="file-history-list">
          {visibleSessions.map((session) => (
            <div className={session.active ? 'file-history-item active' : 'file-history-item'} key={session.versionId}>
              <History size={15} />
              <div>
                <strong>{fileSessionLabel(session.kind)}</strong>
                <span>
                  {session.sourceName || 'Carga sin nombre'} · {formatBytes(session.payloadBytes)} · {formatDateTime(session.updatedAt)}
                </span>
              </div>
              {session.active ? (
                <span className="mini-pill">Activo</span>
              ) : (
                <button type="button" onClick={() => props.onRestore(session.versionId)}>
                  <RotateCcw size={14} />
                  Restaurar
                </button>
              )}
            </div>
          ))}
        </div>
      ) : (
        <p>No hay cargas guardadas todavia.</p>
      )}
    </section>
  )
}

function TransferView(props: {
  result?: ReturnType<typeof analyzeWeeklyForecast>
  rows: ArticleAnalysisResult[]
  tab: TransferTab
  onTab: (tab: TransferTab) => void
  selectedCount: number
  preparedSet: Set<string>
  tarimaFor: (article: string) => string
  orderedRows: Record<string, boolean>
  selectedQuantity: (row: AnalysisRow) => number
  selectedPallets: (row: AnalysisRow) => PalletInventoryDetail[]
  columns: TableColumn<ArticleAnalysisResult>[]
  filters: TableFilters
  sort?: TableSort
  onFilter: (column: string, value: string) => void
  onSort: (column: string) => void
  onClearTable: () => void
  onCopy: (row: ArticleAnalysisResult) => void
  onToggleOrdered: (row: ArticleAnalysisResult, value: boolean) => void
  onOpenPallets: (row: ArticleAnalysisResult) => void
  onSend: (row: ArticleAnalysisResult) => void
  onSendMarked: () => void
  onExport: () => void
}) {
  if (!props.result) {
    return <EmptyState text="Cargue inventario y forecast para calcular traslados." />
  }

  return (
    <section className="module">
      <SummaryGrid
        items={[
          ['Evaluados', props.result.articles.length],
          ['Criticos', props.result.criticalCount],
          ['Para traer', props.rows.filter((row) => canOrder(row, props.preparedSet)).length],
          ['Seleccionados', props.selectedCount],
        ]}
      />
      <SegmentedTabs
        value={props.tab}
        onChange={(value) => props.onTab(value as TransferTab)}
        options={[
          ['Todos', 'Todos'],
          ['ParaTraer', 'Para traer'],
          ['Criticos', 'Criticos'],
          ['Pendientes', 'Pendientes'],
          ['AltoVolumen', 'Alto volumen'],
          ['AlistadoSatelital', 'Alistado sat.'],
        ]}
      />
      <TableActions
        count={props.rows.length}
        selected={props.selectedCount}
        hasTableControls={hasTableControls(props.filters, props.sort)}
        onClearTable={props.onClearTable}
        onSend={props.onSendMarked}
        onExport={props.onExport}
      />
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th></th>
              {props.columns.map((column) => (
                <ColumnHeader
                  column={column}
                  filters={props.filters}
                  key={column.key}
                  sort={props.sort}
                  onFilter={props.onFilter}
                  onSort={props.onSort}
                />
              ))}
              <th></th>
            </tr>
          </thead>
          <tbody>
            {props.rows.map((row) => {
              const key = rowKey('transfer', row)
              const prepared = isPrepared(row, props.preparedSet)
              const quantity = props.selectedQuantity(row)
              return (
                <tr key={key}>
                  <td>
                    <input
                      aria-label="Seleccionar"
                      checked={Boolean(props.orderedRows[key])}
                      disabled={!canOrder(row, props.preparedSet)}
                      type="checkbox"
                      onChange={(event) => props.onToggleOrdered(row, event.currentTarget.checked)}
                    />
                  </td>
                  <td className="mono">{row.article}</td>
                  <td>{row.description || '-'}</td>
                  <td>{coverageText(row.coveragePeriods, props.result?.analyzedPeriods ?? 0)}</td>
                  <td>{formatNumber.format(row.forecastQuantity)}</td>
                  <td>{formatNumber.format(row.principalInventoryQuantity)}</td>
                  <td>{formatNumber.format(row.satelliteInventoryQuantity)}</td>
                  <td>{formatNumber.format(row.pendingTransitQuantity)}</td>
                  <td className="strong">{formatNumber.format(quantity)}</td>
                  <td>
                    <button className="link-button" type="button" onClick={() => props.onOpenPallets(row)}>
                      {props.selectedPallets(row).length}/{availablePallets(row).length}
                    </button>
                  </td>
                  <td>{props.tarimaFor(row.article) || '-'}</td>
                  <td><StatusPill tone={prepared ? 'neutral' : 'ok'}>{prepared ? 'Si' : 'No'}</StatusPill></td>
                  <td className="zones-cell">{formatZones(row)}</td>
                  <td>
                    <button
                      className="icon-action"
                      title="Copiar fila"
                      type="button"
                      onClick={() => props.onCopy(row)}
                    >
                      <Copy size={16} />
                    </button>
                    <button
                      className="icon-action"
                      disabled={!canOrder(row, props.preparedSet)}
                      type="button"
                      onClick={() => props.onSend(row)}
                    >
                      <Truck size={17} />
                    </button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function ExpedicionesView(props: {
  result?: ReturnType<typeof analyzeExpediciones>
  rows: ExpeditionLineResult[]
  tab: ExpeditionTab
  onTab: (tab: ExpeditionTab) => void
  selectedCount: number
  preparedSet: Set<string>
  tarimaFor: (article: string) => string
  orderedRows: Record<string, boolean>
  selectedQuantity: (row: AnalysisRow) => number
  selectedPallets: (row: AnalysisRow) => PalletInventoryDetail[]
  columns: TableColumn<ExpeditionLineResult>[]
  filters: TableFilters
  sort?: TableSort
  onFilter: (column: string, value: string) => void
  onSort: (column: string) => void
  onClearTable: () => void
  onCopy: (row: ExpeditionLineResult) => void
  onCopyExpeditions: (row: ExpeditionLineResult) => void
  onToggleOrdered: (row: ExpeditionLineResult, value: boolean) => void
  onOpenPallets: (row: ExpeditionLineResult) => void
  onSend: (row: ExpeditionLineResult) => void
  onSendMarked: () => void
  onExport: () => void
}) {
  if (!props.result) {
    return <EmptyState text="Cargue inventario y reporte de expediciones para revisar faltantes." />
  }

  return (
    <section className="module">
      <SummaryGrid
        items={[
          ['Evaluados', props.result.evaluated],
          ['Cubiertos', props.result.covered],
          ['Para traer', props.result.toTransfer],
          ['Seleccionados', props.selectedCount],
        ]}
      />
      <SegmentedTabs
        value={props.tab}
        onChange={(value) => props.onTab(value as ExpeditionTab)}
        options={[
          ['Todos', 'Todos'],
          ['ParaTraer', 'Para traer'],
          ['Cubiertas', 'Cubiertas'],
          ['SinSatelite', 'Sin satelite'],
          ['AlistadoSatelital', 'Alistado sat.'],
        ]}
      />
      <TableActions
        count={props.rows.length}
        selected={props.selectedCount}
        hasTableControls={hasTableControls(props.filters, props.sort)}
        onClearTable={props.onClearTable}
        onSend={props.onSendMarked}
        onExport={props.onExport}
      />
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th></th>
              {props.columns.map((column) => (
                <ColumnHeader
                  column={column}
                  filters={props.filters}
                  key={column.key}
                  sort={props.sort}
                  onFilter={props.onFilter}
                  onSort={props.onSort}
                />
              ))}
              <th></th>
            </tr>
          </thead>
          <tbody>
            {props.rows.map((row) => {
              const key = rowKey('expedition', row)
              const prepared = isPrepared(row, props.preparedSet)
              const quantity = props.selectedQuantity(row)
              return (
                <tr key={key}>
                  <td>
                    <input
                      aria-label="Seleccionar"
                      checked={Boolean(props.orderedRows[key])}
                      disabled={!canOrder(row, props.preparedSet)}
                      type="checkbox"
                      onChange={(event) => props.onToggleOrdered(row, event.currentTarget.checked)}
                    />
                  </td>
                  <td>
                    <button className="link-button" type="button" onClick={() => props.onCopyExpeditions(row)}>
                      {row.expeditionNumber || '-'}
                    </button>
                  </td>
                  <td className="mono">{row.article}</td>
                  <td>{row.description || '-'}</td>
                  <td>{formatNumber.format(row.demandQuantity)}</td>
                  <td>{formatNumber.format(row.principalInventoryQuantity)}</td>
                  <td>{formatNumber.format(row.satelliteInventoryQuantity)}</td>
                  <td>{formatNumber.format(row.pendingTransitQuantity)}</td>
                  <td className="strong">{formatNumber.format(quantity)}</td>
                  <td>
                    <button className="link-button" type="button" onClick={() => props.onOpenPallets(row)}>
                      {props.selectedPallets(row).length}/{availablePallets(row).length}
                    </button>
                  </td>
                  <td>{props.tarimaFor(row.article) || '-'}</td>
                  <td><StatusPill tone={prepared ? 'neutral' : row.remainingShortageAfterTransfer > 0 ? 'warn' : 'ok'}>{expeditionStatus(row, prepared)}</StatusPill></td>
                  <td>{formatNumber.format(Math.max(0, row.calculatedTransferNeedQuantity - quantity))}</td>
                  <td>
                    <button
                      className="icon-action"
                      title="Copiar fila"
                      type="button"
                      onClick={() => props.onCopy(row)}
                    >
                      <Copy size={16} />
                    </button>
                    <button
                      className="icon-action"
                      disabled={!canOrder(row, props.preparedSet)}
                      type="button"
                      onClick={() => props.onSend(row)}
                    >
                      <Truck size={17} />
                    </button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function TransitView(props: {
  items: TransitItem[]
  tab: TransitTab
  onTab: (tab: TransitTab) => void
  filters: TableFilters
  sort?: TableSort
  onFilter: (column: string, value: string) => void
  onSort: (column: string) => void
  onClearTable: () => void
  onCopy: (row: TransitItem) => void
  onReceive: (id: string) => void
  onReceiveVisible: (items: TransitItem[]) => void
}) {
  const pending = props.items.filter((item) => !item.receivedAt && !item.reconciledAt)
  const history = props.items.filter((item) => item.receivedAt || item.reconciledAt)
  const tabRows = props.tab === 'Pendientes' ? pending : props.tab === 'Recibidos' ? history : props.items
  const columns: TableColumn<TransitItem>[] = [
    { key: 'article', label: 'Articulo', text: (row) => row.article },
    { key: 'description', label: 'Descripcion', text: (row) => row.description },
    { key: 'pallet', label: 'Pallet', text: (row) => row.pallet },
    { key: 'quantity', label: 'Cantidad', text: (row) => formatNumber.format(row.quantity), number: (row) => row.quantity },
    { key: 'origin', label: 'Origen', text: (row) => row.origin || resolveStorageOrigin(row.storageZone) },
    { key: 'zone', label: 'Zona origen', text: (row) => row.storageZone },
    { key: 'location', label: 'Ubicacion', text: (row) => row.location },
    { key: 'status', label: 'Estado', text: (row) => row.receivedAt ? 'Recibido' : 'Pendiente' },
  ]
  const visible = applyTableControls(tabRows, columns, props.filters, props.sort)
  return (
    <section className="module">
      <SummaryGrid
        items={[
          ['Pendientes', pending.length],
          ['Historial', history.length],
          ['Unidades pendientes', pending.reduce((sum, item) => sum + item.quantity, 0)],
          ['Articulos', new Set(pending.map((item) => item.article)).size],
        ]}
      />
      <SegmentedTabs
        value={props.tab}
        onChange={(value) => props.onTab(value as TransitTab)}
        options={[
          ['Pendientes', 'Pendientes'],
          ['Recibidos', 'Recibidos'],
          ['Todos', 'Todos'],
        ]}
      />
      <div className="table-actions">
        <span>{visible.length.toLocaleString('es-CR')} filas visibles</span>
        <div>
          {hasTableControls(props.filters, props.sort) && (
            <button className="secondary-button" type="button" onClick={props.onClearTable}>
              <Eraser size={16} /> Limpiar filtros
            </button>
          )}
          <button className="secondary-button" type="button" onClick={() => props.onReceiveVisible(visible)}>
            Confirmar visibles
          </button>
        </div>
      </div>
      <div className="table-wrap transit-table">
        <table>
          <thead>
            <tr>
              {columns.map((column) => (
                <ColumnHeader
                  column={column}
                  filters={props.filters}
                  key={column.key}
                  sort={props.sort}
                  onFilter={props.onFilter}
                  onSort={props.onSort}
                />
              ))}
              <th></th>
            </tr>
          </thead>
          <tbody>
            {visible.map((item) => (
              <tr key={item.id}>
                <td className="mono">{item.article}</td>
                <td>{item.description || '-'}</td>
                <td className="mono">{item.pallet}</td>
                <td>{formatNumber.format(item.quantity)}</td>
                <td>{item.origin || resolveStorageOrigin(item.storageZone)}</td>
                <td>{item.storageZone}</td>
                <td>{item.location}</td>
                <td><StatusPill tone={item.receivedAt ? 'ok' : 'warn'}>{item.receivedAt ? 'Recibido' : 'Pendiente'}</StatusPill></td>
                <td>
                  <button className="icon-action" title="Copiar fila" type="button" onClick={() => props.onCopy(item)}>
                    <Copy size={16} />
                  </button>
                  {!item.receivedAt && (
                    <button className="small-button" type="button" onClick={() => props.onReceive(item.id)}>
                      Recibir
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function ComparisonView(props: {
  result?: ReturnType<typeof analyzeWarehouseComparison>
  rows: WarehouseComparisonRow[]
  mode: 'Tabla' | 'Histograma'
  onMode: (mode: 'Tabla' | 'Histograma') => void
  histogram: ReturnType<typeof buildWarehouseComparisonHistogram>
  comments: Record<string, string>
  columns: TableColumn<WarehouseComparisonRow>[]
  filters: TableFilters
  sort?: TableSort
  onFilter: (column: string, value: string) => void
  onSort: (column: string) => void
  onClearTable: () => void
  onClearComments: () => void
  onCopy: (row: WarehouseComparisonRow) => void
  onComment: (article: string, comment: string) => void
  onExport: () => void
}) {
  if (!props.result) {
    return <EmptyState text="Cargue inventario y forecast para ver comparativa." />
  }

  return (
    <section className="module">
      <SummaryGrid
        items={[
          ['Articulos SERVICA', props.rows.length],
          ['No solicita', props.rows.filter((row) => !row.hasFutureForecast).length],
          ['Periodos', props.result.periodCount],
          ['Inv Servica', props.rows.reduce((sum, row) => sum + row.servicaQuantity, 0)],
        ]}
      />
      <div className="table-actions">
        <SegmentedTabs
          value={props.mode}
          onChange={(value) => props.onMode(value as 'Tabla' | 'Histograma')}
          options={[
            ['Tabla', 'Tabla'],
            ['Histograma', 'Histograma'],
          ]}
        />
        <button className="secondary-button" type="button" onClick={props.onExport}>
          Exportar comparativa
        </button>
        {hasTableControls(props.filters, props.sort) && (
          <button className="secondary-button" type="button" onClick={props.onClearTable}>
            <Eraser size={16} /> Limpiar filtros
          </button>
        )}
        <button className="secondary-button" type="button" onClick={props.onClearComments}>
          Limpiar comentarios
        </button>
      </div>
      {props.mode === 'Tabla' ? (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                {props.columns.map((column) => (
                  <ColumnHeader
                    column={column}
                    filters={props.filters}
                    key={column.key}
                    sort={props.sort}
                    onFilter={props.onFilter}
                    onSort={props.onSort}
                  />
                ))}
                <th></th>
              </tr>
            </thead>
            <tbody>
              {props.rows.map((row) => (
                <tr key={row.article}>
                  <td className="mono">{row.article}</td>
                  <td>{row.description || '-'}</td>
                  <td>{formatNumber.format(row.oloQuantity)}</td>
                  <td>{formatNumber.format(row.servicaQuantity)}</td>
                  <td>{formatNumber.format(row.forecastQuantity)}</td>
                  <td>{row.coveragePeriods === undefined ? '-' : `${formatNumber.format(row.coveragePeriods)} per.`}</td>
                  <td>
                    <input
                      value={props.comments[row.article] ?? row.automaticComment}
                      onChange={(event) => props.onComment(row.article, event.target.value)}
                    />
                  </td>
                  <td>
                    <button className="icon-action" title="Copiar fila" type="button" onClick={() => props.onCopy(row)}>
                      <Copy size={16} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="histogram">
          {props.histogram.map((bucket) => (
            <div className="histogram-row" key={bucket.label}>
              <span>{bucket.label}</span>
              <div><i style={{ width: `${Math.max(3, bucket.percentage)}%` }} /></div>
              <strong>{bucket.articleCount}</strong>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}

function SimulationView(props: {
  result: ReturnType<typeof simulateScenario>
  months: number
  buffer: number
  onMonths: (value: number) => void
  onBuffer: (value: number) => void
  onReset: () => void
}) {
  const maxLoad = Math.max(1, ...props.result.periods.map((period) => period.transferLoad))
  if (props.result.periods.length === 0) {
    return <EmptyState text="Cargue inventario y forecast para simular carga logistica." />
  }

  return (
    <section className="module">
      <div className="simulation-controls">
        <label>
          Meses
          <input type="number" min={1} max={12} value={props.months} onChange={(event) => props.onMonths(Number(event.target.value))} />
        </label>
        <label>
          Buffer %
          <input type="number" min={0} max={100} value={props.buffer} onChange={(event) => props.onBuffer(Number(event.target.value))} />
        </label>
        <button className="secondary-button" type="button" onClick={props.onReset}>
          <RotateCcw size={16} /> Restablecer
        </button>
      </div>
      <SummaryGrid
        items={[
          ['Periodos', props.result.periods.length],
          ['Demanda EPA', props.result.periods.reduce((sum, period) => sum + (period.epaDemand ?? 0), 0)],
          ['Demanda Cofersa', props.result.periods.reduce((sum, period) => sum + (period.cofersaDemand ?? 0), 0)],
          ['Carga traslado', props.result.periods.reduce((sum, period) => sum + period.transferLoad, 0)],
        ]}
      />
      <div className="scenario-grid">
        {props.result.periods.map((period) => (
          <div className="scenario-period" key={period.label}>
            <span>{period.label}</span>
            <strong>{formatNumber.format(period.transferLoad)}</strong>
            <div><i style={{ height: `${Math.max(4, (period.transferLoad / maxLoad) * 100)}%` }} /></div>
            <small>EPA {formatNumber.format(period.epaDemand ?? 0)} · Cofersa {formatNumber.format(period.cofersaDemand ?? 0)}</small>
          </div>
        ))}
      </div>
    </section>
  )
}

function SettingsView(props: {
  settings: AppSettings
  onSave: (settings: AppSettings) => void
  onPreparedFile: (file: File) => void | Promise<void>
  onTarimaFile: (file: File) => void | Promise<void>
}) {
  const [satelliteZones, setSatelliteZones] = useState(props.settings.satelliteZones.join('\n'))
  const [redPercent, setRedPercent] = useState(props.settings.thresholds.redPercent)
  const [healthyPercent, setHealthyPercent] = useState(props.settings.thresholds.healthyPercent)
  const [preparedText, setPreparedText] = useState(formatPreparedText(props.settings.preparedArticles))
  const [tarimaText, setTarimaText] = useState(formatTarimaText(props.settings.tarimaSizes))

  return (
    <section className="module settings-grid">
      <div className="settings-panel">
        <h2>Zonas satelite</h2>
        <textarea value={satelliteZones} onChange={(event) => setSatelliteZones(event.target.value)} />
      </div>
      <div className="settings-panel">
        <h2>Umbrales</h2>
        <label>
          Rojo %
          <input type="number" value={redPercent} onChange={(event) => setRedPercent(Number(event.target.value))} />
        </label>
        <label>
          Verde %
          <input type="number" value={healthyPercent} onChange={(event) => setHealthyPercent(Number(event.target.value))} />
        </label>
      </div>
      <div className="settings-panel">
        <div className="panel-heading">
          <h2>Alistado satelital</h2>
          <FileLoader icon={<Upload size={16} />} title="Importar" loaded={props.settings.preparedArticles.length > 0} onFile={props.onPreparedFile} />
        </div>
        <textarea value={preparedText} onChange={(event) => setPreparedText(event.target.value)} />
      </div>
      <div className="settings-panel">
        <div className="panel-heading">
          <h2>Tamano tarima</h2>
          <FileLoader icon={<Upload size={16} />} title="Importar" loaded={props.settings.tarimaSizes.length > 0} onFile={props.onTarimaFile} />
        </div>
        <textarea value={tarimaText} onChange={(event) => setTarimaText(event.target.value)} />
      </div>
      <button
        className="primary-button"
        type="button"
        onClick={() =>
          props.onSave({
            company: props.settings.company,
            satelliteZones: splitLines(satelliteZones),
            excludedZones: [],
            thresholds: { redPercent, healthyPercent },
            preparedArticles: parsePreparedText(preparedText),
            tarimaSizes: parseTarimaText(tarimaText),
          })
        }
      >
        Guardar ajustes
      </button>
    </section>
  )
}

function PalletPane(props: {
  row: AnalysisRow
  rowKeyValue: string
  selected: PalletInventoryDetail[]
  onClose: () => void
  onSetSelection: (pallets: PalletInventoryDetail[]) => void
  onSelectFifo: (quantity: number) => void
}) {
  const available = availablePallets(props.row)
  const selectedKeys = new Set(props.selected.map(palletIdentity))
  const quantity = props.selected.reduce((sum, pallet) => sum + pallet.quantity, 0)
  const surplus = Math.max(0, quantity - props.row.calculatedTransferNeedQuantity)
  const pending = Math.max(0, props.row.calculatedTransferNeedQuantity - quantity)

  return (
    <div className="pane-overlay" onMouseDown={props.onClose}>
      <aside className="pallet-pane" onMouseDown={(event) => event.stopPropagation()}>
        <header>
          <div>
            <h2>Palets a traer</h2>
            <p>{props.row.article} · {props.row.description || 'Sin descripcion'}</p>
          </div>
          <button className="icon-action" type="button" onClick={props.onClose}><X size={18} /></button>
        </header>
        <div className="pane-metrics">
          <span><small>Cantidad</small><strong>{formatNumber.format(quantity)}</strong></span>
          <span><small>Palets</small><strong>{props.selected.length}</strong></span>
          <span><small>Sobrante</small><strong>{formatNumber.format(surplus)}</strong></span>
          <span><small>Pendiente</small><strong>{formatNumber.format(pending)}</strong></span>
        </div>
        <label className="quantity-selector">
          Cantidad objetivo
          <input type="number" min={0} value={Math.round(quantity)} onChange={(event) => props.onSelectFifo(Number(event.target.value))} />
        </label>
        <button className="secondary-button" type="button" onClick={() => props.onSetSelection(available)}>
          Seleccionar todos
        </button>
        <div className="pallet-list">
          {available.map((pallet) => {
            const key = palletIdentity(pallet)
            const checked = selectedKeys.has(key)
            return (
              <label className="pallet-option" key={key}>
                <input
                  checked={checked}
                  type="checkbox"
                  onChange={(event) => {
                    const next = event.currentTarget.checked
                      ? [...props.selected, pallet]
                      : props.selected.filter((item) => palletIdentity(item) !== key)
                    props.onSetSelection(next)
                  }}
                />
                <span>
                  <strong>{pallet.pallet || 'Sin pallet'}</strong>
                  <small>{pallet.validationDate ?? 'Sin fecha'} · {pallet.location || 'Sin ubicacion'} · {pallet.storageZone}</small>
                </span>
                <b>{formatNumber.format(pallet.quantity)}</b>
              </label>
            )
          })}
        </div>
        <button className="primary-button" type="button" onClick={props.onClose}>
          Confirmar seleccion de pallets
        </button>
      </aside>
    </div>
  )
}

function SummaryGrid(props: { items: Array<[string, number]> }) {
  return (
    <div className="summary-grid">
      {props.items.map(([label, value]) => (
        <div className="summary-tile" key={label}>
          <span>{label}</span>
          <strong>{formatNumber.format(value)}</strong>
        </div>
      ))}
    </div>
  )
}

function TableActions(props: {
  count: number
  selected: number
  hasTableControls: boolean
  onClearTable: () => void
  onSend: () => void
  onExport: () => void
}) {
  return (
    <div className="table-actions">
      <span>{props.count.toLocaleString('es-CR')} filas visibles · {props.selected.toLocaleString('es-CR')} seleccionadas</span>
      <div>
        {props.hasTableControls && (
          <button className="secondary-button" type="button" onClick={props.onClearTable}>
            <Eraser size={16} /> Limpiar filtros
          </button>
        )}
        <button className="secondary-button" type="button" onClick={props.onExport}>
          Exportar requisicion
        </button>
        <button className="primary-button" type="button" onClick={props.onSend}>
          Mandar a pedir
        </button>
      </div>
    </div>
  )
}

function ColumnHeader<T>(props: {
  column: TableColumn<T>
  filters: TableFilters
  sort?: TableSort
  onFilter: (column: string, value: string) => void
  onSort: (column: string) => void
}) {
  const activeSort = props.sort?.column === props.column.key ? props.sort.direction : undefined
  return (
    <th>
      <div className="column-header">
        <button
          className={activeSort ? 'sort-button active' : 'sort-button'}
          title={`Ordenar ${props.column.label}`}
          type="button"
          onClick={() => props.onSort(props.column.key)}
        >
          <span>{props.column.label}</span>
          <ArrowDownUp size={13} />
          {activeSort && <small>{activeSort === 'asc' ? 'Asc' : 'Desc'}</small>}
        </button>
        <input
          aria-label={`Filtrar ${props.column.label}`}
          value={props.filters[props.column.key] ?? ''}
          onChange={(event) => props.onFilter(props.column.key, event.target.value)}
          placeholder="Filtrar"
        />
      </div>
    </th>
  )
}

function SegmentedTabs(props: { value: string; options: Array<[string, string]>; onChange: (value: string) => void }) {
  return (
    <div className="segmented-tabs">
      {props.options.map(([value, label]) => (
        <button className={props.value === value ? 'active' : ''} key={value} type="button" onClick={() => props.onChange(value)}>
          {label}
        </button>
      ))}
    </div>
  )
}

function StatusPill(props: { tone: 'ok' | 'warn' | 'danger' | 'neutral'; children: ReactNode }) {
  return <span className={`status-pill ${props.tone}`}>{props.children}</span>
}

function EmptyState(props: { text: string }) {
  return (
    <div className="empty-state">
      <AlertTriangle size={22} />
      <span>{props.text}</span>
    </div>
  )
}

function filterTransferRows(rows: ArticleAnalysisResult[], tab: TransferTab, text: string, preparedSet: Set<string>): ArticleAnalysisResult[] {
  const nonPrepared = rows.filter((row) => !isPrepared(row, preparedSet))
  const highVolumeCutoff = [...nonPrepared].sort((a, b) => b.transferSuggestionQuantity - a.transferSuggestionQuantity)[Math.max(0, Math.floor(nonPrepared.length * 0.2) - 1)]?.transferSuggestionQuantity ?? Number.MAX_SAFE_INTEGER
  const tabRows = rows.filter((row) => {
    const prepared = isPrepared(row, preparedSet)
    if (tab === 'AlistadoSatelital') return prepared
    if (prepared) return false
    if (tab === 'ParaTraer') return canOrder(row, preparedSet)
    if (tab === 'Criticos') return row.status === 'Critical'
    if (tab === 'Pendientes') return row.remainingShortageAfterTransfer > 0
    if (tab === 'AltoVolumen') return row.transferSuggestionQuantity >= highVolumeCutoff
    return true
  })
  return filterRows(tabRows, text)
}

function filterExpeditionRows(rows: ExpeditionLineResult[], tab: ExpeditionTab, text: string, preparedSet: Set<string>): ExpeditionLineResult[] {
  const tabRows = rows.filter((row) => {
    const prepared = isPrepared(row, preparedSet)
    if (tab === 'AlistadoSatelital') return prepared
    if (prepared) return false
    if (tab === 'ParaTraer') return canOrder(row, preparedSet)
    if (tab === 'Cubiertas') return row.transferSuggestionQuantity <= 0 && row.remainingShortageAfterTransfer <= 0
    if (tab === 'SinSatelite') return row.satelliteInventoryQuantity <= 0 && row.remainingShortageAfterTransfer > 0
    return true
  })
  return filterRows(tabRows, text)
}

function filterRows<T extends AnalysisRow>(rows: T[], text: string): T[] {
  if (!normalizeSearchText(text)) {
    return rows
  }
  return rows.filter((row) => matchesSearch(rowSearchHaystack(row), text))
}

function filterComparisonRows(
  rows: WarehouseComparisonRow[],
  text: string,
  comments: Record<string, string>,
): WarehouseComparisonRow[] {
  if (!normalizeSearchText(text)) {
    return rows
  }
  return rows.filter((row) =>
    matchesSearch(`${row.article} ${row.description} ${comments[row.article] ?? row.automaticComment}`, text),
  )
}

function rowSearchHaystack(row: AnalysisRow): string {
  return [
    row.article,
    row.description,
    availablePallets(row).map((pallet) => `${pallet.pallet} ${pallet.storageZone} ${pallet.location}`).join(' '),
    row.satelliteZones.map((zone) => zone.storageZone).join(' '),
    isExpedition(row) ? row.expeditionNumber : '',
  ].join(' ')
}

function transitSearchHaystack(row: TransitItem): string {
  return [
    row.article,
    row.description,
    row.pallet,
    row.quantity,
    row.origin,
    row.storageZone,
    row.location,
    row.receivedAt ? 'Recibido' : 'Pendiente',
  ].join(' ')
}

function parseSearchQuery(text: string): { alternatives: string[]; terms: string[] } {
  const listParts = text
    .split(/[\r\n\t,;]+/)
    .map((part) => normalizeSearchText(part))
    .filter(Boolean)

  if (listParts.length > 1) {
    return { alternatives: [...new Set(listParts)], terms: [] }
  }

  return {
    alternatives: [],
    terms: normalizeSearchText(text).split(' ').filter(Boolean),
  }
}

function matchesSearch(haystackText: string, text: string): boolean {
  const haystack = normalizeSearchText(haystackText)
  const query = parseSearchQuery(text)
  if (query.alternatives.length > 0) {
    return query.alternatives.some((alternative) => haystack.includes(alternative))
  }
  return query.terms.every((term) => haystack.includes(term))
}

function summarizeSearch<T>(
  rows: T[],
  text: string,
  haystack: (row: T) => string,
): { badge: string; detail: string; unmatched: string[] } {
  const query = parseSearchQuery(text)
  if (query.alternatives.length === 0) {
    return { badge: '', detail: '', unmatched: [] }
  }

  const normalizedRows = rows.map((row) => normalizeSearchText(haystack(row)))
  const unmatched = query.alternatives.filter((term) => !normalizedRows.some((row) => row.includes(term)))
  const found = query.alternatives.length - unmatched.length
  return {
    badge: `${found}/${query.alternatives.length}`,
    detail: unmatched.length > 0
      ? `${unmatched.length.toLocaleString('es-CR')} sin resultado. Toca para copiarlos.`
      : 'Todos los codigos pegados tienen resultado.',
    unmatched,
  }
}

function flattenPastedSearch(text: string): string {
  const parts = text.split(/[\r\n\t]+/).map((part) => part.trim()).filter(Boolean)
  return parts.length > 1 ? parts.join(', ') : text.trim()
}

function insertAtSelection(input: HTMLInputElement, value: string): string {
  const current = input.value
  const start = input.selectionStart ?? current.length
  const end = input.selectionEnd ?? current.length
  return `${current.slice(0, start)}${value}${current.slice(end)}`
}

function hasTableControls(filters: TableFilters, sort?: TableSort): boolean {
  return Boolean(sort) || Object.values(filters).some((value) => value.trim())
}

function buildOrderState(
  orderedRows: Record<string, boolean>,
  selectedPalletKeys: Record<string, string[]>,
): Record<string, OrderStateEntry> {
  const keys = new Set([...Object.keys(orderedRows), ...Object.keys(selectedPalletKeys)])
  return Object.fromEntries(
    [...keys]
      .map((key) => [
        key,
        {
          ordered: Boolean(orderedRows[key]),
          selectedPalletKeys: selectedPalletKeys[key] ?? [],
        },
      ] as const)
      .filter(([, entry]) => entry.ordered || entry.selectedPalletKeys.length > 0),
  )
}

function copyTransferRow(
  row: ArticleAnalysisResult,
  quantity: number,
  pallets: PalletInventoryDetail[],
  tarima: string,
  prepared: boolean,
): string {
  return toClipboardRow([
    row.article,
    row.description,
    row.coveragePeriods === undefined ? '-' : row.coveragePeriods,
    row.forecastQuantity,
    row.principalInventoryQuantity,
    row.satelliteInventoryQuantity,
    row.pendingTransitQuantity,
    quantity,
    pallets.map((pallet) => pallet.pallet).join(', '),
    tarima || '-',
    prepared ? 'Si' : 'No',
    formatZones(row),
  ])
}

function copyExpeditionRow(
  row: ExpeditionLineResult,
  quantity: number,
  pallets: PalletInventoryDetail[],
  tarima: string,
  prepared: boolean,
): string {
  return toClipboardRow([
    row.expeditionNumber,
    row.article,
    row.description,
    row.demandQuantity,
    row.principalInventoryQuantity,
    row.satelliteInventoryQuantity,
    row.pendingTransitQuantity,
    quantity,
    pallets.map((pallet) => pallet.pallet).join(', '),
    tarima || '-',
    expeditionStatus(row, prepared),
    Math.max(0, row.calculatedTransferNeedQuantity - quantity),
  ])
}

function copyTransitRow(row: TransitItem): string {
  return toClipboardRow([
    row.article,
    row.description,
    row.pallet,
    row.quantity,
    row.origin || resolveStorageOrigin(row.storageZone),
    row.storageZone,
    row.location,
    row.receivedAt ? 'Recibido' : 'Pendiente',
    row.createdAt,
    row.receivedAt ?? '',
  ])
}

function copyComparisonRow(row: WarehouseComparisonRow, comment: string): string {
  return toClipboardRow([
    row.article,
    row.description,
    row.oloQuantity,
    row.servicaQuantity,
    row.forecastQuantity,
    row.coveragePeriods ?? '-',
    comment,
  ])
}

function toClipboardRow(values: Array<string | number>): string {
  return values.map((value) => String(value).replace(/\r?\n/g, ' | ')).join('\t')
}

function splitExpeditionNumbers(value: string): string[] {
  return value.split(';').map((item) => item.trim()).filter(Boolean)
}

function canOrder(row: AnalysisRow, preparedSet: Set<string>): boolean {
  return !isPrepared(row, preparedSet) && row.transferSuggestionQuantity > 0 && availablePallets(row).length > 0
}

function isPrepared(row: AnalysisRow, preparedSet: Set<string>): boolean {
  return preparedSet.has(normalizeArticleKey(row.article).toUpperCase())
}

function availablePallets(row: AnalysisRow): PalletInventoryDetail[] {
  return row.availablePallets.length > 0 ? row.availablePallets : row.suggestedPallets
}

function rowKey(scope: PalletScope, row: AnalysisRow): string {
  const base = normalizeArticleKey(row.article).toUpperCase()
  return scope === 'expedition' && isExpedition(row)
    ? `${scope}|${base}|${row.expeditionNumber}`
    : `${scope}|${base}`
}

function isExpedition(row: AnalysisRow): row is ExpeditionLineResult {
  return 'expeditionNumber' in row
}

function palletIdentity(pallet: PalletInventoryDetail): string {
  return [
    pallet.pallet.trim().toUpperCase(),
    pallet.storageZone.trim().toUpperCase(),
    pallet.location.trim().toUpperCase(),
    pallet.validationDate ?? '',
  ].join('|')
}

function comparePallets(a: PalletInventoryDetail, b: PalletInventoryDetail): number {
  return (
    (a.validationDate ?? '9999-12-31').localeCompare(b.validationDate ?? '9999-12-31') ||
    a.pallet.localeCompare(b.pallet) ||
    a.storageZone.localeCompare(b.storageZone)
  )
}

function buildTransitItem(
  company: Company,
  periodKey: string,
  row: AnalysisRow,
  pallet: PalletInventoryDetail,
  tarimaSize: string,
): TransitItem {
  return {
    id: crypto.randomUUID(),
    company,
    periodKey,
    article: row.article,
    description: row.description,
    pallet: pallet.pallet,
    quantity: pallet.quantity,
    storageZone: pallet.storageZone,
    location: pallet.location,
    validationDate: pallet.validationDate,
    tarimaSize,
    origin: resolveStorageOrigin(pallet.storageZone),
    createdAt: new Date().toISOString(),
  }
}

function simulateCombinedScenario(
  filesByCompany: Record<Company, LoadedFiles>,
  periodByCompany: Record<Company, string>,
  settingsByCompany: Record<Company, AppSettings>,
  horizonMonths: number,
  safetyBufferPercent: number,
): ReturnType<typeof simulateScenario> {
  const byLabel = new Map<string, { label: string; epaDemand: number; cofersaDemand: number; demand: number; transferLoad: number }>()
  let hasEpa = false
  let hasCofersa = false

  for (const item of companies) {
    const files = filesByCompany[item]
    const selectedPeriod = periodByCompany[item] || files.forecast?.availablePeriods[0] || ''
    if (!files.inventory || !files.forecast || !selectedPeriod) {
      continue
    }

    const result = simulateScenario({
      inventory: files.inventory.positions,
      forecast: files.forecast.entries,
      availablePeriods: files.forecast.availablePeriods,
      selectedPeriod,
      horizonMonths,
      safetyBufferPercent,
      satelliteZones: settingsByCompany[item].satelliteZones,
    })

    if (item === 'EPA') {
      hasEpa = true
    } else {
      hasCofersa = true
    }

    for (const period of result.periods) {
      const existing = byLabel.get(period.label) ?? {
        label: period.label,
        epaDemand: 0,
        cofersaDemand: 0,
        demand: 0,
        transferLoad: 0,
      }
      if (item === 'EPA') {
        existing.epaDemand += period.demand
      } else {
        existing.cofersaDemand += period.demand
      }
      existing.demand += period.demand
      existing.transferLoad += period.transferLoad
      byLabel.set(period.label, existing)
    }
  }

  return {
    periods: [...byLabel.values()],
    hasEpa,
    hasCofersa,
  }
}

function buildArticleSet(items: CatalogArticle[]): Set<string> {
  return new Set(items.map((item) => normalizeArticleKey(item.article).toUpperCase()).filter(Boolean))
}

function buildTarimaMap(items: TarimaSizeEntry[]): Map<string, string> {
  return new Map(items.map((item) => [normalizeArticleKey(item.article).toUpperCase(), item.sizeCode]))
}

function normalizeCatalogArticles(items: CatalogArticle[]): CatalogArticle[] {
  const byArticle = new Map<string, CatalogArticle>()
  for (const item of items) {
    const article = item.article.trim()
    if (article) {
      byArticle.set(article.toUpperCase(), { article, description: item.description.trim() })
    }
  }
  return [...byArticle.values()].sort((a, b) => a.article.localeCompare(b.article))
}

function normalizeTarimaSizes(items: TarimaSizeEntry[]): TarimaSizeEntry[] {
  const byArticle = new Map<string, TarimaSizeEntry>()
  for (const item of items) {
    const article = item.article.trim()
    const sizeCode = normalizeTarimaCode(item.sizeCode)
    if (article && sizeCode) {
      byArticle.set(article.toUpperCase(), { article, description: item.description.trim(), sizeCode })
    }
  }
  return [...byArticle.values()].sort((a, b) => a.article.localeCompare(b.article))
}

function normalizeTarimaCode(value: string): string {
  const token = normalizeSearchText(value).replace(/[^A-Z0-9]/g, '')
  if (['S', '1', 'SENCILLA', 'SIMPLE'].includes(token)) return 'S'
  if (['D', '2', 'DOBLE'].includes(token)) return 'D'
  if (['T', '3', 'TRIPLE'].includes(token)) return 'T'
  return ''
}

function splitLines(value: string): string[] {
  return value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
}

function parsePreparedText(value: string): CatalogArticle[] {
  return normalizeCatalogArticles(
    splitLines(value).map((line) => {
      const [article, ...rest] = line.split(/\t|,/)
      return { article: article?.trim() ?? '', description: rest.join(' ').trim() }
    }),
  )
}

function parseTarimaText(value: string): TarimaSizeEntry[] {
  return normalizeTarimaSizes(
    splitLines(value).map((line) => {
      const [article, sizeCode, ...description] = line.split(/\t|,/)
      return {
        article: article?.trim() ?? '',
        sizeCode: sizeCode?.trim() ?? '',
        description: description.join(' ').trim(),
      }
    }),
  )
}

function formatPreparedText(items: CatalogArticle[]): string {
  return items.map((item) => `${item.article}\t${item.description}`).join('\n')
}

function formatTarimaText(items: TarimaSizeEntry[]): string {
  return items.map((item) => `${item.article}\t${item.sizeCode}\t${item.description}`).join('\n')
}

function fileSessionLabel(kind: FileSessionKind): string {
  return {
    inventory: 'Inventario',
    forecast: 'Forecast',
    expediciones: 'Expediciones',
    comparisonInventory: 'Inventario comparativa',
    comparisonForecast: 'Forecast comparativa',
  }[kind]
}

function formatBytes(value: number): string {
  if (value < 1024) {
    return `${value} B`
  }
  if (value < 1024 * 1024) {
    return `${formatNumber.format(value / 1024)} KB`
  }
  return `${formatNumber.format(value / (1024 * 1024))} MB`
}

function formatDateTime(value: string): string {
  if (!value) {
    return 'sin fecha'
  }
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  return new Intl.DateTimeFormat('es-CR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(date)
}

function resolveAvailablePeriod(current: string, availablePeriods?: string[]): string {
  if (!availablePeriods?.length) {
    return ''
  }
  return current && availablePeriods.includes(current) ? current : availablePeriods[0]
}

function settingsStateKey(settings: AppSettings): string {
  return [
    settings.company,
    settings.satelliteZones.join('|'),
    settings.thresholds.redPercent,
    settings.thresholds.healthyPercent,
    settings.preparedArticles.length,
    settings.tarimaSizes.length,
  ].join('::')
}

function sectionTitle(view: ViewKey): string {
  return {
    traslados: 'Traslados',
    expediciones: 'Expediciones',
    transito: 'Transito',
    comparativa: 'Comparativa e histograma',
    simulacion: 'Simulacion Logistica',
    ajustes: 'Ajustes de Abasto',
  }[view]
}

function sectionSubtitle(view: ViewKey): string {
  return {
    traslados: 'Requisicion operativa con palets FIFO, transito y catalogos.',
    expediciones: 'Faltantes por expedicion con seleccion de palets.',
    transito: 'Pallets enviados y confirmacion de recibido.',
    comparativa: 'Inventario OLO contra SERVICA con cobertura por periodo.',
    simulacion: 'Carga estimada de traslado por horizonte.',
    ajustes: 'Umbrales, zonas satelite, alistado satelital y tamanos de tarima.',
  }[view]
}

function coverageText(value: number | undefined, analyzedPeriods: number): string {
  if (value === undefined) return '-'
  if (value >= analyzedPeriods) return `>=${analyzedPeriods}`
  return formatNumber.format(value)
}

function formatZones(row: AnalysisRow): string {
  return row.satelliteZones
    .filter((zone) => zone.quantity > 0)
    .sort((a, b) => b.quantity - a.quantity)
    .map((zone) => `${formatNumber.format(zone.quantity)} - ${zone.storageZone}`)
    .join('\n') || 'Sin zona'
}

function expeditionStatus(row: ExpeditionLineResult, prepared: boolean): string {
  if (prepared) return 'Alistado sat.'
  if (row.transferSuggestionQuantity > 0 && row.remainingShortageAfterTransfer > 0) return 'Parcial externa'
  if (row.transferSuggestionQuantity > 0) return 'Traslado sugerido'
  if (row.satelliteInventoryQuantity <= 0 && row.remainingShortageAfterTransfer > 0) return 'Sin satelite'
  return 'Cubierto'
}

function todayKey(): string {
  return new Date().toISOString().slice(0, 10).replace(/-/g, '')
}

export default App
