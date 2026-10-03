import { Link } from '@tanstack/react-router'
import {
  resolveDetailReturn,
  type DetailReturnContext,
  type DetailReturnFallback,
} from '@/features/shared/detail-navigation'
const backLinkClassName =
  'inline-flex min-h-10 items-center text-sm font-semibold text-[var(--primary)] hover:underline focus-visible:rounded focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:outline-none'

export function DetailBackLink({
  context,
  fallback,
  className = backLinkClassName,
}: {
  context?: DetailReturnContext | undefined
  fallback: DetailReturnFallback
  className?: string | undefined
}) {
  const target = resolveDetailReturn(context, fallback)
  const searchContext =
    'returnContext' in target && target.returnContext
      ? { returnContext: target.returnContext }
      : {}

  switch (target.kind) {
    case 'dashboard':
      return (
        <Link to="/app/dashboard" search={target.search} className={className}>
          {target.label}
        </Link>
      )
    case 'assetRegistry':
      return (
        <Link to="/app/assets" search={target.search} className={className}>
          {target.label}
        </Link>
      )
    case 'inspectionRegistry':
      return (
        <Link
          to="/app/inspections"
          search={target.search}
          className={className}
        >
          {target.label}
        </Link>
      )
    case 'scheduleRegistry':
      return (
        <Link to="/app/schedules" search={target.search} className={className}>
          {target.label}
        </Link>
      )
    case 'formRegistry':
      return (
        <Link to="/app/preventive-maintenance-forms" className={className}>
          {target.label}
        </Link>
      )
    case 'batchReview':
      return (
        <Link
          to="/app/preventive-maintenance-forms/$formId/review"
          params={{ formId: target.formId }}
          search={{ ...target.search, returnContext: target.returnContext }}
          className={className}
        >
          {target.label}
        </Link>
      )
    case 'assetDetail':
      return (
        <Link
          to="/app/assets/$assetId"
          params={{ assetId: target.assetId }}
          search={searchContext}
          className={className}
        >
          {target.label}
        </Link>
      )
    case 'inspectionDetail':
      return (
        <Link
          to="/app/inspections/$inspectionId"
          params={{ inspectionId: target.inspectionId }}
          search={searchContext}
          className={className}
        >
          {target.label}
        </Link>
      )
    case 'scheduleDetail':
      return (
        <Link
          to="/app/schedules/$scheduleId"
          params={{ scheduleId: target.scheduleId }}
          search={searchContext}
          className={className}
        >
          {target.label}
        </Link>
      )
  }
}
