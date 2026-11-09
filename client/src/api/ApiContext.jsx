import { createContext, useContext } from 'react'
import { httpApi } from './httpApi.js'

// Components get the API from here instead of importing httpApi directly,
// so tests can hand them a fake: <ApiContext value={createFakeApi()}>…</ApiContext>
export const ApiContext = createContext(httpApi)

export function useApi() {
  return useContext(ApiContext)
}
