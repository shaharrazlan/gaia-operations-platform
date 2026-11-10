import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { ApiContext } from './api/ApiContext.jsx'
import { createFakeApi } from './test/fakeApi.js'
import App from './App.jsx'

function renderApp(api = createFakeApi()) {
  render(
    <ApiContext value={api}>
      <App />
    </ApiContext>,
  )
  return api
}

describe('App — view switch', () => {
  it('R12.AC1: the top bar switches Calculator ↔ Manage operations with one click each way', async () => {
    renderApp()
    expect(await screen.findByRole('button', { name: 'Calculate' })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Manage operations' }))
    expect(await screen.findByRole('heading', { name: 'Manage operations' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Calculate' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Calculator' }))
    expect(await screen.findByRole('button', { name: 'Calculate' })).toBeInTheDocument()
  })

  it('R12.AC1: the current view is marked in the top bar', async () => {
    renderApp()

    expect(screen.getByRole('button', { name: 'Calculator' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Manage operations' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('R12.AC7: returning to the calculator loads the operation list again', async () => {
    const api = renderApp()
    await screen.findByRole('button', { name: 'Calculate' })
    const callsBefore = api.getOperations.mock.calls.length

    await userEvent.click(screen.getByRole('button', { name: 'Manage operations' }))
    await userEvent.click(screen.getByRole('button', { name: 'Calculator' }))
    await screen.findByRole('button', { name: 'Calculate' })

    // The calculator asks for the enabled list again (the manage view's own calls aren't counted here).
    const calculatorCalls = api.getOperations.mock.calls.slice(callsBefore).filter((args) => !args[0]?.includeDisabled)
    expect(calculatorCalls).toHaveLength(1)
  })
})
