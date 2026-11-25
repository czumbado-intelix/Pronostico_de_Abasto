import { describe, expect, it } from 'vitest'
import {
  analyzeWarehouseComparison,
  analyzeExpediciones,
  analyzeWeeklyForecast,
  buildWarehouseComparisonHistogram,
  outstandingPalletKeys,
  pendingTransitQuantities,
  simulateScenario,
  summarizeInventory,
} from './analysis'
import { defaultExcludedZones, defaultSatelliteZones, defaultThresholds } from './defaults'
import type { InventoryPosition, TransitItem } from './types'

describe('web analysis port', () => {
  it('classifies ZA36 with double spaces as principal stock', () => {
    const inventory: InventoryPosition[] = [
      {
        article: '100019412003',
        storageZone: 'ZA36-ZONA  ALMACENAJE MUELLE RECEPCION - CLIRO',
        quantity: 25,
        description: 'PUERTA PRINTED BOURBON HDF. 80X210CM',
        pallet: '22O0015417516',
        palletType: 'MULART',
        location: 'PURT04-C001-N01-1',
      },
    ]

    const summaries = summarizeInventory(inventory, defaultSatelliteZones, defaultExcludedZones)
    const summary = summaries.get('100019412003')

    expect(summary?.principalQuantity).toBe(25)
    expect(summary?.satelliteQuantity).toBe(0)
  })

  it('sums repeated forecast rows for the same article and period', () => {
    const result = analyzeWeeklyForecast({
      inventory: [],
      forecast: [
        { article: '100019414004', period: '2026-11-16', quantity: 10 },
        { article: '100019414004', period: '2026-11-16', quantity: 15 },
      ],
      selectedPeriod: '2026-11-16',
      horizonPeriods: 1,
      thresholds: defaultThresholds,
      satelliteZones: defaultSatelliteZones,
      ignoredStorageZones: defaultExcludedZones,
      availablePeriods: ['2026-11-16'],
    })

    expect(result.articles.find((article) => article.article === '100019414004')?.forecastQuantity).toBe(25)
  })

  it('removes pending transit pallets from available principal inventory', () => {
    const transit: TransitItem[] = [
      {
        id: '1',
        company: 'EPA',
        periodKey: '2026-11-16',
        article: '100019412003',
        description: '',
        pallet: '22O0015417516',
        quantity: 25,
        storageZone: 'ZA52-ZONA ALMACENAJE MUELLE RECEPCION - GUACIMA',
        location: '',
        tarimaSize: '',
        origin: 'COCO',
        createdAt: '2026-11-16T00:00:00.000Z',
      },
    ]

    const result = analyzeWeeklyForecast({
      inventory: [
        {
          article: '100019412003',
          storageZone: 'ZA36-ZONA  ALMACENAJE MUELLE RECEPCION - CLIRO',
          quantity: 25,
          description: '',
          pallet: '22O0015417516',
          palletType: 'MULART',
          location: 'PURT04-C001-N01-1',
        },
      ],
      forecast: [{ article: '100019412003', period: '2026-11-16', quantity: 25 }],
      selectedPeriod: '2026-11-16',
      horizonPeriods: 1,
      thresholds: defaultThresholds,
      satelliteZones: defaultSatelliteZones,
      ignoredStorageZones: defaultExcludedZones,
      pendingTransitQuantities: pendingTransitQuantities(transit),
      unavailablePalletKeys: outstandingPalletKeys(transit),
      availablePeriods: ['2026-11-16'],
    })

    const article = result.articles.find((row) => row.article === '100019412003')
    expect(article?.principalInventoryQuantity).toBe(0)
    expect(article?.pendingTransitQuantity).toBe(25)
    expect(article?.calculatedTransferNeedQuantity).toBe(0)
  })

  it('applies excluded inventory zones to expediciones', () => {
    const result = analyzeExpediciones({
      inventory: [
        {
          article: '10000001',
          storageZone: 'ZA99-ZONA BLOQUEADA',
          quantity: 8,
          description: '',
          pallet: 'P1',
          palletType: 'MULART',
          location: '',
        },
      ],
      lines: [
        {
          article: '10000001',
          description: 'Articulo prueba',
          quantity: 8,
          expeditionNumber: '20000001',
          requestedQuantity: 8,
          preparedQuantity: 0,
          situation: 'DISP',
        },
      ],
      satelliteZones: [],
      ignoredStorageZones: ['ZA99'],
    })

    const row = result.lines.find((line) => line.article === '10000001')
    expect(row?.principalInventoryQuantity).toBe(0)
    expect(row?.transferSuggestionQuantity).toBe(0)
    expect(row?.remainingShortageAfterTransfer).toBe(8)
  })

  it('applies excluded inventory zones to simulation', () => {
    const result = simulateScenario({
      inventory: [
        {
          article: '10000001',
          storageZone: 'ZA99-ZONA BLOQUEADA',
          quantity: 8,
          description: '',
          pallet: 'P1',
          palletType: 'MULART',
          location: '',
        },
      ],
      forecast: [{ article: '10000001', period: '2026-11-01', quantity: 8 }],
      availablePeriods: ['2026-11-01'],
      selectedPeriod: '2026-11-01',
      horizonMonths: 1,
      safetyBufferPercent: 0,
      satelliteZones: ['ZA99'],
      ignoredStorageZones: ['ZA99'],
    })

    expect(result.periods[0]?.transferLoad).toBe(0)
  })

  it('excludes merma pallets and ignored zones from warehouse comparison', () => {
    const result = analyzeWarehouseComparison({
      inventory: [
        {
          article: '1000',
          storageZone: 'ZA36-ZONA  ALMACENAJE MUELLE RECEPCION - CLIRO',
          quantity: 10,
          description: 'Articulo prueba',
          pallet: 'P1',
          palletType: 'MULART',
          location: '',
        },
        {
          article: '1000',
          storageZone: 'ZA46-ZA45 ZONA ALMACENAJE SERVICA',
          quantity: 5,
          description: 'Articulo prueba',
          pallet: 'P2',
          palletType: 'Tipo pallet Merma',
          location: '',
        },
        {
          article: '1000',
          storageZone: 'ZA30-ZONA BLOQUEADA',
          quantity: 7,
          description: 'Articulo prueba',
          pallet: 'P3',
          palletType: 'MULART',
          location: '',
        },
      ],
      forecast: [{ article: '1000', period: '2026-01-05', quantity: 10 }],
      availablePeriods: ['2026-01-05'],
      selectedPeriod: '2026-01-05',
      ignoredStorageZones: ['ZA30'],
    })

    const row = result.rows.find((item) => item.article === '1000')
    expect(row?.oloQuantity).toBe(10)
    expect(row?.servicaQuantity).toBe(0)
    expect(row?.coveragePeriods).toBe(1)
  })

  it('leaves weekly comparison coverage blank when demand starts after five periods', () => {
    const periods = [
      '2026-01-05',
      '2026-01-12',
      '2026-01-19',
      '2026-01-26',
      '2026-02-02',
      '2026-02-09',
    ]

    const result = analyzeWarehouseComparison({
      inventory: [],
      forecast: [{ article: 'A1', period: '2026-02-09', quantity: 10 }],
      availablePeriods: periods,
      selectedPeriod: '2026-01-05',
      granularity: 'Weekly',
    })

    expect(result.rows[0]?.coveragePeriods).toBeUndefined()
    expect(result.rows[0]?.automaticComment).toBe('solicita hasta 09.02.2026')
  })

  it('keeps zero comparison coverage for deferred monthly demand', () => {
    const result = analyzeWarehouseComparison({
      inventory: [],
      forecast: [{ article: 'A1', period: '2026-06-01', quantity: 10 }],
      availablePeriods: [
        '2026-01-01',
        '2026-02-01',
        '2026-03-01',
        '2026-04-01',
        '2026-05-01',
        '2026-06-01',
      ],
      selectedPeriod: '2026-01-01',
      granularity: 'Monthly',
    })

    expect(result.rows[0]?.coveragePeriods).toBe(0)
    expect(result.rows[0]?.automaticComment).toBe('solicita hasta 01.06.2026')
  })

  it('resolves comparison descriptions using article keys without leading zeroes', () => {
    const result = analyzeWarehouseComparison({
      inventory: [],
      forecast: [{ article: '0002001', period: '2026-01-05', quantity: 1 }],
      availablePeriods: ['2026-01-05'],
      selectedPeriod: '2026-01-05',
      descriptions: { '2001': 'Bomba manual' },
    })

    expect(result.rows[0]?.description).toBe('Bomba manual')
  })

  it('excludes manual no solicita comments from warehouse comparison histogram', () => {
    const histogram = buildWarehouseComparisonHistogram(
      [
        {
          article: 'A',
          description: '',
          oloQuantity: 1,
          servicaQuantity: 0,
          forecastQuantity: 1,
          coveragePeriods: 1,
          hasFutureForecast: true,
          automaticComment: '',
        },
        {
          article: 'B',
          description: '',
          oloQuantity: 2,
          servicaQuantity: 0,
          forecastQuantity: 1,
          coveragePeriods: 2,
          hasFutureForecast: true,
          automaticComment: '',
        },
      ],
      4,
      'sem',
      { A: 'no solicita' },
    )

    expect(histogram).toEqual([{ label: '2 sem', articleCount: 1, percentage: 100 }])
  })

  it('uses rolling month horizons for transfer analysis', () => {
    const result = analyzeWeeklyForecast({
      inventory: [],
      forecast: [{ article: '1000', period: '2026-07-20', quantity: 1 }],
      selectedPeriod: '2026-06-22',
      horizon: { unit: 'Months', amount: 1 },
      thresholds: defaultThresholds,
      satelliteZones: defaultSatelliteZones,
      ignoredStorageZones: defaultExcludedZones,
      availablePeriods: [
        '2026-06-22',
        '2026-06-29',
        '2026-07-06',
        '2026-07-13',
        '2026-07-20',
        '2026-07-27',
      ],
    })

    expect(result.analyzedPeriods).toBe(5)
    expect(result.horizonEndPeriod).toBe('2026-07-20')
    expect(result.isIncompleteHorizon).toBe(false)
  })
})
