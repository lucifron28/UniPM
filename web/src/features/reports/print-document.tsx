import { useEffect, useLayoutEffect, useState, type ReactNode } from 'react'
import { createPortal, flushSync } from 'react-dom'
import './print-document.css'

export function PrintDocument({
  children,
  className = '',
  label,
}: {
  children: ReactNode
  className?: string
  label: string
}) {
  const [host] = useState(() => document.createElement('section'))
  const [printing, setPrinting] = useState(
    () => window.matchMedia?.('print').matches ?? false,
  )
  useLayoutEffect(() => {
    if (!printing) return
    host.className = `print-document ${className}`
    host.setAttribute('aria-label', label)
    document.body.prepend(host)
    return () => host.remove()
  }, [printing, host, className, label])
  useEffect(() => {
    const media = window.matchMedia?.('print')
    const before = () => flushSync(() => setPrinting(true))
    const after = () => flushSync(() => setPrinting(media?.matches ?? false))
    const change = (event: MediaQueryListEvent) =>
      flushSync(() => setPrinting(event.matches))
    window.addEventListener('beforeprint', before)
    window.addEventListener('afterprint', after)
    media?.addEventListener('change', change)
    return () => {
      window.removeEventListener('beforeprint', before)
      window.removeEventListener('afterprint', after)
      media?.removeEventListener('change', change)
    }
  }, [])
  if (!printing) return null
  return createPortal(children, host)
}
