import {
  buildArticlePalletKey,
  buildStorageZoneSet,
  compareOptionalDateThenText,
  isIgnoredStorageZone,
  normalizeArticleKey,
  normalizeZoneKey,
} from './normalization'
import type {
  ArticleAnalysisResult,
  ArticleInventorySummary,
  CoverageStatus,
  CoverageThresholds,
  ExpeditionLineResult,
  ExpedicionesAnalysisResult,
  ExpeditionLine,
  ForecastEntry,
  InventoryPosition,
  PalletInventoryDetail,
  TransferPlan,
  TransferRecommendationState,
  TransitItem,
  WeeklyAnalysisResult,
  ZoneInventoryDetail,
} from './types'

export function summarizeInventory(
  inventory: InventoryPosition[],
  satelliteZones?: string[],
  ignoredStorageZones?: string[],
): Map<string, ArticleInventorySummary> {
  const satelliteZoneSet = buildStorageZoneSet(satelliteZones)
  const ignoredZoneSet = buildStorageZoneSet(ignoredStorageZones)
  const treatAllZonesAsSatellite = satelliteZoneSet.size === 0
  const byArticle = new Map<string, InventoryPosition[]>()

  for (const position of inventory) {
    if (!position.article.trim() || isIgnoredStorageZone(position.storageZone, ignoredZoneSet)) {
      continue
    }

    const key = normalizeArticleKey(position.article).toUpperCase()
    const positions = byArticle.get(key) ?? []
    positions.push(position)
    byArticle.set(key, positions)
  }

  const summaries = new Map<string, ArticleInventorySummary>()
  for (const [key, positions] of byArticle) {
    const classified = positions.map((position) => {
      const storageZone = position.storageZone.trim() || 'Sin zona'
      const isSatellite =
        treatAllZonesAsSatellite || satelliteZoneSet.has(normalizeZoneKey(storageZone))
      return { position, storageZone, isSatellite }
    })

    const zonesByKey = new Map<string, ZoneInventoryDetail>()
    for (const item of classified) {
      const zoneKey = normalizeZoneKey(item.storageZone)
      const existing = zonesByKey.get(zoneKey)
      if (existing) {
        existing.quantity += item.position.quantity
      } else {
        zonesByKey.set(zoneKey, {
          storageZone: item.storageZone,
          quantity: item.position.quantity,
          isSatellite: item.isSatellite,
        })
      }
    }

    const zones = [...zonesByKey.values()].sort((a, b) =>
      normalizeZoneKey(a.storageZone).localeCompare(normalizeZoneKey(b.storageZone)),
    )
    const satelliteZoneDetails = zones.filter((zone) => zone.isSatellite)
    const satellitePallets = buildPalletDetails(classified.filter((item) => item.isSatellite))
    const principalPallets = buildPalletDetails(classified.filter((item) => !item.isSatellite))

    summaries.set(key, {
      article: positions[0].article.trim(),
      principalQuantity: zones
        .filter((zone) => !zone.isSatellite)
        .reduce((sum, zone) => sum + zone.quantity, 0),
      satelliteQuantity: satelliteZoneDetails.reduce((sum, zone) => sum + zone.quantity, 0),
      totalQuantity: positions.reduce((sum, position) => sum + position.quantity, 0),
      zones,
      satelliteZones: satelliteZoneDetails,
      satellitePallets,
      principalPallets,
    })
  }

  return summaries
}

function buildPalletDetails(
  positions: Array<{ position: InventoryPosition; storageZone: string }>,
): PalletInventoryDetail[] {
  const byKey = new Map<string, PalletInventoryDetail>()
  for (const item of positions) {
    if (item.position.quantity <= 0) {
      continue
    }

    const pallet = item.position.pallet.trim() || 'Sin pallet'
    const location = item.position.location.trim() || 'Sin ubicacion'
    const palletType = item.position.palletType.trim()
    const key = [
      pallet.toUpperCase(),
      normalizeZoneKey(item.storageZone),
      location.toUpperCase(),
      item.position.validationDate ?? '',
      palletType.toUpperCase(),
    ].join('|')

    const existing = byKey.get(key)
    if (existing) {
      existing.quantity += item.position.quantity
    } else {
      byKey.set(key, {
        pallet,
        storageZone: item.storageZone,
        quantity: item.position.quantity,
        validationDate: item.position.validationDate,
        palletType,
        location,
      })
    }
  }

  return [...byKey.values()].sort((a, b) => {
    const date = compareOptionalDateThenText(a.validationDate, b.validationDate)
    if (date !== 0) {
      return date
    }
    return (
      a.pallet.localeCompare(b.pallet) ||
      a.storageZone.localeCompare(b.storageZone) ||
      a.location.localeCompare(b.location)
    )
  })
}

export function planTransfer(
  demandQuantity: number,
  principalInventoryQuantity: number,
  satelliteInventoryQuantity: number,
  pendingTransitQuantity: number,
  satellitePallets: PalletInventoryDetail[],
): TransferPlan {
  const pending = Math.max(0, pendingTransitQuantity)
  const principalShortage = Math.max(0, demandQuantity - principalInventoryQuantity)
  const totalNeed = Math.max(0, principalShortage - pending)
  const exactTransferQuantity = Math.min(totalNeed, satelliteInventoryQuantity)
  const suggestedPallets = selectPallets(satellitePallets, exactTransferQuantity)
  const transferSuggestionQuantity = suggestedPallets.reduce((sum, pallet) => sum + pallet.quantity, 0)
  const palletRoundUpSurplusQuantity = Math.max(0, transferSuggestionQuantity - totalNeed)
  const remainingShortageAfterTransfer = Math.max(0, totalNeed - transferSuggestionQuantity)
  const recommendationState = resolveTransferRecommendationState(
    totalNeed,
    satelliteInventoryQuantity,
    remainingShortageAfterTransfer,
  )

  return {
    calculatedNeedQuantity: totalNeed,
    transferSuggestionQuantity,
    newTransferSuggestionQuantity: transferSuggestionQuantity,
    palletRoundUpSurplusQuantity,
    remainingShortageAfterTransfer,
    recommendationState,
    suggestedPallets,
  }
}

function selectPallets(
  satellitePallets: PalletInventoryDetail[],
  requestedQuantity: number,
): PalletInventoryDetail[] {
  if (requestedQuantity <= 0 || satellitePallets.length === 0) {
    return []
  }

  let remaining = requestedQuantity
  const selected: PalletInventoryDetail[] = []
  for (const pallet of [...satellitePallets].sort((a, b) => {
    const date = compareOptionalDateThenText(a.validationDate, b.validationDate)
    if (date !== 0) {
      return date
    }
    return a.pallet.localeCompare(b.pallet) || a.storageZone.localeCompare(b.storageZone)
  })) {
    if (remaining <= 0) {
      break
    }
    if (pallet.quantity > 0) {
      selected.push(pallet)
      remaining -= pallet.quantity
    }
  }

  return selected
}

function resolveTransferRecommendationState(
  totalNeed: number,
  satelliteInventoryQuantity: number,
  remainingShortageAfterTransfer: number,
): TransferRecommendationState {
  if (totalNeed <= 0) {
    return 'NotRequired'
  }
  if (satelliteInventoryQuantity <= 0) {
    return 'Unavailable'
  }
  return remainingShortageAfterTransfer <= 0 ? 'Suggested' : 'Partial'
}

export function analyzeWeeklyForecast(args: {
  inventory: InventoryPosition[]
  forecast: ForecastEntry[]
  selectedPeriod: string
  horizonPeriods: number
  thresholds: CoverageThresholds
  satelliteZones?: string[]
  pendingTransitQuantities?: Map<string, number>
  ignoredStorageZones?: string[]
  unavailablePalletKeys?: Set<string>
  availablePeriods?: string[]
  descriptions?: Record<string, string>
}): WeeklyAnalysisResult {
  const unavailable = args.unavailablePalletKeys ?? new Set<string>()
  const availableInventory =
    unavailable.size === 0
      ? args.inventory
      : args.inventory.filter(
          (position) => !unavailable.has(buildArticlePalletKey(position.article, position.pallet)),
        )

  const futurePeriods = [
    ...new Set((args.availablePeriods ?? args.forecast.map((entry) => entry.period)).filter(Boolean)),
  ]
    .filter((period) => period >= args.selectedPeriod)
    .sort()
  const horizon = futurePeriods.slice(0, args.horizonPeriods)
  const horizonSet = new Set(horizon)
  const horizonEndPeriod = horizon[horizon.length - 1] ?? args.selectedPeriod

  const inventoryGroups = summarizeInventory(
    availableInventory,
    args.satelliteZones,
    args.ignoredStorageZones,
  )
  const forecastGroups = groupForecast(args.forecast.filter((entry) => horizonSet.has(entry.period)))
  const articleKeys = new Set([...inventoryGroups.keys(), ...forecastGroups.keys()])

  const articles: ArticleAnalysisResult[] = [...articleKeys]
    .sort((a, b) => a.localeCompare(b))
    .map((key) => {
      const inventoryInfo = inventoryGroups.get(key)
      const forecastInfo = forecastGroups.get(key)
      const article = inventoryInfo?.article ?? forecastInfo?.article ?? key
      const forecastQuantity = forecastInfo?.quantity ?? 0
      const principalInventoryQuantity = inventoryInfo?.principalQuantity ?? 0
      const satelliteInventoryQuantity = inventoryInfo?.satelliteQuantity ?? 0
      const totalInventoryQuantity = inventoryInfo?.totalQuantity ?? 0
      const difference = principalInventoryQuantity - forecastQuantity
      const satelliteDifference = satelliteInventoryQuantity - forecastQuantity
      const pendingTransitQuantity = args.pendingTransitQuantities?.get(key) ?? 0
      const transferPlan = planTransfer(
        forecastQuantity,
        principalInventoryQuantity,
        satelliteInventoryQuantity,
        pendingTransitQuantity,
        inventoryInfo?.satellitePallets ?? [],
      )

      return {
        article,
        description: args.descriptions?.[article] ?? args.descriptions?.[key] ?? '',
        principalInventoryQuantity,
        totalInventoryQuantity,
        forecastQuantity,
        difference,
        status: resolveStatus(principalInventoryQuantity, forecastQuantity, args.thresholds),
        zones: inventoryInfo?.zones ?? [],
        satelliteInventoryQuantity,
        satelliteDifference,
        satelliteStatus: resolveStatus(satelliteInventoryQuantity, forecastQuantity, args.thresholds),
        transferSuggestionQuantity: transferPlan.transferSuggestionQuantity,
        calculatedTransferNeedQuantity: transferPlan.calculatedNeedQuantity,
        palletRoundUpSurplusQuantity: transferPlan.palletRoundUpSurplusQuantity,
        remainingShortageAfterTransfer: transferPlan.remainingShortageAfterTransfer,
        transferRecommendationState: transferPlan.recommendationState,
        satelliteZones: inventoryInfo?.satelliteZones ?? [],
        pendingTransitQuantity,
        newTransferSuggestionQuantity: transferPlan.newTransferSuggestionQuantity,
        suggestedPallets: transferPlan.suggestedPallets,
        availablePallets: inventoryInfo?.satellitePallets ?? [],
        coveragePeriods: computeCoveragePeriods(principalInventoryQuantity, forecastInfo?.byPeriod, horizon),
      }
    })
    .sort(
      (a, b) =>
        statusOrder(a.status) - statusOrder(b.status) ||
        statusOrder(a.satelliteStatus) - statusOrder(b.satelliteStatus) ||
        a.article.localeCompare(b.article),
    )

  return {
    selectedPeriod: args.selectedPeriod,
    horizonEndPeriod,
    requestedPeriods: args.horizonPeriods,
    analyzedPeriods: horizon.length,
    isIncompleteHorizon: horizon.length < args.horizonPeriods,
    articles,
    criticalCount: articles.filter((article) => article.status === 'Critical').length,
    warningCount: articles.filter((article) => article.status === 'Warning').length,
    healthyCount: articles.filter((article) => article.status === 'Healthy').length,
    satelliteCriticalCount: articles.filter((article) => article.satelliteStatus === 'Critical').length,
    satelliteWarningCount: articles.filter((article) => article.satelliteStatus === 'Warning').length,
    satelliteHealthyCount: articles.filter((article) => article.satelliteStatus === 'Healthy').length,
  }
}

export function analyzeExpediciones(args: {
  lines: ExpeditionLine[]
  inventory: InventoryPosition[]
  satelliteZones?: string[]
  pendingTransitQuantities?: Map<string, number>
  ignoredStorageZones?: string[]
  unavailablePalletKeys?: Set<string>
}): ExpedicionesAnalysisResult {
  const unavailable = args.unavailablePalletKeys ?? new Set<string>()
  const availableInventory =
    unavailable.size === 0
      ? args.inventory
      : args.inventory.filter(
          (position) => !unavailable.has(buildArticlePalletKey(position.article, position.pallet)),
        )
  const summaries = summarizeInventory(availableInventory, args.satelliteZones, args.ignoredStorageZones)
  const lineGroups = new Map<string, ExpeditionLine[]>()

  for (const line of args.lines) {
    if (!line.article.trim()) {
      continue
    }
    const key = normalizeArticleKey(line.article).toUpperCase()
    const group = lineGroups.get(key) ?? []
    group.push(line)
    lineGroups.set(key, group)
  }

  const lines: ExpeditionLineResult[] = [...lineGroups.entries()].map(([key, group]) => {
    const summary = summaries.get(key)
    const firstLine = group[0]
    const demandQuantity = group.reduce((sum, line) => sum + line.quantity, 0)
    const description = group.find((line) => line.description.trim())?.description ?? ''
    const expeditionNumber = [
      ...new Set(group.map((line) => line.expeditionNumber.trim()).filter(Boolean)),
    ].join('; ')
    const principal = summary?.principalQuantity ?? 0
    const satellite = summary?.satelliteQuantity ?? 0
    const pendingTransitQuantity = args.pendingTransitQuantities?.get(key) ?? 0
    const transferPlan = planTransfer(
      demandQuantity,
      principal,
      satellite,
      pendingTransitQuantity,
      summary?.satellitePallets ?? [],
    )

    return {
      article: firstLine.article,
      description,
      expeditionNumber,
      demandQuantity,
      principalInventoryQuantity: principal,
      satelliteInventoryQuantity: satellite,
      transferSuggestionQuantity: transferPlan.transferSuggestionQuantity,
      calculatedTransferNeedQuantity: transferPlan.calculatedNeedQuantity,
      palletRoundUpSurplusQuantity: transferPlan.palletRoundUpSurplusQuantity,
      remainingShortageAfterTransfer: transferPlan.remainingShortageAfterTransfer,
      transferRecommendationState: transferPlan.recommendationState,
      satelliteZones: summary?.satelliteZones ?? [],
      pendingTransitQuantity,
      newTransferSuggestionQuantity: transferPlan.newTransferSuggestionQuantity,
      suggestedPallets: transferPlan.suggestedPallets,
      availablePallets: summary?.satellitePallets ?? [],
    }
  })

  return {
    lines,
    evaluated: lines.filter((line) => line.demandQuantity > 0 || line.pendingTransitQuantity > 0).length,
    covered: lines.filter(
      (line) =>
        line.demandQuantity > 0 &&
        line.calculatedTransferNeedQuantity <= 0 &&
        line.remainingShortageAfterTransfer <= 0,
    ).length,
    toTransfer: lines.filter((line) => line.transferSuggestionQuantity > 0).length,
    withoutExternal: lines.filter(
      (line) =>
        line.remainingShortageAfterTransfer > 0 &&
        line.transferSuggestionQuantity <= 0 &&
        line.satelliteInventoryQuantity <= 0,
    ).length,
  }
}

function groupForecast(entries: ForecastEntry[]): Map<
  string,
  { article: string; quantity: number; byPeriod: Map<string, number> }
> {
  const groups = new Map<string, { article: string; quantity: number; byPeriod: Map<string, number> }>()
  for (const entry of entries) {
    if (!entry.article.trim()) {
      continue
    }
    const key = normalizeArticleKey(entry.article).toUpperCase()
    const existing = groups.get(key)
    if (existing) {
      existing.quantity += entry.quantity
      existing.byPeriod.set(entry.period, (existing.byPeriod.get(entry.period) ?? 0) + entry.quantity)
    } else {
      const byPeriod = new Map<string, number>()
      byPeriod.set(entry.period, entry.quantity)
      groups.set(key, { article: entry.article.trim(), quantity: entry.quantity, byPeriod })
    }
  }
  return groups
}

function computeCoveragePeriods(
  principalInventoryQuantity: number,
  periodDemands: Map<string, number> | undefined,
  horizonPeriods: string[],
): number | undefined {
  if (horizonPeriods.length === 0 || !periodDemands) {
    return undefined
  }

  const totalDemand = [...periodDemands.values()].reduce((sum, value) => sum + value, 0)
  if (totalDemand <= 0) {
    return undefined
  }

  let remaining = principalInventoryQuantity
  let covered = 0
  for (const period of horizonPeriods) {
    const demand = periodDemands.get(period) ?? 0
    if (demand <= 0) {
      covered += 1
      continue
    }
    if (remaining < demand) {
      break
    }
    covered += 1
    remaining -= demand
  }

  return covered
}

function resolveStatus(
  inventoryQuantity: number,
  forecastQuantity: number,
  thresholds: CoverageThresholds,
): CoverageStatus {
  if (forecastQuantity <= 0) {
    return 'Healthy'
  }
  if (inventoryQuantity <= 0) {
    return 'Critical'
  }
  if (inventoryQuantity < forecastQuantity * (thresholds.redPercent / 100)) {
    return 'Critical'
  }
  return inventoryQuantity >= forecastQuantity * (thresholds.healthyPercent / 100)
    ? 'Healthy'
    : 'Warning'
}

function statusOrder(status: CoverageStatus): number {
  return status === 'Critical' ? 0 : status === 'Warning' ? 1 : 2
}

export function pendingTransitQuantities(items: TransitItem[]): Map<string, number> {
  const result = new Map<string, number>()
  for (const item of items.filter((entry) => !entry.receivedAt && !entry.reconciledAt && entry.quantity > 0)) {
    const key = normalizeArticleKey(item.article).toUpperCase()
    result.set(key, (result.get(key) ?? 0) + item.quantity)
  }
  return result
}

export function outstandingPalletKeys(items: TransitItem[]): Set<string> {
  return new Set(
    items
      .filter((entry) => !entry.receivedAt && !entry.reconciledAt)
      .map((entry) => buildArticlePalletKey(entry.article, entry.pallet)),
  )
}
