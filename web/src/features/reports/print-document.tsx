import { useEffect, useState, type ReactNode } from 'react'
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
  const [printing, setPrinting] = useState(
    () => window.matchMedia?.('print').matches ?? false,
  )
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
  return createPortal(
    <section className={`print-document ${className}`} aria-label={label}>
      {children}
    </section>,
    document.body,
  )
}
