import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { ApiContext } from '../api/ApiContext.jsx'
import { createFakeApi } from '../api/fakeApi.js'
import CalculatorView from './CalculatorView.jsx'

function renderCalculator(api = createFakeApi()) {
  render(
    <ApiContext value={api}>
      <CalculatorView />
    </ApiContext>,
  )
  return api
}

describe('CalculatorView — form', () => {
  it('R5.AC1: the operation list comes from the API, not a hard-coded list', async () => {
    const api = renderCalculator()

    const select = await screen.findByRole('combobox', { name: 'Operation' })

    expect(api.getOperations).toHaveBeenCalledOnce()
    expect(within(select).getAllByRole('option').map((o) => o.textContent)).toEqual(['Add', 'Divide', 'Weather today'])
  })

  it('R5.AC2: the Field A and Field B labels follow the selected operation', async () => {
    renderCalculator()
    const select = await screen.findByRole('combobox', { name: 'Operation' })
    expect(screen.getByLabelText('First number')).toBeInTheDocument()

    await userEvent.selectOptions(select, 'weather')

    expect(screen.getByLabelText('Latitude')).toBeInTheDocument()
    expect(screen.getByLabelText('Longitude')).toBeInTheDocument()
  })

  it('T26: shows a message when no operations are available', async () => {
    renderCalculator(createFakeApi({ getOperations: async () => [] }))

    expect(await screen.findByText(/No operations are available/)).toBeInTheDocument()
  })
})
