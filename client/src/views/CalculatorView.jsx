import { useEffect, useState } from 'react'
import { useApi } from '../api/ApiContext.jsx'
import HistoryPanel from '../components/HistoryPanel.jsx'

/**
 * The end-user screen: Field A, operation, Field B, Calculate (R5).
 * It only displays — the operation list, labels, results and errors all come from the server (R6).
 */
export default function CalculatorView() {
  const api = useApi()
  const [operations, setOperations] = useState(null) // null while loading
  const [loadError, setLoadError] = useState(null)
  const [operationId, setOperationId] = useState('')
  const [fieldA, setFieldA] = useState('')
  const [fieldB, setFieldB] = useState('')
  const [outcome, setOutcome] = useState(null) // { result } or { error, field }
  const [busy, setBusy] = useState(false)
  const [attempt, setAttempt] = useState(0) // bump to load the operation list again

  useEffect(() => {
    let active = true // ignore the answer if the view was closed meanwhile
    api
      .getOperations()
      .then((list) => {
        if (!active) return
        setOperations(list)
        setOperationId((current) => (list.some((o) => o.id === current) ? current : (list[0]?.id ?? '')))
      })
      .catch((error) => active && setLoadError(error.message))
    return () => {
      active = false
    }
  }, [api, attempt])

  if (loadError)
    return (
      <div className="message error" role="alert">
        <p>{loadError}</p>
        <button
          type="button"
          onClick={() => {
            setLoadError(null)
            setAttempt((n) => n + 1)
          }}
        >
          Try again
        </button>
      </div>
    )
  if (operations === null) return <p className="message">Loading operations…</p>
  if (operations.length === 0) return <p className="message">No operations are available right now.</p>

  const operation = operations.find((o) => o.id === operationId) ?? operations[0]

  async function handleSubmit(event) {
    event.preventDefault()
    setBusy(true)
    setOutcome(null) // never show an old result next to a new error
    try {
      const response = await api.execute(operation.id, fieldA, fieldB)
      setOutcome({ result: response.result, history: response.history ?? null, operation })
    } catch (error) {
      setOutcome({ error: error.message, field: error.field ?? null })
    } finally {
      setBusy(false)
    }
  }

  function chooseOperation(id) {
    setOperationId(id)
    setOutcome(null) // a result belongs to the operation that produced it
  }

  const invalid = (field) => (outcome?.field === field ? true : undefined)

  // Typing in the field the server complained about removes its "invalid" mark (the message stays).
  function edit(field, setValue, value) {
    setValue(value)
    setOutcome((current) => (current?.field === field ? { ...current, field: null } : current))
  }

  return (
    <form className="calculator" onSubmit={handleSubmit}>
      <div className="row">
        <label className="field">
          <span>{operation.fieldALabel}</span>
          <input name="fieldA" value={fieldA} onChange={(e) => edit('fieldA', setFieldA, e.target.value)} aria-invalid={invalid('fieldA')} autoComplete="off" />
        </label>

        <label className="field">
          <span>Operation</span>
          {/* Locked while calculating, so a late answer can't appear under a different operation. */}
          <select name="operation" value={operation.id} onChange={(e) => chooseOperation(e.target.value)} disabled={busy}>
            {operations.map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>{operation.fieldBLabel}</span>
          <input name="fieldB" value={fieldB} onChange={(e) => edit('fieldB', setFieldB, e.target.value)} aria-invalid={invalid('fieldB')} autoComplete="off" />
        </label>

        <button type="submit" disabled={busy}>
          {busy ? 'Calculating…' : 'Calculate'}
        </button>
      </div>

      {outcome?.error && (
        <p className="message error" role="alert">
          {outcome.error}
        </p>
      )}

      {outcome?.result !== undefined && (
        <output className="result" aria-label="Result">
          <span className="result-label">Result</span>
          <span className="result-value">{outcome.result}</span>
        </output>
      )}

      {/* The history belongs to the run that produced this result, so it uses that run's operation. */}
      {outcome?.result !== undefined && <HistoryPanel operation={outcome.operation} history={outcome.history} />}
    </form>
  )
}
