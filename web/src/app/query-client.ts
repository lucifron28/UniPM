import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '@/api/problem-details'

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        retry: (attempt, error) =>
          !(error instanceof ApiError && error.status && error.status < 500) &&
          attempt < 2,
      },
      mutations: { retry: false },
    },
  })
}

export const queryClient = createQueryClient()
