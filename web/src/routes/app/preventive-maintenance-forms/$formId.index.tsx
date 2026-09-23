import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { FormDetail } from '@/features/preventive-maintenance-forms/form-detail'
import type { PmAcknowledgementReviewContext } from '@/features/preventive-maintenance-forms/pm-acknowledgement-review'

const searchSchema = z.object({
  readonly: z.coerce.boolean().optional(),
  reviewFormId: z.string().uuid().optional(),
  department: z.string().trim().max(256).optional(),
  assetCategory: z.string().trim().max(128).optional(),
  pmCycle: z
    .string()
    .regex(/^\d{4}-\d{2}$/)
    .optional(),
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
  const reviewContext: PmAcknowledgementReviewContext | undefined =
    search.reviewFormId && search.assetCategory && search.pmCycle
      ? {
          reviewFormId: search.reviewFormId,
          department: search.department,
          assetCategory: search.assetCategory,
          pmCycle: search.pmCycle,
        }
      : undefined
  return (
    <FormDetail
      formId={formId}
      readOnly={search.readonly}
      reviewContext={reviewContext}
    />
  )
}
