import { defaultSettings } from '../domain/defaults'
import type {
  AppSettings,
  Company,
  FileSessionInfo,
  FileSessionKind,
  LoadedFiles,
  OrderStateEntry,
  TransitItem,
} from '../domain/types'

const settingsKey = (company: Company) => `pronostico-abasto:settings:${company}`
const transitKey = (company: Company) => `pronostico-abasto:transit:${company}`
const fileSessionsKey = (company: Company) => `pronostico-abasto:file-sessions:${company}`
const orderStateKey = (company: Company, periodKey: string) => `pronostico-abasto:orders:${company}:${periodKey}`
const commentsKey = (company: Company) => `pronostico-abasto:comparison-comments:${company}`
const fileSessionKinds: FileSessionKind[] = [
  'transferInventory',
  'transferForecast',
  'expeditionInventory',
  'expeditionReport',
  'simulationInventory',
  'simulationForecast',
  'inventory',
  'forecast',
  'expediciones',
  'comparisonInventory',
  'comparisonForecast',
]

export interface FileSessionState {
  files: LoadedFiles
  sessions: FileSessionInfo[]
  storage: {
    objectStore: 'd1' | 'r2'
    r2Enabled: boolean
  }
}

export interface CurrentUser {
  email: string
}

export async function loadCurrentUser(): Promise<CurrentUser> {
  try {
    const response = await fetch('/api/auth/me')
    if (!response.ok) {
      return { email: '' }
    }
    const body = (await response.json()) as Partial<CurrentUser>
    return { email: String(body.email ?? '').trim() }
  } catch {
    return { email: '' }
  }
}

export async function loadSettings(company: Company): Promise<AppSettings> {
  const fallback = normalizeSettings(company, readLocal(settingsKey(company), defaultSettings(company)))
  try {
    const response = await fetch(`/api/settings/${company}`)
    if (!response.ok) {
      return fallback
    }
    const settings = normalizeSettings(company, (await response.json()) as AppSettings)
    writeLocal(settingsKey(company), settings)
    return settings
  } catch {
    return fallback
  }
}

export async function saveSettings(settings: AppSettings): Promise<AppSettings> {
  writeLocal(settingsKey(settings.company), settings)
  try {
    const response = await fetch(`/api/settings/${settings.company}`, {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(settings),
    })
    return response.ok ? ((await response.json()) as AppSettings) : settings
  } catch {
    return settings
  }
}

export async function loadTransit(company: Company): Promise<TransitItem[]> {
  const fallback = readLocal<TransitItem[]>(transitKey(company), [])
  try {
    const response = await fetch(`/api/transit/${company}`)
    if (!response.ok) {
      return fallback
    }
    const items = (await response.json()) as TransitItem[]
    writeLocal(transitKey(company), items)
    return items
  } catch {
    return fallback
  }
}

export async function addTransit(company: Company, items: TransitItem[]): Promise<TransitItem[]> {
  const existing = readLocal<TransitItem[]>(transitKey(company), [])
  const pendingKeys = new Set(
    existing
      .filter((item) => !item.receivedAt && !item.reconciledAt)
      .map((item) => `${item.article.toUpperCase()}|${item.pallet.toUpperCase()}`),
  )
  const merged = [
    ...existing,
    ...items.filter((item) => {
      const key = `${item.article.toUpperCase()}|${item.pallet.toUpperCase()}`
      if (pendingKeys.has(key)) {
        return false
      }
      pendingKeys.add(key)
      return true
    }),
  ]
  writeLocal(transitKey(company), merged)

  try {
    const response = await fetch(`/api/transit/${company}`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ items }),
    })
    if (response.ok) {
      const saved = (await response.json()) as TransitItem[]
      writeLocal(transitKey(company), saved)
      return saved
    }
  } catch {
    // Local fallback already persisted.
  }

  return merged
}

export async function markTransitReceived(company: Company, ids: string[]): Promise<TransitItem[]> {
  const now = new Date().toISOString()
  const idSet = new Set(ids)
  const local = readLocal<TransitItem[]>(transitKey(company), []).map((item) =>
    idSet.has(item.id) && !item.receivedAt && !item.reconciledAt ? { ...item, receivedAt: now } : item,
  )
  writeLocal(transitKey(company), local)

  try {
    const response = await fetch(`/api/transit/${company}/receive`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ ids }),
    })
    if (response.ok) {
      const saved = (await response.json()) as TransitItem[]
      writeLocal(transitKey(company), saved)
      return saved
    }
  } catch {
    // Local fallback already persisted.
  }

  return local
}

export async function loadFileSessions(company: Company): Promise<FileSessionState> {
  const fallback = normalizeFileSessionState(readLocal(fileSessionsKey(company), {}))
  try {
    const response = await fetch(`/api/file-sessions/${company}`)
    if (!response.ok) {
      return fallback
    }
    const state = normalizeFileSessionState(await response.json())
    writeLocal(fileSessionsKey(company), state)
    return state
  } catch {
    return fallback
  }
}

export async function saveFileSession<K extends FileSessionKind>(
  company: Company,
  kind: K,
  payload: NonNullable<LoadedFiles[K]>,
  sourceName?: string,
): Promise<FileSessionState> {
  const local = {
    ...normalizeFileSessionState(readLocal(fileSessionsKey(company), {})),
  }
  local.files = {
    ...local.files,
    [kind]: payload,
  }
  writeLocal(fileSessionsKey(company), local)

  try {
    const response = await fetch(`/api/file-sessions/${company}`, {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ kind, payload, sourceName }),
    })
    if (response.ok) {
      const saved = normalizeFileSessionState(await response.json())
      writeLocal(fileSessionsKey(company), saved)
      return saved
    }
    throw new Error(await responseError(response, 'No se pudo guardar el archivo en la nube.'))
  } catch (error) {
    throw error instanceof Error ? error : new Error('No se pudo guardar el archivo en la nube.')
  }
}

export async function restoreFileSession(company: Company, versionId: string): Promise<FileSessionState> {
  const response = await fetch(`/api/file-sessions/${company}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ action: 'restore', versionId }),
  })
  if (!response.ok) {
    throw new Error(await responseError(response, 'No se pudo restaurar la carga.'))
  }
  const restored = normalizeFileSessionState(await response.json())
  writeLocal(fileSessionsKey(company), restored)
  return restored
}

export async function deleteFileSession(company: Company, kind: FileSessionKind): Promise<FileSessionState> {
  const local = normalizeFileSessionState(readLocal(fileSessionsKey(company), {}))
  delete local.files[kind]
  writeLocal(fileSessionsKey(company), local)

  try {
    const response = await fetch(`/api/file-sessions/${company}?kind=${encodeURIComponent(kind)}`, {
      method: 'DELETE',
    })
    if (response.ok) {
      const saved = normalizeFileSessionState(await response.json())
      writeLocal(fileSessionsKey(company), saved)
      return saved
    }
  } catch {
    // Local fallback already persisted.
  }

  return local
}

export function backupUrl(company: Company): string {
  return `/api/backup/${company}`
}

export async function loadOrderState(company: Company, periodKey: string): Promise<Record<string, OrderStateEntry>> {
  const fallback = readLocal<Record<string, OrderStateEntry>>(orderStateKey(company, periodKey), {})
  if (!periodKey) {
    return {}
  }

  try {
    const response = await fetch(`/api/order-state/${company}?period=${encodeURIComponent(periodKey)}`)
    if (!response.ok) {
      return fallback
    }
    const state = normalizeOrderState((await response.json()) as Record<string, OrderStateEntry>)
    writeLocal(orderStateKey(company, periodKey), state)
    return state
  } catch {
    return fallback
  }
}

export async function saveOrderState(
  company: Company,
  periodKey: string,
  entries: Record<string, OrderStateEntry>,
): Promise<Record<string, OrderStateEntry>> {
  const state = normalizeOrderState(entries)
  writeLocal(orderStateKey(company, periodKey), state)
  if (!periodKey) {
    return {}
  }

  try {
    const response = await fetch(`/api/order-state/${company}`, {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ periodKey, entries: state }),
    })
    if (response.ok) {
      const saved = normalizeOrderState((await response.json()) as Record<string, OrderStateEntry>)
      writeLocal(orderStateKey(company, periodKey), saved)
      return saved
    }
  } catch {
    // Local fallback already persisted.
  }

  return state
}

export async function loadComparisonComments(company: Company): Promise<Record<string, string>> {
  const fallback = readLocal<Record<string, string>>(commentsKey(company), {})
  try {
    const response = await fetch(`/api/comparison-comments/${company}`)
    if (!response.ok) {
      return fallback
    }
    const comments = normalizeComments((await response.json()) as Record<string, string>)
    writeLocal(commentsKey(company), comments)
    return comments
  } catch {
    return fallback
  }
}

export async function saveComparisonComments(
  company: Company,
  comments: Record<string, string>,
): Promise<Record<string, string>> {
  const normalized = normalizeComments(comments)
  writeLocal(commentsKey(company), normalized)
  try {
    const response = await fetch(`/api/comparison-comments/${company}`, {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ comments: normalized }),
    })
    if (response.ok) {
      const saved = normalizeComments((await response.json()) as Record<string, string>)
      writeLocal(commentsKey(company), saved)
      return saved
    }
  } catch {
    // Local fallback already persisted.
  }
  return normalized
}

function readLocal<T>(key: string, fallback: T): T {
  try {
    const raw = window.localStorage.getItem(key)
    return raw ? (JSON.parse(raw) as T) : fallback
  } catch {
    return fallback
  }
}

function writeLocal<T>(key: string, value: T): void {
  try {
    window.localStorage.setItem(key, JSON.stringify(value))
  } catch {
    // Best-effort local cache.
  }
}

async function responseError(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { error?: string }
    return body.error?.trim() || fallback
  } catch {
    return fallback
  }
}

function normalizeOrderState(entries: Record<string, OrderStateEntry>): Record<string, OrderStateEntry> {
  return Object.fromEntries(
    Object.entries(entries ?? {})
      .map(([key, entry]) => [
        key.trim(),
        {
          ordered: Boolean(entry?.ordered),
          selectedPalletKeys: cleanStringList(entry?.selectedPalletKeys),
        },
      ] as const)
      .filter(([key, entry]) => key && (entry.ordered || entry.selectedPalletKeys.length > 0)),
  )
}

function normalizeComments(comments: Record<string, string>): Record<string, string> {
  return Object.fromEntries(
    Object.entries(comments ?? {})
      .map(([article, comment]) => [article.trim(), String(comment ?? '').trim()] as const)
      .filter(([article, comment]) => article && comment),
  )
}

function normalizeFileSessionState(value: unknown): FileSessionState {
  const source = value && typeof value === 'object' ? (value as { files?: unknown; sessions?: unknown; storage?: unknown }) : {}
  return {
    files: normalizeLoadedFiles(source.files ?? value),
    sessions: normalizeFileSessionInfoList(source.sessions),
    storage: normalizeStorageInfo(source.storage),
  }
}

function normalizeLoadedFiles(value: unknown): LoadedFiles {
  const source = value && typeof value === 'object' ? (value as Partial<Record<FileSessionKind, unknown>>) : {}
  const files: Partial<Record<FileSessionKind, unknown>> = {}
  for (const kind of fileSessionKinds) {
    const payload = source[kind]
    if (payload && typeof payload === 'object') {
      files[kind] = payload
    }
  }
  return files as LoadedFiles
}

function normalizeFileSessionInfoList(value: unknown): FileSessionInfo[] {
  return Array.isArray(value)
    ? value
      .map((item): FileSessionInfo | undefined => {
        const source = item && typeof item === 'object' ? (item as Partial<FileSessionInfo>) : {}
        const kind = fileSessionKinds.includes(source.kind as FileSessionKind)
          ? (source.kind as FileSessionKind)
          : undefined
        const versionId = String(source.versionId ?? '').trim()
        if (!kind || !versionId) {
          return undefined
        }
        return {
          versionId,
          kind,
          sourceName: String(source.sourceName ?? '').trim(),
          payloadBytes: Number(source.payloadBytes) || 0,
          storageBackend: source.storageBackend === 'r2' ? 'r2' : 'd1',
          updatedAt: String(source.updatedAt ?? ''),
          active: Boolean(source.active),
        }
      })
      .filter((item): item is FileSessionInfo => Boolean(item))
    : []
}

function normalizeStorageInfo(value: unknown): FileSessionState['storage'] {
  const source = value && typeof value === 'object' ? (value as Partial<FileSessionState['storage']>) : {}
  return {
    objectStore: source.objectStore === 'r2' ? 'r2' : 'd1',
    r2Enabled: Boolean(source.r2Enabled),
  }
}

function cleanStringList(value: unknown): string[] {
  return Array.isArray(value)
    ? [...new Set(value.map((item) => String(item).trim()).filter(Boolean))]
    : []
}

function normalizeSettings(company: Company, settings: Partial<AppSettings>): AppSettings {
  const defaults = defaultSettings(company)
  return {
    company,
    satelliteZones: Array.isArray(settings.satelliteZones) ? settings.satelliteZones : defaults.satelliteZones,
    excludedZones: Array.isArray(settings.excludedZones) ? settings.excludedZones : defaults.excludedZones,
    thresholds: settings.thresholds ?? defaults.thresholds,
    preparedArticles: Array.isArray(settings.preparedArticles) ? settings.preparedArticles : [],
    tarimaSizes: Array.isArray(settings.tarimaSizes) ? settings.tarimaSizes : [],
  }
}
