import { createFileRoute } from '@tanstack/react-router'
import { AppShell } from '@/components/layout/app-shell'
import { requireAppAccess } from '@/routes/app/guard'
export const Route = createFileRoute('/app')({
  beforeLoad: ({ context, location }) => {
    requireAppAccess(context.getAccessToken, location.pathname)
  },
  component: AppShell,
})
