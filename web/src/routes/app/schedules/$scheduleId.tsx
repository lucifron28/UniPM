import { createFileRoute } from '@tanstack/react-router'
import { ScheduleDetail } from '@/features/schedules/schedule-detail'

export const Route = createFileRoute('/app/schedules/$scheduleId')({
  component: ScheduleDetailPage,
})

function ScheduleDetailPage() {
  const { scheduleId } = Route.useParams()
  return <ScheduleDetail scheduleId={scheduleId} />
}
