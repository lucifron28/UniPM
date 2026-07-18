import { createRouter } from '@tanstack/react-router'
import { queryClient } from '@/app/query-client'
import { routeTree } from '@/routeTree.gen'
import { useAuthStore } from '@/stores/auth-store'

export const router = createRouter({
  routeTree,
  context: {
    queryClient,
    getAccessToken: () => useAuthStore.getState().accessToken,
  },
  defaultPreload: 'intent',
})

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}
