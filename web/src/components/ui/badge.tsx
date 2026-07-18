import type { HTMLAttributes } from 'react'
import { cn } from '@/lib/utils'
export const Badge = ({
  className,
  ...props
}: HTMLAttributes<HTMLSpanElement>) => (
  <span
    className={cn(
      'inline-flex rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-700',
      className,
    )}
    {...props}
  />
)
