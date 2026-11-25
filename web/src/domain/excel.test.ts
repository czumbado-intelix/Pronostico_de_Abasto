import { strToU8, zipSync } from 'fflate'
import { describe, expect, it } from 'vitest'
import { parseExpedicionesFile, parseExpedicionesWorkbook, readWorkbook } from './excel'

describe('expediciones parser', () => {
  it('reads xlsx sheets whose cells omit address references', async () => {
    const file = makeXlsxFile([
      ['EXPEDICION', 'Id Compania', 'Expedicion', 'Situacion'],
      ['', '0029', '2000019720', 'GENE'],
    ])

    const workbook = await readWorkbook(file)

    expect(workbook[0]?.[0]).toEqual(['EXPEDICION', 'Id Compania', 'Expedicion', 'Situacion'])
    expect(workbook[0]?.[1]).toEqual(['', '0029', '2000019720', 'GENE'])
  })

  it('loads expedition files whose cells omit address references', async () => {
    const file = makeXlsxFile([
      ['EXPEDICION', 'Id Compania', 'Id Sucursal', 'Nombre Cliente', 'Expedicion', 'Tipo Expedicion', 'Estado', 'Situacion'],
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
    ])

    const parsed = await parseExpedicionesFile(file)

    expect(parsed.lines).toEqual([
      expect.objectContaining({
        article: '30000107',
        expeditionNumber: '2000019720',
        quantity: 20,
      }),
    ])
  })

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

function makeXlsxFile(rows: unknown[][]): File {
  const archive = zipSync({
    'xl/workbook.xml': strToU8(
      '<?xml version="1.0" encoding="UTF-8"?>' +
      '<x:workbook xmlns:x="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">' +
      '<x:sheets><x:sheet name="Expediciones" sheetId="1" r:id="rId1"/></x:sheets>' +
      '</x:workbook>',
    ),
    'xl/_rels/workbook.xml.rels': strToU8(
      '<?xml version="1.0" encoding="UTF-8"?>' +
      '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
      '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>' +
      '</Relationships>',
    ),
    'xl/worksheets/sheet1.xml': strToU8(
      '<?xml version="1.0" encoding="UTF-8"?>' +
      '<x:worksheet xmlns:x="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
      '<x:sheetData>' +
      rows.map((row, rowIndex) => (
        `<x:row r="${rowIndex + 1}">` +
        row.map((value) => makeCell(value)).join('') +
        '</x:row>'
      )).join('') +
      '</x:sheetData>' +
      '</x:worksheet>',
    ),
  })
  return new File([archive], 'expediciones.xlsx', {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  })
}

function makeCell(value: unknown): string {
  if (typeof value === 'number') {
    return `<x:c><x:v>${value}</x:v></x:c>`
  }
  const escaped = String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
  return `<x:c t="inlineStr"><x:is><x:t>${escaped}</x:t></x:is></x:c>`
}
