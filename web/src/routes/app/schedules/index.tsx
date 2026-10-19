import { createFileRoute, Link } from '@tanstack/react-router'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  scheduleQuarterCodes,
  scheduleStatusCodes,
} from '@/features/schedules/schedule-contract'
import { assetCategoryCodes } from '@/features/assets/asset-contract'
import {
  ScheduleRegistry,
  type ScheduleSearch,
} from '@/features/schedules/schedule-registry'
import { ScheduleAssignmentQueue } from '@/features/schedules/schedule-assignment-queue'

const dateTime = z.string().datetime({ offset: true })
const searchSchema = z
  .object({
    assetId: z.string().uuid().optional(),
    assetCategory: z.enum(assetCategoryCodes).optional(),
    department: z.string().trim().max(256).optional(),
    search: z.string().trim().max(256).optional(),
    status: z.enum(scheduleStatusCodes).optional(),
    from: dateTime.optional(),
    to: dateTime.optional(),
    quarter: z.enum(scheduleQuarterCodes).optional(),
    pmCycle: z
      .string()
      .regex(/^(?!0000)\d{4}-(0[1-9]|1[0-2])$/)
      .optional(),
    year: z.coerce
      .number()
      .int()
      .min(2000)
      .max(new Date().getUTCFullYear() + 5)
      .optional(),
    view: z.enum(['assignments', 'history']).optional(),
    assignmentFilter: z
      .enum(['needs-supervisor', 'needs-inspector'])
      .optional(),
    page: z.coerce.number().int().positive().max(10000).optional(),
  })
  .refine((value) => !value.from || !value.to || value.from <= value.to)
  .refine(
    (value) =>
      !value.pmCycle ||
      ((!value.year || Number(value.pmCycle.slice(0, 4)) === value.year) &&
        (!value.quarter ||
          `Q${Math.floor((Number(value.pmCycle.slice(5, 7)) - 1) / 3) + 1}` ===
            value.quarter)),
  )

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
  const currentUser = useCurrentUser()
  const roles = currentUser.data?.roles ?? []
  const assignmentRole = roles.includes('GSD')
    ? 'GSD'
    : roles.includes('Supervisor')
      ? 'Supervisor'
      : null
  const showAssignments = assignmentRole !== null && search.view !== 'history'

  return (
    <div className="space-y-5">
      {assignmentRole && (
        <nav aria-label="Schedule views" className="flex flex-wrap gap-2">
          <Button asChild variant={showAssignments ? 'secondary' : 'primary'}>
            <Link
              to="/app/schedules"
              search={{ ...search, view: 'history', page: undefined }}
            >
              Schedule history
            </Link>
          </Button>
          <Button asChild variant={showAssignments ? 'primary' : 'secondary'}>
            <Link
              to="/app/schedules"
              search={{ ...search, view: 'assignments', page: undefined }}
            >
              Planning &amp; assignment
            </Link>
          </Button>
        </nav>
      )}
      {showAssignments ? (
        <ScheduleAssignmentQueue
          role={assignmentRole === 'Supervisor' ? 'Supervisor' : 'GSD'}
          search={search}
          onSearchChange={(next) =>
            void navigate({
              search: {
                ...next,
                ...(search.view ? { view: search.view } : {}),
              },
            })
          }
        />
      ) : (
        <ScheduleRegistry
          search={search}
          onSearchChange={(next, options) =>
            void navigate({
              search: {
                ...next,
                ...(search.view ? { view: search.view } : {}),
              },
              ...(options?.replace ? { replace: true } : {}),
              ...(options?.preserveScroll ? { resetScroll: false } : {}),
            })
          }
        />
      )}
    </div>
  )
}
