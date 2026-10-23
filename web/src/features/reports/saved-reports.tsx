import { useEffect, useState } from 'react'
import {
  createSavedAnalyticsReport,
  deleteSavedAnalyticsReport,
  listSavedAnalyticsReports,
  updateSavedAnalyticsReport,
} from '@/api/generated/endpoints'
import type {
  PmAnalyticsV3ReportPlan,
  SavedAnalyticsReportResponse,
} from '@/api/generated/models'
import { ApiError } from '@/api/problem-details'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Label } from '@/components/ui/label'

const inputClass =
  'min-h-10 rounded-md border border-[var(--border-soft)] bg-white px-3 py-2 text-sm'
const periods = [
  ['Fixed', 'Keep the reviewed dates'],
  ['ThisMonth', 'This month'],
  ['LastMonth', 'Previous month'],
  ['ThisYear', 'This year'],
  ['LastYear', 'Previous year'],
] as const

function message(error: unknown) {
  if (error instanceof ApiError) {
    return (
      Object.values(error.problem?.errors ?? {})
        .flat()
        .join(' ') || error.message
    )
  }
  return 'The saved report could not be updated. Try again.'
}

export function SavedReports({
  reviewedPlan,
  disabled,
  onOpen,
}: {
  reviewedPlan: PmAnalyticsV3ReportPlan | null
  disabled: boolean
  onOpen: (
    report: SavedAnalyticsReportResponse,
    edit: boolean,
  ) => Promise<boolean>
}) {
  const [reports, setReports] = useState<SavedAnalyticsReportResponse[]>([])
  const [selected, setSelected] = useState<SavedAnalyticsReportResponse | null>(
    null,
  )
  const [name, setName] = useState('')
  const [periodMode, setPeriodMode] = useState('Fixed')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  const [rename, setRename] = useState<{ id: string; name: string } | null>(
    null,
  )
  const [deleteId, setDeleteId] = useState<string | null>(null)
  const blocked = disabled || busy || loading

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    void listSavedAnalyticsReports(controller.signal)
      .then((rows) => {
        if (!controller.signal.aborted) {
          setReports(rows)
          setSelected(null)
          setError(null)
        }
      })
      .catch((failure: unknown) => {
        if (!controller.signal.aborted) setError(message(failure))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [attempt])

  const perform = async (action: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    setNotice(null)
    try {
      await action()
    } catch (failure) {
      setError(message(failure))
    } finally {
      setBusy(false)
    }
  }
  const remember = (row: SavedAnalyticsReportResponse) => {
    setReports((rows) => [
      row,
      ...rows.filter((current) => current.id !== row.id),
    ])
  }
  const save = (replace: boolean) => {
    if (!reviewedPlan || (replace && !selected)) return
    void perform(async () => {
      const settings = {
        name: name.trim(),
        plan: reviewedPlan,
        periodMode:
          reviewedPlan.subject === 'pmExecution' ? periodMode : 'Fixed',
      }
      const row =
        replace && selected
          ? await updateSavedAnalyticsReport(selected.id, {
              ...settings,
              revision: selected.revision,
            })
          : await createSavedAnalyticsReport(settings)
      remember(row)
      setSelected(row)
      setName(row.name)
      setPeriodMode(row.periodMode)
      setNotice(
        'Report settings saved. Running it will read current data after you confirm the preview.',
      )
    })
  }
  const open = (row: SavedAnalyticsReportResponse, edit: boolean) => {
    void perform(async () => {
      if (await onOpen(row, edit)) {
        setSelected(row)
        setName(row.name)
        setPeriodMode(row.periodMode)
      }
    })
  }

  return (
    <Card className="space-y-4 p-4 shadow-none sm:p-5">
      <details>
        <summary className="cursor-pointer text-base font-semibold">
          My reports{reports.length > 0 ? ` (${reports.length})` : ''}
        </summary>
        <div className="mt-4 space-y-4">
          <p className="text-sm text-[var(--text-secondary)]">
            Save report settings for your own use. Each run reads current data
            and asks you to confirm the scope.
          </p>
          <Button
            type="button"
            variant="secondary"
            disabled={disabled || busy || loading}
            onClick={() => setAttempt((value) => value + 1)}
          >
            Reload my reports
          </Button>
          {loading && (
            <p role="status" className="text-sm">
              Loading saved reports...
            </p>
          )}
          {!loading && reports.length === 0 && (
            <p className="text-sm">
              No saved reports yet. Preview a report to save its settings.
            </p>
          )}
          <ul className="space-y-3">
            {reports.map((row) => (
              <li
                key={row.id}
                className="rounded-md border border-[var(--border-soft)] p-3"
              >
                <p className="font-medium">{row.name}</p>
                <p className="mt-1 text-sm text-[var(--text-secondary)]">
                  {periods.find(([mode]) => mode === row.periodMode)?.[1] ??
                    row.periodMode}{' '}
                  ·{' '}
                  {row.plan.subject === 'assetInventory'
                    ? 'Asset inventory'
                    : 'PM execution'}
                </p>
                <div className="mt-2 flex flex-wrap gap-2">
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={blocked}
                    onClick={() => open(row, false)}
                  >
                    Open preview
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={blocked}
                    onClick={() => open(row, true)}
                  >
                    Edit settings
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={blocked}
                    onClick={() => {
                      setRename({ id: row.id, name: row.name })
                      setDeleteId(null)
                    }}
                  >
                    Rename
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={blocked}
                    onClick={() => {
                      setDeleteId(row.id)
                      setRename(null)
                    }}
                  >
                    Delete
                  </Button>
                </div>
                {rename?.id === row.id && (
                  <form
                    className="mt-3 flex flex-wrap gap-2"
                    onSubmit={(event) => {
                      event.preventDefault()
                      void perform(async () => {
                        const updated = await updateSavedAnalyticsReport(
                          row.id,
                          {
                            name: rename.name.trim(),
                            plan: row.plan,
                            periodMode: row.periodMode,
                            revision: row.revision,
                          },
                        )
                        remember(updated)
                        if (selected?.id === row.id) {
                          setSelected(updated)
                          setName(updated.name)
                        }
                        setRename(null)
                        setNotice('Report renamed.')
                      })
                    }}
                  >
                    <input
                      className={inputClass}
                      aria-label={`New name for ${row.name}`}
                      required
                      maxLength={120}
                      value={rename.name}
                      disabled={blocked}
                      onChange={(event) =>
                        setRename({ id: row.id, name: event.target.value })
                      }
                    />
                    <Button
                      disabled={blocked || !rename.name.trim()}
                      type="submit"
                    >
                      Save name
                    </Button>
                    <Button
                      disabled={blocked}
                      type="button"
                      variant="secondary"
                      onClick={() => setRename(null)}
                    >
                      Cancel
                    </Button>
                  </form>
                )}
                {deleteId === row.id && (
                  <div className="mt-3 flex flex-wrap items-center gap-2">
                    <span className="text-sm">
                      Delete these saved settings?
                    </span>
                    <Button
                      type="button"
                      disabled={blocked}
                      onClick={() =>
                        void perform(async () => {
                          await deleteSavedAnalyticsReport(row.id)
                          setReports((rows) =>
                            rows.filter((current) => current.id !== row.id),
                          )
                          if (selected?.id === row.id) setSelected(null)
                          setDeleteId(null)
                          setNotice('Saved report deleted.')
                        })
                      }
                    >
                      Confirm delete
                    </Button>
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={blocked}
                      onClick={() => setDeleteId(null)}
                    >
                      Cancel
                    </Button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </div>
      </details>
      {reviewedPlan && (
        <form
          className="space-y-3 border-t border-[var(--border-soft)] pt-4"
          onSubmit={(event) => {
            event.preventDefault()
            save(false)
          }}
        >
          <p className="text-sm font-medium">
            Save the reviewed report settings
          </p>
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <Label htmlFor="saved-report-name">Report name</Label>
              <input
                id="saved-report-name"
                className={`${inputClass} mt-1 w-full`}
                value={name}
                onChange={(event) => setName(event.target.value)}
                required
                maxLength={120}
                disabled={blocked}
              />
            </div>
            {reviewedPlan.subject === 'pmExecution' && (
              <div>
                <Label htmlFor="saved-report-period">
                  Dates on future runs
                </Label>
                <select
                  id="saved-report-period"
                  className={`${inputClass} mt-1 w-full`}
                  value={periodMode}
                  disabled={blocked}
                  onChange={(event) => setPeriodMode(event.target.value)}
                >
                  {periods.map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>
          {reviewedPlan.subject === 'pmExecution' && periodMode !== 'Fixed' && (
            <p className="text-sm text-[var(--text-secondary)]">
              The selected period is recalculated using Philippine time whenever
              you open this report. Review the actual dates before running. A
              month must include a scheduled CPMP cycle for the selected
              categories.
            </p>
          )}
          <div className="flex flex-wrap gap-2">
            <Button type="submit" disabled={blocked || !name.trim()}>
              Save as new report
            </Button>
            {selected && (
              <Button
                type="button"
                variant="secondary"
                disabled={blocked || !name.trim()}
                onClick={() => save(true)}
              >
                Replace settings for {selected.name}
              </Button>
            )}
          </div>
        </form>
      )}
      {error && (
        <p role="alert" className="text-sm text-red-700">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="text-sm text-[var(--text-secondary)]">
          {notice}
        </p>
      )}
    </Card>
  )
}
