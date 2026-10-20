import { useEffect, useState } from 'react'

const registryZoomValues = [90, 100, 110, 120, 130] as const
const registryZoomStorageKey = 'unipm.registry.recordsZoom'

export type RegistryZoom = (typeof registryZoomValues)[number]

function isRegistryZoom(value: number): value is RegistryZoom {
  return registryZoomValues.some((allowedValue) => allowedValue === value)
}

function getInitialRegistryZoom(): RegistryZoom {
  if (typeof window === 'undefined') return 100

  try {
    const storedValue = Number(
      window.localStorage.getItem(registryZoomStorageKey),
    )
    return isRegistryZoom(storedValue) ? storedValue : 100
  } catch {
    return 100
  }
}

export function useRegistryZoom() {
  const [zoom, setZoom] = useState<RegistryZoom>(getInitialRegistryZoom)

  useEffect(() => {
    try {
      window.localStorage.setItem(registryZoomStorageKey, String(zoom))
    } catch {
      // Zoom remains usable for this page when browser storage is unavailable.
    }
  }, [zoom])

  return {
    zoom,
    decrease: () =>
      setZoom((current) => Math.max(90, current - 10) as RegistryZoom),
    increase: () =>
      setZoom((current) => Math.min(130, current + 10) as RegistryZoom),
    reset: () => setZoom(100),
  }
}
