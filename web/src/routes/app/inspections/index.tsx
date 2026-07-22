import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  InspectionRegistry,
  type InspectionSearch,
} from '@/features/inspections/inspection-registry'

const dateTime = z.string().datetime({ offset: true })
const searchSchema = z
  .object({
    assetId: z.string().uuid().optional(),
    scheduleId: z.string().uuid().optional(),
    isOperational: z.coerce.boolean().optional(),
    dateFrom: dateTime.optional(),
    dateTo: dateTime.optional(),
    page: z.coerce.number().int().positive().max(10000).optional(),
  })
  .refine(
    (value) =>
      !value.dateFrom || !value.dateTo || value.dateFrom <= value.dateTo,
  )

export const Route = createFileRoute('/app/inspections/')({
  validateSearch: (search): InspectionSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
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
        })
      }
    />
  )
}
