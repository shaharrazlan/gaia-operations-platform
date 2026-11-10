import { readFileSync } from 'node:fs'
import { expect } from '@playwright/test'

/** The management key of the E2E server, read from its settings (not copied into the tests). */
export function e2eManagementKey() {
  const settings = JSON.parse(readFileSync(new URL('../../src/Gaia.Api/appsettings.E2E.json', import.meta.url), 'utf8'))
  return settings.Management.Key
}

/** Picks an operation in the Calculator, fills both fields (by the labels the operation defines), and calculates. */
export async function calculate(page, operationId, fieldA, fieldB) {
  await page.getByRole('combobox', { name: 'Operation' }).selectOption(operationId)
  const fields = page.locator('form.calculator input')
  await fields.nth(0).fill(fieldA)
  await fields.nth(1).fill(fieldB)
  await page.getByRole('button', { name: 'Calculate' }).click()
}

export async function openCalculator(page) {
  await page.goto('/')
  await expect(page.getByRole('combobox', { name: 'Operation' })).toBeVisible()
}
