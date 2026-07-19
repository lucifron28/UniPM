import type { LabelHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

export function Label({
  className,
  ...props
}: LabelHTMLAttributes<HTMLLabelElement>) {
  return (
    <label
      className={cn(
        'text-xs font-semibold tracking-[0.08em] text-[var(--text-secondary)] uppercase',
        className,
      )}
      {...props}
    />
  )
}
