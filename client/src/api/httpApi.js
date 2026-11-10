import { ApiError, ServiceUnavailableError } from './errors.js'

/** Longest a request may take: the weather call alone may use 5 s, so leave room. */
export const REQUEST_TIMEOUT_MS = 15_000

/**
 * Talks to the Gaia API over HTTP. In development Vite forwards /api to the .NET server.
 * The client computes nothing itself: every result and every validation message comes from the server (R6).
 */
export function createHttpApi({ timeoutMs = REQUEST_TIMEOUT_MS } = {}) {
  async function request(path, { body, ...options } = {}) {
    let response
    try {
      response = await fetch(path, {
        ...options,
        // Only requests with a body declare one (keeps GETs "simple", no CORS preflight).
        headers: body === undefined ? { Accept: 'application/json' } : { Accept: 'application/json', 'Content-Type': 'application/json' },
        body,
        signal: AbortSignal.timeout(timeoutMs),
      })
    } catch {
      throw new ServiceUnavailableError() // network down, server not running, no answer in time, …
    }

    if (response.ok) {
      if (response.status === 204) return null
      try {
        return await response.json()
      } catch {
        throw new ServiceUnavailableError() // a 2xx that isn't JSON didn't come from the Gaia API
      }
    }

    // Every Gaia error carries a "code". No code means something else answered (e.g. the dev proxy
    // reporting that the API is down), so treat it as "service unavailable".
    const problem = await response.json().catch(() => null)
    if (!problem?.code) {
      throw new ServiceUnavailableError()
    }
    throw new ApiError({ status: response.status, ...problem })
  }

  return {
    /** The calculator's list (enabled only), or with includeDisabled the management list (R12.AC2). */
    getOperations: ({ includeDisabled = false } = {}) =>
      request(includeDisabled ? '/api/operations?includeDisabled=true' : '/api/operations'),

    createOperation: (definition) => request('/api/operations', { method: 'POST', body: JSON.stringify(definition) }),

    /** `definition.version` must be the version that was loaded; a stale one is rejected with edit_conflict (R4.AC7). */
    updateOperation: (id, definition) =>
      request(`/api/operations/${encodeURIComponent(id)}`, { method: 'PUT', body: JSON.stringify(definition) }),

    deleteOperation: (id) => request(`/api/operations/${encodeURIComponent(id)}`, { method: 'DELETE' }),

    execute: (operationId, fieldA, fieldB) =>
      request(`/api/operations/${encodeURIComponent(operationId)}/execute`, {
        method: 'POST',
        body: JSON.stringify({ fieldA, fieldB }),
      }),
  }
}

export const httpApi = createHttpApi()
