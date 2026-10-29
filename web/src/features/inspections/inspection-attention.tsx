import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ArrowRight } from 'lucide-react'
import { httpClient } from '@/api/http-client'
import type { InspectionAttentionResponse } from '@/api/generated/models'
import { useCurrentUser } from '@/features/auth/current-user'

export function InspectionAttention() {
  const user = useCurrentUser()
  const enabled =
    user.data?.roles.some((role) => ['GSD', 'Supervisor'].includes(role)) ===
    true
  const query = useQuery({
    queryKey: ['inspection-attention'],
    queryFn: async ({ signal }) =>
      (
        await httpClient.get<InspectionAttentionResponse>(
          '/api/v1/inspections/attention',
          { signal },
        )
      ).data,
    enabled,
  })
  if (!enabled) return null
  const acknowledgement = query.data
    ? Number(query.data.awaitingAcknowledgement)
    : null
  const referral = query.data ? Number(query.data.awaitingReferral) : null
  const linkClass =
    'group flex min-h-14 cursor-pointer items-center gap-3 rounded-md border border-[var(--border-soft)] bg-white px-4 py-3 text-sm text-[var(--text-primary)] transition-colors hover:border-[var(--primary)] hover:bg-[var(--surface-muted)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--primary)]'
  const indicator = (count: number | null) => (
    <span
      aria-hidden="true"
      className={`h-2 w-2 shrink-0 rounded-full ${count !== null && count > 0 ? 'bg-amber-500' : 'bg-[var(--border-soft)]'}`}
    />
  )
  return (
    <section
      aria-labelledby="inspection-attention-title"
      className="space-y-2 print:hidden"
    >
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <h2 id="inspection-attention-title" className="text-sm font-semibold">
          Needs attention
        </h2>
        <p className="text-xs text-[var(--text-neutral)]">
          Non-operational findings · All periods
        </p>
        {acknowledgement === 0 && referral === 0 && (
          <p className="text-xs text-[var(--text-secondary)]">
            No pending findings
          </p>
        )}
      </div>
      {query.isError ? (
        <p role="alert" className="text-sm">
          Attention counts unavailable.{' '}
          <button
            type="button"
            className="cursor-pointer font-semibold text-[var(--primary)] underline"
            onClick={() => void query.refetch()}
          >
            Retry
          </button>
        </p>
      ) : (
        <div className="grid gap-2 md:grid-cols-2">
          <Link
            to="/app/preventive-maintenance-forms"
            search={{ attention: 'awaitingAcknowledgement' }}
            className={linkClass}
          >
            {indicator(acknowledgement)}
            <span className="flex-1">Awaiting acknowledgement</span>
            <strong className="text-lg tabular-nums">
              {acknowledgement ?? '…'}
            </strong>
            <ArrowRight
              size={16}
              aria-hidden="true"
              className="text-[var(--text-neutral)] group-hover:text-[var(--primary)]"
            />
          </Link>
          <Link
            to="/app/inspections"
            search={{
              isOperational: false,
              wmsReferralStatus: 'CorrectiveFollowUpPending',
            }}
            className={linkClass}
          >
            {indicator(referral)}
            <span className="flex-1">Awaiting PM number</span>
            <strong className="text-lg tabular-nums">{referral ?? '…'}</strong>
            <ArrowRight
              size={16}
              aria-hidden="true"
              className="text-[var(--text-neutral)] group-hover:text-[var(--primary)]"
            />
          </Link>
        </div>
      )}
    </section>
  )
}
