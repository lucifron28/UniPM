import { Slot } from '@radix-ui/react-slot'
import type { ButtonHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

export function Button({
  className,
  asChild,
  variant = 'primary',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  asChild?: boolean
  variant?: 'primary' | 'secondary'
}) {
  const Component = asChild ? Slot : 'button'
  return (
    <Component
      className={cn(
        'inline-flex min-h-10 items-center justify-center rounded-lg px-4 text-sm font-semibold transition-[background-color,color,border-color,box-shadow,transform] duration-150 ease-out outline-none focus-visible:ring-2 focus-visible:ring-[var(--primary-active)] focus-visible:ring-offset-2 active:scale-[0.98] disabled:transform-none disabled:cursor-not-allowed disabled:opacity-50',
        variant === 'primary'
          ? 'bg-[var(--primary)] text-white hover:bg-[var(--primary-strong)]'
          : 'border border-[var(--border-soft)] bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]',
        className,
      )}
      {...props}
    />
  )
}
