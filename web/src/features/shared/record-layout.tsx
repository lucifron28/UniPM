import type { ReactNode, Ref } from 'react'
import { ChevronDown, ClipboardList } from 'lucide-react'

export function RecordHeader({
  id,
  eyebrow,
  title,
  description,
  status,
  headingRef,
}: {
  id: string
  eyebrow: string
  title: ReactNode
  description?: ReactNode
  status?: ReactNode
  headingRef?: Ref<HTMLHeadingElement>
}) {
  return (
    <header className="record-header">
      <div className="record-header-icon" aria-hidden="true">
        <ClipboardList className="size-6" />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-xs font-semibold text-[var(--primary)]">{eyebrow}</p>
        <h1
          id={id}
          ref={headingRef}
          tabIndex={headingRef ? -1 : undefined}
          className="mt-1 text-2xl font-bold break-words text-[var(--text-primary)] outline-none sm:text-3xl"
        >
          {title}
        </h1>
        {description && (
          <div className="mt-2 max-w-[65ch] text-sm text-[var(--text-secondary)]">
            {description}
          </div>
        )}
      </div>
      {status && <div className="shrink-0 self-start">{status}</div>}
    </header>
  )
}

export function RecordField({
  label,
  value,
}: {
  label: string
  value: ReactNode
}) {
  return (
    <div className="record-field">
      <dt>{label}</dt>
      <dd>{value == null || value === '' ? 'Not recorded' : value}</dd>
    </div>
  )
}

export function RecordSection({
  title,
  children,
  className = '',
}: {
  title: string
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`record-section ${className}`}>
      <h2 className="record-section-heading">{title}</h2>
      <div className="record-section-body">{children}</div>
    </section>
  )
}

export function RecordMetadata({
  title = 'Additional details',
  children,
}: {
  title?: string
  children: ReactNode
}) {
  return (
    <details className="record-metadata">
      <summary>
        <h2 className="text-sm font-semibold">{title}</h2>
        <ChevronDown className="size-4" aria-hidden="true" />
      </summary>
      <div className="record-section-body">{children}</div>
    </details>
  )
}

export function FormLifecycle({ status }: { status: string }) {
  const stages = ['Draft', 'Submitted', 'Acknowledged']
  const current = stages.indexOf(status)
  return (
    <ol className="record-lifecycle" aria-label="Form lifecycle">
      {stages.map((stage, index) => (
        <li
          key={stage}
          aria-current={stage === status ? 'step' : undefined}
          data-state={
            index < current
              ? 'complete'
              : index === current
                ? 'current'
                : 'pending'
          }
        >
          <span aria-hidden="true">{index + 1}</span>
          {stage}
        </li>
      ))}
    </ol>
  )
}
