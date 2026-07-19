import type { Asset, AssetCategory } from '@/features/assets/asset-contract'

export function categoryLabel(
  category: AssetCategory | undefined,
  assetCategory: Asset['assetCategory'],
) {
  return category?.displayName ?? assetCategory
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
