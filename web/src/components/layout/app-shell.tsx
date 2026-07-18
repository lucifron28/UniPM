import { Link, Outlet } from '@tanstack/react-router'
import { Badge } from '@/components/ui/badge'
export function AppShell() {
  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white">
        <nav
          aria-label="Primary"
          className="mx-auto flex max-w-5xl items-center justify-between gap-4 px-4 py-4"
        >
          <Link to="/" className="font-semibold">
            UniPM
          </Link>
          <div className="flex items-center gap-4">
            <Link
              to="/login"
              className="text-sm underline-offset-4 hover:underline"
            >
              Login placeholder
            </Link>
            <Badge>Foundation</Badge>
          </div>
        </nav>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-10">
        <Outlet />
      </main>
    </div>
  )
}
