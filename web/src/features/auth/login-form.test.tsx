import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { queryClient } from '@/app/query-client'
import { currentUserQueryKey } from '@/features/auth/current-user'
import { LoginForm } from '@/features/auth/login-form'
import { useAuthStore } from '@/stores/auth-store'
import { server } from '@/test/server'

const loginUrl = 'http://localhost:5000/api/v1/auth/login'
const user = {
  id: '11111111-1111-4111-8111-111111111111',
  email: 'fictional.inspector@example.test',
  displayName: 'Fictional Inspector',
  roles: ['Inspector'],
}
const session = {
  accessToken: 'synthetic-login-token',
  expiresAtUtc: '2026-07-18T12:00:00Z',
  user,
}

function setup(onAuthenticated = vi.fn()) {
  const actor = userEvent.setup()
  render(<LoginForm onAuthenticated={onAuthenticated} />)
  return { actor, onAuthenticated }
}

async function submit(
  actor: ReturnType<typeof userEvent.setup>,
  email: string,
  password: string,
) {
  await actor.type(screen.getByLabelText('Institutional email'), email)
  await actor.type(screen.getByLabelText('Password'), password)
  await actor.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('login form', () => {
  it('renders labeled, keyboard-usable email and password controls', async () => {
    const { actor } = setup()
    const email = screen.getByLabelText('Institutional email')
    const password = screen.getByLabelText('Password')

    expect(email).toHaveAttribute('type', 'email')
    expect(email).toHaveAttribute('autocomplete', 'email')
    expect(password).toHaveAttribute('type', 'password')
    expect(password).toHaveAttribute('autocomplete', 'current-password')
    await actor.tab()
    expect(email).toHaveFocus()
    await actor.tab()
    expect(password).toHaveFocus()
  })

  it('rejects invalid email and empty password without an HTTP request', async () => {
    let loginCount = 0
    server.use(
      http.post(loginUrl, () => {
        loginCount += 1
        return HttpResponse.json(session)
      }),
    )
    const { actor } = setup()
    await actor.type(screen.getByLabelText('Institutional email'), 'invalid')
    await actor.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(
      await screen.findByText('Enter a valid email address.'),
    ).toBeVisible()
    expect(await screen.findByText('Enter your password.')).toBeVisible()
    expect(loginCount).toBe(0)
  })

  it('trims email, preserves password, and seeds the in-memory session', async () => {
    const onAuthenticated = vi.fn()
    server.use(
      http.post(loginUrl, async ({ request }) => {
        expect(await request.json()).toEqual({
          email: 'fictional.inspector@example.test',
          password: '  Synthetic Password  ',
        })
        return HttpResponse.json(session)
      }),
    )
    const { actor } = setup(onAuthenticated)
    await submit(
      actor,
      '  fictional.inspector@example.test  ',
      '  Synthetic Password  ',
    )

    await vi.waitFor(() => expect(onAuthenticated).toHaveBeenCalledOnce())
    expect(useAuthStore.getState()).toMatchObject({
      status: 'authenticated',
      accessToken: session.accessToken,
    })
    expect(queryClient.getQueryData(currentUserQueryKey)).toEqual(user)
    expect(localStorage).toHaveLength(0)
    expect(sessionStorage).toHaveLength(0)
  })

  it('shows generic invalid credentials and clears the password field', async () => {
    server.use(
      http.post(loginUrl, () =>
        HttpResponse.json(
          { title: 'Unauthorized', detail: 'Account-specific detail.' },
          { status: 401 },
        ),
      ),
    )
    const { actor, onAuthenticated } = setup()
    await submit(
      actor,
      'fictional.inspector@example.test',
      'Synthetic Password',
    )

    expect(await screen.findByText('Invalid email or password.')).toBeVisible()
    expect(
      screen.queryByText('Account-specific detail.'),
    ).not.toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toHaveValue('')
    expect(onAuthenticated).not.toHaveBeenCalled()
  })

  it('maps safe validation errors to their fields', async () => {
    server.use(
      http.post(loginUrl, () =>
        HttpResponse.json(
          {
            title: 'Validation failed',
            errors: { Email: ['Use an institutional email address.'] },
          },
          { status: 400 },
        ),
      ),
    )
    const { actor } = setup()
    await submit(
      actor,
      'fictional.inspector@example.test',
      'Synthetic Password',
    )

    expect(
      await screen.findByText('Use an institutional email address.'),
    ).toBeVisible()
  })

  it.each([
    [403, 'Sign in is unavailable because this web origin is not allowed.'],
    [
      'network',
      'The service could not be reached. Check your connection and try again.',
    ],
  ])('shows a safe message for a %s failure', async (kind, expected) => {
    server.use(
      http.post(loginUrl, () =>
        kind === 'network'
          ? HttpResponse.error()
          : HttpResponse.json(
              { title: 'Forbidden' },
              { status: typeof kind === 'number' ? kind : 500 },
            ),
      ),
    )
    const { actor } = setup()
    await submit(
      actor,
      'fictional.inspector@example.test',
      'Synthetic Password',
    )

    expect(await screen.findByText(expected)).toBeVisible()
  })

  it('rejects malformed success data without authenticating', async () => {
    server.use(
      http.post(loginUrl, () =>
        HttpResponse.json({ ...session, accessToken: '' }),
      ),
    )
    const { actor, onAuthenticated } = setup()
    await submit(
      actor,
      'fictional.inspector@example.test',
      'Synthetic Password',
    )

    expect(
      await screen.findByText(
        'The authentication response could not be verified. Please try again.',
      ),
    ).toBeVisible()
    expect(useAuthStore.getState().accessToken).toBeNull()
    expect(onAuthenticated).not.toHaveBeenCalled()
  })
})
