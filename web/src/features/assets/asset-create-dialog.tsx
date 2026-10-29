import { useEffect, useRef, useState } from 'react'
import { X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { AssetCreate } from './asset-create'

export function AssetCreateDialog({ onClose }: { onClose: () => void }) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    dialog.showModal()
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      dialog.close()
      document.body.style.overflow = previousOverflow
    }
  }, [])

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby="asset-create-dialog-title"
      aria-describedby="asset-create-dialog-description"
      className="fixed inset-0 m-auto max-h-[90dvh] w-[calc(100%_-_2rem)] max-w-3xl overflow-y-auto rounded-xl border border-[var(--border-soft)] bg-white p-0 text-[var(--text-primary)] shadow-2xl backdrop:bg-black/40"
      onCancel={(event) => {
        event.preventDefault()
        if (!busy) onClose()
      }}
    >
      <header className="sticky top-0 z-10 flex items-start justify-between gap-4 border-b border-[var(--border-soft)] bg-white p-5">
        <div>
          <h2 id="asset-create-dialog-title" className="text-xl font-bold">
            Add asset
          </h2>
          <p
            id="asset-create-dialog-description"
            className="mt-1 text-sm text-[var(--text-secondary)]"
          >
            Enter the equipment details. Your registry filters will stay in
            place.
          </p>
        </div>
        <Button
          type="button"
          variant="secondary"
          aria-label="Close add asset"
          disabled={busy}
          onClick={onClose}
          className="cursor-pointer hover:bg-[var(--surface-muted)]"
        >
          <X className="size-5" aria-hidden="true" />
        </Button>
      </header>
      <AssetCreate
        embedded
        onCreated={onClose}
        onCancel={onClose}
        onBusyChange={setBusy}
      />
    </dialog>
  )
}
