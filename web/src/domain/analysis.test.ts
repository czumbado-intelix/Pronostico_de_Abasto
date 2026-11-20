import { describe, expect, it } from 'vitest'
import {
  analyzeWeeklyForecast,
  outstandingPalletKeys,
  pendingTransitQuantities,
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
})
