import { expect, test } from '@playwright/test'
import { calculate, openCalculator } from './helpers.js'

// Uses "power", which no other E2E test runs. 999999999999999 ^ 20 is a ~300-digit result.
test('R5.AC6: a very long result and its history row stay inside the card (no sideways scroll)', async ({ page }) => {
  await page.setViewportSize({ width: 800, height: 900 })
  await openCalculator(page)

  await calculate(page, 'power', '999999999999999', '20')
  await expect(page.getByRole('status', { name: 'Result' })).toContainText('99999999999998')
  await calculate(page, 'power', '999999999999998', '20')

  const previous = page.getByRole('table', { name: 'Previous runs of Power' })
  await expect(previous.getByRole('row')).toHaveCount(2) // header + the first long run

  // Layout is measured in the browser: nothing may stick out of the calculator card or the page.
  const sizes = await previous.evaluate((table) => {
    const card = document.querySelector('[data-testid="view-card"]')
    const page = document.documentElement
    return {
      pageOverflow: page.scrollWidth - page.clientWidth,
      cardOverflow: card.scrollWidth - card.clientWidth,
      tableRight: table.getBoundingClientRect().right,
      cardRight: card.getBoundingClientRect().right,
    }
  })
  expect(sizes.pageOverflow).toBeLessThanOrEqual(0)
  expect(sizes.cardOverflow).toBeLessThanOrEqual(0)
  expect(sizes.tableRight).toBeLessThanOrEqual(sizes.cardRight)
})
