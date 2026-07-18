import { configureApiRuntime } from '@/api/http-client'
import { queryClient } from '@/app/query-client'
import { router } from '@/app/router'
import { useAuthStore } from '@/stores/auth-store'

export function configureSessionOrchestration() {
  configureApiRuntime({
    getAccessToken: () => useAuthStore.getState().accessToken,
    onUnauthorized: () => {
      useAuthStore.getState().clearSession()
      queryClient.clear()
      void router.invalidate()
    },
  })
}
