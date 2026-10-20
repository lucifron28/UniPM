import type { HTMLAttributes } from 'react'
import { cn } from '@/lib/utils'
export const Card = ({
  className,
  ...props
}: HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn(
      'rounded-lg border border-[var(--border-soft)] bg-white p-5 shadow-none',
      className,
    )}
    {...props}
  />
)
