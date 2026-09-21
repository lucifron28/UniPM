import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { FormDetail } from '@/features/preventive-maintenance-forms/form-detail'

const searchSchema = z.object({
  readonly: z.coerce.boolean().optional(),
  returnTo: z.string().max(1200).optional(),
})

export const Route = createFileRoute(
  '/app/preventive-maintenance-forms/$formId/',
)({
  validateSearch: (search) => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: FormDetailPage,
})

function FormDetailPage() {
  const { formId } = Route.useParams()
  const search = Route.useSearch()
  return (
    <FormDetail
      formId={formId}
      readOnly={search.readonly}
      returnTo={search.returnTo}
    />
  )
}
