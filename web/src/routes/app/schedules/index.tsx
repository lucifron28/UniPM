import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  scheduleQuarterCodes,
  scheduleStatusCodes,
} from '@/features/schedules/schedule-contract'
import {
  ScheduleRegistry,
  type ScheduleSearch,
} from '@/features/schedules/schedule-registry'

const dateTime = z.string().datetime({ offset: true })
const searchSchema = z
  .object({
    assetId: z.string().uuid().optional(),
    status: z.enum(scheduleStatusCodes).optional(),
    from: dateTime.optional(),
    to: dateTime.optional(),
    quarter: z.enum(scheduleQuarterCodes).optional(),
    year: z.coerce
      .number()
      .int()
      .min(2000)
      .max(new Date().getUTCFullYear() + 5)
      .optional(),
    page: z.coerce.number().int().positive().max(10000).optional(),
  })
  .refine((value) => !value.from || !value.to || value.from <= value.to)

export const Route = createFileRoute('/app/schedules/')({
  validateSearch: (search): ScheduleSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: SchedulesPage,
})

function SchedulesPage() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  return (
    <ScheduleRegistry
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
