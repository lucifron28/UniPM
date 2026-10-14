import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import { RegistryZoomControls } from '@/features/shared/registry-zoom'
import { useRegistryZoom } from '@/features/shared/registry-zoom-state'

function ZoomTestHarness() {
  const zoom = useRegistryZoom()

  return (
    <RegistryZoomControls
      label="Test records"
      zoom={zoom.zoom}
      onDecrease={zoom.decrease}
      onIncrease={zoom.increase}
      onReset={zoom.reset}
    />
  )
}

describe('registry record zoom', () => {
  beforeEach(() => {
    window.localStorage.removeItem('unipm.registry.recordsZoom')
  })

  it('steps by ten percent, stops at both limits, resets, and persists', async () => {
    const user = userEvent.setup()
    render(<ZoomTestHarness />)

    const decrease = screen.getByRole('button', {
      name: 'Decrease Test records zoom',
    })
    const reset = screen.getByRole('button', {
      name: 'Reset Test records zoom to 100 percent',
    })
    const increase = screen.getByRole('button', {
      name: 'Increase Test records zoom',
    })

    expect(reset).toHaveTextContent('100%')
    await user.click(increase)
    expect(reset).toHaveTextContent('110%')
    expect(window.localStorage.getItem('unipm.registry.recordsZoom')).toBe(
      '110',
    )

    for (let step = 0; step < 5; step += 1) await user.click(increase)
    expect(reset).toHaveTextContent('130%')
    expect(increase).toBeDisabled()

    for (let step = 0; step < 4; step += 1) await user.click(decrease)
    expect(reset).toHaveTextContent('90%')
    expect(decrease).toBeDisabled()

    await user.click(reset)
    expect(reset).toHaveTextContent('100%')
    expect(window.localStorage.getItem('unipm.registry.recordsZoom')).toBe(
      '100',
    )
  })

  it('restores a valid shared preference and ignores invalid stored values', () => {
    window.localStorage.setItem('unipm.registry.recordsZoom', '120')
    const { unmount } = render(<ZoomTestHarness />)
    expect(
      screen.getByRole('button', {
        name: 'Reset Test records zoom to 100 percent',
      }),
    ).toHaveTextContent('120%')

    unmount()
    window.localStorage.setItem('unipm.registry.recordsZoom', '125')
    render(<ZoomTestHarness />)
    expect(
      screen.getByRole('button', {
        name: 'Reset Test records zoom to 100 percent',
      }),
    ).toHaveTextContent('100%')
  })
})
