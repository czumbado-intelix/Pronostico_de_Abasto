interface Env {
  DB: D1Database
}

const fileKinds = ['inventory', 'forecast', 'expediciones', 'comparisonInventory', 'comparisonForecast'] as const
type FileKind = (typeof fileKinds)[number]

const fileKindSet = new Set<string>(fileKinds)
const chunkSize = 450_000
const maxPayloadBytes = 45_000_000

interface FileSessionRow {
  file_kind: string
  chunk_count: number
}

interface FileSessionChunk {
  payload_chunk: string
}

export const onRequestGet: PagesFunction<Env> = async ({ env, params }) => {
  const company = cleanCompany(params.company)
  return Response.json(await readFileSessions(env.DB, company))
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
      { error: 'El archivo procesado es demasiado grande para guardarlo en D1.' },
      { status: 413 },
    )
  }

  const chunks = chunkText(payloadText)

  await env.DB.prepare('DELETE FROM file_session_chunks WHERE company = ? AND file_kind = ?')
    .bind(company, kind)
    .run()

  await env.DB.prepare(
    `INSERT INTO file_sessions(company, file_kind, source_name, payload_bytes, chunk_count, updated_at)
     VALUES (?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
     ON CONFLICT(company, file_kind) DO UPDATE SET
       source_name = excluded.source_name,
       payload_bytes = excluded.payload_bytes,
       chunk_count = excluded.chunk_count,
       updated_at = CURRENT_TIMESTAMP`,
  )
    .bind(company, kind, cleanSourceName(body.sourceName), payloadBytes, chunks.length)
    .run()

  if (chunks.length > 0) {
    const statement = env.DB.prepare(
      `INSERT INTO file_session_chunks(company, file_kind, chunk_index, payload_chunk)
       VALUES (?, ?, ?, ?)`,
    )
    await env.DB.batch(chunks.map((chunk, index) => statement.bind(company, kind, index, chunk)))
  }

  return Response.json(await readFileSessions(env.DB, company))
}

export const onRequestDelete: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = cleanCompany(params.company)
  const kind = cleanKind(new URL(request.url).searchParams.get('kind'))

  if (new URL(request.url).searchParams.has('kind') && !kind) {
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

  return Response.json(await readFileSessions(env.DB, company))
}

async function readFileSessions(db: D1Database, company: string): Promise<Record<string, unknown>> {
  const { results } = await db.prepare(
    `SELECT file_kind, chunk_count
     FROM file_sessions
     WHERE company = ?
     ORDER BY file_kind COLLATE NOCASE`,
  )
    .bind(company)
    .all<FileSessionRow>()

  const sessions: Record<string, unknown> = {}
  for (const row of results) {
    const kind = cleanKind(row.file_kind)
    if (!kind) {
      continue
    }

    const { results: chunks } = await db.prepare(
      `SELECT payload_chunk
       FROM file_session_chunks
       WHERE company = ? AND file_kind = ?
       ORDER BY chunk_index ASC`,
    )
      .bind(company, kind)
      .all<FileSessionChunk>()

    if (chunks.length !== row.chunk_count) {
      continue
    }

    const payload = safeJson(chunks.map((chunk) => chunk.payload_chunk).join(''))
    if (payload !== undefined) {
      sessions[kind] = payload
    }
  }

  return sessions
}

function chunkText(value: string): string[] {
  const chunks: string[] = []
  for (let index = 0; index < value.length; index += chunkSize) {
    chunks.push(value.slice(index, index + chunkSize))
  }
  return chunks
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

function safeJson(value: string): unknown {
  try {
    return JSON.parse(value) as unknown
  } catch {
    return undefined
  }
}
