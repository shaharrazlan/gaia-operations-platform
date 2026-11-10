import { useMemo, useState } from 'react'
import { ApiContext, useApi } from '../api/ApiContext.jsx'
import { ApiError } from '../api/errors.js'

/** Where the key is kept: sessionStorage lives only as long as this browser tab (R13.AC5). */
export const KEY_STORAGE = 'gaia.managementKey'

// Storage can be blocked (private mode, strict settings); then the key simply isn't remembered.
function readKey() {
  try {
    return sessionStorage.getItem(KEY_STORAGE)
  } catch {
    return null
  }
}

function writeKey(key) {
  try {
    if (key === null) sessionStorage.removeItem(KEY_STORAGE)
    else sessionStorage.setItem(KEY_STORAGE, key)
  } catch {
    // not remembered; the operator types it again next time
  }
}

/**
 * Locks its children (the Manage view) behind the management key (R13.AC4). The key is checked by the server
 * with a call that needs it (the full list), never compared in the browser. Once unlocked, the children get an API
 * that sends the key with every call.
 */
export default function KeyGate({ children }) {
  const api = useApi()
  const [key, setKey] = useState(readKey)
  const [typed, setTyped] = useState('')
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)
  const keyedApi = useMemo(() => {
    if (!key) return null
    // If the server ever rejects the key (it was changed there), lock again and ask for the new one.
    const inner = api.withManagementKey(key)
    return Object.fromEntries(
      Object.entries(inner).map(([name, call]) => [
        name,
        typeof call !== 'function'
          ? call
          : async (...args) => {
              try {
                return await call(...args)
              } catch (e) {
                if (e instanceof ApiError && e.status === 401) {
                  writeKey(null)
                  setKey(null)
                  setError('Invalid management key.')
                }
                throw e
              }
            },
      ]),
    )
  }, [api, key])

  async function unlock(event) {
    event.preventDefault()
    // An HTTP header can only carry plain characters; a key typed in another keyboard layout (e.g. Hebrew)
    // can't be the right one, and sending it would fail as if the server were down.
    if (!/^[\x20-\x7E]+$/.test(typed)) {
      setError('Invalid management key.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await api.withManagementKey(typed).getOperations({ includeDisabled: true })
      writeKey(typed)
      setKey(typed)
      setTyped('')
    } catch (e) {
      setError(e instanceof ApiError && e.status === 401 ? 'Invalid management key.' : e.message)
    } finally {
      setBusy(false)
    }
  }

  function lock() {
    writeKey(null)
    setKey(null)
  }

  if (keyedApi)
    return (
      <>
        <div className="key-bar">
          <span>Management unlocked for this tab.</span>
          <button type="button" className="secondary" onClick={lock}>
            Lock
          </button>
        </div>
        <ApiContext value={keyedApi}>{children}</ApiContext>
      </>
    )

  return (
    <form className="key-gate" onSubmit={unlock}>
      <h2>Manage operations</h2>
      <p className="hint">Adding, changing and removing operations needs the management key (set on the server).</p>
      <div className="key-row">
        <label className="field">
          <span>Management key</span>
          <input type="password" value={typed} onChange={(e) => setTyped(e.target.value)} autoComplete="off" required />
        </label>
        <button type="submit" disabled={busy}>
          {busy ? 'Checking…' : 'Unlock'}
        </button>
      </div>
      {error && (
        <p className="message error" role="alert">
          {error}
        </p>
      )}
    </form>
  )
}
