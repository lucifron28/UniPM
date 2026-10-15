import { useContext } from 'react'
import type { ReactNode, Ref } from 'react'
import { createPortal } from 'react-dom'
import { PageHeadingHostContext } from '@/components/layout/page-heading-context'

export function PageHeading({
  id,
  title,
  description,
  eyebrow,
  headingRef,
  tabIndex,
}: {
  id: string
  title: ReactNode
  description?: ReactNode
  eyebrow?: string
  headingRef?: Ref<HTMLHeadingElement>
  tabIndex?: number
}) {
  const host = useContext(PageHeadingHostContext)
  const content = (
    <div data-page-heading className="min-w-0">
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
  )

  if (host === undefined) return content
  return host ? createPortal(content, host) : null
}
