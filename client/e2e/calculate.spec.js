import { expect, test } from '@playwright/test'
import { calculate, openCalculator } from './helpers.js'

test('R5.AC3: 7 ÷ 2 shows 3.5 on the same screen', async ({ page }) => {
  await openCalculator(page)

  await calculate(page, 'divide', '7', '2')

  await expect(page.getByRole('status', { name: 'Result' })).toContainText('3.5')
  await expect(page.getByLabel('Dividend')).toHaveValue('7') // labels come from the chosen operation (R5.AC2)
})
