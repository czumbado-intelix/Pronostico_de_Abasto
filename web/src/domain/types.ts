export type Company = 'EPA' | 'Cofersa'

export type CoverageStatus = 'Critical' | 'Warning' | 'Healthy'

export type TransferRecommendationState =
  | 'NotRequired'
  | 'Suggested'
  | 'Partial'
  | 'Unavailable'

export type ForecastGranularity = 'Weekly' | 'Monthly'

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

export interface AppSettings {
  company: Company
  satelliteZones: string[]
  excludedZones: string[]
  thresholds: CoverageThresholds
}

export interface LoadedFiles {
  inventory?: InventoryWorkbook
  forecast?: ForecastWorkbook
  expediciones?: ExpedicionesWorkbook
}
