import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools'
import { RouterProvider } from '@tanstack/react-router'
import { Toaster } from 'sonner'
import { queryClient } from '@/app/query-client'
import { router } from '@/app/router'
import { SessionInitializer } from '@/features/auth/session-initializer'

export function AppProviders() {
  return (
    <QueryClientProvider client={queryClient}>
      <SessionInitializer />
      <RouterProvider router={router} />
      <Toaster richColors />
      {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
      {import.meta.env.DEV && (
        <TanStackRouterDevtools router={router} position="bottom-right" />
      )}
    </QueryClientProvider>
  )
}
