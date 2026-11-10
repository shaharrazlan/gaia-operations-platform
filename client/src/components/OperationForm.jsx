import { useEffect, useId, useRef, useState } from 'react'
import { useApi } from '../api/ApiContext.jsx'
import { toDefinition } from '../api/definitions.js'

const KINDS = ['Arithmetic', 'String', 'Other']
const INPUT_TYPES = ['Number', 'Text', 'Coordinate']

const EMPTY = { id: '', name: '', kind: 'Arithmetic', fieldALabel: '', fieldBLabel: '', inputType: 'Number', formula: '', isEnabled: true }

export const CONFLICT_MESSAGE = 'This operation was changed by someone else. Reload to see the latest version.'

/** One line of help under a field (R12.AC9); the field points at it with aria-describedby. */
const HELP = {
  kind: "Only a category for the list; it doesn't change how the formula runs.",
  inputType: 'How the two values typed in the Calculator are read: Number (12, -2.5), Text (any words), Coordinate (latitude, longitude).',
  fieldALabel: 'The name shown above the first box in the Calculator, e.g. Base — not a value.',
  fieldBLabel: 'The name shown above the second box in the Calculator, e.g. Exponent — not a value.',
  formula: 'Use A and B, e.g. A * 3 + B. Checked when you save: it must work with the input type.',
}

/** Form values for an operation as the API returned it (a code operation's null formula shows as empty). */
const valuesOf = (operation) => {
  const { version: _version, ...values } = toDefinition(operation)
  return { ...values, formula: values.formula ?? '' }
}

/**
 * Add a new operation (R12.AC3) or edit one (R12.AC4). The server checks everything; a rejected definition
 * shows its message next to the form, marks the field it names, and keeps what the operator typed (R12.AC6).
 * A built-in operation's logic (formula, kind, input type) is read-only here, as on the server (R4.AC6).
 * A save based on an old version is refused by the server; Reload then loads the latest version (R12.AC8).
 */
export default function OperationForm({ operation = null, onSaved, onCancel }) {
  const api = useApi()
  const editing = operation !== null
  const logicLocked = operation?.isSystem ?? false
  const isCodeOperation = editing && operation.formula === null
  const [values, setValues] = useState(editing ? valuesOf(operation) : EMPTY)
  const [version, setVersion] = useState(operation?.version)
  const [error, setError] = useState(null) // { message, field, conflict }
  const [busy, setBusy] = useState(false)
  const firstField = useRef(null)
  const helpIdPrefix = useId()
  const helpId = (field) => `${helpIdPrefix}-${field}-help`
  const help = (field) => (
    <small id={helpId(field)} className="field-help">
      {HELP[field]}
    </small>
  )

  // Move the keyboard focus into the form when it opens (the Edit button may be far down the table).
  useEffect(() => firstField.current?.focus(), [])

  const set =(field) => (event) => {
    const value = event.target.type === 'checkbox' ? event.target.checked : event.target.value
    setValues((current) => ({ ...current, [field]: value }))
  }
  const invalid = (field) => (error?.field === field ? true : undefined)

  async function run(action) {
    setBusy(true)
    setError(null)
    try {
      await action()
    } catch (e) {
      setError({ message: e.message, field: e.field ?? null, conflict: e.code === 'edit_conflict' })
    } finally {
      setBusy(false)
    }
  }

  function handleSubmit(event) {
    event.preventDefault()
    run(async () => {
      if (!editing) return onSaved(await api.createOperation(values))
      const definition = { ...values, formula: isCodeOperation ? null : values.formula, version }
      onSaved(await api.updateOperation(operation.id, definition))
    })
  }

  /** After a conflict: replace the form with the latest saved version (the operator's typing is dropped on purpose). */
  const reload = () =>
    run(async () => {
      const latest = (await api.getOperations({ includeDisabled: true })).find((o) => o.id === operation.id)
      if (!latest) throw new Error('This operation no longer exists. Close the form to see the current list.')
      setValues(valuesOf(latest))
      setVersion(latest.version)
    })

  const title = editing ? `Edit operation: ${operation.name}` : 'Add operation'

  return (
    <form className="operation-form" aria-label={title} onSubmit={handleSubmit}>
      <h3>{title}</h3>
      {logicLocked && <p className="hint">Built-in operation: you can change its name, labels and whether it is enabled.</p>}
      <div className="form-grid">
        <label className="field">
          <span>Id</span>
          <input ref={editing ? undefined : firstField} value={values.id} onChange={set('id')} disabled={editing} aria-invalid={invalid('id')} placeholder="e.g. average" autoComplete="off" />
        </label>
        <label className="field">
          <span>Name</span>
          <input ref={editing ? firstField : undefined} value={values.name} onChange={set('name')} aria-invalid={invalid('name')} autoComplete="off" />
        </label>
        <div className="field-with-help">
          <label className="field">
            <span>Kind</span>
            <select value={values.kind} onChange={set('kind')} disabled={logicLocked} aria-invalid={invalid('kind')} aria-describedby={helpId('kind')}>
              {KINDS.map((k) => (
                <option key={k}>{k}</option>
              ))}
            </select>
          </label>
          {help('kind')}
        </div>
        <div className="field-with-help">
          <label className="field">
            <span>Input type</span>
            <select
              value={values.inputType}
              onChange={set('inputType')}
              disabled={logicLocked}
              aria-invalid={invalid('inputType')}
              aria-describedby={helpId('inputType')}
            >
              {INPUT_TYPES.map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>
          </label>
          {help('inputType')}
        </div>
        <div className="field-with-help">
          <label className="field">
            <span>Field A label</span>
            <input value={values.fieldALabel} onChange={set('fieldALabel')} aria-invalid={invalid('fieldALabel')} aria-describedby={helpId('fieldALabel')} autoComplete="off" />
          </label>
          {help('fieldALabel')}
        </div>
        <div className="field-with-help">
          <label className="field">
            <span>Field B label</span>
            <input value={values.fieldBLabel} onChange={set('fieldBLabel')} aria-invalid={invalid('fieldBLabel')} aria-describedby={helpId('fieldBLabel')} autoComplete="off" />
          </label>
          {help('fieldBLabel')}
        </div>
        <div className="field-with-help wide">
          <label className="field">
            <span>Formula</span>
            <input
              value={values.formula}
              onChange={set('formula')}
              disabled={logicLocked}
              aria-invalid={invalid('formula')}
              aria-describedby={helpId('formula')}
              placeholder={isCodeOperation ? 'built-in (runs code, not a formula)' : 'e.g. (A + B) / 2'}
              autoComplete="off"
              spellCheck={false}
            />
          </label>
          {help('formula')}
        </div>
        <label className="checkbox">
          <input type="checkbox" checked={values.isEnabled} onChange={set('isEnabled')} />
          <span>Enabled</span>
        </label>
      </div>

      {error && (
        <div className="message error" role="alert">
          <p>{error.conflict ? CONFLICT_MESSAGE : error.message}</p>
          {error.conflict && (
            <button type="button" className="secondary" onClick={reload}>
              Reload
            </button>
          )}
        </div>
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
