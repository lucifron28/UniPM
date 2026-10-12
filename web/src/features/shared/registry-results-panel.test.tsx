import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RegistryResultsPanel } from '@/features/shared/registry-results-panel'

describe('registry results panel', () => {
  it('makes the desktop results scroller keyboard focusable and announces refreshes', () => {
    render(
      <RegistryResultsPanel
        label="Schedules"
        breakpoint="md"
        viewportSize="standard"
        isUpdating
        desktopContent={
          <table>
            <tbody>
              <tr>
                <td>FE-001</td>
              </tr>
            </tbody>
          </table>
        }
        mobileContent={<p>FE-001</p>}
      />,
    )

    const scrollRegion = screen.getByRole('region', {
      name: 'Schedules table',
    })
    expect(scrollRegion).toHaveAttribute('tabindex', '0')
    expect(scrollRegion.className).toContain('focus-visible:ring-2')
    expect(screen.getByRole('status')).toHaveTextContent('Updating results...')
  })
})
