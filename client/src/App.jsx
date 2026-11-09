import CalculatorView from './views/CalculatorView.jsx'
import './styles.css'

export default function App() {
  return (
    <main className="app">
      <header className="app-header">
        <h1>Gaia</h1>
        <p className="tagline">Run any operation on two values</p>
      </header>
      <section className="card">
        <CalculatorView />
      </section>
    </main>
  )
}
