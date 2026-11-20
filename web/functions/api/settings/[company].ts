interface Env {
  DB: D1Database
}

const defaultSatelliteZones = [
  'ZA31-ZONA ALMACENAJE ALGEFISA - CLIRO',
  'ZA46-ZA45 - ZONA ALMACENAJE - SERVICA',
  'ZA47-ZA47 - ZONA ALMACENAJE 1 - GUACIMA',
  'ZA48-ZA48 - ZONA ALMACENAJE 2 - GUACIMA',
  'ZA49-ZA49 - ZONA ALMACENAJE 3 - GUACIMA',
  'ZA50-ZA50 - ZONA ALMACENAJE 4 - GUACIMA',
  'ZA52-ZONA ALMACENAJE MUELLE RECEPCION - GUACIMA',
  'ZA55-ZA55 - ZONA ALMACENAJE 5 - GUACIMA',
]

const defaultExcludedZones: string[] = []
const defaultPreparedArticles: CatalogArticle[] = []
const defaultTarimaSizes: TarimaSizeEntry[] = []

interface CatalogArticle {
  article: string
  description: string
}

interface TarimaSizeEntry {
  article: string
  description: string
  sizeCode: string
}

export const onRequestGet: PagesFunction<Env> = async ({ env, params }) => {
  const company = String(params.company ?? 'EPA')
  const row = await env.DB.prepare(
    'SELECT company, satellite_zones, excluded_zones, thresholds, prepared_articles, tarima_sizes FROM settings WHERE company = ?',
  )
    .bind(company)
    .first<{
      company: string
      satellite_zones: string
      excluded_zones: string
      thresholds: string
      prepared_articles: string | null
      tarima_sizes: string | null
    }>()

  if (!row) {
    return Response.json(defaultSettings(company))
  }

  return Response.json({
    company: row.company,
    satelliteZones: safeJson<string[]>(row.satellite_zones, defaultSatelliteZones),
    excludedZones: safeJson<string[]>(row.excluded_zones, defaultExcludedZones),
    thresholds: safeJson(row.thresholds, { redPercent: 0, healthyPercent: 100 }),
    preparedArticles: safeJson<CatalogArticle[]>(row.prepared_articles ?? '[]', defaultPreparedArticles),
    tarimaSizes: safeJson<TarimaSizeEntry[]>(row.tarima_sizes ?? '[]', defaultTarimaSizes),
  })
}

export const onRequestPut: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = String(params.company ?? 'EPA')
  const body = (await request.json()) as {
    satelliteZones?: string[]
    excludedZones?: string[]
    thresholds?: { redPercent: number; healthyPercent: number }
    preparedArticles?: CatalogArticle[]
    tarimaSizes?: TarimaSizeEntry[]
  }

  const settings = {
    company,
    satelliteZones: cleanList(body.satelliteZones, defaultSatelliteZones),
    excludedZones: cleanList(body.excludedZones, defaultExcludedZones),
    thresholds: {
      redPercent: clampNumber(body.thresholds?.redPercent, 0, 100),
      healthyPercent: Math.max(
        clampNumber(body.thresholds?.healthyPercent, 0, 999),
        clampNumber(body.thresholds?.redPercent, 0, 100),
      ),
    },
    preparedArticles: cleanCatalogArticles(body.preparedArticles),
    tarimaSizes: cleanTarimaSizes(body.tarimaSizes),
  }

  await env.DB.prepare(
    `INSERT INTO settings(company, satellite_zones, excluded_zones, thresholds, prepared_articles, tarima_sizes, updated_at)
     VALUES (?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
     ON CONFLICT(company) DO UPDATE SET
       satellite_zones = excluded.satellite_zones,
       excluded_zones = excluded.excluded_zones,
       thresholds = excluded.thresholds,
       prepared_articles = excluded.prepared_articles,
       tarima_sizes = excluded.tarima_sizes,
       updated_at = CURRENT_TIMESTAMP`,
  )
    .bind(
      company,
      JSON.stringify(settings.satelliteZones),
      JSON.stringify(settings.excludedZones),
      JSON.stringify(settings.thresholds),
      JSON.stringify(settings.preparedArticles),
      JSON.stringify(settings.tarimaSizes),
    )
    .run()

  return Response.json(settings)
}

function defaultSettings(company: string) {
  return {
    company,
    satelliteZones: defaultSatelliteZones,
    excludedZones: defaultExcludedZones,
    thresholds: { redPercent: 0, healthyPercent: 100 },
    preparedArticles: defaultPreparedArticles,
    tarimaSizes: defaultTarimaSizes,
  }
}

function cleanList(value: unknown, fallback: string[]): string[] {
  return Array.isArray(value)
    ? value.map((item) => String(item).trim()).filter(Boolean)
    : fallback
}

function clampNumber(value: unknown, min: number, max: number): number {
  const parsed = Number(value)
  if (!Number.isFinite(parsed)) {
    return min
  }
  return Math.min(max, Math.max(min, parsed))
}

function cleanCatalogArticles(value: unknown): CatalogArticle[] {
  if (!Array.isArray(value)) {
    return []
  }
  const byArticle = new Map<string, CatalogArticle>()
  for (const item of value) {
    const article = String((item as CatalogArticle | undefined)?.article ?? '').trim()
    if (!article) {
      continue
    }
    byArticle.set(article.toUpperCase(), {
      article,
      description: String((item as CatalogArticle | undefined)?.description ?? '').trim(),
    })
  }
  return [...byArticle.values()].sort((a, b) => a.article.localeCompare(b.article))
}

function cleanTarimaSizes(value: unknown): TarimaSizeEntry[] {
  if (!Array.isArray(value)) {
    return []
  }
  const byArticle = new Map<string, TarimaSizeEntry>()
  for (const item of value) {
    const article = String((item as TarimaSizeEntry | undefined)?.article ?? '').trim()
    const sizeCode = normalizeTarimaSize(String((item as TarimaSizeEntry | undefined)?.sizeCode ?? ''))
    if (!article || !sizeCode) {
      continue
    }
    byArticle.set(article.toUpperCase(), {
      article,
      description: String((item as TarimaSizeEntry | undefined)?.description ?? '').trim(),
      sizeCode,
    })
  }
  return [...byArticle.values()].sort((a, b) => a.article.localeCompare(b.article))
}

function normalizeTarimaSize(value: string): string {
  const token = value
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[^a-z0-9]/g, '')
  if (['s', '1', 'sencilla', 'simple'].includes(token)) return 'S'
  if (['d', '2', 'doble'].includes(token)) return 'D'
  if (['t', '3', 'triple'].includes(token)) return 'T'
  return ''
}

function safeJson<T>(value: string, fallback: T): T {
  try {
    return JSON.parse(value) as T
  } catch {
    return fallback
  }
}
