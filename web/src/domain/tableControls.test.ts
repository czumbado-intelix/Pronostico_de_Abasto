import { describe, expect, it } from 'vitest'
import { applyTableControls, type TableColumn } from './tableControls'

interface Row {
  article: string
  description: string
  quantity: number
}

const columns: TableColumn<Row>[] = [
  { key: 'article', label: 'Articulo', text: (row) => row.article },
  { key: 'description', label: 'Descripcion', text: (row) => row.description },
  { key: 'quantity', label: 'Cantidad', text: (row) => String(row.quantity), number: (row) => row.quantity },
]

const rows: Row[] = [
  { article: '1002', description: 'Beta', quantity: 5 },
  { article: '1001', description: 'Alfa', quantity: 20 },
  { article: '1003', description: 'Gamma', quantity: 10 },
]

describe('applyTableControls', () => {
  it('filters by the selected column text', () => {
    const result = applyTableControls(rows, columns, { description: 'alf' }, undefined)

    expect(result.map((row) => row.article)).toEqual(['1001'])
  })

  it('sorts numeric columns without string ordering', () => {
    const result = applyTableControls(rows, columns, {}, { column: 'quantity', direction: 'asc' })

    expect(result.map((row) => row.quantity)).toEqual([5, 10, 20])
  })
})
