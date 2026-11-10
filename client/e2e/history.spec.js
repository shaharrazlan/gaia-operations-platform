import { expect, test } from '@playwright/test'
import { calculate, openCalculator } from './helpers.js'

// Uses "subtract", which no other E2E test runs, so its history in the fresh E2E database is only this test's.
test('R8.AC1: the second calculation shows the first one in the history panel', async ({ page }) => {
  await openCalculator(page)

  await calculate(page, 'subtract', '9', '4')
  await expect(page.getByRole('status', { name: 'Result' })).toContainText('5')
  await expect(page.getByText('No previous runs')).toBeVisible()

  await calculate(page, 'subtract', '10', '3')
  await expect(page.getByRole('status', { name: 'Result' })).toContainText('7')

  const previous = page.getByRole('table', { name: 'Previous runs of Subtract' })
  await expect(previous.getByRole('row').nth(1)).toContainText('9')
  await expect(previous.getByRole('row').nth(1)).toContainText('4')
  await expect(previous.getByRole('row').nth(1)).toContainText('5')
  await expect(previous.getByRole('row')).toHaveCount(2) // header + the one earlier run (the current run is not listed)
  await expect(page.getByText('Subtract ran 2 times this month.')).toBeVisible()
})
