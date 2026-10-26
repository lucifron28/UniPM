import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/api/problem-details'
import { SavedReports } from './saved-reports'

const api = vi.hoisted(() => ({
  create: vi.fn(),
  delete: vi.fn(),
  list: vi.fn(),
  update: vi.fn(),
}))

vi.mock('@/api/generated/endpoints', () => ({
  createSavedAnalyticsReport: api.create,
  deleteSavedAnalyticsReport: api.delete,
  listSavedAnalyticsReports: api.list,
  updateSavedAnalyticsReport: api.update,
}))

const reportId = '99999999-9999-4999-8999-999999999999'
const revision1 = '00000000-0000-4000-8000-000000000001'
const revision2 = '00000000-0000-4000-8000-000000000002'

function report(name: string, revision: string) {
  return {
    id: reportId,
    name,
    plan: {
      schemaVersion: 3,
      catalogVersion: 2,
      subject: 'assetInventory',
      mode: 'Aggregate',
      period: null,
      categories: ['fire-extinguisher'],
      measures: ['registeredAssetCount'],
      groupBy: [],
      baseFilters: [],
      matchingFilters: [],
      aggregateFilters: [],
      orderBy: [],
      top: null,
      presentation: 'Summary',
      columns: ['registeredAssetCount'],
    },
    periodMode: 'Fixed',
    revision,
    updatedAtUtc: '2026-10-26T00:00:00Z',
  }
}

describe('saved report controls', () => {
  beforeEach(() => {
    api.create.mockReset()
    api.delete.mockReset()
    api.list.mockReset().mockResolvedValue([])
    api.update.mockReset()
  })

  it('does not show an empty list when the saved-report request fails', async () => {
    api.list.mockRejectedValueOnce(
      new ApiError('Saved reports unavailable', 503),
    )

    render(
      <SavedReports
        reviewedPlan={null}
        disabled={false}
        onOpen={async () => true}
      />,
    )

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Saved reports unavailable',
    )
    expect(
      screen.queryByText(
        'No saved reports yet. Preview a report to save its settings.',
      ),
    ).not.toBeInTheDocument()
  })

  it('keeps a report after stale-delete conflict and retries with the reloaded revision', async () => {
    let current: ReturnType<typeof report> | null = report(
      'My inventory',
      revision1,
    )
    api.list.mockImplementation(async () => (current ? [{ ...current }] : []))
    api.delete.mockImplementation(
      async (_id: string, params: { revision: string }) => {
        if (params.revision !== current?.revision) {
          throw new ApiError(
            'This saved report changed. Reload My reports before editing it again.',
            409,
          )
        }
        current = null
      },
    )

    render(
      <SavedReports
        reviewedPlan={null}
        disabled={false}
        onOpen={async () => true}
      />,
    )
    fireEvent.click(await screen.findByText('My reports (1)'))

    current = report('Updated in another tab', revision2)
    fireEvent.click(screen.getByRole('button', { name: 'Delete', exact: true }))
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm delete', exact: true }),
    )
    expect(await screen.findByRole('alert')).toHaveTextContent('changed')
    expect(api.delete).toHaveBeenNthCalledWith(1, reportId, {
      revision: revision1,
    })

    fireEvent.click(screen.getByRole('button', { name: 'Reload my reports' }))
    expect(
      await screen.findByText('Updated in another tab'),
    ).toBeInTheDocument()
    fireEvent.click(
      screen.getByRole('button', { name: 'Confirm delete', exact: true }),
    )

    await waitFor(() => expect(current).toBeNull())
    expect(api.delete).toHaveBeenNthCalledWith(2, reportId, {
      revision: revision2,
    })
    expect(
      await screen.findByText(
        'No saved reports yet. Preview a report to save its settings.',
      ),
    ).toBeInTheDocument()
  })
})
