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

  it('T35: Id is editable when adding', async () => {
    renderManage()
    const f = await openAddForm()

    expect(within(f).getByLabelText('Id')).toBeEnabled()
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

async function openEditForm(name) {
  await userEvent.click(within(await row(name)).getByRole('button', { name: `Edit ${name}` }))
  return form()
}

describe('OperationForm — edit', () => {
  it('R12.AC4: a built-in operation can be renamed; its formula, kind and input type are read-only', async () => {
    const api = renderManage()
    const f = await openEditForm('Divide')

    expect(within(f).getByLabelText('Formula')).toBeDisabled()
    expect(within(f).getByLabelText('Kind')).toBeDisabled()
    expect(within(f).getByLabelText('Input type')).toBeDisabled()
    expect(within(f).getByLabelText('Id')).toBeDisabled()
    await userEvent.clear(within(f).getByLabelText('Name'))
    await userEvent.type(within(f).getByLabelText('Name'), 'Division')
    await userEvent.click(within(f).getByRole('button', { name: 'Save' }))

    expect(api.updateOperation).toHaveBeenCalledWith('divide', expect.objectContaining({ name: 'Division', formula: 'A / B', version: 'AAAAAAAAB9I=' }))
    expect(await row('Division')).toBeInTheDocument()
    expect(screen.queryByRole('rowheader', { name: 'Divide' })).not.toBeInTheDocument()
  })

  it('R12.AC4: a built-in code operation keeps formula null when renamed', async () => {
    const api = renderManage()
    const f = await openEditForm('Weather today')

    await userEvent.click(within(f).getByRole('button', { name: 'Save' }))

    expect(api.updateOperation).toHaveBeenCalledWith('weather', expect.objectContaining({ formula: null }))
  })

  it('R12.AC4: a custom operation can change its formula and the list shows it', async () => {
    const api = renderManage()
    const f = await openEditForm('Average')
    expect(within(f).getByLabelText('Formula')).toBeEnabled()

    await userEvent.clear(within(f).getByLabelText('Formula'))
    await userEvent.type(within(f).getByLabelText('Formula'), '(A + B + 1) / 2')
    await userEvent.click(within(f).getByRole('button', { name: 'Save' }))

    expect(api.updateOperation).toHaveBeenCalledWith('average', expect.objectContaining({ formula: '(A + B + 1) / 2' }))
    expect(within(await row('Average')).getByText('(A + B + 1) / 2')).toBeInTheDocument()
  })

  it('S7: while an operation is being edited, its row can not be toggled or deleted', async () => {
    renderManage()
    await openEditForm('Average')

    const average = await row('Average')
    expect(within(average).getByRole('checkbox', { name: 'Enabled: Average' })).toBeDisabled()
    expect(within(average).getByRole('button', { name: 'Delete Average' })).toBeDisabled()
    expect(within(await row('Hypotenuse')).getByRole('checkbox', { name: 'Enabled: Hypotenuse' })).toBeEnabled()
  })

  it('S7: opening the form puts the keyboard focus in it', async () => {
    renderManage()

    const f = await openEditForm('Average')

    expect(within(f).getByLabelText('Name')).toHaveFocus()
  })

  it('S7: a successful save clears an earlier error notice', async () => {
    const conflict = new ApiError({ status: 409, code: 'edit_conflict', detail: 'Changed by someone else.' })
    renderManage({ updateOperation: vi.fn().mockRejectedValueOnce(conflict).mockImplementation(async (id, d) => ({ ...d, id, isSystem: false, version: 'v3' })) })
    await userEvent.click(within(await row('Average')).getByRole('checkbox', { name: 'Enabled: Average' }))
    expect(await screen.findByRole('alert')).toBeInTheDocument()

    const f = await openEditForm('Hypotenuse')
    await userEvent.click(within(f).getByRole('button', { name: 'Save' }))

    await screen.findByRole('button', { name: 'Add operation' })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('R12.AC8: an edit conflict shows the message, keeps the inputs, and Reload loads the latest version', async () => {
    const conflict = new ApiError({ status: 409, code: 'edit_conflict', detail: 'Conflict.' })
    const latest = { ...sampleManagedOperations[3], name: 'Average (renamed elsewhere)', version: 'AAAAAAAACAA=' }
    const getOperations = vi
      .fn()
      .mockResolvedValueOnce(sampleManagedOperations)
      .mockResolvedValueOnce([...sampleManagedOperations.slice(0, 3), latest, sampleManagedOperations[4]])
    const api = renderManage({ getOperations, updateOperation: vi.fn().mockRejectedValueOnce(conflict).mockImplementation(async (id, d) => ({ ...d, id, isSystem: false, version: 'v3' })) })
    const f = await openEditForm('Average')
    await userEvent.clear(within(f).getByLabelText('Name'))
    await userEvent.type(within(f).getByLabelText('Name'), 'My mean')

    await userEvent.click(within(f).getByRole('button', { name: 'Save' }))

    expect(await within(f).findByRole('alert')).toHaveTextContent('This operation was changed by someone else. Reload to see the latest version.')
    expect(within(f).getByLabelText('Name')).toHaveValue('My mean')

    await userEvent.click(within(f).getByRole('button', { name: 'Reload' }))

    expect(await within(f).findByDisplayValue('Average (renamed elsewhere)')).toBeInTheDocument()
    expect(within(f).queryByRole('alert')).not.toBeInTheDocument()
    await userEvent.click(within(f).getByRole('button', { name: 'Save' }))
    expect(api.updateOperation).toHaveBeenLastCalledWith('average', expect.objectContaining({ version: 'AAAAAAAACAA=' }))
  })
})
