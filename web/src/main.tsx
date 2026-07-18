import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { AppProviders } from '@/app/providers'
import { configureSessionOrchestration } from '@/app/session-orchestration'
import './index.css'

configureSessionOrchestration()
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppProviders />
  </StrictMode>,
)
