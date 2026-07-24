import { Link } from '@tanstack/react-router'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useInspectionHistory } from '@/features/inspections/inspection-queries'
import {
  excerpt,
  formatInspectionDate,
  inspectionOutcome,
} from '@/features/inspections/inspection-presentation'

export function InspectionHistory({ assetId }: { assetId: string }) {
  const history = useInspectionHistory(assetId)

  return (
    <Card className="shadow-none">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-semibold">Recent inspection history</h2>
          <p className="mt-1 text-sm text-[var(--text-secondary)]">
            Latest recorded source records for this asset.
          </p>
        </div>
        <Link
          to="/app/inspections"
          search={{ assetId }}
          className="text-sm font-semibold text-[var(--primary)] hover:underline"
        >
          View all inspections
        </Link>
      </div>

      {history.isPending ? (
        <div className="mt-4 space-y-3" role="status">
          <span className="sr-only">Loading inspection history...</span>
          {Array.from({ length: 3 }, (_, index) => (
            <Skeleton key={index} className="h-14 w-full" />
          ))}
        </div>
      ) : history.isError ? (
        <div
          role="alert"
          className="mt-4 rounded-lg bg-[var(--page-background)] p-4"
        >
          <p className="text-sm text-[var(--text-secondary)]">
            Inspection history is currently unavailable.
          </p>
          <Button
            type="button"
            className="mt-3"
            onClick={() => void history.refetch()}
          >
            Retry history
          </Button>
        </div>
      ) : history.data.length === 0 ? (
        <p className="mt-4 text-sm text-[var(--text-secondary)]">
          No inspection history has been recorded for this asset.
        </p>
      ) : (
        <ul className="mt-4 divide-y divide-[var(--border-soft)]">
          {history.data.slice(0, 5).map((record) => (
            <li key={record.id} className="py-4 first:pt-0 last:pb-0">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="font-semibold text-[var(--text-primary)]">
                    {formatInspectionDate(record.dateInspected)}
                  </p>
                  <p className="mt-1 text-sm text-[var(--text-secondary)]">
                    {inspectionOutcome(record.isOperational)}:{' '}
                    {excerpt(record.remarks)}
                  </p>
                </div>
                <Link
                  to="/app/inspections/$inspectionId"
                  params={{ inspectionId: record.id }}
                  className="text-sm font-semibold text-[var(--primary)] hover:underline"
                >
                  View source
                </Link>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}
