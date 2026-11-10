import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import HistoryPanel from './HistoryPanel.jsx'

const divide = { id: 'divide', name: 'Divide', fieldALabel: 'Dividend', fieldBLabel: 'Divisor' }

const history = {
  previousRuns: [
    { fieldA: '9', fieldB: '3', result: '3', createdAtUtc: '2026-11-15T10:05:00Z' },
    { fieldA: '8', fieldB: '2', result: '4', createdAtUtc: '2026-11-15T10:04:00Z' },
    { fieldA: '7', fieldB: '2', result: '3.5', createdAtUtc: '2026-11-15T10:03:00Z' },
  ],
  monthCount: 12,
}

describe('HistoryPanel', () => {
  it('R8.AC1: shows the previous runs newest first, with the operation\'s labels, result and Israel time', () => {
    render(<HistoryPanel operation={divide} history={history} />)

    const table = screen.getByRole('table', { name: 'Previous runs of Divide' })
    expect(within(table).getAllByRole('columnheader').map((h) => h.textContent)).toEqual(['Dividend', 'Divisor', 'Result', 'Time'])
    const rows = within(table).getAllByRole('row').slice(1).map((r) => within(r).getAllByRole('cell').map((c) => c.textContent))
    expect(rows.map((r) => r.slice(0, 3))).toEqual([
      ['9', '3', '3'],
      ['8', '2', '4'],
      ['7', '2', '3.5'],
    ])
    expect(rows[0][3]).toContain('12:05') // 10:05 UTC = 12:05 in Israel (winter, UTC+2)
  })

  it('R8.AC2: says "No previous runs" when there are none', () => {
    render(<HistoryPanel operation={divide} history={{ previousRuns: [], monthCount: 1 }} />)

    expect(screen.getByText('No previous runs')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('R9.AC1: shows how many times the operation ran this month', () => {
    render(<HistoryPanel operation={divide} history={history} />)

    expect(screen.getByText(/Divide ran 12 times this month/)).toBeInTheDocument()
  })

  it('R9.AC1: uses "once" for a single run', () => {
    render(<HistoryPanel operation={divide} history={{ previousRuns: [], monthCount: 1 }} />)

    expect(screen.getByText(/Divide ran once this month/)).toBeInTheDocument()
  })

  it('R7.AC3: says history is unavailable when the server sent none', () => {
    render(<HistoryPanel operation={divide} history={null} />)

    expect(screen.getByText(/History is not available right now/)).toBeInTheDocument()
  })
})
