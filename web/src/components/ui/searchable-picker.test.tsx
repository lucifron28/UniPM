import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { SearchablePicker } from '@/components/ui/searchable-picker'

const departments = [
  { value: 'gsd', label: 'General Services Department' },
  { value: 'registrar', label: 'Office of the University Registrar' },
]

describe('SearchablePicker', () => {
  it('filters local options, supports keyboard selection, and clears to All', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()

    render(
      <SearchablePicker
        label="Department"
        value="registrar"
        options={departments}
        allLabel="All departments"
        onChange={onChange}
      />,
    )

    const picker = screen.getByRole('combobox', { name: 'Department' })
    expect(picker).toHaveValue('Office of the University Registrar')

    await user.click(picker)
    await user.type(picker, 'General Services')
    expect(
      screen.getByRole('option', { name: 'General Services Department' }),
    ).toBeVisible()
    expect(
      screen.queryByRole('option', {
        name: 'Office of the University Registrar',
      }),
    ).toBeNull()

    await user.keyboard('{ArrowDown}{Enter}')
    expect(onChange).toHaveBeenLastCalledWith('gsd', departments[0])

    await user.click(picker)
    expect(screen.getByRole('listbox')).toBeVisible()
    await user.keyboard('{Home}{Enter}')
    expect(onChange).toHaveBeenLastCalledWith(undefined, undefined)

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
    await user.click(picker)
    expect(screen.getByRole('listbox')).toBeVisible()
  })

  it('preserves the selected option when reopened and provides a clear action', async () => {
    const user = userEvent.setup()

    function ControlledPicker() {
      const [value, setValue] = useState('registrar')
      return (
        <SearchablePicker
          label="Department"
          value={value}
          options={departments}
          allLabel="All departments"
          onChange={(nextValue) => setValue(nextValue ?? '')}
        />
      )
    }

    render(<ControlledPicker />)
    const picker = screen.getByRole('combobox', { name: 'Department' })

    await user.click(picker)
    await user.type(picker, 'General Services')
    await user.keyboard('{ArrowDown}{Enter}')
    expect(picker).toHaveValue('General Services Department')

    await user.click(picker)
    expect(
      screen.getByRole('option', { name: 'General Services Department' }),
    ).toHaveAttribute('aria-selected', 'true')

    await user.click(
      screen.getByRole('button', { name: 'Clear department selection' }),
    )
    expect(picker).toHaveValue('All departments')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('closes with Escape and shows a bounded popup near the viewport edge', async () => {
    const user = userEvent.setup()
    const rect = {
      x: 20,
      y: 680,
      top: 680,
      bottom: 720,
      left: 20,
      right: 300,
      width: 280,
      height: 40,
      toJSON: () => ({}),
    }
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue(
      rect,
    )

    render(
      <SearchablePicker
        label="Department"
        options={departments}
        allLabel="All departments"
        onChange={vi.fn()}
      />,
    )

    const picker = screen.getByRole('combobox', { name: 'Department' })
    await user.click(picker)
    const listbox = screen.getByRole('listbox')
    const popup = listbox.parentElement

    expect(popup?.style.maxHeight).toBe('280px')
    expect(popup?.style.bottom).not.toBe('')

    await user.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull())
  })

  it('lets keyboard users reach retry and load-more controls', async () => {
    const user = userEvent.setup()
    const onRetry = vi.fn()
    const onLoadMore = vi.fn()

    render(
      <SearchablePicker
        label="Inspector"
        options={[]}
        errorMessage="Inspector options could not be loaded."
        onRetry={onRetry}
        hasMore
        onLoadMore={onLoadMore}
        onChange={vi.fn()}
      />,
    )

    await user.click(screen.getByRole('combobox', { name: 'Inspector' }))
    await user.keyboard('{Tab}')
    const retry = screen.getByRole('button', { name: 'Retry' })
    expect(retry).toHaveFocus()
    expect(screen.getByRole('combobox', { name: 'Inspector' })).toHaveAttribute(
      'aria-expanded',
      'true',
    )
    await user.keyboard('{Enter}')
    expect(onRetry).toHaveBeenCalledOnce()
    await user.keyboard('{Tab}')
    const loadMore = screen.getByRole('button', { name: 'Load more options' })
    expect(loadMore).toHaveFocus()
    await user.keyboard('{Enter}')
    expect(onLoadMore).toHaveBeenCalledOnce()
    await user.keyboard('{Escape}')
    expect(
      screen.queryByRole('button', { name: 'Load more options' }),
    ).toBeNull()
    expect(screen.getByRole('combobox', { name: 'Inspector' })).toHaveFocus()
  })
})
