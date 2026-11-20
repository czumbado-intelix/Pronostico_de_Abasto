import { normalizeSearchText } from './normalization'

export type SortDirection = 'asc' | 'desc'

export interface TableColumn<T> {
  key: string
  label: string
  text: (row: T) => string
  number?: (row: T) => number | undefined
}

export interface TableSort {
  column: string
  direction: SortDirection
}

export type TableFilters = Record<string, string>

export function applyTableControls<T>(
  rows: T[],
  columns: TableColumn<T>[],
  filters: TableFilters | undefined,
  sort: TableSort | undefined,
): T[] {
  const activeFilters = Object.entries(filters ?? {})
    .map(([key, value]) => [key, normalizeSearchText(value)] as const)
    .filter(([, value]) => Boolean(value))

  let result = activeFilters.length === 0
    ? rows
    : rows.filter((row) =>
        activeFilters.every(([key, query]) => {
          const column = columns.find((item) => item.key === key)
          return column ? normalizeSearchText(column.text(row)).includes(query) : true
        }),
      )

  if (sort) {
    const column = columns.find((item) => item.key === sort.column)
    if (column) {
      result = [...result].sort((a, b) => {
        const numericA = column.number?.(a)
        const numericB = column.number?.(b)
        const multiplier = sort.direction === 'asc' ? 1 : -1
        if (numericA !== undefined || numericB !== undefined) {
          if (numericA === undefined) return 1
          if (numericB === undefined) return -1
          return (numericA - numericB) * multiplier
        }
        return column.text(a).localeCompare(column.text(b), 'es', { sensitivity: 'base' }) * multiplier
      })
    }
  }

  return result
}
