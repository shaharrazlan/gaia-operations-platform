import { vi } from 'vitest'

/** Sample catalog in the same shape GET /api/operations returns. */
export const sampleOperations = [
  { id: 'add', name: 'Add', kind: 'Arithmetic', fieldALabel: 'First number', fieldBLabel: 'Second number', inputType: 'Number', formula: 'A + B', isSystem: true, isEnabled: true, version: 'AAAAAAAAB9E=' },
  { id: 'divide', name: 'Divide', kind: 'Arithmetic', fieldALabel: 'Dividend', fieldBLabel: 'Divisor', inputType: 'Number', formula: 'A / B', isSystem: true, isEnabled: true, version: 'AAAAAAAAB9I=' },
  { id: 'weather', name: 'Weather today', kind: 'Other', fieldALabel: 'Latitude', fieldBLabel: 'Longitude', inputType: 'Coordinate', formula: null, isSystem: true, isEnabled: true, version: 'AAAAAAAAB9M=' },
]

/** The management list (GET ?includeDisabled=true): adds a custom formula operation and a disabled one. */
export const sampleManagedOperations = [
  ...sampleOperations,
  { id: 'average', name: 'Average', kind: 'Arithmetic', fieldALabel: 'First number', fieldBLabel: 'Second number', inputType: 'Number', formula: '(A + B) / 2', isSystem: false, isEnabled: true, version: 'AAAAAAAAB9Q=' },
  { id: 'hypotenuse', name: 'Hypotenuse', kind: 'Arithmetic', fieldALabel: 'Side a', fieldBLabel: 'Side b', inputType: 'Number', formula: 'sqrt(A^2 + B^2)', isSystem: false, isEnabled: false, version: 'AAAAAAAAB9U=' },
]

/**
 * A stand-in for httpApi in component tests: same functions, no network.
 * Every function is a Vitest mock, so a test can change its answer or check how it was called.
 */
export function createFakeApi(overrides = {}) {
  return {
    getOperations: vi.fn().mockResolvedValue(sampleOperations),
    execute: vi.fn(async (operationId, fieldA, fieldB) => ({ operationId, fieldA, fieldB, result: 'fake result' })),
    // Management calls answer like the server: the saved operation with a new version.
    createOperation: vi.fn(async (definition) => ({ isSystem: false, isEnabled: true, ...definition, version: 'v-new' })),
    updateOperation: vi.fn(async (id, definition) => ({ isSystem: false, ...definition, id, version: 'v-next' })),
    deleteOperation: vi.fn().mockResolvedValue(null),
    ...overrides,
  }
}
