import { Button } from '@/components/ui/button'
import type { RegistryZoom } from '@/features/shared/registry-zoom-state'

export function RegistryZoomControls({
  label,
  zoom,
  onDecrease,
  onIncrease,
  onReset,
}: {
  label: string
  zoom: RegistryZoom
  onDecrease: () => void
  onIncrease: () => void
  onReset: () => void
}) {
  return (
    <div
      role="group"
      aria-label={`${label} zoom controls`}
      className="flex shrink-0 items-center gap-1 self-end sm:self-auto"
    >
      <Button
        type="button"
        variant="secondary"
        aria-label={`Decrease ${label} zoom`}
        disabled={zoom === 90}
        onClick={onDecrease}
        className="size-10 min-h-10 px-0 text-lg"
      >
        −
      </Button>
      <Button
        type="button"
        variant="secondary"
        aria-label={`Reset ${label} zoom to 100 percent`}
        title="Reset zoom to 100%"
        onClick={onReset}
        className="min-h-10 min-w-14 px-2 tabular-nums"
      >
        <span aria-live="polite" aria-atomic="true">
          {zoom}%
        </span>
      </Button>
      <Button
        type="button"
        variant="secondary"
        aria-label={`Increase ${label} zoom`}
        disabled={zoom === 130}
        onClick={onIncrease}
        className="size-10 min-h-10 px-0 text-lg"
      >
        +
      </Button>
    </div>
  )
}
