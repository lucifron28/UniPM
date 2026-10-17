import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { Card } from '@/components/ui/card'

export type SummaryMetricTone =
  'brand' | 'information' | 'success' | 'warning' | 'danger' | 'neutral'

const iconToneClasses: Record<SummaryMetricTone, string> = {
  brand:
    'bg-[color-mix(in_srgb,var(--primary)_9%,white)] text-[var(--primary)]',
  information: 'bg-blue-50 text-blue-700',
  success: 'bg-emerald-50 text-emerald-700',
  warning: 'bg-amber-50 text-amber-800',
  danger: 'bg-red-50 text-red-700',
  neutral: 'bg-[var(--surface-muted)] text-[var(--text-neutral)]',
}

export function SummaryMetricCard({
  label,
  value,
  icon: Icon,
  tone,
  note,
}: {
  label: string
  value: ReactNode
  icon: LucideIcon
  tone: SummaryMetricTone
  note?: string
}) {
  return (
    <Card className="content-card rounded-lg p-4">
      <div className="relative min-h-[4.75rem] pr-11">
        <p className="min-h-8 text-xs leading-4 font-semibold tracking-[0.04em] text-[var(--text-neutral)] uppercase">
          {label}
        </p>
        <p className="mt-2 text-3xl leading-none font-bold tracking-tight text-[var(--text-primary)]">
          {value}
        </p>
        {note && (
          <p className="mt-2 text-xs leading-4 text-[var(--text-secondary)]">
            {note}
          </p>
        )}
        <span
          aria-hidden="true"
          className={`absolute top-0 right-0 inline-flex size-9 items-center justify-center rounded-md ${iconToneClasses[tone]}`}
        >
          <Icon className="size-4" />
        </span>
      </div>
    </Card>
  )
}
