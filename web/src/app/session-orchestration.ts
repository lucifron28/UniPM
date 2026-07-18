import { configureApiRuntime } from '@/api/http-client'
import { router } from '@/app/router'
import {
  clearLocalSession,
  configureAuthSessionRuntime,
  getSessionGeneration,
  refreshAccessToken,
} from '@/features/auth/auth-session-service'
import { useAuthStore } from '@/stores/auth-store'

export function configureSessionOrchestration() {
  configureAuthSessionRuntime({
    invalidateRoutes: () => void router.invalidate(),
  })
  configureApiRuntime({
    getAccessToken: () => useAuthStore.getState().accessToken,
    getSessionGeneration,
    refreshAccessToken,
    onTerminalUnauthorized: clearLocalSession,
  })
}
