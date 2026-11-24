interface Env {
  DB: D1Database
  FILES?: R2Bucket
}

const fileKinds = ['inventory', 'forecast', 'expediciones', 'comparisonInventory', 'comparisonForecast'] as const
type FileKind = (typeof fileKinds)[number]
type StorageBackend = 'd1' | 'r2'

const fileKindSet = new Set<string>(fileKinds)
const chunkSize = 450_000
const maxPayloadBytes = 45_000_000
const maxHistoryPerKind = 12

interface ActiveSessionRow {
  file_kind: string
  source_name: string
  payload_bytes: number
  chunk_count: number
  storage_backend: string | null
  storage_key: string | null
  version_id: string | null
  updated_at: string
}

interface HistoryRow {
  version_id: string
  file_kind: string
  source_name: string
  payload_bytes: number
  storage_backend: string | null
  storage_key: string | null
  chunk_count: number
  created_at: string
}

interface PayloadChunk {
  payload_chunk: string
}

export const onRequestGet: PagesFunction<Env> = async ({ env, params }) => {
  const company = cleanCompany(params.company)
  return Response.json(await readFileSessionState(env, company))
}

export const onRequestPut: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = cleanCompany(params.company)
  const body = (await request.json()) as { kind?: string; payload?: unknown; sourceName?: string }
  const kind = cleanKind(body.kind)

  if (!kind) {
    return Response.json({ error: 'Tipo de archivo no valido.' }, { status: 400 })
  }

  const payloadText = JSON.stringify(body.payload ?? {})
  const payloadBytes = new TextEncoder().encode(payloadText).length
  if (payloadBytes > maxPayloadBytes) {
    return Response.json(
      { error: 'El archivo procesado es demasiado grande para guardarlo en la nube.' },
      { status: 413 },
    )
  }

  await saveFileSession(env, {
    company,
    kind,
    payloadText,
    payloadBytes,
    sourceName: cleanSourceName(body.sourceName),
    versionId: crypto.randomUUID(),
  })
  await pruneHistory(env, company, kind)

  return Response.json(await readFileSessionState(env, company))
}

export const onRequestPost: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = cleanCompany(params.company)
  const body = (await request.json()) as { action?: string; versionId?: string }
  if (body.action !== 'restore') {
    return Response.json({ error: 'Accion no valida.' }, { status: 400 })
  }

  const versionId = String(body.versionId ?? '').trim()
  if (!versionId) {
    return Response.json({ error: 'Version no valida.' }, { status: 400 })
  }

  const history = await env.DB.prepare(
    `SELECT version_id, file_kind, source_name, payload_bytes, storage_backend, storage_key, chunk_count, created_at
     FROM file_session_history
     WHERE company = ? AND version_id = ?`,
  )
    .bind(company, versionId)
    .first<HistoryRow>()

  if (!history) {
    return Response.json({ error: 'No se encontro la carga para restaurar.' }, { status: 404 })
  }

  const kind = cleanKind(history.file_kind)
  if (!kind) {
    return Response.json({ error: 'La carga tiene un tipo invalido.' }, { status: 400 })
  }

  const payloadText = await readPayloadText(env, {
    company,
    kind,
    versionId,
    storageBackend: normalizeBackend(history.storage_backend),
    storageKey: history.storage_key ?? '',
    chunkCount: history.chunk_count,
    source: 'history',
  })
  if (!payloadText) {
    return Response.json({ error: 'No se pudo leer el contenido de la carga para restaurar.' }, { status: 404 })
  }

  await saveFileSession(env, {
    company,
    kind,
    payloadText,
    payloadBytes: new TextEncoder().encode(payloadText).length,
    sourceName: `${history.source_name || 'Carga'} (restaurada)`,
    versionId: crypto.randomUUID(),
  })
  await env.DB.prepare('UPDATE file_session_history SET restored_at = CURRENT_TIMESTAMP WHERE version_id = ?')
    .bind(versionId)
    .run()
  await pruneHistory(env, company, kind)

  return Response.json(await readFileSessionState(env, company))
}

export const onRequestDelete: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = cleanCompany(params.company)
  const searchParams = new URL(request.url).searchParams
  const kind = cleanKind(searchParams.get('kind'))

  if (searchParams.has('kind') && !kind) {
    return Response.json({ error: 'Tipo de archivo no valido.' }, { status: 400 })
  }

  if (kind) {
    await env.DB.prepare('DELETE FROM file_session_chunks WHERE company = ? AND file_kind = ?')
      .bind(company, kind)
      .run()
    await env.DB.prepare('DELETE FROM file_sessions WHERE company = ? AND file_kind = ?')
      .bind(company, kind)
      .run()
  } else {
    await env.DB.prepare('DELETE FROM file_session_chunks WHERE company = ?')
      .bind(company)
      .run()
    await env.DB.prepare('DELETE FROM file_sessions WHERE company = ?')
      .bind(company)
      .run()
  }

  return Response.json(await readFileSessionState(env, company))
}

async function saveFileSession(
  env: Env,
  args: {
    company: string
    kind: FileKind
    payloadText: string
    payloadBytes: number
    sourceName: string
    versionId: string
  },
): Promise<void> {
  const backend: StorageBackend = env.FILES ? 'r2' : 'd1'
  const storageKey = backend === 'r2'
    ? `file-sessions/${encodeKey(args.company)}/${args.kind}/${args.versionId}.json`
    : ''
  const chunks = backend === 'd1' ? chunkText(args.payloadText) : []

  if (backend === 'r2') {
    await env.FILES!.put(storageKey, args.payloadText, {
      httpMetadata: { contentType: 'application/json; charset=utf-8' },
      customMetadata: {
        company: args.company,
        kind: args.kind,
        sourceName: args.sourceName,
      },
    })
  }

  await env.DB.prepare('DELETE FROM file_session_chunks WHERE company = ? AND file_kind = ?')
    .bind(args.company, args.kind)
    .run()

  if (chunks.length > 0) {
    const activeStatement = env.DB.prepare(
      `INSERT INTO file_session_chunks(company, file_kind, chunk_index, payload_chunk)
       VALUES (?, ?, ?, ?)`,
    )
    const historyStatement = env.DB.prepare(
      `INSERT INTO file_session_history_chunks(version_id, chunk_index, payload_chunk)
       VALUES (?, ?, ?)`,
    )
    await env.DB.batch([
      ...chunks.map((chunk, index) => activeStatement.bind(args.company, args.kind, index, chunk)),
      ...chunks.map((chunk, index) => historyStatement.bind(args.versionId, index, chunk)),
    ])
  }

  await env.DB.prepare(
    `INSERT INTO file_session_history(
       version_id, company, file_kind, source_name, payload_bytes, storage_backend, storage_key, chunk_count, created_at
     )
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP)`,
  )
    .bind(
      args.versionId,
      args.company,
      args.kind,
      args.sourceName,
      args.payloadBytes,
      backend,
      storageKey,
      chunks.length,
    )
    .run()

  await env.DB.prepare(
    `INSERT INTO file_sessions(
       company, file_kind, source_name, payload_bytes, chunk_count, storage_backend, storage_key, version_id, updated_at
     )
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
     ON CONFLICT(company, file_kind) DO UPDATE SET
       source_name = excluded.source_name,
       payload_bytes = excluded.payload_bytes,
       chunk_count = excluded.chunk_count,
       storage_backend = excluded.storage_backend,
       storage_key = excluded.storage_key,
       version_id = excluded.version_id,
       updated_at = CURRENT_TIMESTAMP`,
  )
    .bind(
      args.company,
      args.kind,
      args.sourceName,
      args.payloadBytes,
      chunks.length,
      backend,
      storageKey,
      args.versionId,
    )
    .run()
}

async function readFileSessionState(env: Env, company: string) {
  const { results: activeRows } = await env.DB.prepare(
    `SELECT file_kind, source_name, payload_bytes, chunk_count, storage_backend, storage_key, version_id, updated_at
     FROM file_sessions
     WHERE company = ?
     ORDER BY file_kind COLLATE NOCASE`,
  )
    .bind(company)
    .all<ActiveSessionRow>()

  const files: Record<string, unknown> = {}
  const activeVersionIds = new Set<string>()
  for (const row of activeRows) {
    const kind = cleanKind(row.file_kind)
    if (!kind) {
      continue
    }
    const versionId = row.version_id?.trim() || ''
    if (versionId) {
      activeVersionIds.add(versionId)
    }

    const payloadText = await readPayloadText(env, {
      company,
      kind,
      versionId,
      storageBackend: normalizeBackend(row.storage_backend),
      storageKey: row.storage_key ?? '',
      chunkCount: row.chunk_count,
      source: 'active',
    })
    const payload = safeJson(payloadText)
    if (payload !== undefined) {
      files[kind] = payload
    }
  }

  const { results: historyRows } = await env.DB.prepare(
    `SELECT version_id, file_kind, source_name, payload_bytes, storage_backend, storage_key, chunk_count, created_at
     FROM file_session_history
     WHERE company = ?
     ORDER BY created_at DESC`,
  )
    .bind(company)
    .all<HistoryRow>()

  return {
    files,
    sessions: [
      ...activeRows.map((row) => ({
        versionId: row.version_id?.trim() || `active-${row.file_kind}`,
        kind: row.file_kind,
        sourceName: row.source_name,
        payloadBytes: row.payload_bytes,
        storageBackend: normalizeBackend(row.storage_backend),
        updatedAt: row.updated_at,
        active: true,
      })),
      ...historyRows
        .filter((row) => row.version_id && !activeVersionIds.has(row.version_id))
        .map((row) => ({
          versionId: row.version_id,
          kind: row.file_kind,
          sourceName: row.source_name,
          payloadBytes: row.payload_bytes,
          storageBackend: normalizeBackend(row.storage_backend),
          updatedAt: row.created_at,
          active: false,
        })),
    ],
    storage: {
      objectStore: env.FILES ? 'r2' : 'd1',
      r2Enabled: Boolean(env.FILES),
    },
  }
}

async function readPayloadText(
  env: Env,
  args: {
    company: string
    kind: FileKind
    versionId: string
    storageBackend: StorageBackend
    storageKey: string
    chunkCount: number
    source: 'active' | 'history'
  },
): Promise<string> {
  if (args.storageBackend === 'r2') {
    if (!env.FILES || !args.storageKey) {
      return ''
    }
    const object = await env.FILES.get(args.storageKey)
    return object ? await object.text() : ''
  }

  const query = args.source === 'history'
    ? `SELECT payload_chunk FROM file_session_history_chunks WHERE version_id = ? ORDER BY chunk_index ASC`
    : `SELECT payload_chunk FROM file_session_chunks WHERE company = ? AND file_kind = ? ORDER BY chunk_index ASC`
  const statement = env.DB.prepare(query)
  const bound = args.source === 'history'
    ? statement.bind(args.versionId)
    : statement.bind(args.company, args.kind)
  const { results } = await bound.all<PayloadChunk>()

  if (results.length !== args.chunkCount) {
    return ''
  }

  return results.map((chunk) => chunk.payload_chunk).join('')
}

async function pruneHistory(env: Env, company: string, kind: FileKind): Promise<void> {
  const { results } = await env.DB.prepare(
    `SELECT version_id, storage_backend, storage_key
     FROM file_session_history
     WHERE company = ? AND file_kind = ?
     ORDER BY created_at DESC`,
  )
    .bind(company, kind)
    .all<{ version_id: string; storage_backend: string | null; storage_key: string | null }>()

  const stale = results.slice(maxHistoryPerKind)
  if (stale.length === 0) {
    return
  }

  for (const row of stale) {
    if (normalizeBackend(row.storage_backend) === 'r2' && env.FILES && row.storage_key) {
      await env.FILES.delete(row.storage_key)
    }
  }

  const ids = stale.map((row) => row.version_id)
  const placeholders = ids.map(() => '?').join(', ')
  await env.DB.prepare(`DELETE FROM file_session_history_chunks WHERE version_id IN (${placeholders})`)
    .bind(...ids)
    .run()
  await env.DB.prepare(`DELETE FROM file_session_history WHERE version_id IN (${placeholders})`)
    .bind(...ids)
    .run()
}

function chunkText(value: string): string[] {
  const chunks: string[] = []
  for (let index = 0; index < value.length; index += chunkSize) {
    chunks.push(value.slice(index, index + chunkSize))
  }
  return chunks
}

function encodeKey(value: string): string {
  return encodeURIComponent(value).replace(/%/g, '_')
}

function cleanCompany(value: unknown): string {
  const company = String(value ?? 'EPA').trim()
  return company || 'EPA'
}

function cleanKind(value: unknown): FileKind | undefined {
  const kind = String(value ?? '').trim()
  return fileKindSet.has(kind) ? (kind as FileKind) : undefined
}

function cleanSourceName(value: unknown): string {
  return String(value ?? '').trim().slice(0, 300)
}

function normalizeBackend(value: unknown): StorageBackend {
  return value === 'r2' ? 'r2' : 'd1'
}

function safeJson(value: string): unknown {
  try {
    return value ? (JSON.parse(value) as unknown) : undefined
  } catch {
    return undefined
  }
}
