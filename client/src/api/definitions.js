/** The fields POST/PUT /api/operations take, picked from an operation as the API returned it. */
export function toDefinition({ id, name, kind, fieldALabel, fieldBLabel, inputType, formula, isEnabled, version }) {
  return { id, name, kind, fieldALabel, fieldBLabel, inputType, formula, isEnabled, version }
}
