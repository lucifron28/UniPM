import { createFileRoute } from '@tanstack/react-router'
import { FormDetail } from '@/features/preventive-maintenance-forms/form-detail'

export const Route = createFileRoute(
  '/app/preventive-maintenance-forms/$formId',
)({
  component: FormDetailPage,
})

function FormDetailPage() {
  const { formId } = Route.useParams()
  return <FormDetail formId={formId} />
}
