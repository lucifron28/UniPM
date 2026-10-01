import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import {
  PmAcknowledgementReview,
  type PmAcknowledgementReviewSearch,
} from '@/features/preventive-maintenance-forms/pm-acknowledgement-review'
import { parseDetailReturnContext } from '@/features/shared/detail-navigation'

const searchSchema = z.object({
  department: z.string().trim().max(256).optional(),
  assetCategory: z.string().trim().max(128).optional(),
  pmCycle: z
    .string()
    .regex(/^\d{4}-\d{2}$/)
    .optional(),
  year: z.coerce.number().int().positive().optional(),
  condition: z
    .enum(['Operational', 'NonOperational', 'NotInspected'])
    .optional(),
  timeliness: z
    .enum(['OnTime', 'Late', 'Scheduled', 'Pending', 'NotCompleted'])
    .optional(),
  search: z.string().trim().max(256).optional(),
  returnContext: z.unknown().optional(),
})

export const Route = createFileRoute(
  '/app/preventive-maintenance-forms/$formId/review',
)({
  validateSearch: (search): PmAcknowledgementReviewSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success
      ? {
          ...parsed.data,
          returnContext: parseDetailReturnContext(parsed.data.returnContext),
        }
      : {}
  },
  component: PmAcknowledgementReviewPage,
})

function PmAcknowledgementReviewPage() {
  const { formId } = Route.useParams()
  const search = Route.useSearch()
  return <PmAcknowledgementReview formId={formId} search={search} />
}
