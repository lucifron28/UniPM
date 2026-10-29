import { createFileRoute } from '@tanstack/react-router'
import { ScheduleDetail } from '@/features/schedules/schedule-detail'
import {
  parseDetailReturnContext,
  type DetailReturnContext,
} from '@/features/shared/detail-navigation'
import type { ScheduleSearch } from '@/features/schedules/schedule-registry'
import { scheduleSearchSchema } from '@/features/schedules/schedule-search'

export const Route = createFileRoute('/app/schedules/$scheduleId')({
  validateSearch: (
    search,
  ): ScheduleSearch & {
    returnContext?: DetailReturnContext | undefined
  } => {
    const parsed = scheduleSearchSchema.safeParse(search)
    return parsed.success
      ? {
          ...parsed.data,
          returnContext: parseDetailReturnContext(search.returnContext),
        }
      : {}
  },
  component: ScheduleDetailPage,
})

function ScheduleDetailPage() {
  const { scheduleId } = Route.useParams()
  const { returnContext, ...detailSearch } = Route.useSearch()
  const registrySearch: ScheduleSearch = detailSearch

  return (
    <ScheduleDetail
      scheduleId={scheduleId}
      registrySearch={registrySearch}
      returnContext={returnContext}
    />
  )
}
