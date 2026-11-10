import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiContext } from '../api/ApiContext.jsx'
import { ApiError } from '../api/errors.js'
import { createFakeApi, sampleManagedOperations } from '../test/fakeApi.js'
import ManageView from '../views/ManageView.jsx'

// The form is tested inside the Manage view, because the ACs are about what the operator sees in the list.
function renderManage(overrides = {}) {
  const api = createFakeApi({ getOperations: vi.fn().mockResolvedValue(sampleManagedOperations), ...overrides })
  render(
    <ApiContext value={api}>
      <ManageView />
    </ApiContext>,
  )
  return api
}

const row = async (name) => (await screen.findByRole('rowheader', { name })).closest('tr')
const form = () => screen.getByRole('form', { name: /operation/i })

async function openAddForm() {
  await userEvent.click(await screen.findByRole('button', { name: 'Add operation' }))
  return form()
}

async function fillNewOperation() {
  const f = await openAddForm()
  await userEvent.type(within(f).getByLabelText('Id'), 'mean')
  await userEvent.type(within(f).getByLabelText('Name'), 'Mean')
  await userEvent.selectOptions(within(f).getByLabelText('Kind'), 'Arithmetic')
  await userEvent.type(within(f).getByLabelText('Field A label'), 'First')
  await userEvent.type(within(f).getByLabelText('Field B label'), 'Second')
  await userEvent.selectOptions(within(f).getByLabelText('Input type'), 'Number')
  await userEvent.type(within(f).getByLabelText('Formula'), '(A + B) / 2')
  return f
}

describe('OperationForm — add', () => {
  it('R12.AC3: saving a new operation sends the definition and adds it to the list', async () => {
    const api = renderManage()
    const f = await fillNewOperation()

    await userEvent.click(within(f).getByRole('button', { name: 'Save' }))

    expect(api.createOperation).toHaveBeenCalledWith({
      id: 'mean',
      name: 'Mean',
      kind: 'Arithmetic',
      fieldALabel: 'First',
      fieldBLabel: 'Second',
      inputType: 'Number',
      formula: '(A + B) / 2',
      isEnabled: true,
    })
    expect(within(await row('Mean')).getByText('(A + B) / 2')).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: /operation/i })).not.toBeInTheDocument()
  })

  it('R12.AC6: a rejected definition shows the server message next to the form and keeps the inputs', async () => {
    const error = new ApiError({ status: 400, code: 'formula_invalid', detail: "Unexpected end of formula at position 4.", field: 'formula' })
    renderManage({ createOperation: vi.fn().mockRejectedValue(error) })
    const f = await fillNewOperation()
    await userEvent.clear(within(f).getByLabelText('Formula'))
    await userEvent.type(within(f).getByLabelText('Formula'), 'A +')

    await userEvent.click(within(f).getByRole('button', { name: 'Save' }))

    expect(await within(f).findByRole('alert')).toHaveTextContent('Unexpected end of formula at position 4.')
    expect(within(f).getByLabelText('Formula')).toHaveValue('A +')
    expect(within(f).getByLabelText('Formula')).toHaveAttribute('aria-invalid', 'true')
    expect(within(f).getByLabelText('Id')).toHaveValue('mean')
    expect(screen.queryByRole('rowheader', { name: 'Mean' })).not.toBeInTheDocument()
  })

  it('T35: there is no Add button until the list has loaded', async () => {
    renderManage({ getOperations: vi.fn(() => new Promise(() => {})) })

    expect(await screen.findByText(/Loading operations/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add operation' })).not.toBeInTheDocument()
  })

  it('T35: Cancel closes the form without saving', async () => {
    const api = renderManage()
    const f = await openAddForm()

    await userEvent.click(within(f).getByRole('button', { name: 'Cancel' }))

    expect(api.createOperation).not.toHaveBeenCalled()
    expect(screen.queryByRole('form', { name: /operation/i })).not.toBeInTheDocument()
  })
})
