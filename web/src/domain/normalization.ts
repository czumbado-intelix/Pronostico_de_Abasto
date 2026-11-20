import type { PalletInventoryDetail } from './types'

export function normalizeArticleKey(article: string): string {
  return article.trim()
}

export function normalizeHeader(value: unknown): string {
  return String(value ?? '')
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[^a-z0-9]/g, '')
    .normalize('NFC')
}

export function normalizeSearchText(value: unknown): string {
  return String(value ?? '')
    .normalize('NFD')
    .replace(/[\p{M}\p{Cf}\p{Cc}]/gu, '')
    .replace(/\s+/g, ' ')
    .trim()
    .toUpperCase()
}

export function normalizeZoneKey(value: string): string {
  const trimmed = value.trim()
  if (!trimmed) {
    return 'SIN ZONA'
  }

  return trimmed.replace(/\s+/g, ' ').toUpperCase()
}

export function extractZoneCode(normalizedZone: string): string {
  const separator = normalizedZone.indexOf('-')
  return (separator > 0 ? normalizedZone.slice(0, separator) : normalizedZone).trim()
}

export function buildStorageZoneSet(zones?: string[]): Set<string> {
  const result = new Set<string>()
  for (const zone of zones ?? []) {
    if (!zone.trim()) {
      continue
    }

    const normalized = normalizeZoneKey(zone)
    result.add(normalized)

    const code = extractZoneCode(normalized)
    if (code) {
      result.add(code)
    }
  }

  return result
}

export function isIgnoredStorageZone(storageZone: string, ignoredZoneSet: Set<string>): boolean {
  if (ignoredZoneSet.size === 0) {
    return false
  }

  const normalized = normalizeZoneKey(storageZone)
  if (ignoredZoneSet.has(normalized)) {
    return true
  }

  const code = extractZoneCode(normalized)
  return Boolean(code && ignoredZoneSet.has(code))
}

export function normalizePalletIdentityPart(value: string): string {
  return value.trim().replace(/\s+/g, ' ').toUpperCase()
}

export function buildArticlePalletKey(article: string, pallet: string): string {
  return `${normalizePalletIdentityPart(article)}|${normalizePalletIdentityPart(pallet)}`
}

export function buildArticlePalletKeyFromDetail(article: string, pallet: PalletInventoryDetail): string {
  return buildArticlePalletKey(article, pallet.pallet)
}

export function resolveStorageOrigin(storageZone: string): string {
  const normalized = normalizeToken(storageZone)
  if (
    normalized.includes('SERVICA') ||
    normalized.includes('ZA46ZA45') ||
    normalized.startsWith('ZA46')
  ) {
    return 'SERVICA'
  }

  if (
    normalized.includes('GUACIMA') ||
    ['ZA47', 'ZA48', 'ZA49', 'ZA50', 'ZA52', 'ZA55'].some((prefix) =>
      normalized.startsWith(prefix),
    )
  ) {
    return 'COCO'
  }

  return 'OTRAS'
}

export function normalizeToken(value: unknown): string {
  return String(value ?? '')
    .trim()
    .toUpperCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[^A-Z0-9]/g, '')
    .normalize('NFC')
}

export function parseNumber(value: unknown): number | undefined {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value
  }

  const text = String(value ?? '').trim()
  if (!text) {
    return undefined
  }

  const leading = text.match(/^[+-]?[0-9.,]+/)
  const candidate = leading?.[0] ?? text
  const decimalNormalized =
    candidate.includes(',') && !candidate.includes('.')
      ? candidate.replace(',', '.')
      : candidate.replace(/,/g, '')
  const parsed = Number(decimalNormalized)
  return Number.isFinite(parsed) ? parsed : undefined
}

export function compareOptionalDateThenText(a?: string, b?: string): number {
  const aa = a || '9999-12-31'
  const bb = b || '9999-12-31'
  return aa.localeCompare(bb)
}
