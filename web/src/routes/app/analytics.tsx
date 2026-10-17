import { createFileRoute } from '@tanstack/react-router'
import { PageHeading } from '@/components/layout/page-heading'
import { PmAnalyticsPanel } from '@/features/reports/pm-analytics'

export const Route = createFileRoute('/app/analytics')({
  component: AnalyticsPage,
})

function AnalyticsPage() {
  return (
    <section aria-labelledby="pm-analytics-page-title" className="space-y-4">
      <PageHeading
        id="pm-analytics-page-title"
        title="PM Analytics"
        description="Review a supported PM measure and its source records."
      />
      <PmAnalyticsPanel />
    </section>
  )
}
