import {
  getGetCurrentUserQueryKey,
  useGetCurrentUser,
} from '@/api/generated/endpoints'
import type { AuthUserResponse } from '@/api/generated/models'
import { parseAuthUser } from '@/features/auth/auth-session-schema'
import { useAuthStore } from '@/stores/auth-store'

export const currentUserQueryKey = getGetCurrentUserQueryKey()

export function useCurrentUser() {
  const isAuthenticated = useAuthStore(
    (state) => state.status === 'authenticated' && state.accessToken !== null,
  )

  return useGetCurrentUser<AuthUserResponse>({
    query: { enabled: isAuthenticated, select: parseAuthUser },
  })
}
