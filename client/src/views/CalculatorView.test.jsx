import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiContext } from '../api/ApiContext.jsx'
import { ApiError, ServiceUnavailableError } from '../api/errors.js'
import { createFakeApi } from '../test/fakeApi.js'
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

async function calculate(a, b) {
  await screen.findByRole('combobox', { name: 'Operation' })
  const [fieldA, fieldB] = screen.getAllByRole('textbox')
  await userEvent.type(fieldA, a)
  await userEvent.type(fieldB, b)
  await userEvent.click(screen.getByRole('button', { name: 'Calculate' }))
}

describe('CalculatorView — calculate', () => {
  it('R5.AC3: Calculate sends the inputs to the server and shows the result on the same screen', async () => {
    const api = renderCalculator(createFakeApi({ execute: vi.fn().mockResolvedValue({ result: '5' }) }))

    await calculate('2', '3')

    expect(api.execute).toHaveBeenCalledWith('add', '2', '3')
    expect(await screen.findByRole('status')).toHaveTextContent('5')
  })

  it('R6.AC1: the shown result is exactly the value the server returned (the client computes nothing)', async () => {
    renderCalculator(createFakeApi({ execute: vi.fn().mockResolvedValue({ result: '42 (from server)' }) }))

    await calculate('1', '1')

    expect(await screen.findByRole('status')).toHaveTextContent('42 (from server)')
  })

  it('R5.AC4: a server error is shown near the form and no result is shown', async () => {
    const execute = vi
      .fn()
      .mockResolvedValueOnce({ result: '3.5' })
      .mockRejectedValueOnce(new ApiError({ status: 422, code: 'domain_error', detail: 'Division by zero.' }))
    renderCalculator(createFakeApi({ execute }))

    await calculate('7', '2')
    expect(await screen.findByRole('status')).toHaveTextContent('3.5')
    await userEvent.click(screen.getByRole('button', { name: 'Calculate' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Division by zero.')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('R5.AC4: a validation error marks the field it is about', async () => {
    const error = new ApiError({ status: 400, code: 'validation_error', detail: 'Field A must be a number.', field: 'fieldA' })
    renderCalculator(createFakeApi({ execute: vi.fn().mockRejectedValue(error) }))

    await calculate('abc', '2')

    await screen.findByRole('alert')
    const [fieldA, fieldB] = screen.getAllByRole('textbox')
    expect(fieldA).toHaveAttribute('aria-invalid', 'true')
    expect(fieldB).not.toHaveAttribute('aria-invalid', 'true')
  })

  it('R5.AC5: an unreachable server shows "Service unavailable" instead of failing silently', async () => {
    renderCalculator(createFakeApi({ execute: vi.fn().mockRejectedValue(new ServiceUnavailableError()) }))

    await calculate('1', '2')

    expect(await screen.findByRole('alert')).toHaveTextContent(/Service unavailable/)
  })

  it('R5.AC5: an unreachable server while loading the operations is reported too', async () => {
    renderCalculator(createFakeApi({ getOperations: vi.fn().mockRejectedValue(new ServiceUnavailableError()) }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/Service unavailable/)
  })

  it('R5.AC5: "Try again" loads the operation list again once the server is back', async () => {
    const getOperations = vi
      .fn()
      .mockRejectedValueOnce(new ServiceUnavailableError())
      .mockResolvedValueOnce([{ id: 'add', name: 'Add', fieldALabel: 'A', fieldBLabel: 'B' }])
    renderCalculator(createFakeApi({ getOperations }))

    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('combobox', { name: 'Operation' })).toBeInTheDocument()
    expect(getOperations).toHaveBeenCalledTimes(2)
  })

  it('R5.AC4: editing the field the server complained about clears its invalid mark', async () => {
    const error = new ApiError({ status: 400, code: 'validation_error', detail: 'Field A must be a number.', field: 'fieldA' })
    renderCalculator(createFakeApi({ execute: vi.fn().mockRejectedValue(error) }))
    await calculate('abc', '2')
    await screen.findByRole('alert')
    const [fieldA] = screen.getAllByRole('textbox')

    await userEvent.type(fieldA, '1')

    expect(fieldA).not.toHaveAttribute('aria-invalid', 'true')
  })

  it('T27: the button is disabled while waiting for the server', async () => {
    let finish
    const execute = vi.fn(() => new Promise((resolve) => (finish = resolve)))
    renderCalculator(createFakeApi({ execute }))

    await calculate('1', '2')

    expect(screen.getByRole('button', { name: /Calculat/ })).toBeDisabled()
    expect(screen.getByRole('combobox', { name: 'Operation' })).toBeDisabled()
    finish({ result: '3' })
    expect(await screen.findByRole('status')).toHaveTextContent('3')
    expect(screen.getByRole('button', { name: 'Calculate' })).toBeEnabled()
  })
})
