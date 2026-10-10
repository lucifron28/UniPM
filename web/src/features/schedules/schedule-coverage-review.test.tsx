import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { ScheduleCoverageReview } from '@/features/schedules/schedule-coverage-review'
import { server } from '@/test/server'

describe('schedule coverage review', () => {
  it('shows the asset, department, category, cycle, and coverage-boundary reason', async () => {
    configureApiRuntime({
      getAccessToken: () => 'synthetic-test-token',
      getSessionGeneration: () => 0,
      refreshAccessToken: async () => null,
      onTerminalUnauthorized: () => undefined,
    })
    server.use(
      http.get('*/api/v1/schedules/coverage-review', () =>
        HttpResponse.json({
          year: 2026,
          page: 1,
          pageSize: 10,
          total: 1,
          items: [
            {
              assetId: '11111111-1111-4111-8111-111111111111',
              assetCode: 'FE-CCMS-01',
              department: 'CCMS',
              assetCategory: 'fire-extinguisher',
              pmCycle: '2026-05',
              reason:
                'No schedule or enrollment deferral exists for this past PM cycle, and the approved coverage start date is not configured.',
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
        <ScheduleCoverageReview />
      </QueryClientProvider>,
    )

    expect(
      await screen.findByRole('heading', {
        name: 'Unconfirmed past-cycle coverage',
      }),
    ).toBeInTheDocument()
    expect(screen.getByRole('table')).toBeInTheDocument()
    expect(screen.getByRole('row', { name: /FE-CCMS-01/ })).toHaveTextContent(
      'CCMSfire-extinguisherMay 2026',
    )
    expect(screen.getByRole('row', { name: /FE-CCMS-01/ })).toHaveTextContent(
      /approved coverage start date is not configured/,
    )
    expect(screen.getByText('1 cycle to review')).toBeInTheDocument()
  })
})
