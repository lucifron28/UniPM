import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import {
  getAssetRegistryOptions,
  getAssetRegistrySummary,
  getGetAssetRegistryOptionsQueryKey,
  getGetAssetRegistrySummaryQueryKey,
  getListAssetRegistryQueryKey,
  getGetAssetQueryKey,
  getListAssetCategoriesQueryKey,
  getListAssetsQueryKey,
  listAssetRegistry,
  getAsset,
  listAssetCategories,
  listAssets,
} from '@/api/generated/endpoints'
import type {
  GetAssetRegistryOptionsParams,
  ListAssetRegistryParams,
  ListAssetsParams,
} from '@/api/generated/models'
import {
  parseAssetRegistryOptions,
  parseAssetRegistryPage,
  parseAssetRegistrySummary,
  parseAsset,
  parseAssetCategories,
  parseAssets,
} from '@/features/assets/asset-contract'

export type AssetServerFilters = Pick<
  ListAssetsParams,
  'assetCategory' | 'status' | 'building' | 'department' | 'search' | 'limit'
>

export type AssetRegistryFilters = Pick<
  ListAssetRegistryParams,
  | 'assetCategory'
  | 'status'
  | 'building'
  | 'department'
  | 'search'
  | 'page'
  | 'pageSize'
  | 'hasDepartment'
>

export function useAssets(filters: AssetServerFilters = {}) {
  return useQuery({
    queryKey: getListAssetsQueryKey(filters),
    queryFn: ({ signal }) => listAssets(filters, signal).then(parseAssets),
    placeholderData: (previousData) => previousData,
  })
}

export function useAssetRegistry(filters: AssetRegistryFilters = {}) {
  return useQuery({
    queryKey: getListAssetRegistryQueryKey(filters),
    queryFn: ({ signal }) =>
      listAssetRegistry(filters, signal).then(parseAssetRegistryPage),
    placeholderData: (previousData) => previousData,
  })
}

export function useAssetRegistrySearch(
  filters: Omit<AssetRegistryFilters, 'page' | 'pageSize'> = {},
  enabled = true,
) {
  const params = { ...filters, pageSize: 20 }
  return useInfiniteQuery({
    queryKey: getListAssetRegistryQueryKey(params),
    queryFn: ({ pageParam, signal }) =>
      listAssetRegistry({ ...params, page: pageParam }, signal).then(
        parseAssetRegistryPage,
      ),
    initialPageParam: 1,
    getNextPageParam: (page) =>
      page.page * page.pageSize < page.totalCount ? page.page + 1 : undefined,
    enabled,
  })
}

export function useAssetRegistryOptions(
  filters: GetAssetRegistryOptionsParams = {},
) {
  return useQuery({
    queryKey: getGetAssetRegistryOptionsQueryKey(filters),
    queryFn: ({ signal }) =>
      getAssetRegistryOptions(filters, signal).then(parseAssetRegistryOptions),
  })
}

export function useAssetRegistrySummary() {
  return useQuery({
    queryKey: getGetAssetRegistrySummaryQueryKey(),
    queryFn: ({ signal }) =>
      getAssetRegistrySummary(signal).then(parseAssetRegistrySummary),
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
