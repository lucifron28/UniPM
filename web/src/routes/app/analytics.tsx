import { createFileRoute, Navigate } from '@tanstack/react-router'

export const Route = createFileRoute('/app/analytics')({
  component: AnalyticsPage,
})

function AnalyticsPage() {
  return <Navigate to="/app/dashboard" search={{ view: 'question' }} replace />
}
