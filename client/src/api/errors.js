/**
 * The server answered with an error (a ProblemDetails body).
 * `code` is the machine-readable error code (e.g. "validation_error"), `message` the text to show,
 * `field` the input it is about ("fieldA" / "fieldB"), if any.
 */
export class ApiError extends Error {
  constructor({ status, code, detail, field }) {
    super(detail ?? 'Something went wrong.')
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.field = field ?? null
  }
}

/** The server could not be reached, or something other than the Gaia API answered. */
export class ServiceUnavailableError extends Error {
  constructor() {
    super('Service unavailable. Please try again in a moment.')
    this.name = 'ServiceUnavailableError'
  }
}
