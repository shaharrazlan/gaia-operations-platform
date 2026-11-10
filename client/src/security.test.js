// @vitest-environment node
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

const clientRoot = resolve(import.meta.dirname, '..')
const apiRoot = resolve(clientRoot, '../src/Gaia.Api')
const dist = join(clientRoot, 'dist')

/** Every management key configured in the server's appsettings*.json files (dev, E2E, …). */
function configuredKeys() {
  return readdirSync(apiRoot)
    .filter((name) => /^appsettings.*\.json$/.test(name))
    .map((name) => JSON.parse(readFileSync(join(apiRoot, name), 'utf8'))?.Management?.Key)
    .filter((key) => typeof key === 'string' && key.length > 0)
}

function filesUnder(folder) {
  return readdirSync(folder).flatMap((name) => {
    const path = join(folder, name)
    return statSync(path).isDirectory() ? filesUnder(path) : [path]
  })
}

function filesContaining(folder, secrets) {
  return filesUnder(folder).filter((path) => {
    const text = readFileSync(path, 'utf8')
    return secrets.some((secret) => text.includes(secret))
  })
}

describe('management key is server-only (R13.AC6)', () => {
  const keys = configuredKeys()

  it('R13.AC6: the server config has at least the dev key to look for', () => {
    expect(keys.length).toBeGreaterThan(0)
  })

  it('R13.AC6: no configured key appears anywhere in client/src', () => {
    expect(filesContaining(join(clientRoot, 'src'), keys)).toEqual([])
  })

  // The build output is what browsers download. test-all.ps1 builds before this runs; a plain
  // `npm test` without a build skips it (and says so) rather than failing for a missing folder.
  it.skipIf(!existsSync(dist))('R13.AC6: no configured key appears anywhere in the client build (client/dist)', () => {
    expect(filesContaining(dist, keys)).toEqual([])
  })
})
