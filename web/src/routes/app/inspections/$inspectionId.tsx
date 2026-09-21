import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { InspectionDetail } from '@/features/inspections/inspection-detail'
import type { PmAcknowledgementReviewContext } from '@/features/preventive-maintenance-forms/pm-acknowledgement-review'

const searchSchema = z.object({
  reviewFormId: z.string().uuid().optional(),
  department: z.string().trim().max(256).optional(),
  assetCategory: z.string().trim().max(128).optional(),
  pmCycle: z
    .string()
    .regex(/^\d{4}-\d{2}$/)
    .optional(),
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
    <InspectionDetail
      inspectionId={inspectionId}
      reviewContext={reviewContext}
    />
  )
}
