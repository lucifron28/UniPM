import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  PmPeriodDashboard,
  type PmPeriodDashboardSearch,
} from '@/features/reports/pm-period-dashboard'

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
  timeliness: z.enum(['OnTime', 'Late', 'NotCompleted']).optional(),
  search: z.string().trim().max(256).optional(),
})

export const Route = createFileRoute('/app/dashboard')({
  validateSearch: (search): PmPeriodDashboardSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: DashboardPage,
})

function DashboardPage() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  return (
    <PmPeriodDashboard
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

export const PreventiveMaintenanceDashboard = DashboardPage
