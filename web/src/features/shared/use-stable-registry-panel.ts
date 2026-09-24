import { useRef, useState } from 'react'

export function useStableRegistryPanel(filterKey: string) {
  const panelRef = useRef<HTMLDivElement>(null)
  const [heightState, setHeightState] = useState({ filterKey, minHeight: 0 })
  const minHeight =
    heightState.filterKey === filterKey ? heightState.minHeight : 0

  const preserveHeight = () => {
    const height = panelRef.current?.getBoundingClientRect().height ?? 0
    setHeightState({ filterKey, minHeight: Math.max(minHeight, height) })
  }

  return { panelRef, minHeight, preserveHeight }
}
