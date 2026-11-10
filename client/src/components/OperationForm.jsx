import { useState } from 'react'
import { useApi } from '../api/ApiContext.jsx'

const KINDS = ['Arithmetic', 'String', 'Other']
const INPUT_TYPES = ['Number', 'Text', 'Coordinate']

const EMPTY = { id: '', name: '', kind: 'Arithmetic', fieldALabel: '', fieldBLabel: '', inputType: 'Number', formula: '', isEnabled: true }

/**
 * Add a new operation (R12.AC3). The server checks everything; a rejected definition shows its message
 * next to the form, marks the field it names, and keeps what the operator typed (R12.AC6).
 */
export default function OperationForm({ onSaved, onCancel }) {
  const api = useApi()
  const [values, setValues] = useState(EMPTY)
  const [error, setError] = useState(null) // { message, field }
  const [busy, setBusy] = useState(false)

  const set = (field) => (event) => {
    const value = event.target.type === 'checkbox' ? event.target.checked : event.target.value
    setValues((current) => ({ ...current, [field]: value }))
  }
  const invalid = (field) => (error?.field === field ? true : undefined)

  async function handleSubmit(event) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      onSaved(await api.createOperation(values))
    } catch (e) {
      setError({ message: e.message, field: e.field ?? null })
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="operation-form" aria-label="Add operation" onSubmit={handleSubmit}>
      <h3>Add operation</h3>
      <div className="form-grid">
        <label className="field">
          <span>Id</span>
          <input value={values.id} onChange={set('id')} aria-invalid={invalid('id')} placeholder="e.g. average" autoComplete="off" />
        </label>
        <label className="field">
          <span>Name</span>
          <input value={values.name} onChange={set('name')} aria-invalid={invalid('name')} autoComplete="off" />
        </label>
        <label className="field">
          <span>Kind</span>
          <select value={values.kind} onChange={set('kind')}>
            {KINDS.map((k) => (
              <option key={k}>{k}</option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Input type</span>
          <select value={values.inputType} onChange={set('inputType')}>
            {INPUT_TYPES.map((t) => (
              <option key={t}>{t}</option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Field A label</span>
          <input value={values.fieldALabel} onChange={set('fieldALabel')} aria-invalid={invalid('fieldALabel')} autoComplete="off" />
        </label>
        <label className="field">
          <span>Field B label</span>
          <input value={values.fieldBLabel} onChange={set('fieldBLabel')} aria-invalid={invalid('fieldBLabel')} autoComplete="off" />
        </label>
        <label className="field wide">
          <span>Formula</span>
          <input value={values.formula} onChange={set('formula')} aria-invalid={invalid('formula')} placeholder="e.g. (A + B) / 2" autoComplete="off" spellCheck={false} />
        </label>
        <label className="checkbox">
          <input type="checkbox" checked={values.isEnabled} onChange={set('isEnabled')} />
          <span>Enabled</span>
        </label>
      </div>

      {error && (
        <p className="message error" role="alert">
          {error.message}
        </p>
      )}

      <div className="form-actions">
        <button type="submit" disabled={busy}>
          {busy ? 'Saving…' : 'Save'}
        </button>
        <button type="button" className="secondary" onClick={onCancel} disabled={busy}>
          Cancel
        </button>
      </div>
    </form>
  )
}
