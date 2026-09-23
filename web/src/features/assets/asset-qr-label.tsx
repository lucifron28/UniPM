import { useRef } from 'react'
import { Copy, Download, Printer } from 'lucide-react'
import { QRCodeSVG } from 'qrcode.react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import './asset-qr-label.css'

export type AssetQrLabelProps = {
  qrCodeValue: string | null | undefined
  assetCode: string
  assetCategory: string
  department?: string | null
  building?: string | null
  location?: string | null
}

function displayValue(value: string | null | undefined) {
  return value?.trim() ? value : 'Not recorded'
}

export function AssetQrLabel({
  qrCodeValue,
  assetCode,
  assetCategory,
  department,
  building,
  location,
}: AssetQrLabelProps) {
  const labelRef = useRef<HTMLElement>(null)
  const hasQrCode = typeof qrCodeValue === 'string' && qrCodeValue.length > 0

  const downloadQr = () => {
    const svg = labelRef.current?.querySelector('svg[data-asset-qr-image]')
    if (!svg) return

    let url: string | null = null
    let link: HTMLAnchorElement | null = null

    try {
      const source = new XMLSerializer().serializeToString(svg)
      url = URL.createObjectURL(
        new Blob([source], { type: 'image/svg+xml;charset=utf-8' }),
      )
      link = document.createElement('a')
      link.href = url
      link.download = `${assetCode.replace(/[^a-zA-Z0-9._-]+/g, '-') || 'asset'}-qr.svg`
      document.body.append(link)
      link.click()
      toast.success('QR code downloaded.')
    } catch {
      toast.error('QR code could not be downloaded.')
    } finally {
      link?.remove()
      if (url) {
        const downloadUrl = url
        window.setTimeout(() => URL.revokeObjectURL(downloadUrl), 0)
      }
    }
  }

  const copyIdentifier = async () => {
    if (typeof qrCodeValue !== 'string' || qrCodeValue.length === 0) return

    try {
      await navigator.clipboard.writeText(qrCodeValue)
      toast.success('QR identifier copied.')
    } catch {
      toast.error('QR identifier could not be copied.')
    }
  }

  return (
    <section
      ref={labelRef}
      aria-label={`Asset QR label for ${assetCode}`}
      className="asset-qr-label rounded-xl border border-[var(--border-soft)] bg-white p-6 shadow-sm"
    >
      <div className="grid gap-6 sm:grid-cols-[auto_1fr] sm:items-center">
        <div className="flex min-h-48 items-center justify-center rounded-lg bg-white">
          {hasQrCode ? (
            <QRCodeSVG
              value={qrCodeValue}
              size={216}
              level="M"
              marginSize={4}
              title={`QR code for ${assetCode}`}
              data-asset-qr-image=""
              className="h-auto max-w-full"
            />
          ) : (
            <p className="text-center text-sm text-[var(--text-secondary)]">
              QR code not generated
            </p>
          )}
        </div>
        <div className="min-w-0">
          <p className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
            Asset label
          </p>
          <p className="mt-1 text-xl font-bold break-all text-[var(--text-primary)]">
            {assetCode}
          </p>
          <dl className="mt-4 grid gap-x-5 gap-y-3 text-sm sm:grid-cols-2">
            {[
              ['Category', assetCategory],
              ['Department', department],
              ['Building', building],
              ['Location', location],
            ].map(([label, value]) => (
              <div key={label} className="min-w-0">
                <dt className="font-semibold text-[var(--text-neutral)]">
                  {label}
                </dt>
                <dd className="mt-0.5 break-words text-[var(--text-primary)]">
                  {displayValue(value)}
                </dd>
              </div>
            ))}
          </dl>
        </div>
      </div>
      <div className="asset-qr-label-actions mt-6 flex flex-wrap gap-2 border-t border-[var(--border-soft)] pt-5">
        <Button
          type="button"
          variant="secondary"
          disabled={!hasQrCode}
          onClick={() => window.print()}
        >
          <Printer aria-hidden="true" className="mr-2 size-4" />
          Print label
        </Button>
        <Button
          type="button"
          variant="secondary"
          disabled={!hasQrCode}
          onClick={downloadQr}
        >
          <Download aria-hidden="true" className="mr-2 size-4" />
          Download QR
        </Button>
        <Button
          type="button"
          variant="secondary"
          disabled={!hasQrCode}
          onClick={() => void copyIdentifier()}
        >
          <Copy aria-hidden="true" className="mr-2 size-4" />
          Copy identifier
        </Button>
      </div>
    </section>
  )
}
