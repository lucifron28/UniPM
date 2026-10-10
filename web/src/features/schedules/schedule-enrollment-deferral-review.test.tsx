import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { ScheduleEnrollmentDeferralReview } from '@/features/schedules/schedule-enrollment-deferral-review'
import { server } from '@/test/server'

describe('schedule enrollment deferral review', () => {
  it('shows deferred cycle details, planning guidance, and review counts', async () => {
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
          total: '1',
          pendingCount: '1',
          reviewedCount: '0',
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
              status: 'Needs review',
              deferredAt: '2026-08-15T04:00:00Z',
              reviewedAt: null,
              reviewedByUserId: null,
              reviewedByDisplayName: null,
              reviewNote: null,
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
        name: 'Deferred asset enrollment',
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('FE-LOCKED-01')).toBeInTheDocument()
    expect(screen.getByText('August 2026')).toBeInTheDocument()
    expect(screen.getByText('November 2026')).toBeInTheDocument()
    expect(
      screen.getByText('Needs review', { selector: 'span' }),
    ).toBeInTheDocument()
    expect(screen.getByText('1 needs review')).toBeInTheDocument()
    expect(screen.getByText('0 reviewed')).toBeInTheDocument()
    expect(screen.getByText(/1 matching cycle$/)).toBeInTheDocument()
    expect(
      screen.getByText(
        'The batch already had an assigned Supervisor or Inspector.',
      ),
    ).toBeInTheDocument()
  })

  it('hides the panel when the API returns empty counts as strings', async () => {
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
          total: 0,
          pendingCount: '0',
          reviewedCount: '0',
          items: [],
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

    await waitFor(() =>
      expect(
        screen.queryByText('Checking for deferred asset cycles…'),
      ).not.toBeInTheDocument(),
    )
    expect(
      screen.queryByRole('heading', { name: 'Deferred asset enrollment' }),
    ).not.toBeInTheDocument()
  })

  it('lets GSD record a review and retains the reviewer, time, and note', async () => {
    configureApiRuntime({
      getAccessToken: () => 'synthetic-test-token',
      getSessionGeneration: () => 0,
      refreshAccessToken: async () => null,
      onTerminalUnauthorized: () => undefined,
    })
    let reviewed = false
    let reviewNote: string | null = null
    server.use(
      http.get('*/api/v1/schedules/enrollment-deferrals', ({ request }) => {
        const onlyPending =
          new URL(request.url).searchParams.get('status') === 'NeedsReview'
        const items =
          reviewed && onlyPending
            ? []
            : [
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
                  status: reviewed ? 'Reviewed' : 'Needs review',
                  deferredAt: '2026-08-15T04:00:00Z',
                  reviewedAt: reviewed ? '2026-08-16T04:00:00Z' : null,
                  reviewedByUserId: reviewed
                    ? '22222222-2222-4222-8222-222222222222'
                    : null,
                  reviewedByDisplayName: reviewed ? 'GSD User' : null,
                  reviewNote,
                },
              ]

        return HttpResponse.json({
          page: 1,
          pageSize: 10,
          total: items.length,
          pendingCount: reviewed ? 0 : 1,
          reviewedCount: reviewed ? 1 : 0,
          items,
        })
      }),
      http.post(
        '*/api/v1/schedules/enrollment-deferrals/:assetId/:pmCycle/review',
        async ({ request }) => {
          const body = (await request.json()) as { note?: string | null }
          reviewNote = body.note ?? null
          reviewed = true
          return new HttpResponse(null, { status: 204 })
        },
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

    fireEvent.change(await screen.findByLabelText('Optional review note'), {
      target: { value: 'Reviewed with GSD.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Mark reviewed' }))

    expect(
      await screen.findByText('No deferred cycles match this review status.'),
    ).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Review status'), {
      target: { value: 'Reviewed' },
    })
    expect(
      await screen.findByText(/Review note: Reviewed with GSD\./),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/Reviewed Aug 16, 2026, 12:00 PM by GSD User/),
    ).toBeInTheDocument()
  })
})
