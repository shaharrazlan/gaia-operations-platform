import { useEffect, useState } from 'react'
import { useApi } from '../api/ApiContext.jsx'
import { toDefinition } from '../api/definitions.js'
import OperationForm from '../components/OperationForm.jsx'

/**
 * The operator screen (R12): every operation, enabled or not, with enable/disable and delete.
 * All rules (what may change, what is valid) are the server's; this view shows its answers (R6).
 */
export default function ManageView() {
  const api = useApi()
  const [operations, setOperations] = useState(null) // null while loading
  const [loadError, setLoadError] = useState(null)
  const [attempt, setAttempt] = useState(0) // bump to load the list again
  const [notice, setNotice] = useState(null) // the server's message after a rejected change
  const [busyId, setBusyId] = useState(null) // the row being saved
  const [form, setForm] = useState(null) // null, or { operation } — a null operation means adding a new one

  useEffect(() => {
    let active = true
    api
      .getOperations({ includeDisabled: true })
      .then((list) => active && setOperations(list))
      .catch((error) => active && setLoadError(error.message))
    return () => {
      active = false
    }
  }, [api, attempt])

  const reload = () => setAttempt((n) => n + 1)

  /** Runs one change against the server; on rejection shows why and reloads, so the table shows the truth. */
  async function change(operation, action) {
    setBusyId(operation.id)
    setNotice(null)
    try {
      await action()
    } catch (error) {
      setNotice(error.message)
      reload()
    } finally {
      setBusyId(null)
    }
  }

  const toggle = (operation) =>
    change(operation, async () => {
      const saved = await api.updateOperation(operation.id, { ...toDefinition(operation), isEnabled: !operation.isEnabled })
      setOperations((list) => list.map((o) => (o.id === saved.id ? saved : o)))
    })

  /** A saved operation replaces its row (edit) or joins the list (add), sorted by name like the server's list. */
  function saved(operation) {
    setOperations((list) => [...list.filter((o) => o.id !== operation.id), operation].sort((a, b) => a.name.localeCompare(b.name)))
    setForm(null)
    setNotice(null)
  }

  // The row open in the form can't be toggled or deleted meanwhile, or the form's version would go stale.
  const locked = (operation) => busyId === operation.id || form?.operation?.id === operation.id

  const remove = (operation) => {
    if (!window.confirm(`Delete "${operation.name}"? It disappears from the calculator; its history is kept.`)) return
    return change(operation, async () => {
      await api.deleteOperation(operation.id)
      setOperations((list) => list.filter((o) => o.id !== operation.id))
    })
  }

  return (
    <div className="manage">
      <div className="manage-header">
        <h2>Manage operations</h2>
        {/* Only once the list is shown: a new operation is added to that list. */}
        {operations && !loadError && !form && (
          <button type="button" onClick={() => setForm({ operation: null })}>
            Add operation
          </button>
        )}
      </div>

      {form && (
        <OperationForm key={form.operation?.id ?? 'new'} operation={form.operation} onSaved={saved} onCancel={() => setForm(null)} />
      )}

      {notice && (
        <p className="message error" role="alert">
          {notice}
        </p>
      )}

      {loadError ? (
        <div className="message error" role="alert">
          <p>{loadError}</p>
          <button
            type="button"
            onClick={() => {
              setLoadError(null)
              reload()
            }}
          >
            Try again
          </button>
        </div>
      ) : operations === null ? (
        <p className="message">Loading operations…</p>
      ) : (
        <div className="table-scroll">
          <table className="operations">
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Id</th>
                <th scope="col">Kind</th>
                <th scope="col">Field A</th>
                <th scope="col">Field B</th>
                <th scope="col">Formula</th>
                <th scope="col">Enabled</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {operations.map((o) => (
                <tr key={o.id} className={o.isEnabled ? undefined : 'disabled'}>
                  <th scope="row">{o.name}</th>
                  <td>
                    <code>{o.id}</code>
                  </td>
                  <td>{o.kind}</td>
                  <td>{o.fieldALabel}</td>
                  <td>{o.fieldBLabel}</td>
                  <td>{o.formula === null ? <em>built-in</em> : <code>{o.formula}</code>}</td>
                  <td>
                    <input
                      type="checkbox"
                      aria-label={`Enabled: ${o.name}`}
                      checked={o.isEnabled}
                      disabled={locked(o)}
                      onChange={() => toggle(o)}
                    />
                  </td>
                  <td className="actions">
                    <button type="button" className="secondary" disabled={busyId === o.id} onClick={() => setForm({ operation: o })} aria-label={`Edit ${o.name}`}>
                      Edit
                    </button>
                    {!o.isSystem && (
                      <button type="button" className="secondary" disabled={locked(o)} onClick={() => remove(o)} aria-label={`Delete ${o.name}`}>
                        Delete
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
