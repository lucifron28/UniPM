import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  PmPeriodDashboardRecords,
  type PmPeriodDashboardRecordsSearch,
} from '@/features/reports/pm-period-dashboard-records'

const searchSchema = z.object({
  assetCategory: z.string().trim().max(128).optional(),
  year: z.coerce.number().int().positive().optional(),
  pmCycle: z
    .string()
    .regex(/^\d{4}-\d{2}$/)
    .optional(),
  department: z.string().trim().max(256).optional(),
  condition: z
    .enum(['Operational', 'NonOperational', 'NotInspected'])
    .optional(),
  timeliness: z
    .enum(['OnTime', 'Late', 'Scheduled', 'Pending', 'NotCompleted'])
    .optional(),
  search: z.string().trim().max(256).optional(),
  assignment: z
    .enum(['assigned', 'unassigned', 'unavailable', 'overdue'])
    .optional(),
  formStatus: z.enum(['Draft', 'Submitted', 'Acknowledged', 'none']).optional(),
  inspectionStatus: z.enum(['completed', 'outstanding']).optional(),
  assignmentTiming: z
    .enum([
      'WithinPeriod',
      'Overdue',
      'CompletedWithinPeriod',
      'CompletedLate',
      'Unavailable',
    ])
    .optional(),
  page: z.coerce.number().int().positive().max(10000).optional(),
})

export const Route = createFileRoute('/app/dashboard-records')({
  validateSearch: (search): PmPeriodDashboardRecordsSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: DashboardRecordsPage,
})

function DashboardRecordsPage() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  const onSearchChange = (
    next: PmPeriodDashboardRecordsSearch,
    options?: { replace?: boolean },
  ) =>
    void navigate({
      search: next,
      ...(options?.replace ? { replace: true } : {}),
    })

  return (
    <PmPeriodDashboardRecords search={search} onSearchChange={onSearchChange} />
  )
}
