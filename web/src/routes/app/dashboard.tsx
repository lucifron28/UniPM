import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  DashboardHeader,
  DashboardModeSwitch,
  PmPeriodDashboard,
  type PmPeriodDashboardSearch,
} from '@/features/reports/pm-period-dashboard'
import { PmAnalyticsPanel } from '@/features/reports/pm-analytics'
import { useCurrentUser } from '@/features/auth/current-user'

const searchSchema = z.object({
  view: z.enum(['question']).optional(),
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
})

export const Route = createFileRoute('/app/dashboard')({
  validateSearch: (search): PmPeriodDashboardSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: DashboardPage,
})

function DashboardPage() {
  return <PreventiveMaintenanceDashboard />
}

export function PreventiveMaintenanceDashboard() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  const currentUser = useCurrentUser()
  const canAskNla = Boolean(
    currentUser.data?.id && currentUser.data.roles.includes('GSD'),
  )
  const onSearchChange = (
    next: PmPeriodDashboardSearch,
    options?: { replace?: boolean },
  ) =>
    void navigate({
      search: next,
      ...(options?.replace ? { replace: true } : {}),
    })

  if (search.view === 'question' && canAskNla) {
    return (
      <section aria-labelledby="dashboard-title" className="space-y-4">
        <DashboardHeader />
        <DashboardModeSwitch
          view="question"
          canAskNla={canAskNla}
          onChange={(view) =>
            onSearchChange(
              { ...search, view: view === 'question' ? 'question' : undefined },
              { replace: true },
            )
          }
        />
        <PmAnalyticsPanel />
      </section>
    )
  }

  return (
    <PmPeriodDashboard
      key={`${search.assetCategory ?? ''}|${search.year ?? ''}|${search.pmCycle ?? ''}`}
      search={search}
      onSearchChange={onSearchChange}
    />
  )
}
