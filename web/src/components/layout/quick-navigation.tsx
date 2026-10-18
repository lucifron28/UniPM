import { useEffect, useRef, useState } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent } from 'react'
import { useRouter } from '@tanstack/react-router'
import { Search, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  canReadSchedules,
  canReviewForms,
} from '@/features/auth/app-route-access'

type Destination = {
  id: string
  title: string
  description: string
  keywords: string[]
  navigate: () => void
}

export function QuickNavigation({
  roles,
  sidebarCollapsed,
  onNavigate,
}: {
  roles: readonly string[]
  sidebarCollapsed: boolean
  onNavigate: () => void
}) {
  const router = useRouter()
  const [isOpen, setIsOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const dialogRef = useRef<HTMLDivElement>(null)
  const returnFocusRef = useRef<HTMLElement | null>(null)

  const destinations: Destination[] = [
    {
      id: 'dashboard',
      title: 'Dashboard',
      description: 'Choose a PM period and review its report.',
      keywords: ['home', 'report', 'overview'],
      navigate: () => void router.navigate({ to: '/app/dashboard' }),
    },
    {
      id: 'assets',
      title: 'Find asset',
      description: 'Search the asset registry.',
      keywords: ['asset', 'equipment', 'registry', 'search'],
      navigate: () => void router.navigate({ to: '/app/assets' }),
    },
    {
      id: 'inspection',
      title: 'Find inspection',
      description: 'Open recorded inspection results.',
      keywords: ['inspection', 'record', 'history'],
      navigate: () => void router.navigate({ to: '/app/inspections' }),
    },
    ...(canReadSchedules(roles)
      ? [
          {
            id: 'schedules',
            title: 'Schedules',
            description: 'Review PM cycles and schedule records.',
            keywords: ['schedule', 'pm cycle', 'history'],
            navigate: () => void router.navigate({ to: '/app/schedules' }),
          },
        ]
      : []),
    ...(roles.includes('GSD')
      ? [
          {
            id: 'ask-pm-results',
            title: 'Ask about PM results',
            description: 'Ask a question about preventive-maintenance data.',
            keywords: ['analytics', 'question', 'report', 'nla'],
            navigate: () =>
              void router.navigate({
                to: '/app/dashboard',
                search: { view: 'question' },
              }),
          },
          {
            id: 'assign-supervisor',
            title: 'Assign supervisor',
            description: 'Open GSD batches that need a Supervisor.',
            keywords: ['assign', 'supervisor', 'batch', 'gsd'],
            navigate: () =>
              void router.navigate({
                to: '/app/schedules',
                search: {
                  view: 'assignments',
                  assignmentFilter: 'needs-supervisor',
                },
              }),
          },
          {
            id: 'record-wms-number',
            title: 'Record WMS PM number',
            description:
              'Open acknowledged forms and check eligible follow-up rows.',
            keywords: ['wms', 'pm number', 'corrective', 'follow-up'],
            navigate: () =>
              void router.navigate({
                to: '/app/preventive-maintenance-forms',
                search: { status: 'Acknowledged' },
              }),
          },
        ]
      : []),
    ...(roles.includes('Supervisor') && !roles.includes('GSD')
      ? [
          {
            id: 'assign-inspector',
            title: 'Assign inspector',
            description: 'Open your batches that need an Inspector.',
            keywords: ['assign', 'inspector', 'worker', 'batch'],
            navigate: () =>
              void router.navigate({
                to: '/app/schedules',
                search: {
                  view: 'assignments',
                  assignmentFilter: 'needs-inspector',
                },
              }),
          },
        ]
      : []),
    ...(canReviewForms(roles)
      ? [
          {
            id: 'form-review',
            title: 'Form review',
            description: 'Review submitted preventive-maintenance forms.',
            keywords: ['form', 'review', 'acknowledgement'],
            navigate: () =>
              void router.navigate({ to: '/app/preventive-maintenance-forms' }),
          },
        ]
      : []),
  ]

  const normalizedQuery = query.trim().toLocaleLowerCase()
  const results = destinations.filter((destination) =>
    [destination.title, destination.description, ...destination.keywords]
      .join(' ')
      .toLocaleLowerCase()
      .includes(normalizedQuery),
  )

  useEffect(() => {
    if (isOpen) {
      inputRef.current?.focus()
      return
    }

    returnFocusRef.current?.focus()
    returnFocusRef.current = null
  }, [isOpen])

  useEffect(() => {
    function handleShortcut(event: globalThis.KeyboardEvent) {
      if (
        !(event.ctrlKey || event.metaKey) ||
        event.key.toLowerCase() !== 'k'
      ) {
        return
      }

      event.preventDefault()
      if (isOpen) {
        setIsOpen(false)
        return
      }

      returnFocusRef.current =
        document.activeElement instanceof HTMLElement
          ? document.activeElement
          : null
      setQuery('')
      setActiveIndex(0)
      setIsOpen(true)
    }

    document.addEventListener('keydown', handleShortcut)
    return () => document.removeEventListener('keydown', handleShortcut)
  }, [isOpen])

  const activeDestinationId = results[activeIndex]?.id
  useEffect(() => {
    if (!isOpen || !activeDestinationId) return

    const activeOption = document.getElementById(
      `quick-navigation-option-${activeDestinationId}`,
    )
    if (typeof activeOption?.scrollIntoView === 'function') {
      activeOption.scrollIntoView({ block: 'nearest' })
    }
  }, [activeDestinationId, isOpen])

  function open() {
    returnFocusRef.current =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null
    setQuery('')
    setActiveIndex(0)
    setIsOpen(true)
  }

  function close() {
    setIsOpen(false)
    setQuery('')
    setActiveIndex(0)
  }

  function selectDestination(destination: Destination | undefined) {
    if (!destination) return
    destination.navigate()
    onNavigate()
    close()
  }

  function handleDialogKeyDown(event: ReactKeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      event.preventDefault()
      close()
      return
    }

    if (event.key === 'Tab') {
      const focusable = [
        ...(dialogRef.current?.querySelectorAll<HTMLElement>(
          'input:not([disabled]), button:not([disabled])',
        ) ?? []),
      ].filter((element) => element.tabIndex >= 0)
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last?.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first?.focus()
      }
      return
    }

    if (event.target !== inputRef.current) return
    if (event.key === 'ArrowDown' && results.length > 0) {
      event.preventDefault()
      setActiveIndex((index) => (index + 1) % results.length)
    } else if (event.key === 'ArrowUp' && results.length > 0) {
      event.preventDefault()
      setActiveIndex((index) => (index - 1 + results.length) % results.length)
    } else if (event.key === 'Enter') {
      event.preventDefault()
      selectDestination(results[activeIndex])
    }
  }

  return (
    <>
      <button
        type="button"
        aria-label="Quick search"
        aria-keyshortcuts="Control+K Meta+K"
        aria-haspopup="dialog"
        aria-expanded={isOpen}
        aria-controls={isOpen ? 'quick-navigation-dialog' : undefined}
        title={sidebarCollapsed ? 'Quick search · Ctrl+K' : undefined}
        onClick={open}
        className="sidebar-nav-link relative z-10 flex min-h-11 w-full items-center gap-3 rounded-md px-3 py-2 text-left text-sm font-medium text-[var(--text-secondary)] hover:bg-[var(--surface-muted)]"
      >
        <Search aria-hidden="true" className="size-5 shrink-0" />
        <span className="sidebar-nav-label">Quick search</span>
        <kbd
          aria-hidden="true"
          className={`ml-auto rounded border border-[var(--border-soft)] px-1.5 py-0.5 text-[0.65rem] text-[var(--text-neutral)] ${sidebarCollapsed ? 'hidden' : 'hidden sm:inline-block'}`}
        >
          Ctrl K
        </kbd>
      </button>
      {isOpen && (
        <div
          className="fixed inset-0 z-[70] flex items-center justify-center bg-black/40 p-4"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) close()
          }}
        >
          <div
            ref={dialogRef}
            id="quick-navigation-dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="quick-navigation-title"
            onKeyDown={handleDialogKeyDown}
            className="flex max-h-[calc(100dvh-2rem)] w-full max-w-xl flex-col overflow-hidden rounded-xl border border-[var(--border-soft)] bg-white shadow-2xl"
          >
            <h2 id="quick-navigation-title" className="sr-only">
              Quick search
            </h2>
            <div className="flex items-center gap-3 border-b border-[var(--border-soft)] px-4">
              <Search
                aria-hidden="true"
                className="size-5 shrink-0 text-[var(--text-neutral)]"
              />
              <Input
                ref={inputRef}
                aria-label="Search pages and tasks"
                aria-controls="quick-navigation-results"
                aria-activedescendant={
                  results[activeIndex]
                    ? `quick-navigation-option-${results[activeIndex].id}`
                    : undefined
                }
                autoComplete="off"
                placeholder="Search pages or tasks"
                value={query}
                onChange={(event) => {
                  setQuery(event.target.value)
                  setActiveIndex(0)
                }}
                className="h-14 border-0 px-0 shadow-none focus-visible:border-0 focus-visible:ring-0"
              />
              <Button
                type="button"
                variant="secondary"
                aria-label="Close quick search"
                onClick={close}
                className="size-8 min-h-8 px-0"
              >
                <X aria-hidden="true" className="size-4" />
              </Button>
            </div>
            <div
              id="quick-navigation-results"
              role="listbox"
              aria-label="Available destinations"
              className="max-h-[min(68vh,36rem)] min-h-0 overflow-y-auto p-2"
            >
              {results.length > 0 ? (
                results.map((destination, index) => (
                  <button
                    key={destination.id}
                    id={`quick-navigation-option-${destination.id}`}
                    type="button"
                    role="option"
                    aria-selected={index === activeIndex}
                    tabIndex={-1}
                    onMouseEnter={() => setActiveIndex(index)}
                    onMouseDown={(event) => event.preventDefault()}
                    onClick={() => selectDestination(destination)}
                    className={`flex w-full flex-col rounded-lg px-3 py-2.5 text-left ${index === activeIndex ? 'bg-[var(--page-background)]' : 'hover:bg-[var(--page-background)]'}`}
                  >
                    <span className="font-semibold text-[var(--text-primary)]">
                      {destination.title}
                    </span>
                    <span className="mt-0.5 text-sm text-[var(--text-secondary)]">
                      {destination.description}
                    </span>
                  </button>
                ))
              ) : (
                <p
                  role="status"
                  className="px-3 py-6 text-center text-sm text-[var(--text-secondary)]"
                >
                  No destinations match. Try another task or page name.
                </p>
              )}
            </div>
            <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--border-soft)] px-4 py-2 text-xs text-[var(--text-neutral)]">
              <span>Use ↑ ↓ to move and Enter to open.</span>
              <span>Esc closes · Ctrl+K opens or closes</span>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
