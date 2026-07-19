import type { HTMLAttributes } from 'react'
import { cn } from '@/lib/utils'
export const Alert = ({
  className,
  ...props
}: HTMLAttributes<HTMLDivElement>) => (
  <div
    role="alert"
    className={cn(
      'rounded-lg border border-[var(--border-soft)] bg-[var(--surface-muted)] p-4 text-sm text-[var(--text-neutral)]',
      className,
    )}
    {...props}
  />
)
