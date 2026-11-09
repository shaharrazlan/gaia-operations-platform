import { vi } from 'vitest'

/** Sample catalog in the same shape GET /api/operations returns. */
export const sampleOperations = [
  { id: 'add', name: 'Add', kind: 'Arithmetic', fieldALabel: 'First number', fieldBLabel: 'Second number', inputType: 'Number', formula: 'A + B', isSystem: true, isEnabled: true, version: 'AAAAAAAAB9E=' },
  { id: 'divide', name: 'Divide', kind: 'Arithmetic', fieldALabel: 'Dividend', fieldBLabel: 'Divisor', inputType: 'Number', formula: 'A / B', isSystem: true, isEnabled: true, version: 'AAAAAAAAB9I=' },
  { id: 'weather', name: 'Weather today', kind: 'Other', fieldALabel: 'Latitude', fieldBLabel: 'Longitude', inputType: 'Coordinate', formula: null, isSystem: true, isEnabled: true, version: 'AAAAAAAAB9M=' },
]

/**
 * A stand-in for httpApi in component tests: same functions, no network.
 * Every function is a Vitest mock, so a test can change its answer or check how it was called.
 */
export function createFakeApi(overrides = {}) {
  return {
    getOperations: vi.fn().mockResolvedValue(sampleOperations),
    execute: vi.fn(async (operationId, fieldA, fieldB) => ({ operationId, fieldA, fieldB, result: 'fake result' })),
    ...overrides,
  }
}
