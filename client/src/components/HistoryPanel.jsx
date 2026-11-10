// Times are shown in Israel time, the same clock the monthly count uses (assumption A7).
const timeFormat = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Jerusalem', dateStyle: 'short', timeStyle: 'short' })

/**
 * Shown under a result: the 3 successful runs of the same operation before this one, newest first (R8),
 * and how many times it ran this month (R9). Both come from the server; nothing is counted here (R6).
 */
export default function HistoryPanel({ operation, history }) {
  if (!history) return <p className="message history-note">History is not available right now.</p>

  const { previousRuns, monthCount } = history
  const times = monthCount === 1 ? 'once' : `${monthCount} times`

  return (
    <section className="history" aria-label="History">
      <p className="month-count">
        {operation.name} ran {times} this month.
      </p>
      {previousRuns.length === 0 ? (
        <p className="history-note">No previous runs</p>
      ) : (
        <table className="history-table" aria-label={`Previous runs of ${operation.name}`}>
          <thead>
            <tr>
              <th scope="col">{operation.fieldALabel}</th>
              <th scope="col">{operation.fieldBLabel}</th>
              <th scope="col">Result</th>
              <th scope="col">Time</th>
            </tr>
          </thead>
          <tbody>
            {previousRuns.map((run, index) => (
              <tr key={`${run.createdAtUtc}-${index}`}>
                <td>{run.fieldA}</td>
                <td>{run.fieldB}</td>
                <td>{run.result}</td>
                <td>{timeFormat.format(new Date(run.createdAtUtc))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
