import { ApiError, ServiceUnavailableError } from './errors.js'

/**
 * Talks to the Gaia API over HTTP. In development Vite forwards /api to the .NET server.
 * The client computes nothing itself: every result and every validation message comes from the server (R6).
 */
async function request(path, options = {}) {
  let response
  try {
    response = await fetch(path, {
      ...options,
      headers: { Accept: 'application/json', 'Content-Type': 'application/json', ...options.headers },
    })
  } catch {
    throw new ServiceUnavailableError() // network down, server not running, CORS, …
  }

  if (response.ok) {
    return response.status === 204 ? null : response.json()
  }

  // Every Gaia error carries a "code". No code means something else answered (e.g. the dev proxy
  // reporting that the API is down), so treat it as "service unavailable".
  const problem = await response.json().catch(() => null)
  if (!problem?.code) {
    throw new ServiceUnavailableError()
  }
  throw new ApiError({ status: response.status, ...problem })
}

export const httpApi = {
  getOperations: () => request('/api/operations'),

  execute: (operationId, fieldA, fieldB) =>
    request(`/api/operations/${encodeURIComponent(operationId)}/execute`, {
      method: 'POST',
      body: JSON.stringify({ fieldA, fieldB }),
    }),
}
