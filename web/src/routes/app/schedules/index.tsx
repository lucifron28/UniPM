import { createFileRoute, Link } from '@tanstack/react-router'
import { PageHeading } from '@/components/layout/page-heading'
import { Button } from '@/components/ui/button'
import { useCurrentUser } from '@/features/auth/current-user'
import { scheduleSearchSchema } from '@/features/schedules/schedule-search'
import {
  ScheduleRegistry,
  type ScheduleSearch,
} from '@/features/schedules/schedule-registry'
import { ScheduleAssignmentQueue } from '@/features/schedules/schedule-assignment-queue'
import { ScheduleOngoingWork } from '@/features/schedules/schedule-ongoing-work'

export const Route = createFileRoute('/app/schedules/')({
  validateSearch: (search): ScheduleSearch => {
    const parsed = scheduleSearchSchema.safeParse(search)
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

  return (
    <div className="space-y-5">
      <PageHeading
        id="schedules-title"
        eyebrow="Preventive maintenance"
        title="Schedules"
        description="Review current PM batches and assignments."
      />
      {assignmentRole && (
        <nav aria-label="Schedule views" className="flex flex-wrap gap-2">
          <Button
            asChild
            variant={scheduleView === 'history' ? 'primary' : 'secondary'}
          >
            <Link
              to="/app/schedules"
              search={{ ...search, view: 'history', page: undefined }}
              aria-current={scheduleView === 'history' ? 'page' : undefined}
            >
              Schedule history
            </Link>
          </Button>
          <Button asChild variant={showAssignments ? 'primary' : 'secondary'}>
            <Link
              to="/app/schedules"
              search={{ ...search, view: 'assignments', page: undefined }}
              aria-current={scheduleView === 'assignments' ? 'page' : undefined}
            >
              Planning &amp; assignment
            </Link>
          </Button>
          <Button asChild variant={showTracking ? 'primary' : 'secondary'}>
            <Link
              to="/app/schedules"
              search={{ ...search, view: 'tracking', page: undefined }}
              aria-current={scheduleView === 'tracking' ? 'page' : undefined}
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
                  view: scheduleView,
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
                  view: scheduleView,
                },
              })
            }
          />
        ) : (
          <ScheduleRegistry
            embedded
            search={search}
            onSearchChange={(next, options) =>
              void navigate({
                search: {
                  ...next,
                  view: scheduleView,
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
