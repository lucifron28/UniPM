import { redirect } from '@tanstack/react-router'

export function requireAppAccess(
  getAccessToken: () => string | null,
  pathname: string,
) {
  if (getAccessToken()) return

  throw redirect({
    to: '/login',
    search: { redirect: pathname.startsWith('/app') ? pathname : '/app' },
  })
}
