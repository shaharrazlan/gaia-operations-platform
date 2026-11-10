import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiContext, useApi } from '../api/ApiContext.jsx'
import { ApiError, ServiceUnavailableError } from '../api/errors.js'
import { createFakeApi } from '../test/fakeApi.js'
import KeyGate, { KEY_STORAGE } from './KeyGate.jsx'

/** Stands in for the Manage view: shows which API it was given. */
function Protected() {
  const api = useApi()
  return <p>Protected area for {api.label}</p>
}

function renderGate(overrides = {}) {
  const scoped = createFakeApi({ label: 'keyed api', ...overrides })
  const api = createFakeApi({ label: 'open api', withManagementKey: vi.fn(() => scoped) })
  render(
    <ApiContext value={api}>
      <KeyGate>
        <Protected />
      </KeyGate>
    </ApiContext>,
  )
  return { api, scoped }
}

async function enterKey(key) {
  await userEvent.type(screen.getByLabelText('Management key'), key)
  await userEvent.click(screen.getByRole('button', { name: 'Unlock' }))
}

afterEach(() => {
  sessionStorage.clear()
})

describe('KeyGate', () => {
  it('R13.AC4: asks for the key before showing management actions', () => {
    renderGate()

    expect(screen.getByLabelText('Management key')).toHaveAttribute('type', 'password')
    expect(screen.queryByText(/Protected area/)).not.toBeInTheDocument()
  })

  it('R13.AC4: a wrong key shows "Invalid management key" and stays locked', async () => {
    renderGate({ getOperations: vi.fn().mockRejectedValue(new ApiError({ status: 401, code: 'unauthorized', detail: 'nope' })) })

    await enterKey('wrong')

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid management key')
    expect(screen.queryByText(/Protected area/)).not.toBeInTheDocument()
    expect(sessionStorage.getItem(KEY_STORAGE)).toBeNull()
  })

  it('R13.AC4: the right key unlocks the view, which then uses an API that sends the key', async () => {
    const { api, scoped } = renderGate()

    await enterKey('gaia-dev-key')

    expect(await screen.findByText('Protected area for keyed api')).toBeInTheDocument()
    expect(api.withManagementKey).toHaveBeenCalledWith('gaia-dev-key')
    expect(scoped.getOperations).toHaveBeenCalledWith({ includeDisabled: true })
  })

  it('T39: a server that cannot be reached is reported as such, not as a wrong key', async () => {
    renderGate({ getOperations: vi.fn().mockRejectedValue(new ServiceUnavailableError()) })

    await enterKey('gaia-dev-key')

    expect(await screen.findByRole('alert')).toHaveTextContent(/Service unavailable/)
  })

  it('R13.AC5: the key is kept for this tab only (sessionStorage), so the view opens unlocked again', async () => {
    renderGate()
    await enterKey('gaia-dev-key')
    await screen.findByText(/Protected area/)

    expect(sessionStorage.getItem(KEY_STORAGE)).toBe('gaia-dev-key')
    expect(localStorage.getItem(KEY_STORAGE)).toBeNull()
  })

  it('R13.AC5: a key already kept in this tab opens the view without asking', () => {
    sessionStorage.setItem(KEY_STORAGE, 'gaia-dev-key')

    renderGate()

    expect(screen.getByText('Protected area for keyed api')).toBeInTheDocument()
  })

  it('T39: if the server rejects a remembered key later (it was changed), the gate locks and asks again', async () => {
    sessionStorage.setItem(KEY_STORAGE, 'old-key')
    const rejected = new ApiError({ status: 401, code: 'unauthorized', detail: 'A valid management key is required.' })
    const scoped = createFakeApi({ getOperations: vi.fn().mockRejectedValue(rejected) })
    function LoadsList() {
      const keyed = useApi()
      keyed.getOperations({ includeDisabled: true }).catch(() => {})
      return <p>Protected area</p>
    }
    render(
      <ApiContext value={createFakeApi({ withManagementKey: vi.fn(() => scoped) })}>
        <KeyGate>
          <LoadsList />
        </KeyGate>
      </ApiContext>,
    )

    expect(await screen.findByLabelText('Management key')).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('Invalid management key')
    expect(sessionStorage.getItem(KEY_STORAGE)).toBeNull()
  })

  it('R13.AC5: Lock forgets the key and asks for it again', async () => {
    sessionStorage.setItem(KEY_STORAGE, 'gaia-dev-key')
    renderGate()

    await userEvent.click(screen.getByRole('button', { name: 'Lock' }))

    expect(screen.getByLabelText('Management key')).toBeInTheDocument()
    expect(screen.queryByText(/Protected area/)).not.toBeInTheDocument()
    expect(sessionStorage.getItem(KEY_STORAGE)).toBeNull()
  })
})
