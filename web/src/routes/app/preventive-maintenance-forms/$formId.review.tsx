import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  PmAcknowledgementReview,
  type PmAcknowledgementReviewSearch,
} from '@/features/preventive-maintenance-forms/pm-acknowledgement-review'

const searchSchema = z.object({
  department: z.string().trim().max(256).optional(),
  assetCategory: z.string().trim().max(128).optional(),
  pmCycle: z
    .string()
    .regex(/^\d{4}-\d{2}$/)
    .optional(),
})

export const Route = createFileRoute(
  '/app/preventive-maintenance-forms/$formId/review',
)({
  validateSearch: (search): PmAcknowledgementReviewSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: PmAcknowledgementReviewPage,
})

function PmAcknowledgementReviewPage() {
  const { formId } = Route.useParams()
  const search = Route.useSearch()
  return <PmAcknowledgementReview formId={formId} search={search} />
}
