import { Link } from '@tanstack/react-router'
import { ArrowLeft } from 'lucide-react'
import {
  resolveDetailReturn,
  type DetailReturnContext,
  type DetailReturnFallback,
} from '@/features/shared/detail-navigation'
const backLinkClassName =
  'inline-flex w-fit min-h-11 cursor-pointer items-center gap-2 rounded-lg border border-[var(--border-soft)] bg-white px-4 py-2 text-sm font-semibold text-[var(--primary)] transition-colors hover:border-[var(--primary)] hover:bg-[var(--primary)] hover:text-white focus-visible:ring-2 focus-visible:ring-[var(--primary)] focus-visible:ring-offset-2 focus-visible:outline-none print:hidden'

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
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
          {target.label}
        </Link>
      )
    case 'analytics':
      return (
        <Link
          to="/app/analytics"
          search={{
            returnContext: {
              kind: 'analytics',
              sessionId: target.sessionId,
            },
          }}
          className={className}
        >
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
          {target.label}
        </Link>
      )
    case 'assetRegistry':
      return (
        <Link to="/app/assets" search={target.search} className={className}>
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
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
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
          {target.label}
        </Link>
      )
    case 'scheduleRegistry':
      return (
        <Link to="/app/schedules" search={target.search} className={className}>
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
          {target.label}
        </Link>
      )
    case 'formRegistry':
      return (
        <Link
          to="/app/preventive-maintenance-forms"
          search={target.search}
          className={className}
        >
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
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
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
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
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
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
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
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
          <ArrowLeft className="size-4 shrink-0" aria-hidden="true" />
          {target.label}
        </Link>
      )
  }
}
