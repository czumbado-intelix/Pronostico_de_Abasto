import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { saveSettings } from './api'
import type { AppSettings } from '../domain/types'

describe('settings api', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('sends excluded zones when saving settings', async () => {
    const settings: AppSettings = {
      company: 'EPA',
      satelliteZones: ['ZA31', 'ZA31', ''],
      excludedZones: ['ZA30', 'ZA38', 'ZA30', ''],
      thresholds: { redPercent: 0, healthyPercent: 100 },
      preparedArticles: [],
      tarimaSizes: [],
    }
    const fetchMock = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
      const body = JSON.parse(String(init?.body)) as AppSettings
      return new Response(JSON.stringify(body), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      })
    })
    vi.stubGlobal('fetch', fetchMock)

    const saved = await saveSettings(settings)
    const requestBody = JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body)) as AppSettings

    expect(requestBody.satelliteZones).toEqual(['ZA31'])
    expect(requestBody.excludedZones).toEqual(['ZA30', 'ZA38'])
    expect(saved.excludedZones).toEqual(['ZA30', 'ZA38'])
  })

  it('does not report success when the cloud save fails', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ error: 'D1 no disponible' }), { status: 500 })))

    await expect(saveSettings({
      company: 'EPA',
      satelliteZones: [],
      excludedZones: ['ZA99'],
      thresholds: { redPercent: 0, healthyPercent: 100 },
      preparedArticles: [],
      tarimaSizes: [],
    })).rejects.toThrow('D1 no disponible')

    expect(window.localStorage.length).toBe(0)
  })
})
