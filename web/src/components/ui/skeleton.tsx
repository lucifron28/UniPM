import { cn } from '@/lib/utils'
export const Skeleton = ({ className }: { className?: string }) => (
  <div
    aria-hidden="true"
    className={cn('animate-pulse rounded bg-[var(--input-surface)]', className)}
  />
)
