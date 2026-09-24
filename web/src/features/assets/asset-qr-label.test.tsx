import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AssetQrLabel } from '@/features/assets/asset-qr-label'

vi.mock('qrcode.react', () => ({
  QRCodeSVG: ({ value, title }: { value: string; title: string }) => (
    <svg
      role="img"
      aria-label={title}
      data-asset-qr-image=""
      data-encoded-value={value}
    />
  ),
}))

function replaceProperty(target: object, key: string, value: unknown) {
  const original = Object.getOwnPropertyDescriptor(target, key)
  Object.defineProperty(target, key, { configurable: true, value })

  return () => {
    if (original) Object.defineProperty(target, key, original)
    else Reflect.deleteProperty(target, key)
  }
}

afterEach(() => vi.restoreAllMocks())

describe('AssetQrLabel', () => {
  it('uses the exact backend QR value and supports print, download, and copy', async () => {
    const qrCodeValue =
      'unipm://assets/11111111-1111-4111-8111-111111111111?source=backend'
    const actor = userEvent.setup()
    const writeText = vi.fn().mockResolvedValue(undefined)
    const createObjectURL = vi.fn().mockReturnValue('blob:asset-qr')
    const revokeObjectURL = vi.fn()
    const restoreClipboard = replaceProperty(navigator, 'clipboard', {
      writeText,
    })
    const restoreCreateObjectURL = replaceProperty(
      URL,
      'createObjectURL',
      createObjectURL,
    )
    const restoreRevokeObjectURL = replaceProperty(
      URL,
      'revokeObjectURL',
      revokeObjectURL,
    )
    const print = vi.spyOn(window, 'print').mockImplementation(() => {})
    let downloadedFile = ''
    let downloadedHref = ''
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      downloadedFile = this.download
      downloadedHref = this.href
    })

    try {
      render(
        <AssetQrLabel
          qrCodeValue={qrCodeValue}
          assetCode="FE-001"
          assetCategory="Fire extinguisher"
          department="GSD"
          building="Main Building"
          location="Ground floor"
        />,
      )

      expect(
        screen.getByRole('img', { name: 'QR code for FE-001' }),
      ).toHaveAttribute('data-encoded-value', qrCodeValue)
      expect(screen.getByText('FE-001')).toBeInTheDocument()
      expect(screen.getByText('Fire extinguisher')).toBeInTheDocument()
      expect(screen.getByText('GSD')).toBeInTheDocument()
      expect(screen.getByText('Main Building')).toBeInTheDocument()
      expect(screen.getByText('Ground floor')).toBeInTheDocument()

      await actor.click(screen.getByRole('button', { name: 'Print label' }))
      expect(print).toHaveBeenCalledOnce()

      await actor.click(screen.getByRole('button', { name: 'Download QR' }))
      expect(createObjectURL).toHaveBeenCalledOnce()
      expect(createObjectURL).toHaveBeenCalledWith(expect.any(Blob))
      expect(downloadedFile).toBe('FE-001-qr.svg')
      expect(downloadedHref).toBe('blob:asset-qr')

      await actor.click(screen.getByRole('button', { name: 'Copy identifier' }))
      await waitFor(() => expect(writeText).toHaveBeenCalledWith(qrCodeValue))
      await waitFor(() =>
        expect(revokeObjectURL).toHaveBeenCalledWith('blob:asset-qr'),
      )
    } finally {
      await new Promise((resolve) => setTimeout(resolve, 0))
      restoreClipboard()
      restoreCreateObjectURL()
      restoreRevokeObjectURL()
    }
  })

  it('does not offer a scannable label without a backend QR value', () => {
    render(
      <AssetQrLabel
        qrCodeValue={null}
        assetCode="FE-001"
        assetCategory="Fire extinguisher"
      />,
    )

    expect(screen.queryByRole('img')).not.toBeInTheDocument()
    expect(screen.getByText('QR code not generated')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Print label' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Download QR' })).toBeDisabled()
    expect(
      screen.getByRole('button', { name: 'Copy identifier' }),
    ).toBeDisabled()
  })
})
