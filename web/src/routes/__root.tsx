import {
  createRootRouteWithContext,
  Link,
  Outlet,
} from '@tanstack/react-router'
import type { QueryClient } from '@tanstack/react-query'
import { Alert } from '@/components/ui/alert'
import { Card } from '@/components/ui/card'
type RouterContext = {
  queryClient: QueryClient
  getAccessToken: () => string | null
}
export const Route = createRootRouteWithContext<RouterContext>()({
  component: () => <Outlet />,
  pendingComponent: () => (
    <main
      className="flex min-h-screen items-center justify-center p-6"
      aria-live="polite"
      role="status"
    >
      Restoring your session...
    </main>
  ),
  errorComponent: ({ reset }) => (
    <main className="mx-auto max-w-xl p-6">
      <Card>
        <Alert>
          Unable to load this page. No server details are displayed.
        </Alert>
        <button className="mt-4 underline" onClick={reset}>
          Try again
        </button>
      </Card>
    </main>
  ),
  notFoundComponent: () => (
    <main className="mx-auto max-w-xl p-6">
      <Card>
        <h1 className="text-xl font-semibold">Page not found</h1>
        <p className="mt-2 text-slate-600">
          This route is not part of the current web foundation.
        </p>
        <Link className="mt-4 inline-block underline" to="/">
          Return to UniPM
        </Link>
      </Card>
    </main>
  ),
})
