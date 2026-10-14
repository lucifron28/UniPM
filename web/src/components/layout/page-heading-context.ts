import { createContext } from 'react'

export const PageHeadingHostContext = createContext<
  HTMLElement | null | undefined
>(undefined)
