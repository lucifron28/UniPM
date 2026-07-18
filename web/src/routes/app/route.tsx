import { createFileRoute, redirect } from '@tanstack/react-router'
import { AppShell } from '@/components/layout/app-shell'
export const Route = createFileRoute('/app')({
  beforeLoad: ({ context, location }) => {
    if (!context.getAccessToken())
      throw redirect({
        to: '/login',
        search: {
          redirect: location.pathname.startsWith('/app')
            ? location.pathname
            : '/',
        },
      })
  },
  component: AppShell,
})
