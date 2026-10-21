import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { PageHeading } from '@/components/layout/page-heading'
import { PmAnalyticsV3Panel } from '@/features/reports/pm-analytics-v3'
import {
  parseDetailReturnContext,
  type DetailReturnContext,
} from '@/features/shared/detail-navigation'

const searchSchema = z.object({ returnContext: z.unknown().optional() })

export const Route = createFileRoute('/app/analytics')({
  validateSearch: (
    search,
  ): { returnContext?: DetailReturnContext | undefined } => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success
      ? { returnContext: parseDetailReturnContext(parsed.data.returnContext) }
      : {}
  },
  component: AnalyticsPage,
})

function AnalyticsPage() {
  const { returnContext } = Route.useSearch()
  return (
    <section aria-labelledby="pm-analytics-page-title" className="space-y-4">
      <PageHeading
        id="pm-analytics-page-title"
        title="Natural Language Analytics"
        description="Ask a maintenance question, review the scope, then run the report."
      />
      <PmAnalyticsV3Panel returnContext={returnContext} />
    </section>
  )
}
