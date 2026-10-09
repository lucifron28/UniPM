import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { ScheduleEnrollmentDeferralReview } from '@/features/schedules/schedule-enrollment-deferral-review'
import { server } from '@/test/server'

describe('schedule enrollment deferral review', () => {
  it('shows the deferred PM cycle, next cycle, reason, and GSD review status', async () => {
    configureApiRuntime({
      getAccessToken: () => 'synthetic-test-token',
      getSessionGeneration: () => 0,
      refreshAccessToken: async () => null,
      onTerminalUnauthorized: () => undefined,
    })
    server.use(
      http.get('*/api/v1/schedules/enrollment-deferrals', () =>
        HttpResponse.json({
          page: 1,
          pageSize: 10,
          total: 1,
          items: [
            {
              assetId: '11111111-1111-4111-8111-111111111111',
              assetCode: 'FE-LOCKED-01',
              department: 'CCMS',
              assetCategory: 'fire-extinguisher',
              deferredPmCycle: '2026-08',
              nextEligiblePmCycle: '2026-11',
              reasonCode: 'BatchAssigned',
              reason:
                'The batch already had an assigned Supervisor or Inspector.',
              status: 'Needs GSD scheduling review',
              deferredAt: '2026-08-15T04:00:00Z',
            },
          ],
        }),
      ),
    )
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })

    render(
      <QueryClientProvider client={queryClient}>
        <ScheduleEnrollmentDeferralReview />
      </QueryClientProvider>,
    )

    expect(
      await screen.findByRole('heading', {
        name: 'Asset enrollment needs GSD review',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('FE-LOCKED-01')).toBeInTheDocument()
    expect(screen.getByText('August 2026')).toBeInTheDocument()
    expect(screen.getByText('November 2026')).toBeInTheDocument()
    expect(screen.getByText('Needs GSD scheduling review')).toBeInTheDocument()
    expect(
      screen.getByText(
        'The batch already had an assigned Supervisor or Inspector.',
      ),
    ).toBeInTheDocument()
  })
})
