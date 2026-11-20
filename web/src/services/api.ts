import { defaultSettings } from '../domain/defaults'
import type { AppSettings, Company, TransitItem } from '../domain/types'

const settingsKey = (company: Company) => `pronostico-abasto:settings:${company}`
const transitKey = (company: Company) => `pronostico-abasto:transit:${company}`

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
