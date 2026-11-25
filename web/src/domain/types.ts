export type Company = 'EPA' | 'Cofersa'

export type CoverageStatus = 'Critical' | 'Warning' | 'Healthy'

export type TransferRecommendationState =
  | 'NotRequired'
  | 'Suggested'
  | 'Partial'
  | 'Unavailable'

export type ForecastGranularity = 'Weekly' | 'Monthly'

export type HorizonUnit = 'Weeks' | 'Months'

export interface AnalysisHorizon {
  unit: HorizonUnit
  amount: number
}

export interface CoverageThresholds {
  redPercent: number
  healthyPercent: number
}

export interface InventoryPosition {
  article: string
  storageZone: string
  quantity: number
  description: string
  pallet: string
  validationDate?: string
  palletType: string
  location: string
}

export interface ForecastEntry {
  article: string
  period: string
  quantity: number
}

export interface ForecastWorkbook {
  entries: ForecastEntry[]
  availablePeriods: string[]
  granularity: ForecastGranularity
  descriptions: Record<string, string>
}

export interface InventoryWorkbook {
  positions: InventoryPosition[]
  descriptions: Record<string, string>
}

export interface ExpeditionLine {
  article: string
  description: string
  quantity: number
  expeditionNumber: string
  requestedQuantity: number
  preparedQuantity: number
  situation: string
}

export interface ExpedicionesWorkbook {
  lines: ExpeditionLine[]
}

export interface ZoneInventoryDetail {
  storageZone: string
  quantity: number
  isSatellite: boolean
}

export interface PalletInventoryDetail {
  pallet: string
  storageZone: string
  quantity: number
  validationDate?: string
  palletType: string
  location: string
}

export interface ArticleInventorySummary {
  article: string
  principalQuantity: number
  satelliteQuantity: number
  totalQuantity: number
  zones: ZoneInventoryDetail[]
  satelliteZones: ZoneInventoryDetail[]
  satellitePallets: PalletInventoryDetail[]
  principalPallets: PalletInventoryDetail[]
}

export interface TransferPlan {
  calculatedNeedQuantity: number
  transferSuggestionQuantity: number
  newTransferSuggestionQuantity: number
  palletRoundUpSurplusQuantity: number
  remainingShortageAfterTransfer: number
  recommendationState: TransferRecommendationState
  suggestedPallets: PalletInventoryDetail[]
}

export interface ArticleAnalysisResult {
  article: string
  description: string
  principalInventoryQuantity: number
  totalInventoryQuantity: number
  forecastQuantity: number
  difference: number
  status: CoverageStatus
  zones: ZoneInventoryDetail[]
  satelliteInventoryQuantity: number
  satelliteDifference: number
  satelliteStatus: CoverageStatus
  transferSuggestionQuantity: number
  calculatedTransferNeedQuantity: number
  palletRoundUpSurplusQuantity: number
  remainingShortageAfterTransfer: number
  transferRecommendationState: TransferRecommendationState
  satelliteZones: ZoneInventoryDetail[]
  pendingTransitQuantity: number
  newTransferSuggestionQuantity: number
  suggestedPallets: PalletInventoryDetail[]
  availablePallets: PalletInventoryDetail[]
  coveragePeriods?: number
}

export interface WeeklyAnalysisResult {
  selectedPeriod: string
  horizonEndPeriod: string
  requestedPeriods: number
  analyzedPeriods: number
  isIncompleteHorizon: boolean
  articles: ArticleAnalysisResult[]
  criticalCount: number
  warningCount: number
  healthyCount: number
  satelliteCriticalCount: number
  satelliteWarningCount: number
  satelliteHealthyCount: number
}

export interface ExpeditionLineResult {
  article: string
  description: string
  expeditionNumber: string
  demandQuantity: number
  principalInventoryQuantity: number
  satelliteInventoryQuantity: number
  transferSuggestionQuantity: number
  calculatedTransferNeedQuantity: number
  palletRoundUpSurplusQuantity: number
  remainingShortageAfterTransfer: number
  transferRecommendationState: TransferRecommendationState
  satelliteZones: ZoneInventoryDetail[]
  pendingTransitQuantity: number
  newTransferSuggestionQuantity: number
  suggestedPallets: PalletInventoryDetail[]
  availablePallets: PalletInventoryDetail[]
}

export interface ExpedicionesAnalysisResult {
  lines: ExpeditionLineResult[]
  evaluated: number
  covered: number
  toTransfer: number
  withoutExternal: number
}

export interface WarehouseComparisonRow {
  article: string
  description: string
  oloQuantity: number
  servicaQuantity: number
  forecastQuantity: number
  coveragePeriods?: number
  hasFutureForecast: boolean
  automaticComment: string
}

export interface WarehouseComparisonResult {
  selectedPeriod: string
  horizonEndPeriod: string
  periodCount: number
  rows: WarehouseComparisonRow[]
}

export interface WarehouseComparisonHistogramBucket {
  label: string
  articleCount: number
  percentage: number
}

export interface ScenarioPeriod {
  period?: string
  label: string
  epaDemand?: number
  cofersaDemand?: number
  demand: number
  transferLoad: number
}

export interface ScenarioResult {
  periods: ScenarioPeriod[]
  hasEpa?: boolean
  hasCofersa?: boolean
}

export interface TransitItem {
  id: string
  company: Company
  periodKey: string
  article: string
  description: string
  pallet: string
  quantity: number
  storageZone: string
  location: string
  validationDate?: string
  tarimaSize: string
  origin: string
  createdAt: string
  receivedAt?: string
  reconciledAt?: string
}

export interface CatalogArticle {
  article: string
  description: string
}

export interface TarimaSizeEntry {
  article: string
  description: string
  sizeCode: string
}

export interface AppSettings {
  company: Company
  satelliteZones: string[]
  excludedZones: string[]
  thresholds: CoverageThresholds
  preparedArticles: CatalogArticle[]
  tarimaSizes: TarimaSizeEntry[]
}

export interface LoadedFiles {
  transferInventory?: InventoryWorkbook
  transferForecast?: ForecastWorkbook
  expeditionInventory?: InventoryWorkbook
  expeditionReport?: ExpedicionesWorkbook
  simulationInventory?: InventoryWorkbook
  simulationForecast?: ForecastWorkbook
  inventory?: InventoryWorkbook
  forecast?: ForecastWorkbook
  expediciones?: ExpedicionesWorkbook
  comparisonInventory?: InventoryWorkbook
  comparisonForecast?: ForecastWorkbook
}

export type FileSessionKind = keyof LoadedFiles

export interface FileSessionInfo {
  versionId: string
  kind: FileSessionKind
  sourceName: string
  payloadBytes: number
  storageBackend: 'd1' | 'r2'
  updatedAt: string
  active: boolean
}

export interface OrderStateEntry {
  ordered: boolean
  selectedPalletKeys: string[]
}
