import { createFileRoute } from '@tanstack/react-router'
import { PageHeading } from '@/components/layout/page-heading'
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
    </section>
  )
}
