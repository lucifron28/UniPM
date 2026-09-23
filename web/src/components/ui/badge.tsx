import type { HTMLAttributes } from 'react'
import { cn } from '@/lib/utils'
export const Badge = ({
  className,
  variant = 'neutral',
  ...props
}: HTMLAttributes<HTMLSpanElement> & {
  variant?: 'neutral' | 'success' | 'warning' | 'danger'
}) => (
  <span
    className={cn(
      'inline-flex rounded-full px-2.5 py-1 text-xs font-medium',
      {
        neutral: 'bg-[var(--surface-muted)] text-[var(--text-neutral)]',
        success:
          'bg-[color-mix(in_srgb,var(--success)_12%,white)] text-[var(--success)]',
        warning:
          'bg-[color-mix(in_srgb,var(--warning)_12%,white)] text-[var(--warning)]',
        danger:
          'bg-[color-mix(in_srgb,var(--error)_12%,white)] text-[var(--error)]',
      }[variant],
      className,
    )}
    {...props}
  />
)
