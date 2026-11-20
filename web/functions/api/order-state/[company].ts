interface Env {
  DB: D1Database
}

interface OrderStateInput {
  ordered?: boolean
  selectedPalletKeys?: string[]
}

export const onRequestGet: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = String(params.company ?? 'EPA')
  const periodKey = new URL(request.url).searchParams.get('period')?.trim() ?? ''
  if (!periodKey) {
    return Response.json({})
  }

  const { results } = await env.DB.prepare(
    `SELECT row_key, ordered, selected_pallet_keys
     FROM order_state
     WHERE company = ? AND period_key = ?
     ORDER BY row_key COLLATE NOCASE`,
  )
    .bind(company, periodKey)
    .all<{ row_key: string; ordered: number; selected_pallet_keys: string }>()

  return Response.json(
    Object.fromEntries(
      results.map((row) => [
        row.row_key,
        {
          ordered: Boolean(row.ordered),
          selectedPalletKeys: safeJson<string[]>(row.selected_pallet_keys, []),
        },
      ]),
    ),
  )
}

export const onRequestPut: PagesFunction<Env> = async ({ request, env, params }) => {
  const company = String(params.company ?? 'EPA')
  const body = (await request.json()) as { periodKey?: string; entries?: Record<string, OrderStateInput> }
  const periodKey = String(body.periodKey ?? '').trim()
  const entries = body.entries && typeof body.entries === 'object' ? body.entries : {}

  if (!periodKey) {
    return Response.json({})
  }

  await env.DB.prepare('DELETE FROM order_state WHERE company = ? AND period_key = ?')
    .bind(company, periodKey)
    .run()

  const statement = env.DB.prepare(
    `INSERT INTO order_state(company, period_key, row_key, ordered, selected_pallet_keys, updated_at)
     VALUES (?, ?, ?, ?, ?, CURRENT_TIMESTAMP)`,
  )

  const statements = Object.entries(entries)
    .map(([rowKey, entry]) => ({
      rowKey: rowKey.trim(),
      ordered: Boolean(entry.ordered),
      selectedPalletKeys: cleanStringList(entry.selectedPalletKeys),
    }))
    .filter((entry) => entry.rowKey && (entry.ordered || entry.selectedPalletKeys.length > 0))
    .map((entry) =>
      statement.bind(
        company,
        periodKey,
        entry.rowKey,
        entry.ordered ? 1 : 0,
        JSON.stringify(entry.selectedPalletKeys),
      ),
    )

  if (statements.length > 0) {
    await env.DB.batch(statements)
  }

  return Response.json(
    Object.fromEntries(
      Object.entries(entries)
        .map(([rowKey, entry]) => [
          rowKey.trim(),
          {
            ordered: Boolean(entry.ordered),
            selectedPalletKeys: cleanStringList(entry.selectedPalletKeys),
          },
        ] as const)
        .filter(([rowKey, entry]) => rowKey && (entry.ordered || entry.selectedPalletKeys.length > 0)),
    ),
  )
}

function cleanStringList(value: unknown): string[] {
  return Array.isArray(value)
    ? [...new Set(value.map((item) => String(item).trim()).filter(Boolean))]
    : []
}

function safeJson<T>(value: string, fallback: T): T {
  try {
    return JSON.parse(value) as T
  } catch {
    return fallback
  }
}
