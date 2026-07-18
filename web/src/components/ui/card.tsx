import type { HTMLAttributes } from 'react'
import { cn } from '@/lib/utils'
export const Card = ({
  className,
  ...props
}: HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn(
      'rounded-lg border border-slate-200 bg-white p-6 shadow-sm',
      className,
    )}
    {...props}
  />
)
