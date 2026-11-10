import { expect, test } from '@playwright/test'
import { calculate, openCalculator } from './helpers.js'

test('R5.AC4: 5 ÷ 0 shows the server error and no result', async ({ page }) => {
  await openCalculator(page)

  await calculate(page, 'divide', '5', '0')

  await expect(page.getByRole('alert')).toHaveText('Division by zero.')
  await expect(page.getByRole('status', { name: 'Result' })).toHaveCount(0)
})
