import type { ReactNode, Ref } from 'react'

export function PageHeading({
  id,
  title,
  description,
  eyebrow,
  headingRef,
  tabIndex,
  actions,
}: {
  id: string
  title: ReactNode
  description?: ReactNode
  eyebrow?: string
  headingRef?: Ref<HTMLHeadingElement>
  tabIndex?: number
  actions?: ReactNode
}) {
  const content = (
    <div
      data-page-heading
      className="flex min-w-0 flex-wrap items-center justify-between gap-x-4 gap-y-3"
    >
      <div className="min-w-0">
        {eyebrow && (
          <p className="text-[0.68rem] font-semibold tracking-[0.09em] text-[var(--primary)] uppercase">
            {eyebrow}
          </p>
        )}
        <h1
          id={id}
          ref={headingRef}
          tabIndex={tabIndex}
          className="text-2xl font-bold tracking-tight text-[var(--text-primary)] outline-none sm:text-[1.75rem]"
        >
          {title}
        </h1>
        {description && (
          <p className="mt-1 hidden max-w-3xl text-sm text-[var(--text-neutral)] sm:block">
            {description}
          </p>
        )}
      </div>
      {actions && (
        <div className="flex max-w-full min-w-0 shrink-0 flex-wrap items-center gap-2">
          {actions}
        </div>
      )}
    </div>
  )

  return content
}
