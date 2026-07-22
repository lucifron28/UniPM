import { useQuery } from '@tanstack/react-query'
import {
  getGetAssetQueryKey,
  getListAssetCategoriesQueryKey,
  getListAssetsQueryKey,
  getAsset,
  listAssetCategories,
  listAssets,
} from '@/api/generated/endpoints'
import type { ListAssetsParams } from '@/api/generated/models'
import {
  parseAsset,
  parseAssetCategories,
  parseAssets,
} from '@/features/assets/asset-contract'

export type AssetServerFilters = Pick<
  ListAssetsParams,
  'assetCategory' | 'status' | 'building' | 'department'
>

export function useAssets(filters: AssetServerFilters = {}) {
  return useQuery({
    queryKey: getListAssetsQueryKey(filters),
    queryFn: ({ signal }) => listAssets(filters, signal).then(parseAssets),
  })
}

export function useAssetCategories() {
  return useQuery({
    queryKey: getListAssetCategoriesQueryKey(),
    queryFn: ({ signal }) =>
      listAssetCategories(signal).then(parseAssetCategories),
  })
}

export function useAsset(assetId: string, enabled = true) {
  return useQuery({
    queryKey: getGetAssetQueryKey(assetId),
    queryFn: ({ signal }) => getAsset(assetId, signal).then(parseAsset),
    enabled,
  })
}
