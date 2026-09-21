import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { InspectionDetail } from '@/features/inspections/inspection-detail'

const searchSchema = z.object({
  returnTo: z.string().max(1200).optional(),
})

export const Route = createFileRoute('/app/inspections/$inspectionId')({
  validateSearch: (search) => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: InspectionDetailPage,
})

function InspectionDetailPage() {
  const { inspectionId } = Route.useParams()
  const search = Route.useSearch()
  return <InspectionDetail inspectionId={inspectionId} returnTo={search.returnTo} />
}
