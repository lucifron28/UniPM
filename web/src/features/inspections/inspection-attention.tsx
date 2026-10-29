import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { httpClient } from '@/api/http-client'
import { useCurrentUser } from '@/features/auth/current-user'
import { InspectionPhotoEvidencePanel } from './inspection-detail'

type Attention = {
  awaitingAcknowledgement: number
  awaitingReferral: number
  referred: number
  totalCount: number
  page: number
  items: {
    id: string
    formId: string
    assetCode: string
    department: string | null
    location: string | null
    dateInspected: string
    remarks: string | null
    hasPhotoEvidence: boolean
    formStatus: string
    externalPmNumber: string | null
  }[]
}

export function InspectionAttention() {
  const user = useCurrentUser()
  const enabled =
    user.data?.roles.some((role) => ['GSD', 'Supervisor'].includes(role)) ===
    true
  const [stage, setStage] = useState('referral')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState(false)
  const query = useQuery({
    queryKey: ['inspection-attention', stage, page],
    queryFn: async ({ signal }) =>
      (
        await httpClient.get<Attention>('/api/v1/inspections/attention', {
          params: { stage, page },
          signal,
        })
      ).data,
    enabled,
  })
  if (!enabled) return null
  return (
    <section
      aria-labelledby="inspection-attention-title"
      className="rounded-lg border border-amber-300 bg-amber-50 p-4 print:hidden"
    >
      <h2 id="inspection-attention-title" className="text-lg font-bold">
        Non-operational findings needing attention
      </h2>
      <p className="mt-1 text-sm">
        All PM periods ·{' '}
        {user.data?.roles.includes('GSD')
          ? 'All departments'
          : 'Your accessible assignments'}
        . Historical findings do not establish the current asset condition.
      </p>
      {query.isError ? (
        <p role="alert">
          Could not load findings.{' '}
          <button
            className="cursor-pointer underline"
            onClick={() => void query.refetch()}
          >
            Retry
          </button>
        </p>
      ) : (
        <div className="mt-3 flex flex-wrap gap-3">
          {[
            [
              'acknowledgement',
              'Awaiting acknowledgement',
              query.data?.awaitingAcknowledgement,
            ],
            [
              'referral',
              'Awaiting WMS PM number',
              query.data?.awaitingReferral,
            ],
            ['referred', 'Referred to WMS', query.data?.referred],
          ].map(([value, label, count]) => (
            <button
              key={value}
              type="button"
              className="cursor-pointer rounded-md border border-amber-300 bg-white px-4 py-3 text-left hover:bg-amber-100 focus-visible:outline-2 focus-visible:outline-offset-2"
              aria-pressed={open && stage === value}
              onClick={() => {
                setStage(String(value))
                setPage(1)
                setOpen(true)
              }}
            >
              <strong className="mr-2 text-xl">{count ?? '…'}</strong>
              {label}
            </button>
          ))}
        </div>
      )}
      {open && query.data && (
        <div className="mt-4 space-y-3">
          <div className="flex items-center justify-between">
            <p>{query.data.totalCount} findings · oldest inspection first</p>
            <button
              type="button"
              className="cursor-pointer underline"
              onClick={() => setOpen(false)}
            >
              Close list
            </button>
          </div>
          {query.data.items.length === 0 && <p>No findings in this stage.</p>}
          {query.data.items.map((row) => (
            <article
              key={row.id}
              className="rounded-md border border-amber-200 bg-white p-4"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h3 className="font-bold">{row.assetCode}</h3>
                  <p>
                    {row.department} · {row.location}
                  </p>
                  <p className="text-sm">
                    Inspection:{' '}
                    {new Intl.DateTimeFormat('en-PH', {
                      timeZone: 'Asia/Manila',
                    }).format(new Date(row.dateInspected))}{' '}
                    · Form: {row.formStatus}
                  </p>
                </div>
                <Link
                  to="/app/preventive-maintenance-forms/$formId"
                  params={{ formId: row.formId }}
                  className="cursor-pointer rounded-md bg-[var(--primary)] px-4 py-2 font-semibold text-white hover:opacity-80"
                >
                  {stage === 'referral'
                    ? 'Review / enter PM number'
                    : 'Review form'}
                </Link>
              </div>
              <p className="mt-2 whitespace-pre-wrap">
                {row.remarks || 'No remarks recorded.'}
              </p>
              {row.externalPmNumber && (
                <p className="mt-2 font-semibold">
                  WMS PM number: {row.externalPmNumber}
                </p>
              )}
              {row.hasPhotoEvidence && (
                <InspectionPhotoEvidencePanel
                  inspectionId={row.id}
                  hasPhotoEvidence
                />
              )}
            </article>
          ))}
          <div className="flex items-center gap-4">
            <button
              type="button"
              className="cursor-pointer rounded border bg-white px-3 py-2 disabled:cursor-default disabled:opacity-50"
              disabled={page === 1}
              onClick={() => setPage(page - 1)}
            >
              Previous
            </button>
            <span>Page {page}</span>
            <button
              type="button"
              className="cursor-pointer rounded border bg-white px-3 py-2 disabled:cursor-default disabled:opacity-50"
              disabled={page * 20 >= query.data.totalCount}
              onClick={() => setPage(page + 1)}
            >
              Next
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
