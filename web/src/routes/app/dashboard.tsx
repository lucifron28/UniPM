import { createFileRoute, Link, redirect } from '@tanstack/react-router'
import { z } from 'zod'
import { PageHeading } from '@/components/layout/page-heading'
import { Button } from '@/components/ui/button'
import { useCurrentUser } from '@/features/auth/current-user'
import { canReadOperationalData } from '@/features/auth/app-route-access'
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
  timeliness: z
    .enum(['OnTime', 'Late', 'Scheduled', 'Pending', 'NotCompleted'])
    .optional(),
  search: z.string().trim().max(256).optional(),
})

export const Route = createFileRoute('/app/dashboard')({
  beforeLoad: ({ location }) => {
    if (new URLSearchParams(location.searchStr).get('view') === 'question') {
      const dashboardSearch = searchSchema.safeParse(
        Object.fromEntries(new URLSearchParams(location.searchStr)),
      )
      throw redirect({
        to: '/app/analytics',
        search: {
          returnContext: {
            kind: 'dashboard',
            search: dashboardSearch.success ? dashboardSearch.data : {},
          },
        },
        replace: true,
      })
    }
  },
  validateSearch: (search): PmPeriodDashboardSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: DashboardPage,
})

function DashboardPage() {
  const currentUser = useCurrentUser()
  const roles = currentUser.data?.roles ?? []
  if (
    currentUser.isSuccess &&
    roles.includes('Admin') &&
    !canReadOperationalData(roles)
  ) {
    return (
      <section className="max-w-3xl space-y-4">
        <PageHeading
          id="admin-landing-title"
          eyebrow="UniPM administration"
          title="Technical administration"
          description="Administrative tools are available here. Preventive-maintenance records are reserved for operational roles."
        />
        <Button asChild>
          <Link to="/app/admin">Open admin management</Link>
        </Button>
      </section>
    )
  }
  return <PreventiveMaintenanceDashboard />
}

export function PreventiveMaintenanceDashboard() {
  const search: PmPeriodDashboardSearch = Route.useSearch()
  const navigate = Route.useNavigate()
  const onSearchChange = (
    next: PmPeriodDashboardSearch,
    options?: { replace?: boolean },
  ) =>
    void navigate({
      search: next,
      ...(options?.replace ? { replace: true } : {}),
    })

  return (
    <PmPeriodDashboard
      key={`${search.assetCategory ?? ''}|${search.year ?? ''}|${search.pmCycle ?? ''}`}
      search={search}
      onSearchChange={onSearchChange}
    />
  )
}
