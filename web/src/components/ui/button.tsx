import { Slot } from '@radix-ui/react-slot'
import type { ButtonHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

export function Button({
  className,
  asChild,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { asChild?: boolean }) {
  const Component = asChild ? Slot : 'button'
  return (
    <Component
      className={cn(
        'inline-flex min-h-10 items-center justify-center rounded-lg bg-[var(--primary)] px-4 text-sm font-semibold text-white transition outline-none hover:bg-[var(--primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--primary-active)] focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
      {...props}
    />
  )
}
