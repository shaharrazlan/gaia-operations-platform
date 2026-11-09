import { render, screen } from '@testing-library/react'
import { expect, it } from 'vitest'
import App from './App.jsx'

it('T3: app renders', () => {
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Gaia' })).toBeInTheDocument()
})
