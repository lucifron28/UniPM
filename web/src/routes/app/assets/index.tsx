import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  AssetRegistry,
  type AssetSearch,
} from '@/features/assets/asset-registry'
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

export const Route = createFileRoute('/app/assets/')({
  validateSearch: (search): AssetSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: AssetsPage,
})

function AssetsPage() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  return (
    <AssetRegistry
      search={search}
      onSearchChange={(next) => void navigate({ search: next })}
    />
  )
}
