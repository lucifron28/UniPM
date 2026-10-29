import { useState } from 'react'
import { Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { ScheduleBatchPanel } from './schedule-batch-panel'
import { useScheduleRegistry } from './schedule-queries'
import { formatPmCycle } from './schedule-presentation'
import { formatAssetCategory } from '@/features/assets/asset-presentation'
import type { ScheduleAssignmentQueueBatch } from './schedule-contract'
import type { ScheduleSearch } from './schedule-registry'

export function ScheduleAssignmentRecordsPanel({
  batch,
  onClose,
  registrySearch,
}: {
  batch: ScheduleAssignmentQueueBatch
  onClose: () => void
  registrySearch?: ScheduleSearch
}) {
  const [page, setPage] = useState(1)
  const pageSize = 10
  const query = useScheduleRegistry({
    department: batch.department,
    assetCategory: batch.assetCategory,
    pmCycle: batch.pmCycle,
    page,
    pageSize,
  })
  const total = query.data?.totalCount ?? 0
  const pages = Math.max(1, Math.ceil(total / pageSize))
  return (
    <ScheduleBatchPanel
      title={`${batch.department} · ${formatAssetCategory(batch.assetCategory)}`}
      description={formatPmCycle(batch.pmCycle)}
      onClose={onClose}
    >
      {query.isPending ? (
        <p role="status">Loading batch schedules…</p>
      ) : query.isError ? (
        <div role="alert">
          <p>Batch schedules could not be loaded.</p>
          <Button onClick={() => void query.refetch()}>Retry</Button>
        </div>
      ) : (
        <>
          <p className="mb-3 text-sm text-[var(--text-secondary)]">
            {total} schedules in this batch.
          </p>
          <table className="w-full text-left text-sm">
            <caption className="sr-only">
              Schedules in selected assignment batch
            </caption>
            <thead>
              <tr>
                {['Asset', 'Building / location', 'Status', 'Details'].map(
                  (label) => (
                    <th key={label} scope="col" className="border-b p-2">
                      {label}
                    </th>
                  ),
                )}
              </tr>
            </thead>
            <tbody>
              {query.data?.items.map((row) => (
                <tr
                  key={row.id}
                  className="border-b border-[var(--border-soft)]"
                >
                  <th scope="row" className="p-2 break-words">
                    {row.asset?.assetCode ?? 'Asset unavailable'}
                  </th>
                  <td className="p-2">
                    {[row.asset?.building, row.asset?.location]
                      .filter(Boolean)
                      .join(' · ') || 'Not recorded'}
                  </td>
                  <td className="p-2">{row.status}</td>
                  <td className="p-2">
                    <Link
                      to="/app/schedules/$scheduleId"
                      params={{ scheduleId: row.id }}
                      search={{
                        returnContext: {
                          kind: 'scheduleRegistry',
                          search: registrySearch ?? {
                            view: 'assignments',
                            department: batch.department,
                            assetCategory: batch.assetCategory,
                            pmCycle: batch.pmCycle,
                          },
                        },
                      }}
                      className="cursor-pointer font-semibold text-[var(--primary)] hover:underline"
                    >
                      Open schedule
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {total === 0 && (
            <p role="status" className="py-4">
              No schedules are available for this batch.
            </p>
          )}
          <div className="mt-4 flex items-center justify-between gap-2">
            <Button
              variant="secondary"
              disabled={page <= 1 || query.isFetching}
              onClick={() => setPage((p) => p - 1)}
            >
              Previous
            </Button>
            <span className="text-sm">
              Page {page} of {pages}
            </span>
            <Button
              variant="secondary"
              disabled={page >= pages || query.isFetching}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </>
      )}
    </ScheduleBatchPanel>
  )
}
