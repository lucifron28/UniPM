import { useEffect, useId, useRef, type ReactNode } from 'react'
import { ArrowLeft } from 'lucide-react'
import { Button } from '@/components/ui/button'

export function ScheduleBatchPanel({
  title,
  description,
  onClose,
  children,
}: {
  title: string
  description?: string | undefined
  onClose: () => void
  children: ReactNode
}) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    dialog.showModal()
    return () => {
      if (dialog.open) dialog.close()
    }
  }, [])
  return (
    <dialog
      ref={ref}
      id="schedule-batch-panel"
      aria-labelledby={titleId}
      className="fixed inset-y-0 right-0 left-auto m-0 ml-auto h-dvh max-h-none w-full max-w-3xl overflow-hidden border-0 bg-white p-0 text-[var(--text-primary)] shadow-2xl backdrop:bg-black/40"
      onCancel={(event) => {
        event.preventDefault()
        onClose()
      }}
    >
      <div className="flex h-full flex-col">
        <header className="shrink-0 space-y-3 border-b border-[var(--border-soft)] p-4 sm:p-6">
          <Button
            type="button"
            variant="secondary"
            autoFocus
            onClick={onClose}
            className="min-h-12 cursor-pointer gap-2 border-[var(--primary)] text-[var(--primary)] hover:bg-[var(--primary)] hover:text-white"
          >
            <ArrowLeft className="size-5" aria-hidden="true" /> Back to batches
          </Button>
          <h2 id={titleId} className="text-xl font-bold">
            {title}
          </h2>
          {description && (
            <p className="text-sm text-[var(--text-secondary)]">
              {description}
            </p>
          )}
        </header>
        <div className="min-h-0 flex-1 overflow-auto p-4 sm:p-6">
          {children}
        </div>
      </div>
    </dialog>
  )
}
