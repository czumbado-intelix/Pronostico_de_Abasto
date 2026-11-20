import {
  AlertTriangle,
  ArrowDownToLine,
  Boxes,
  Check,
  ClipboardList,
  FileSpreadsheet,
  PackageCheck,
  Search,
  Settings,
  Truck,
  Upload,
} from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import {
  analyzeExpediciones,
  analyzeWeeklyForecast,
  outstandingPalletKeys,
  pendingTransitQuantities,
} from './domain/analysis'
import { defaultSettings } from './domain/defaults'
import { exportTransferRows } from './domain/export'
import {
  parseExpedicionesFile,
  parseForecastFile,
  parseInventoryFile,
} from './domain/excel'
import { normalizeSearchText } from './domain/normalization'
import type {
  AppSettings,
  ArticleAnalysisResult,
  Company,
  ExpeditionLineResult,
  LoadedFiles,
  PalletInventoryDetail,
  TransitItem,
} from './domain/types'
import {
  addTransit,
  loadSettings,
  loadTransit,
  markTransitReceived,
  saveSettings,
} from './services/api'
import './index.css'

type ViewKey = 'traslados' | 'expediciones' | 'transito' | 'ajustes'

const formatNumber = new Intl.NumberFormat('es-CR', { maximumFractionDigits: 2 })

function App() {
  const [company, setCompany] = useState<Company>('EPA')
  const [settings, setSettings] = useState<AppSettings>(defaultSettings('EPA'))
  const [files, setFiles] = useState<LoadedFiles>({})
  const [transit, setTransit] = useState<TransitItem[]>([])
  const [view, setView] = useState<ViewKey>('traslados')
  const [selectedPeriod, setSelectedPeriod] = useState('')
  const [horizonPeriods, setHorizonPeriods] = useState(4)
  const [searchText, setSearchText] = useState('')
  const [busy, setBusy] = useState('')
  const [notice, setNotice] = useState('')

  useEffect(() => {
    let cancelled = false
    async function load() {
      const [loadedSettings, loadedTransit] = await Promise.all([
        loadSettings(company),
        loadTransit(company),
      ])
      if (!cancelled) {
        setSettings(loadedSettings)
        setTransit(loadedTransit)
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [company])

  const pendingQuantities = useMemo(() => pendingTransitQuantities(transit), [transit])
  const unavailablePalletKeys = useMemo(() => outstandingPalletKeys(transit), [transit])
  const descriptions = useMemo(
    () => ({
      ...(files.inventory?.descriptions ?? {}),
      ...(files.forecast?.descriptions ?? {}),
    }),
    [files.forecast?.descriptions, files.inventory?.descriptions],
  )

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

  const transferRows = useMemo(
    () =>
      filterRows(
        (weeklyResult?.articles ?? []).filter(
          (row) =>
            row.transferSuggestionQuantity > 0 ||
            row.remainingShortageAfterTransfer > 0 ||
            row.pendingTransitQuantity > 0,
        ),
        searchText,
      ),
    [searchText, weeklyResult?.articles],
  )

  const expeditionRows = useMemo(
    () =>
      filterRows(
        (expedicionesResult?.lines ?? []).filter(
          (row) =>
            row.transferSuggestionQuantity > 0 ||
            row.remainingShortageAfterTransfer > 0 ||
            row.pendingTransitQuantity > 0,
        ),
        searchText,
      ),
    [expedicionesResult?.lines, searchText],
  )

  async function handleInventory(file: File) {
    await runBusy('Leyendo inventario...', async () => {
      const inventory = await parseInventoryFile(file)
      setFiles((current) => ({ ...current, inventory }))
      setNotice(`Inventario cargado: ${inventory.positions.length.toLocaleString('es-CR')} filas.`)
    })
  }

  async function handleForecast(file: File) {
    await runBusy('Leyendo forecast...', async () => {
      const forecast = await parseForecastFile(file)
      setFiles((current) => ({ ...current, forecast }))
      setSelectedPeriod(forecast.availablePeriods[0] ?? '')
      setNotice(
        `Forecast cargado: ${forecast.entries.length.toLocaleString('es-CR')} combinaciones articulo-periodo.`,
      )
    })
  }

  async function handleExpediciones(file: File) {
    await runBusy('Leyendo expediciones...', async () => {
      const expediciones = await parseExpedicionesFile(file)
      setFiles((current) => ({ ...current, expediciones }))
      setNotice(`Expediciones cargadas: ${expediciones.lines.length.toLocaleString('es-CR')} lineas faltantes.`)
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

  async function sendRowToTransit(row: ArticleAnalysisResult | ExpeditionLineResult) {
    if (row.suggestedPallets.length === 0) {
      setNotice('No hay pallets sugeridos para enviar a transito.')
      return
    }

    const items = row.suggestedPallets.map((pallet) =>
      buildTransitItem(company, selectedPeriod, row, pallet),
    )
    const saved = await addTransit(company, items)
    setTransit(saved)
    setNotice(`${items.length.toLocaleString('es-CR')} pallet(s) agregados a Transito.`)
  }

  async function receiveTransit(id: string) {
    const saved = await markTransitReceived(company, [id])
    setTransit(saved)
    setNotice('Pallet marcado como recibido.')
  }

  async function persistSettings(next: AppSettings) {
    setSettings(next)
    const saved = await saveSettings(next)
    setSettings(saved)
    setNotice('Ajustes guardados.')
  }

  return (
    <main className="app-shell">
      <aside className="sidebar" aria-label="Modulos">
        <div className="brand-mark">
          <Boxes size={22} />
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
          <NavButton
            active={view === 'transito'}
            icon={<PackageCheck size={19} />}
            onClick={() => setView('transito')}
          >
            Transito
          </NavButton>
          <NavButton active={view === 'ajustes'} icon={<Settings size={19} />} onClick={() => setView('ajustes')}>
            Ajustes
          </NavButton>
        </nav>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div>
            <h1>Pronostico de Abasto</h1>
            <p>
              {files.inventory ? `${files.inventory.positions.length.toLocaleString('es-CR')} inventario` : 'Sin inventario'} ·{' '}
              {files.forecast ? `${files.forecast.entries.length.toLocaleString('es-CR')} forecast` : 'Sin forecast'} ·{' '}
              {transit.filter((item) => !item.receivedAt && !item.reconciledAt).length.toLocaleString('es-CR')} en transito
            </p>
          </div>
          <div className="topbar-actions">
            <select value={company} onChange={(event) => setCompany(event.target.value as Company)}>
              <option value="EPA">EPA</option>
              <option value="Cofersa">Cofersa</option>
            </select>
            <div className="search-box">
              <Search size={18} />
              <input
                value={searchText}
                onChange={(event) => setSearchText(event.target.value)}
                placeholder="Buscar articulo, pallet, zona..."
              />
            </div>
          </div>
        </header>

        <section className="file-strip" aria-label="Carga de archivos">
          <FileLoader icon={<FileSpreadsheet size={18} />} title="Inventario" loaded={Boolean(files.inventory)} onFile={handleInventory} />
          <FileLoader icon={<ArrowDownToLine size={18} />} title="Forecast" loaded={Boolean(files.forecast)} onFile={handleForecast} />
          <FileLoader icon={<ClipboardList size={18} />} title="Expediciones" loaded={Boolean(files.expediciones)} onFile={handleExpediciones} />
          <div className="period-controls">
            <label>
              Periodo
              <select value={selectedPeriod} onChange={(event) => setSelectedPeriod(event.target.value)}>
                {(files.forecast?.availablePeriods ?? []).map((period) => (
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
            onSend={sendRowToTransit}
            onExport={() => exportTransferRows(transferRows)}
          />
        )}
        {view === 'expediciones' && (
          <ExpedicionesView
            result={expedicionesResult}
            rows={expeditionRows}
            onSend={sendRowToTransit}
            onExport={() => exportTransferRows(expeditionRows, 'requisicion-expediciones.xlsx')}
          />
        )}
        {view === 'transito' && <TransitView items={transit} onReceive={receiveTransit} />}
        {view === 'ajustes' && (
          <SettingsView key={settingsStateKey(settings)} settings={settings} onSave={persistSettings} />
        )}
      </section>
    </main>
  )
}

function NavButton(props: {
  active: boolean
  icon: React.ReactNode
  children: React.ReactNode
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
  icon: React.ReactNode
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

function TransferView(props: {
  result?: ReturnType<typeof analyzeWeeklyForecast>
  rows: ArticleAnalysisResult[]
  onSend: (row: ArticleAnalysisResult) => void
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
          ['Para traer', props.rows.filter((row) => row.transferSuggestionQuantity > 0).length],
          ['Sin externa', props.rows.filter((row) => row.transferRecommendationState === 'Unavailable').length],
        ]}
      />
      <TableActions count={props.rows.length} onExport={props.onExport} />
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Articulo</th>
              <th>Descripcion</th>
              <th>Forecast</th>
              <th>Principal</th>
              <th>Externas</th>
              <th>A traer</th>
              <th>Transito</th>
              <th>Pendiente</th>
              <th>Pallets</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {props.rows.map((row) => (
              <tr key={row.article}>
                <td className="mono">{row.article}</td>
                <td>{row.description || '-'}</td>
                <td>{formatNumber.format(row.forecastQuantity)}</td>
                <td>{formatNumber.format(row.principalInventoryQuantity)}</td>
                <td>{formatNumber.format(row.satelliteInventoryQuantity)}</td>
                <td className="strong">{formatNumber.format(row.transferSuggestionQuantity)}</td>
                <td>{formatNumber.format(row.pendingTransitQuantity)}</td>
                <td>{formatNumber.format(row.remainingShortageAfterTransfer)}</td>
                <td>{row.suggestedPallets.length}</td>
                <td>
                  <button className="icon-action" type="button" onClick={() => props.onSend(row)}>
                    <Truck size={17} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function ExpedicionesView(props: {
  result?: ReturnType<typeof analyzeExpediciones>
  rows: ExpeditionLineResult[]
  onSend: (row: ExpeditionLineResult) => void
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
          ['Sin externa', props.result.withoutExternal],
        ]}
      />
      <TableActions count={props.rows.length} onExport={props.onExport} />
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Expedicion</th>
              <th>Articulo</th>
              <th>Descripcion</th>
              <th>Faltante</th>
              <th>Principal</th>
              <th>Externas</th>
              <th>A traer</th>
              <th>Pendiente</th>
              <th>Pallets</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {props.rows.map((row) => (
              <tr key={`${row.article}-${row.expeditionNumber}`}>
                <td>{row.expeditionNumber || '-'}</td>
                <td className="mono">{row.article}</td>
                <td>{row.description || '-'}</td>
                <td>{formatNumber.format(row.demandQuantity)}</td>
                <td>{formatNumber.format(row.principalInventoryQuantity)}</td>
                <td>{formatNumber.format(row.satelliteInventoryQuantity)}</td>
                <td className="strong">{formatNumber.format(row.transferSuggestionQuantity)}</td>
                <td>{formatNumber.format(row.remainingShortageAfterTransfer)}</td>
                <td>{row.suggestedPallets.length}</td>
                <td>
                  <button className="icon-action" type="button" onClick={() => props.onSend(row)}>
                    <Truck size={17} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function TransitView(props: { items: TransitItem[]; onReceive: (id: string) => void }) {
  const pending = props.items.filter((item) => !item.receivedAt && !item.reconciledAt)
  const history = props.items.filter((item) => item.receivedAt || item.reconciledAt)
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
      <div className="table-wrap transit-table">
        <table>
          <thead>
            <tr>
              <th>Articulo</th>
              <th>Descripcion</th>
              <th>Pallet</th>
              <th>Cantidad</th>
              <th>Zona origen</th>
              <th>Ubicacion</th>
              <th>Estado</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {props.items.map((item) => (
              <tr key={item.id}>
                <td className="mono">{item.article}</td>
                <td>{item.description || '-'}</td>
                <td className="mono">{item.pallet}</td>
                <td>{formatNumber.format(item.quantity)}</td>
                <td>{item.storageZone}</td>
                <td>{item.location}</td>
                <td>{item.receivedAt ? 'Recibido' : 'Pendiente'}</td>
                <td>
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

function SettingsView(props: { settings: AppSettings; onSave: (settings: AppSettings) => void }) {
  const [satelliteZones, setSatelliteZones] = useState(props.settings.satelliteZones.join('\n'))
  const [redPercent, setRedPercent] = useState(props.settings.thresholds.redPercent)
  const [healthyPercent, setHealthyPercent] = useState(props.settings.thresholds.healthyPercent)

  return (
    <section className="module settings-grid">
      <label>
        Zonas satelite
        <textarea value={satelliteZones} onChange={(event) => setSatelliteZones(event.target.value)} />
      </label>
      <label>
        Rojo %
        <input type="number" value={redPercent} onChange={(event) => setRedPercent(Number(event.target.value))} />
      </label>
      <label>
        Verde %
        <input
          type="number"
          value={healthyPercent}
          onChange={(event) => setHealthyPercent(Number(event.target.value))}
        />
      </label>
      <button
        className="primary-button"
        type="button"
        onClick={() =>
          props.onSave({
            company: props.settings.company,
            satelliteZones: splitLines(satelliteZones),
            excludedZones: [],
            thresholds: { redPercent, healthyPercent },
          })
        }
      >
        Guardar ajustes
      </button>
    </section>
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

function TableActions(props: { count: number; onExport: () => void }) {
  return (
    <div className="table-actions">
      <span>{props.count.toLocaleString('es-CR')} filas visibles</span>
      <button className="secondary-button" type="button" onClick={props.onExport}>
        Exportar requisicion
      </button>
    </div>
  )
}

function EmptyState(props: { text: string }) {
  return (
    <div className="empty-state">
      <AlertTriangle size={22} />
      <span>{props.text}</span>
    </div>
  )
}

function filterRows<T extends ArticleAnalysisResult | ExpeditionLineResult>(rows: T[], text: string): T[] {
  const query = normalizeSearchText(text)
  if (!query) {
    return rows
  }
  const terms = query.split(' ').filter(Boolean)
  return rows.filter((row) => {
    const haystack = normalizeSearchText(
      [
        row.article,
        row.description,
        row.suggestedPallets.map((pallet) => `${pallet.pallet} ${pallet.storageZone} ${pallet.location}`).join(' '),
        row.satelliteZones.map((zone) => zone.storageZone).join(' '),
        'expeditionNumber' in row ? row.expeditionNumber : '',
      ].join(' '),
    )
    return terms.every((term) => haystack.includes(term))
  })
}

function buildTransitItem(
  company: Company,
  periodKey: string,
  row: ArticleAnalysisResult | ExpeditionLineResult,
  pallet: PalletInventoryDetail,
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
    tarimaSize: '',
    origin: resolveOrigin(pallet.storageZone),
    createdAt: new Date().toISOString(),
  }
}

function resolveOrigin(storageZone: string): string {
  const normalized = storageZone.toUpperCase()
  if (normalized.includes('GUACIMA')) return 'COCO'
  if (normalized.includes('SERVICA')) return 'SERVICA'
  if (normalized.includes('ALGEFISA')) return 'ALGEFISA'
  return 'EXTERNA'
}

function splitLines(value: string): string[] {
  return value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
}

function settingsStateKey(settings: AppSettings): string {
  return [
    settings.company,
    settings.satelliteZones.join('|'),
    settings.thresholds.redPercent,
    settings.thresholds.healthyPercent,
  ].join('::')
}

export default App
