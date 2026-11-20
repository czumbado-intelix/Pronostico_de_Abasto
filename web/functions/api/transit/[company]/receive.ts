interface Env {
  DB: D1Database
}

export const onRequestPost: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = String(params.company ?? 'EPA')
  const body = (await request.json()) as { ids?: string[]; article?: string }
  const now = new Date().toISOString()

  if (Array.isArray(body.ids) && body.ids.length > 0) {
    const statement = env.DB.prepare(
      `UPDATE transit_items
       SET received_at = ?
       WHERE company = ? AND id = ? AND received_at IS NULL AND reconciled_at IS NULL`,
    )
    await env.DB.batch(
      body.ids
        .map((id) => String(id).trim())
        .filter(Boolean)
        .map((id) => statement.bind(now, company, id)),
    )
  } else if (body.article) {
    await env.DB.prepare(
      `UPDATE transit_items
       SET received_at = ?
       WHERE company = ? AND article = ? AND received_at IS NULL AND reconciled_at IS NULL`,
    )
      .bind(now, company, String(body.article).trim())
      .run()
  }

  const { results } = await env.DB.prepare(
    `SELECT id, company, period_key, article, description, pallet, quantity, storage_zone,
      location, validation_date, tarima_size, origin, created_at, received_at, reconciled_at
     FROM transit_items
     WHERE company = ?
     ORDER BY created_at DESC`,
  )
    .bind(company)
    .all<Record<string, string | number | null>>()

  return Response.json(
    results.map((row) => ({
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
    })),
  )
}
