import { createFileRoute } from '@tanstack/react-router'
import { InspectionDetail } from '@/features/inspections/inspection-detail'

export const Route = createFileRoute('/app/inspections/$inspectionId')({
  component: InspectionDetailPage,
})

function InspectionDetailPage() {
  const { inspectionId } = Route.useParams()
  return <InspectionDetail inspectionId={inspectionId} />
}
