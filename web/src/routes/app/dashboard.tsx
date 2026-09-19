import { createFileRoute, Link } from '@tanstack/react-router'
import {
  Boxes,
  CalendarDays,
  ClipboardCheck,
  ClipboardList,
} from 'lucide-react'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { useCurrentUser } from '@/features/auth/current-user'

export const Route = createFileRoute('/app/dashboard')({
  component: PreventiveMaintenanceDashboard,
})

const workspaceLinks = [
  {
    to: '/app/assets' as const,
    title: 'Assets',
    description: 'Review registered equipment and QR identifiers.',
    icon: Boxes,
  },
  {
    to: '/app/schedules' as const,
    title: 'Schedules',
    description: 'Review preventive-maintenance dates and schedule status.',
    icon: CalendarDays,
  },
  {
    to: '/app/inspections' as const,
    title: 'Official history',
    description: 'Review acknowledged inspection records by asset.',
    icon: ClipboardCheck,
  },
]

export function PreventiveMaintenanceDashboard() {
  const currentUser = useCurrentUser()
  const canReviewForms =
    currentUser.data?.roles.some(
      (role) => role === 'GSD' || role === 'Inspector',
    ) ?? false

  return (
    <section aria-labelledby="dashboard-title" className="max-w-6xl">
      <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
        Validation prototype
      </p>
      <h1
        id="dashboard-title"
        className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)] sm:text-4xl"
      >
        Preventive Maintenance Portal
      </h1>
      <p className="mt-2 max-w-2xl text-[var(--text-secondary)]">
        Manage the confirmed preventive-maintenance workflow for assets,
        schedules, multi-row inspection forms, acknowledgement, and official
        history.
      </p>

      <div className="mt-8 grid gap-4 md:grid-cols-2">
        {workspaceLinks.map(({ to, title, description, icon: Icon }) => (
          <Link
            key={to}
            to={to}
            className="group rounded-xl focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
          >
            <Card className="h-full p-6 shadow-none transition-colors group-hover:border-[var(--primary)]">
              <Icon
                aria-hidden="true"
                className="size-6 text-[var(--primary)]"
              />
              <h2 className="mt-4 text-lg font-semibold text-[var(--text-primary)]">
                {title}
              </h2>
              <p className="mt-2 text-sm leading-6 text-[var(--text-secondary)]">
                {description}
              </p>
            </Card>
          </Link>
        ))}
        {canReviewForms && (
          <Link
            to="/app/preventive-maintenance-forms"
            className="group rounded-xl focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none"
          >
            <Card className="h-full p-6 shadow-none transition-colors group-hover:border-[var(--primary)]">
              <ClipboardList
                aria-hidden="true"
                className="size-6 text-[var(--primary)]"
              />
              <h2 className="mt-4 text-lg font-semibold text-[var(--text-primary)]">
                Form review
              </h2>
              <p className="mt-2 text-sm leading-6 text-[var(--text-secondary)]">
                Review form rows, acknowledge submitted forms, and inspect
                corrective-action handoff details.
              </p>
            </Card>
          </Link>
        )}
      </div>

      <Card className="mt-8 p-6 shadow-none">
        <h2 className="text-lg font-semibold text-[var(--text-primary)]">
          Form lifecycle
        </h2>
        <div
          className="mt-4 flex flex-wrap items-center gap-2"
          aria-label="Preventive-maintenance form lifecycle"
        >
          <Badge>Draft</Badge>
          <span aria-hidden="true" className="text-[var(--text-neutral)]">
            to
          </span>
          <Badge>Awaiting acknowledgement</Badge>
          <span aria-hidden="true" className="text-[var(--text-neutral)]">
            to
          </span>
          <Badge>Acknowledged</Badge>
        </div>
        <p className="mt-4 max-w-3xl text-sm leading-6 text-[var(--text-secondary)]">
          Field-work completion and acknowledgement are separate.
          Acknowledgement records receipt/noting, locks the form, and makes its
          inspection rows eligible for official history. It does not approve
          corrective work, funding, or an RMRF. Provisional UniPM file numbers
          remain independent from the external GSD work management system.
        </p>
      </Card>
    </section>
  )
}
