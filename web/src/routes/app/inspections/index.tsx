import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  InspectionRegistry,
  type InspectionSearch,
} from '@/features/inspections/inspection-registry'
import { assetCategoryCodes } from '@/features/assets/asset-contract'
import { inspectionFollowUpStatusCodes } from '@/features/inspections/inspection-contract'

const dateTime = z.string().datetime({ offset: true })
const operationalResult = z.union([
  z.literal(true),
  z.literal(false),
  z.literal('true').transform(() => true),
  z.literal('false').transform(() => false),
])

export const inspectionSearchSchema = z
  .object({
    assetId: z.string().uuid().optional(),
    scheduleId: z.string().uuid().optional(),
    assetCategory: z.enum(assetCategoryCodes).optional(),
    department: z.string().trim().max(256).optional(),
    search: z.string().trim().max(256).optional(),
    isOperational: operationalResult.optional(),
    wmsReferralStatus: z.enum(inspectionFollowUpStatusCodes).optional(),
    dateFrom: dateTime.optional(),
    dateTo: dateTime.optional(),
    page: z.coerce.number().int().positive().max(10000).optional(),
  })
  .refine(
    (value) =>
      !value.dateFrom || !value.dateTo || value.dateFrom <= value.dateTo,
  )

export function parseInspectionSearch(search: unknown): InspectionSearch {
  const parsed = inspectionSearchSchema.safeParse(search)
  return parsed.success ? parsed.data : {}
}

export const Route = createFileRoute('/app/inspections/')({
  validateSearch: parseInspectionSearch,
  component: InspectionsPage,
})

function InspectionsPage() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  return (
    <InspectionRegistry
      search={search}
      onSearchChange={(next, options) =>
        void navigate({
          search: next,
          ...(options?.replace ? { replace: true } : {}),
          ...(options?.preserveScroll ? { resetScroll: false } : {}),
        })
      }
    />
  )
}
