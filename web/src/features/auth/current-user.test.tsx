import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { configureApiRuntime } from '@/api/http-client'
import { queryClient } from '@/app/query-client'
import { UserIdentity } from '@/components/layout/app-shell'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

describe('current user query', () => {
  it('fetches missing identity with the in-memory Bearer token', async () => {
    useAuthStore.getState().establishSession('synthetic-current-user-token')
    configureApiRuntime({
      getAccessToken: () => useAuthStore.getState().accessToken,
      getSessionGeneration: () => 0,
      refreshAccessToken: async () => null,
      onTerminalUnauthorized: () => undefined,
    })
    server.use(
      http.get('http://localhost:5000/api/v1/auth/me', ({ request }) => {
        expect(request.headers.get('authorization')).toBe(
          'Bearer synthetic-current-user-token',
        )
        return HttpResponse.json({
          id: '11111111-1111-4111-8111-111111111111',
          email: 'fictional.supervisor@example.test',
          displayName: 'Fictional Supervisor',
          roles: ['Supervisor'],
        })
      }),
    )

    render(
      <QueryClientProvider client={queryClient}>
        <UserIdentity />
      </QueryClientProvider>,
    )

    expect(screen.getByText('Loading signed-in user.')).toBeInTheDocument()
    expect(await screen.findByText('Fictional Supervisor')).toBeVisible()
    expect(screen.getByText('fictional.supervisor@example.test')).toBeVisible()
    expect(screen.getByText('Supervisor')).toBeVisible()
    expect(useAuthStore.getState()).not.toHaveProperty('user')
    expect(useAuthStore.getState()).not.toHaveProperty('roles')
  })

  it('does not render an identity from a malformed current-user response', async () => {
    useAuthStore.getState().establishSession('synthetic-current-user-token')
    configureApiRuntime({
      getAccessToken: () => useAuthStore.getState().accessToken,
      getSessionGeneration: () => 0,
      refreshAccessToken: async () => null,
      onTerminalUnauthorized: () => undefined,
    })
    server.use(
      http.get('http://localhost:5000/api/v1/auth/me', () =>
        HttpResponse.json({
          id: 'not-a-user-id',
          email: 'not-an-email',
          displayName: '',
          roles: 'Inspector',
        }),
      ),
    )

    render(
      <QueryClientProvider client={queryClient}>
        <UserIdentity />
      </QueryClientProvider>,
    )

    expect(
      await screen.findByText(
        'Signed-in user details are temporarily unavailable.',
      ),
    ).toBeVisible()
    expect(screen.queryByText('not-an-email')).not.toBeInTheDocument()
  })
})
