interface Env {
  DB: D1Database
}

export const onRequestGet: PagesFunction<Env> = async ({ env, params }) => {
  const company = String(params.company ?? 'EPA')
  const { results } = await env.DB.prepare(
    `SELECT article, comment
     FROM comparison_comments
     WHERE company = ?
     ORDER BY article COLLATE NOCASE`,
  )
    .bind(company)
    .all<{ article: string; comment: string }>()

  return Response.json(Object.fromEntries(results.map((row) => [row.article, row.comment])))
}

export const onRequestPut: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = String(params.company ?? 'EPA')
  const body = (await request.json()) as { comments?: Record<string, string> }
  const comments = body.comments && typeof body.comments === 'object' ? body.comments : {}

  await env.DB.prepare('DELETE FROM comparison_comments WHERE company = ?')
    .bind(company)
    .run()

  const statement = env.DB.prepare(
    `INSERT INTO comparison_comments(company, article, comment, updated_at)
     VALUES (?, ?, ?, CURRENT_TIMESTAMP)`,
  )

  const rows = Object.entries(comments)
    .map(([article, comment]) => [article.trim(), String(comment ?? '').trim()] as const)
    .filter(([article, comment]) => article && comment)

  if (rows.length > 0) {
    await env.DB.batch(rows.map(([article, comment]) => statement.bind(company, article, comment)))
  }

  return Response.json(Object.fromEntries(rows))
}
