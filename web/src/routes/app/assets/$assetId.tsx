import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { AssetDetail } from '@/features/assets/asset-detail'
import {
  parseDetailReturnContext,
  type DetailReturnContext,
} from '@/features/shared/detail-navigation'
import type { AssetSearch } from '@/features/assets/asset-registry'
import {
  assetCategoryCodes,
  assetStatusCodes,
} from '@/features/assets/asset-contract'

const searchSchema = z.object({
  assetCategory: z.enum(assetCategoryCodes).optional(),
  status: z.enum(assetStatusCodes).optional(),
  building: z.string().trim().max(256).optional(),
  department: z.string().trim().max(256).optional(),
  text: z.string().trim().max(256).optional(),
  page: z.coerce.number().int().positive().max(10000).optional(),
})

const routeSearchSchema = searchSchema.extend({
  returnContext: z.unknown().optional(),
})

type AssetDetailSearch = AssetSearch & {
  returnContext?: DetailReturnContext | undefined
}

export const Route = createFileRoute('/app/assets/$assetId')({
  validateSearch: (search): AssetDetailSearch => {
    const parsed = routeSearchSchema.safeParse(search)
    return parsed.success
      ? {
          ...parsed.data,
          returnContext: parseDetailReturnContext(parsed.data.returnContext),
        }
      : {}
  },
  component: AssetDetailPage,
})

function AssetDetailPage() {
  const { assetId } = Route.useParams()
  const { returnContext, ...registrySearch } = Route.useSearch()
  return (
    <AssetDetail
      assetId={assetId}
      registrySearch={registrySearch}
      returnContext={returnContext}
    />
  )
}
