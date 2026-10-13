import type { Asset, AssetCategory } from '@/features/assets/asset-contract'

const assetCategoryNames: Record<string, string> = {
  'fire-extinguisher': 'Fire extinguishers',
  'fire-alarm': 'Fire alarm systems',
  'emergency-light': 'Emergency lights',
  'water-drinking-station': 'Water drinking stations',
}

export function categoryLabel(
  category: AssetCategory | undefined,
  assetCategory: Asset['assetCategory'],
) {
  return category?.displayName ?? formatAssetCategory(assetCategory)
}

export function formatAssetCategory(assetCategory: string) {
  return (
    assetCategoryNames[assetCategory] ??
    assetCategory
      .split('-')
      .map((part) => `${part.slice(0, 1).toUpperCase()}${part.slice(1)}`)
      .join(' ')
  )
}

export function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function assetSearchText(asset: Asset) {
  return [
    asset.assetCode,
    asset.qrCodeValue,
    asset.building,
    asset.department,
    asset.location,
  ]
    .filter(Boolean)
    .join(' ')
    .toLocaleLowerCase()
}
