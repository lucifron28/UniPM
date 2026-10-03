import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { FormDetail } from '@/features/preventive-maintenance-forms/form-detail'
import type { PmAcknowledgementReviewContext } from '@/features/preventive-maintenance-forms/pm-acknowledgement-review'
import {
  parseDetailReturnContext,
  type DetailReturnContext,
} from '@/features/shared/detail-navigation'

const searchSchema = z.object({
  readonly: z.coerce.boolean().optional(),
  reviewFormId: z.string().uuid().optional(),
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
  '/app/preventive-maintenance-forms/$formId/',
)({
  validateSearch: (
    search,
  ): Omit<z.infer<typeof searchSchema>, 'returnContext'> & {
    returnContext?: DetailReturnContext | undefined
  } => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success
      ? {
          ...parsed.data,
          returnContext: parseDetailReturnContext(parsed.data.returnContext),
        }
      : {}
  },
  component: FormDetailPage,
})

function FormDetailPage() {
  const { formId } = Route.useParams()
  const search = Route.useSearch()
  const dashboardSearch = {
    department: search.department,
    assetCategory: search.assetCategory,
    year: search.year,
    pmCycle: search.pmCycle,
    condition: search.condition,
    timeliness: search.timeliness,
    search: search.search,
  }
  const reviewContext: PmAcknowledgementReviewContext | undefined =
    search.reviewFormId
      ? {
          ...dashboardSearch,
          reviewFormId: search.reviewFormId,
        }
      : undefined
  return (
    <FormDetail
      formId={formId}
      readOnly={search.readonly}
      reviewContext={reviewContext}
      returnContext={search.returnContext}
      dashboardSearch={
        search.readonly && !reviewContext ? dashboardSearch : undefined
      }
    />
  )
}
