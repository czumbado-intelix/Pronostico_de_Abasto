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

export const onRequestGet: PagesFunction<Env> = async ({ env, params }) => {
  const company = String(params.company ?? 'EPA')
  const row = await env.DB.prepare(
    'SELECT company, satellite_zones, excluded_zones, thresholds FROM settings WHERE company = ?',
  )
    .bind(company)
    .first<{
      company: string
      satellite_zones: string
      excluded_zones: string
      thresholds: string
    }>()

  if (!row) {
    return Response.json(defaultSettings(company))
  }

  return Response.json({
    company: row.company,
    satelliteZones: safeJson<string[]>(row.satellite_zones, defaultSatelliteZones),
    excludedZones: safeJson<string[]>(row.excluded_zones, defaultExcludedZones),
    thresholds: safeJson(row.thresholds, { redPercent: 0, healthyPercent: 100 }),
  })
}

export const onRequestPut: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = String(params.company ?? 'EPA')
  const body = (await request.json()) as {
    satelliteZones?: string[]
    excludedZones?: string[]
    thresholds?: { redPercent: number; healthyPercent: number }
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
  }

  await env.DB.prepare(
    `INSERT INTO settings(company, satellite_zones, excluded_zones, thresholds, updated_at)
     VALUES (?, ?, ?, ?, CURRENT_TIMESTAMP)
     ON CONFLICT(company) DO UPDATE SET
       satellite_zones = excluded.satellite_zones,
       excluded_zones = excluded.excluded_zones,
       thresholds = excluded.thresholds,
       updated_at = CURRENT_TIMESTAMP`,
  )
    .bind(
      company,
      JSON.stringify(settings.satelliteZones),
      JSON.stringify(settings.excludedZones),
      JSON.stringify(settings.thresholds),
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

function safeJson<T>(value: string, fallback: T): T {
  try {
    return JSON.parse(value) as T
  } catch {
    return fallback
  }
}
