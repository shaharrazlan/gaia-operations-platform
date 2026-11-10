import { expect, test } from '@playwright/test'
import { calculate, e2eManagementKey } from './helpers.js'

test('R12.AC3: unlock with the key, add "triple" = A * 3 + B, and use it in the Calculator without a restart', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Manage operations' }).click()

  // R13.AC4: the management view asks for the key first.
  await page.getByLabel('Management key').fill(e2eManagementKey())
  await page.getByRole('button', { name: 'Unlock' }).click()

  await page.getByRole('button', { name: 'Add operation' }).click()
  const form = page.getByRole('form', { name: 'Add operation' })
  await form.getByLabel('Id').fill('triple')
  await form.getByLabel('Name').fill('Triple plus')
  await form.getByLabel('Field A label').fill('Number')
  await form.getByLabel('Field B label').fill('Plus')
  await form.getByLabel('Formula').fill('A * 3 + B')
  await form.getByRole('button', { name: 'Save' }).click()
  await expect(page.getByRole('rowheader', { name: 'Triple plus' })).toBeVisible()

  // R12.AC7 / R4.AC1: the new operation is in the Calculator right away.
  await page.getByRole('button', { name: 'Calculator' }).click()
  await calculate(page, 'triple', '2', '1')

  await expect(page.getByRole('status', { name: 'Result' })).toContainText('7')
  await expect(page.getByLabel('Number')).toHaveValue('2')
})
