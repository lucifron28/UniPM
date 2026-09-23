import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { ScheduleDetail } from '@/features/schedules/schedule-detail'
import type { ScheduleSearch } from '@/features/schedules/schedule-registry'
import {
  scheduleQuarterCodes,
  scheduleStatusCodes,
} from '@/features/schedules/schedule-contract'

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

export const Route = createFileRoute('/app/schedules/$scheduleId')({
  validateSearch: (search): ScheduleSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: ScheduleDetailPage,
})

function ScheduleDetailPage() {
  const { scheduleId } = Route.useParams()
  const registrySearch = Route.useSearch()
  return (
    <ScheduleDetail scheduleId={scheduleId} registrySearch={registrySearch} />
  )
}
