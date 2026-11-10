import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiContext } from '../api/ApiContext.jsx'
import { ApiError } from '../api/errors.js'
import { createFakeApi, sampleManagedOperations } from '../test/fakeApi.js'
import ManageView from './ManageView.jsx'

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

afterEach(() => {
  vi.restoreAllMocks()
})

describe('ManageView — list', () => {
  it('R12.AC2: lists enabled and disabled operations with kind, labels and formula or "built-in"', async () => {
    const api = renderManage()

    const hypotenuse = await row('Hypotenuse')
    expect(api.getOperations).toHaveBeenCalledWith({ includeDisabled: true })
    expect(within(hypotenuse).getByText('sqrt(A^2 + B^2)')).toBeInTheDocument()
    expect(within(hypotenuse).getByText('Side a')).toBeInTheDocument()
    expect(within(hypotenuse).getByRole('checkbox', { name: 'Enabled: Hypotenuse' })).not.toBeChecked()
    expect(within(await row('Weather today')).getByText('built-in')).toBeInTheDocument()
    expect(within(await row('Divide')).getByText('A / B')).toBeInTheDocument()
    expect(within(await row('Average')).getByText('Arithmetic')).toBeInTheDocument()
  })

  it('R12.AC2: built-in operations have no Delete button', async () => {
    renderManage()

    expect(within(await row('Divide')).queryByRole('button', { name: /Delete/ })).not.toBeInTheDocument()
    expect(within(await row('Average')).getByRole('button', { name: 'Delete Average' })).toBeInTheDocument()
  })

  it('T34: a failed load shows the error with "Try again"', async () => {
    const getOperations = vi.fn().mockRejectedValueOnce(new Error('Service unavailable.')).mockResolvedValue(sampleManagedOperations)
    renderManage({ getOperations })

    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }))

    expect(await row('Average')).toBeInTheDocument()
  })
})

describe('ManageView — enable, disable, delete', () => {
  it('R12.AC5: disabling sends the change with the loaded version and the row shows it disabled', async () => {
    const api = renderManage()

    await userEvent.click(within(await row('Average')).getByRole('checkbox', { name: 'Enabled: Average' }))

    expect(api.updateOperation).toHaveBeenCalledWith('average', expect.objectContaining({ isEnabled: false, version: 'AAAAAAAAB9Q=' }))
    expect(within(await row('Average')).getByRole('checkbox', { name: 'Enabled: Average' })).not.toBeChecked()
  })

  it('R12.AC5: enabling a disabled operation restores it', async () => {
    const api = renderManage()

    await userEvent.click(within(await row('Hypotenuse')).getByRole('checkbox', { name: 'Enabled: Hypotenuse' }))

    expect(api.updateOperation).toHaveBeenCalledWith('hypotenuse', expect.objectContaining({ isEnabled: true }))
    expect(within(await row('Hypotenuse')).getByRole('checkbox', { name: 'Enabled: Hypotenuse' })).toBeChecked()
  })

  it('T34: a rejected toggle shows the server message and reloads the list', async () => {
    const conflict = new ApiError({ status: 409, code: 'edit_conflict', detail: 'This operation was changed by someone else. Reload to see the latest version.' })
    const api = renderManage({ updateOperation: vi.fn().mockRejectedValue(conflict) })

    await userEvent.click(within(await row('Average')).getByRole('checkbox', { name: 'Enabled: Average' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('changed by someone else')
    expect(api.getOperations).toHaveBeenCalledTimes(2)
  })

  it('R4.AC3: Delete asks for confirmation, calls the API and removes the row', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const api = renderManage()

    await userEvent.click(within(await row('Average')).getByRole('button', { name: 'Delete Average' }))

    expect(api.deleteOperation).toHaveBeenCalledWith('average')
    expect(screen.queryByRole('rowheader', { name: 'Average' })).not.toBeInTheDocument()
  })

  it('R4.AC3: cancelling the confirmation deletes nothing', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const api = renderManage()

    await userEvent.click(within(await row('Average')).getByRole('button', { name: 'Delete Average' }))

    expect(api.deleteOperation).not.toHaveBeenCalled()
    expect(screen.getByRole('rowheader', { name: 'Average' })).toBeInTheDocument()
  })
})
