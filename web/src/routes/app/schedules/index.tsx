import { createFileRoute, Link } from '@tanstack/react-router'
import { z } from 'zod'
import { PageHeading } from '@/components/layout/page-heading'
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
import { ScheduleOngoingWork } from '@/features/schedules/schedule-ongoing-work'

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
    view: z.enum(['assignments', 'tracking', 'history']).optional(),
    progress: z.enum(['not-started', 'in-progress']).optional(),
    overdueOnly: z.boolean().optional(),
    building: z.string().trim().max(256).optional(),
    inspectorUserId: z.string().uuid().optional(),
    supervisorUserId: z.string().uuid().optional(),
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
  const scheduleView = search.view ?? 'assignments'
  const showAssignments =
    assignmentRole !== null && scheduleView === 'assignments'
  const showTracking = assignmentRole !== null && scheduleView === 'tracking'
  const showPlanningOrTracking = showAssignments || showTracking

  return (
    <div className="space-y-5">
      {showPlanningOrTracking && (
        <PageHeading
          id="schedules-title"
          eyebrow="Preventive maintenance"
          title="Schedules"
          description="Review current PM batches and assignments."
        />
      )}
      {assignmentRole && (
        <nav aria-label="Schedule views" className="flex flex-wrap gap-2">
          <Button
            asChild
            variant={scheduleView === 'history' ? 'primary' : 'secondary'}
          >
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
          <Button asChild variant={showTracking ? 'primary' : 'secondary'}>
            <Link
              to="/app/schedules"
              search={{ ...search, view: 'tracking', page: undefined }}
            >
              Work tracking
            </Link>
          </Button>
        </nav>
      )}
      <div
        id={
          showAssignments
            ? 'schedule-assignments'
            : showTracking
              ? 'schedule-tracking'
              : 'schedule-history'
        }
      >
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
        ) : showTracking ? (
          <ScheduleOngoingWork
            search={search}
            role={assignmentRole === 'Supervisor' ? 'Supervisor' : 'GSD'}
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
    </div>
  )
}
