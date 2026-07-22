import { createFileRoute } from '@tanstack/react-router'
import { ScheduleCreate } from '@/features/schedules/schedule-create'

export const Route = createFileRoute('/app/schedules/new')({
  component: ScheduleCreate,
})
