import { describe, expect, it } from 'vitest'
import { parseExpedicionesWorkbook } from './excel'

describe('expediciones parser', () => {
  it('reads blocked expedition reports with summary rows and detail rows', () => {
    const workbook = [[
      [
        'EXPEDICION',
        'Id Compania',
        'Id Sucursal',
        'Nombre Cliente',
        'Expedicion',
        'Tipo Expedicion',
        'Estado',
        'Situacion',
      ],
      ['', '0029', '0001', 'T002 CURRI', '2000019720', 'EXPERP', 'DEPA', 'GENE'],
      [
        '',
        'Linea',
        'Articulo CLiente',
        'Articulo',
        'Descripcion',
        'Presentacion',
        'Pedidas',
        'Preparadas',
        'Lote',
        'Palet Especifico',
        'Movimiento Fiscal',
        'Dua',
        'Contenedor',
        'Problemas',
        'Cantidad pedida presentacion minima',
        'Cantidad preparada presentacion minima',
        'Guia Fiscal',
        'Preparar Mas',
        'Porcentaje Mas',
        'Preparar Menos',
        'Porcentaje Menos',
        'Estado',
        'Situacion',
      ],
      ['', '10', '', '30000107', 'MANGA ANTICORTE', 'UD', '20 UD', '', '', '', '', '', '', '', 20, '', '', 'S', 100, 'S', 100, 'DISP', 'DISP'],
      ['', '20', '', '30000534', 'RESMA DE PAPEL', 'UD', '10 UD', '10 UD', '', '', '', '', '', '', 10, 10, '', 'S', 100, 'S', 100, 'DISP', 'EXCO'],
      [],
      [
        'EXPEDICION',
        'Id Compania',
        'Id Sucursal',
        'Nombre Cliente',
        'Expedicion',
        'Tipo Expedicion',
        'Estado',
        'Situacion',
      ],
      ['', '0029', '0001', 'T003 ESCAZU', '2000019728', 'EXPERP', 'DEPA', 'GENE'],
      [
        '',
        'Linea',
        'Articulo CLiente',
        'Articulo',
        'Descripcion',
        'Presentacion',
        'Pedidas',
        'Preparadas',
        'Lote',
        'Palet Especifico',
        'Movimiento Fiscal',
        'Dua',
        'Contenedor',
        'Problemas',
        'Cantidad pedida presentacion minima',
        'Cantidad preparada presentacion minima',
        'Guia Fiscal',
        'Preparar Mas',
        'Porcentaje Mas',
        'Preparar Menos',
        'Porcentaje Menos',
        'Estado',
        'Situacion',
      ],
      ['', '10', '', '30000107', 'MANGA ANTICORTE', 'UD', '5 UD', '2 UD', '', '', '', '', '', '', 5, 2, '', 'S', 100, 'S', 100, 'DISP', 'DISP'],
    ]]

    const parsed = parseExpedicionesWorkbook(workbook)

    expect(parsed.lines).toEqual([
      expect.objectContaining({
        article: '30000107',
        expeditionNumber: '2000019720',
        quantity: 20,
        requestedQuantity: 20,
        preparedQuantity: 0,
      }),
      expect.objectContaining({
        article: '30000107',
        expeditionNumber: '2000019728',
        quantity: 3,
        requestedQuantity: 5,
        preparedQuantity: 2,
      }),
    ])
  })
})
