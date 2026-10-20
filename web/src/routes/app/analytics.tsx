import { createFileRoute } from '@tanstack/react-router'
import { PageHeading } from '@/components/layout/page-heading'
import { PmAnalyticsPanel } from '@/features/reports/pm-analytics'
import { PmAnalyticsV3Panel } from '@/features/reports/pm-analytics-v3'

export const Route = createFileRoute('/app/analytics')({
  component: AnalyticsPage,
})

function AnalyticsPage() {
  return (
    <section aria-labelledby="pm-analytics-page-title" className="space-y-4">
      <PageHeading
        id="pm-analytics-page-title"
        title="PM Report Builder"
        description="Compose, review and run a read-only preventive-maintenance report."
      />
      <PmAnalyticsV3Panel />
      <details className="rounded-lg border border-[var(--border-soft)] bg-white p-4">
        <summary className="cursor-pointer text-sm font-semibold text-[var(--text-primary)]">
          Open existing PM Analytics reports
        </summary>
        <div className="mt-4">
          <PmAnalyticsPanel />
        </div>
      </details>
    </section>
  )
}
