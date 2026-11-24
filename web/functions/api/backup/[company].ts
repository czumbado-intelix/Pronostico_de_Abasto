interface Env {
  DB: D1Database
}

export const onRequestGet: PagesFunction<Env> = async ({ env, params }) => {
  const company = String(params.company ?? 'EPA').trim() || 'EPA'
  const exportedAt = new Date().toISOString()
  const backup = {
    schemaVersion: 1,
    exportedAt,
    company,
    settings: await first(env.DB, 'SELECT * FROM settings WHERE company = ?', company),
    transitItems: await all(env.DB, 'SELECT * FROM transit_items WHERE company = ? ORDER BY created_at', company),
    orderState: await all(env.DB, 'SELECT * FROM order_state WHERE company = ? ORDER BY period_key, row_key', company),
    comparisonComments: await all(
      env.DB,
      'SELECT * FROM comparison_comments WHERE company = ? ORDER BY article COLLATE NOCASE',
      company,
    ),
    fileSessions: await all(
      env.DB,
      `SELECT company, file_kind, source_name, payload_bytes, chunk_count, storage_backend, storage_key, version_id, updated_at
       FROM file_sessions
       WHERE company = ?
       ORDER BY file_kind COLLATE NOCASE`,
      company,
    ),
    fileSessionHistory: await all(
      env.DB,
      `SELECT version_id, company, file_kind, source_name, payload_bytes, storage_backend, storage_key, chunk_count, created_at, restored_at
       FROM file_session_history
       WHERE company = ?
       ORDER BY created_at DESC`,
      company,
    ),
  }

  return new Response(JSON.stringify(backup, null, 2), {
    headers: {
      'content-type': 'application/json; charset=utf-8',
      'content-disposition': `attachment; filename="pronostico-abasto-${company}-${exportedAt.slice(0, 10)}.json"`,
    },
  })
}

async function first<T = Record<string, unknown>>(db: D1Database, sql: string, ...bindings: unknown[]): Promise<T | null> {
  return db.prepare(sql).bind(...bindings).first<T>()
}

async function all<T = Record<string, unknown>>(db: D1Database, sql: string, ...bindings: unknown[]): Promise<T[]> {
  const { results } = await db.prepare(sql).bind(...bindings).all<T>()
  return results
}
