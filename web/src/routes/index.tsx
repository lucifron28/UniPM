import { createFileRoute, Link } from '@tanstack/react-router'
import { Card } from '@/components/ui/card'
export const Route = createFileRoute('/')({ component: FoundationPage })
function FoundationPage() {
  return (
    <main className="mx-auto max-w-3xl py-10">
      <Card>
        <p className="text-sm font-medium text-slate-500">UniPM web frontend</p>
        <h1 className="mt-2 text-3xl font-semibold">Application foundation</h1>
        <p className="mt-4 max-w-2xl text-slate-600">
          This branch establishes the route shell, typed API boundary, tests,
          and client tooling. Operational modules are intentionally not
          implemented here.
        </p>
        <div className="mt-6 flex gap-4">
          <Link className="underline" to="/login">
            View login placeholder
          </Link>
          <Link className="underline" to="/app/dashboard">
            Open protected placeholder
          </Link>
        </div>
      </Card>
    </main>
  )
}
