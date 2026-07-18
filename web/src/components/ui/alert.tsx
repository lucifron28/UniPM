import type { HTMLAttributes } from 'react'
import { cn } from '@/lib/utils'
export const Alert = ({
  className,
  ...props
}: HTMLAttributes<HTMLDivElement>) => (
  <div
    role="alert"
    className={cn(
      'rounded-md border border-slate-300 bg-slate-50 p-4 text-sm text-slate-700',
      className,
    )}
    {...props}
  />
)
