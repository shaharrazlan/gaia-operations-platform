import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, ServiceUnavailableError } from './errors.js'
import { createHttpApi, httpApi } from './httpApi.js'

function respond(status, body, contentType = 'application/problem+json') {
  return vi.fn().mockResolvedValue(
    new Response(typeof body === 'string' ? body : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': contentType },
    }),
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('httpApi', () => {
  it('T25: returns the JSON body of a successful response', async () => {
    vi.stubGlobal('fetch', respond(200, [{ id: 'add' }], 'application/json'))

    await expect(httpApi.getOperations()).resolves.toEqual([{ id: 'add' }])
  })

  it('T25: sends Field A and Field B as JSON to the execute endpoint', async () => {
    const fetch = respond(200, { result: '3.5' }, 'application/json')
    vi.stubGlobal('fetch', fetch)

    await httpApi.execute('divide', '7', '2')

    const [url, init] = fetch.mock.calls[0]
    expect(url).toBe('/api/operations/divide/execute')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body)).toEqual({ fieldA: '7', fieldB: '2' })
  })

  it('T25: a non-2xx problem response becomes an ApiError with code, detail and field', async () => {
    vi.stubGlobal(
      'fetch',
      respond(400, { status: 400, code: 'validation_error', detail: 'Field A is required.', field: 'fieldA' }),
    )

    const error = await httpApi.execute('add', '', '2').catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 400, code: 'validation_error', message: 'Field A is required.', field: 'fieldA' })
  })

  it('R5.AC5: a network failure becomes a ServiceUnavailableError', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(httpApi.getOperations()).rejects.toBeInstanceOf(ServiceUnavailableError)
  })

  it('R5.AC5: an answer that is not from the API (e.g. the dev proxy with the API down) is ServiceUnavailable', async () => {
    vi.stubGlobal('fetch', respond(500, 'Proxy error: ECONNREFUSED', 'text/plain'))

    await expect(httpApi.getOperations()).rejects.toBeInstanceOf(ServiceUnavailableError)
  })

  it('R5.AC5: a 2xx answer that is not JSON is ServiceUnavailable, not a raw parse error', async () => {
    vi.stubGlobal('fetch', respond(200, '<!doctype html><p>Some other server</p>', 'text/html'))

    await expect(httpApi.getOperations()).rejects.toBeInstanceOf(ServiceUnavailableError)
  })

  it('R5.AC5: a server that never answers becomes ServiceUnavailable after the timeout', async () => {
    const hang = vi.fn(
      (_url, init) =>
        new Promise((_resolve, reject) => init.signal.addEventListener('abort', () => reject(init.signal.reason))),
    )
    vi.stubGlobal('fetch', hang)

    await expect(createHttpApi({ timeoutMs: 20 }).getOperations()).rejects.toBeInstanceOf(ServiceUnavailableError)
  })

  it('T25: GET requests carry no Content-Type (no CORS preflight)', async () => {
    const fetch = respond(200, [], 'application/json')
    vi.stubGlobal('fetch', fetch)

    await httpApi.getOperations()

    expect(fetch.mock.calls[0][1].headers['Content-Type']).toBeUndefined()
  })
})
