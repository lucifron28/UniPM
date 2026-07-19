import { forwardRef, type InputHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

export const Input = forwardRef<
  HTMLInputElement,
  InputHTMLAttributes<HTMLInputElement>
>(({ className, ...props }, ref) => (
  <input
    ref={ref}
    className={cn(
      'h-12 w-full rounded-lg border border-transparent bg-[var(--input-surface)] px-4 text-[var(--text-primary)] transition outline-none placeholder:text-[var(--text-neutral)] focus-visible:border-[var(--primary)] focus-visible:ring-2 focus-visible:ring-[color-mix(in_srgb,var(--primary)_25%,transparent)] disabled:cursor-not-allowed disabled:opacity-60 aria-invalid:border-[var(--error)] aria-invalid:ring-2 aria-invalid:ring-[color-mix(in_srgb,var(--error)_20%,transparent)]',
      className,
    )}
    {...props}
  />
))
Input.displayName = 'Input'
