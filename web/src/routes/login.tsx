import { createFileRoute, Link } from '@tanstack/react-router'
import { z } from 'zod'
import { Card } from '@/components/ui/card'
const search = z.object({ redirect: z.string().startsWith('/').optional() })
export const Route = createFileRoute('/login')({
  validateSearch: search,
  component: LoginPlaceholder,
})
function LoginPlaceholder() {
  const { redirect } = Route.useSearch()
  return (
    <main className="mx-auto max-w-xl py-10">
      <Card>
        <h1 className="text-2xl font-semibold">
          Login integration is deferred
        </h1>
        <p className="mt-3 text-slate-600">
          Real credential submission and session restoration are planned for the
          next focused branch.
        </p>
        {redirect && (
          <p className="mt-3 text-sm text-slate-500">
            After a future login, this app may return to the requested internal
            page.
          </p>
        )}
        <Link className="mt-6 inline-block underline" to="/">
          Return to foundation page
        </Link>
      </Card>
    </main>
  )
}
