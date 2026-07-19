import { createFileRoute, redirect } from '@tanstack/react-router'
import { LockKeyhole } from 'lucide-react'
import { z } from 'zod'
import { BrandMark } from '@/components/brand-mark'
import { Card } from '@/components/ui/card'
import {
  ensureSessionInitialized,
  hasAuthenticatedSession,
} from '@/features/auth/auth-session-service'
import { LoginForm } from '@/features/auth/login-form'
import { isInternalAppRedirect } from '@/routes/login-redirect'

const search = z.object({
  redirect: z
    .string()
    .optional()
    .transform((value) =>
      value && isInternalAppRedirect(value) ? value : undefined,
    ),
})

export const Route = createFileRoute('/login')({
  validateSearch: search,
  beforeLoad: async ({ search }) => {
    await ensureSessionInitialized()
    if (hasAuthenticatedSession()) {
      throw redirect({
        to: search.redirect ?? '/app/dashboard',
        replace: true,
      })
    }
  },
  component: LoginPage,
})

function LoginPage() {
  const { redirect } = Route.useSearch()
  const navigate = Route.useNavigate()

  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden bg-[var(--page-background)] px-5 py-12 sm:px-8">
      <div
        aria-hidden="true"
        className="absolute -top-24 -left-20 h-80 w-44 rounded-full bg-white/75 blur-3xl"
      />
      <div
        aria-hidden="true"
        className="absolute right-[-5rem] bottom-12 h-72 w-40 rounded-full bg-white/80 blur-3xl"
      />

      <div className="relative w-full max-w-[46rem]">
        <div className="mb-9 flex flex-col items-center">
          <BrandMark />
          <p className="mt-3 text-center text-base text-[var(--text-secondary)]">
            Institutional Management Portal
          </p>
        </div>

        <Card className="mx-auto w-full max-w-[40rem] px-7 py-8 shadow-[0_18px_48px_rgba(25,28,29,0.08)] sm:px-10 sm:py-10">
          <h1 className="text-2xl font-bold tracking-tight text-[var(--text-primary)]">
            Welcome back
          </h1>
          <p className="mt-1 text-[var(--text-secondary)]">
            Please enter your credentials to continue.
          </p>
          <LoginForm
            onAuthenticated={() =>
              navigate({
                to: redirect ?? '/app/dashboard',
                replace: true,
              })
            }
          />
        </Card>

        <div className="mt-10 text-center">
          <p className="inline-flex items-center gap-2 rounded-full bg-white/70 px-4 py-2 text-xs font-semibold tracking-[0.1em] text-[var(--text-neutral)] uppercase">
            <LockKeyhole
              aria-hidden="true"
              className="size-4 text-[var(--error)]"
            />
            Authorized institutional users only.
          </p>
        </div>
      </div>
    </main>
  )
}
