import { createFileRoute } from '@tanstack/react-router'
import { Card } from '@/components/ui/card'

export const Route = createFileRoute('/app/dashboard')({
  component: DashboardPlaceholder,
})

function DashboardPlaceholder() {
  return (
    <section aria-labelledby="dashboard-title" className="max-w-4xl">
      <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
        Signed-in workspace
      </p>
      <h1
        id="dashboard-title"
        className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)] sm:text-4xl"
      >
        Dashboard
      </h1>
      <p className="mt-2 max-w-2xl text-[var(--text-secondary)]">
        Your browser session is active. Operational dashboard modules and
        maintenance data will be introduced in their own reviewed branches.
      </p>
      <Card className="mt-8 max-w-2xl p-7 shadow-none">
        <h2 className="text-lg font-semibold text-[var(--text-primary)]">
          Authentication integration complete
        </h2>
        <p className="mt-2 text-sm leading-6 text-[var(--text-neutral)]">
          This placeholder intentionally contains no fabricated assets,
          schedules, inspections, personnel, metrics, or maintenance records.
        </p>
      </Card>
    </section>
  )
}
