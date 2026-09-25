import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'

type RegistryBreakpoint = 'md' | 'lg'

type RegistryPagination = {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
}

type RegistryResultsPanelProps = {
  label: string
  breakpoint: RegistryBreakpoint
  desktopContent: ReactNode
  mobileContent: ReactNode
  pagination?: RegistryPagination
}

function registryDesktopViewportClassName(breakpoint: RegistryBreakpoint) {
  return breakpoint === 'md' ? 'md:min-h-[960px]' : 'lg:min-h-[960px]'
}

export function RegistryLoadingPanel({
  breakpoint,
  label,
  children,
}: {
  breakpoint: RegistryBreakpoint
  label?: string
  children: ReactNode
}) {
  return (
    <Card
      role="status"
      aria-label={label}
      className={
        'space-y-3 p-5 shadow-none ' +
        registryDesktopViewportClassName(breakpoint)
      }
    >
      {children}
    </Card>
  )
}

export function RegistryResultsPanel({
  label,
  breakpoint,
  desktopContent,
  mobileContent,
  pagination,
}: RegistryResultsPanelProps) {
  const desktopClassName =
    breakpoint === 'md' ? 'hidden md:block' : 'hidden lg:block'
  const mobileClassName = breakpoint === 'md' ? 'md:hidden' : 'lg:hidden'
  const pageCount = pagination
    ? Math.max(1, Math.ceil(pagination.total / pagination.pageSize))
    : 1
  const firstResult = pagination
    ? Math.min(
        (pagination.page - 1) * pagination.pageSize + 1,
        pagination.total,
      )
    : 0
  const lastResult = pagination
    ? Math.min(pagination.page * pagination.pageSize, pagination.total)
    : 0

  return (
    <section aria-label={label + ' results'} className="space-y-4">
      <Card
        className={'hidden overflow-hidden p-0 shadow-none ' + desktopClassName}
      >
        <div
          className={
            'overflow-x-auto ' + registryDesktopViewportClassName(breakpoint)
          }
        >
          {desktopContent}
        </div>
      </Card>
      <div className={'space-y-3 ' + mobileClassName}>{mobileContent}</div>
      {pagination && (
        <nav
          aria-label={label + ' pagination'}
          className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between"
        >
          <div
            aria-live="polite"
            className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-[var(--text-secondary)]"
          >
            <p>
              Showing {firstResult}-{lastResult} of {pagination.total}
            </p>
            <p>
              Page {pagination.page} of {pageCount}
            </p>
          </div>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="secondary"
              disabled={pagination.page <= 1}
              onClick={() => pagination.onPageChange(pagination.page - 1)}
            >
              Previous
            </Button>
            <Button
              type="button"
              variant="secondary"
              disabled={pagination.page >= pageCount}
              onClick={() => pagination.onPageChange(pagination.page + 1)}
            >
              Next
            </Button>
          </div>
        </nav>
      )}
    </section>
  )
}
