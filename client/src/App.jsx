import { useState } from 'react'
import CalculatorView from './views/CalculatorView.jsx'
import KeyGate from './components/KeyGate.jsx'
import ManageView from './views/ManageView.jsx'
import './styles.css'

const VIEWS = [
  { id: 'calculator', label: 'Calculator' },
  { id: 'manage', label: 'Manage operations' },
]

/**
 * Two views in one app, switched by a top bar (R12.AC1) — no router, no second URL.
 * Only the visible view is mounted, so coming back to the calculator loads a fresh operation list (R12.AC7).
 */
export default function App() {
  const [view, setView] = useState('calculator')

  return (
    <main className="app">
      <header className="app-header">
        <h1>Gaia</h1>
        <p className="tagline">Run any operation on two values</p>
        <nav className="views" aria-label="Views">
          {VIEWS.map((v) => (
            <button key={v.id} type="button" aria-pressed={view === v.id} onClick={() => setView(v.id)}>
              {v.label}
            </button>
          ))}
        </nav>
      </header>
      <section className="card" data-testid="view-card">
        {view === 'calculator' ? (
          <CalculatorView />
        ) : (
          // The Manage view only opens with the management key (R13.AC4).
          <KeyGate>
            <ManageView />
          </KeyGate>
        )}
      </section>
    </main>
  )
}
