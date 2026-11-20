interface Env {
  DB: D1Database
}

interface TransitInput {
  id?: string
  company?: string
  periodKey?: string
  article?: string
  description?: string
  pallet?: string
  quantity?: number
  storageZone?: string
  location?: string
  validationDate?: string
  tarimaSize?: string
  origin?: string
  createdAt?: string
  receivedAt?: string
  reconciledAt?: string
}

export const onRequestGet: PagesFunction<Env> = async ({ env, params }) => {
  return Response.json(await readTransit(env.DB, String(params.company ?? 'EPA')))
}

export const onRequestPost: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = String(params.company ?? 'EPA')
  const body = (await request.json()) as { items?: TransitInput[] }
  const items = Array.isArray(body.items) ? body.items : []

  const statement = env.DB.prepare(
    `INSERT OR IGNORE INTO transit_items(
      id, company, period_key, article, description, pallet, quantity, storage_zone,
      location, validation_date, tarima_size, origin, created_at, received_at, reconciled_at
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
  )

  await env.DB.batch(
    items
      .map((item) => normalizeTransit(company, item))
      .filter((item) => item.article && item.pallet && item.quantity > 0)
      .map((item) =>
        statement.bind(
          item.id,
          item.company,
          item.periodKey,
          item.article,
          item.description,
          item.pallet,
          item.quantity,
          item.storageZone,
          item.location,
          item.validationDate || null,
          item.tarimaSize,
          item.origin,
          item.createdAt,
          item.receivedAt || null,
          item.reconciledAt || null,
        ),
      ),
  )

  return Response.json(await readTransit(env.DB, company))
}

export async function readTransit(db: D1Database, company: string) {
  const { results } = await db
    .prepare(
      `SELECT id, company, period_key, article, description, pallet, quantity, storage_zone,
        location, validation_date, tarima_size, origin, created_at, received_at, reconciled_at
       FROM transit_items
       WHERE company = ?
       ORDER BY
         CASE WHEN received_at IS NULL AND reconciled_at IS NULL THEN 0
              WHEN reconciled_at IS NULL THEN 1
              ELSE 2 END,
         origin COLLATE NOCASE,
         location COLLATE NOCASE,
         article COLLATE NOCASE,
         validation_date,
         pallet COLLATE NOCASE`,
    )
    .bind(company)
    .all<Record<string, string | number | null>>()

  return results.map((row) => ({
    id: String(row.id),
    company: String(row.company),
    periodKey: String(row.period_key ?? ''),
    article: String(row.article),
    description: String(row.description ?? ''),
    pallet: String(row.pallet),
    quantity: Number(row.quantity ?? 0),
    storageZone: String(row.storage_zone ?? ''),
    location: String(row.location ?? ''),
    validationDate: row.validation_date ? String(row.validation_date) : undefined,
    tarimaSize: String(row.tarima_size ?? ''),
    origin: String(row.origin ?? ''),
    createdAt: String(row.created_at),
    receivedAt: row.received_at ? String(row.received_at) : undefined,
    reconciledAt: row.reconciled_at ? String(row.reconciled_at) : undefined,
  }))
}

function normalizeTransit(company: string, item: TransitInput) {
  const storageZone = clean(item.storageZone)
  return {
    id: clean(item.id) || crypto.randomUUID(),
    company,
    periodKey: clean(item.periodKey),
    article: clean(item.article),
    description: clean(item.description),
    pallet: clean(item.pallet),
    quantity: Number(item.quantity ?? 0),
    storageZone,
    location: clean(item.location),
    validationDate: clean(item.validationDate),
    tarimaSize: clean(item.tarimaSize),
    origin: clean(item.origin) || resolveOrigin(storageZone),
    createdAt: clean(item.createdAt) || new Date().toISOString(),
    receivedAt: clean(item.receivedAt),
    reconciledAt: clean(item.reconciledAt),
  }
}

function resolveOrigin(storageZone: string): string {
  const normalized = storageZone
    .trim()
    .toUpperCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[^A-Z0-9]/g, '')
  if (normalized.includes('SERVICA') || normalized.includes('ZA46ZA45') || normalized.startsWith('ZA46')) {
    return 'SERVICA'
  }
  if (
    normalized.includes('GUACIMA') ||
    ['ZA47', 'ZA48', 'ZA49', 'ZA50', 'ZA52', 'ZA55'].some((prefix) => normalized.startsWith(prefix))
  ) {
    return 'COCO'
  }
  return 'OTRAS'
}

function clean(value: unknown): string {
  return String(value ?? '').trim()
}
