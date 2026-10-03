import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { InspectionDetail } from '@/features/inspections/inspection-detail'
import {
  parseDetailReturnContext,
  type DetailReturnContext,
} from '@/features/shared/detail-navigation'
import type { InspectionSearch } from '@/features/inspections/inspection-registry'
import type { PmAcknowledgementReviewContext } from '@/features/preventive-maintenance-forms/pm-acknowledgement-review'

const searchSchema = z.object({
  assetId: z.string().uuid().optional(),
  scheduleId: z.string().uuid().optional(),
  isOperational: z
    .union([
      z.literal(true),
      z.literal(false),
      z.literal('true').transform(() => true),
      z.literal('false').transform(() => false),
    ])
    .optional(),
  dateFrom: z.string().datetime({ offset: true }).optional(),
  dateTo: z.string().datetime({ offset: true }).optional(),
  page: z.coerce.number().int().positive().max(10000).optional(),
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

export const Route = createFileRoute('/app/inspections/$inspectionId')({
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
  component: InspectionDetailPage,
})

function InspectionDetailPage() {
  const { inspectionId } = Route.useParams()
  const search = Route.useSearch()
  const { returnContext } = search
  const registrySearch: InspectionSearch = {
    assetId: search.assetId,
    scheduleId: search.scheduleId,
    isOperational: search.isOperational,
    dateFrom: search.dateFrom,
    dateTo: search.dateTo,
    page: search.page,
  }
  const reviewContext: PmAcknowledgementReviewContext | undefined =
    search.reviewFormId
      ? {
          reviewFormId: search.reviewFormId,
          department: search.department,
          assetCategory: search.assetCategory,
          year: search.year,
          pmCycle: search.pmCycle,
          condition: search.condition,
          timeliness: search.timeliness,
          search: search.search,
        }
      : undefined
  return (
    <InspectionDetail
      inspectionId={inspectionId}
      reviewContext={reviewContext}
      registrySearch={registrySearch}
      returnContext={returnContext}
    />
  )
}
