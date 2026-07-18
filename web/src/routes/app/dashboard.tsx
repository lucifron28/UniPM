import { createFileRoute } from '@tanstack/react-router'
import { Card } from '@/components/ui/card'
export const Route = createFileRoute('/app/dashboard')({
  component: DashboardPlaceholder,
})
function DashboardPlaceholder() {
  return (
    <Card>
      <h1 className="text-2xl font-semibold">
        Protected dashboard placeholder
      </h1>
      <p className="mt-3 text-slate-600">
        The protected route boundary is active. Dashboard modules, records,
        charts, and operational data are deferred.
      </p>
    </Card>
  )
}
