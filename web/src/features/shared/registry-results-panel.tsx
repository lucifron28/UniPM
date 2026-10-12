import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'

type RegistryBreakpoint = 'md' | 'lg'
type RegistryViewportSize = 'compact' | 'standard'

type RegistryPagination = {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
}

type RegistryResultsPanelProps = {
  label: string
  breakpoint: RegistryBreakpoint
  viewportSize: RegistryViewportSize
  desktopContent: ReactNode
  mobileContent: ReactNode
  isUpdating?: boolean
  pagination?: RegistryPagination
}

function registryDesktopViewportClassName(
  breakpoint: RegistryBreakpoint,
  viewportSize: RegistryViewportSize,
) {
  if (breakpoint === 'md') {
    return viewportSize === 'compact' ? 'md:h-[544px]' : 'md:h-[760px]'
  }
  return viewportSize === 'compact' ? 'lg:h-[544px]' : 'lg:h-[760px]'
}

export function RegistryLoadingPanel({
  breakpoint,
  viewportSize,
  label,
  children,
}: {
  breakpoint: RegistryBreakpoint
  viewportSize: RegistryViewportSize
  label?: string
  children: ReactNode
}) {
  return (
    <Card
      role="status"
      aria-label={label}
      className={
        'space-y-3 p-5 shadow-none ' +
        registryDesktopViewportClassName(breakpoint, viewportSize)
      }
    >
      {children}
    </Card>
  )
}

export function RegistryResultsPanel({
  label,
  breakpoint,
  viewportSize,
  desktopContent,
  mobileContent,
  isUpdating = false,
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
      <p
        className="min-h-5 text-sm text-[var(--text-neutral)]"
        role={isUpdating ? 'status' : undefined}
        aria-live="polite"
      >
        {isUpdating ? 'Updating results...' : null}
      </p>
      <Card
        className={'hidden overflow-hidden p-0 shadow-none ' + desktopClassName}
      >
        <div
          className={
            'overflow-auto ' +
            registryDesktopViewportClassName(breakpoint, viewportSize)
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
