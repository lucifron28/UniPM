import { cn } from '@/lib/utils'

export function BrandMark({
  compact = false,
  className,
}: {
  compact?: boolean
  className?: string
}) {
  return (
    <div
      className={cn(
        'flex items-center text-[var(--text-primary)]',
        compact ? 'gap-2' : 'flex-col',
        className,
      )}
    >
      <img
        src="/unipm-logo.png"
        alt=""
        className={
          compact ? 'size-10 object-contain' : 'h-24 w-28 object-contain'
        }
      />
      <div className={compact ? 'leading-tight' : 'text-center'}>
        <p
          className={cn(
            'font-extrabold tracking-[-0.045em]',
            compact ? 'text-2xl' : 'text-3xl',
          )}
        >
          <span className="text-[var(--primary)]">Uni</span>PM
        </p>
        {compact && (
          <p className="text-[0.6rem] font-semibold tracking-[0.12em] text-[var(--text-neutral)] uppercase">
            Institutional operations
          </p>
        )}
      </div>
    </div>
  )
}
