import type { AppSettings, Company, CoverageThresholds } from './types'

export const companies: Company[] = ['EPA', 'Cofersa']

export const defaultSatelliteZones = [
  'ZA31-ZONA ALMACENAJE ALGEFISA - CLIRO',
  'ZA46-ZA45 - ZONA ALMACENAJE - SERVICA',
  'ZA47-ZA47 - ZONA ALMACENAJE 1 - GUACIMA',
  'ZA48-ZA48 - ZONA ALMACENAJE 2 - GUACIMA',
  'ZA49-ZA49 - ZONA ALMACENAJE 3 - GUACIMA',
  'ZA50-ZA50 - ZONA ALMACENAJE 4 - GUACIMA',
  'ZA52-ZONA ALMACENAJE MUELLE RECEPCION - GUACIMA',
  'ZA55-ZA55 - ZONA ALMACENAJE 5 - GUACIMA',
]

export const defaultExcludedZones: string[] = []

export const defaultThresholds: CoverageThresholds = {
  redPercent: 0,
  healthyPercent: 100,
}

export function defaultSettings(company: Company): AppSettings {
  return {
    company,
    satelliteZones: [...defaultSatelliteZones],
    excludedZones: [...defaultExcludedZones],
    thresholds: { ...defaultThresholds },
  }
}
