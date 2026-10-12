const scheduleReadRoles = ['GSD', 'Inspector', 'Supervisor'] as const
const scheduleManageRoles = ['GSD', 'Supervisor'] as const
const assetManageRoles = ['GSD'] as const
const formReviewRoles = ['GSD', 'Inspector'] as const

function isAtOrBelow(pathname: string, basePath: string) {
  return pathname === basePath || pathname.startsWith(`${basePath}/`)
}

export function getRequiredAppRoles(
  pathname: string,
): readonly string[] | null {
  if (isAtOrBelow(pathname, '/app/schedules/new')) return scheduleManageRoles
  if (isAtOrBelow(pathname, '/app/schedules')) return scheduleReadRoles
  if (isAtOrBelow(pathname, '/app/assets/new')) return assetManageRoles
  if (isAtOrBelow(pathname, '/app/preventive-maintenance-forms')) {
    return formReviewRoles
  }
  return null
}

export function hasAnyRole(
  roles: readonly string[],
  permittedRoles: readonly string[],
) {
  return permittedRoles.some((role) => roles.includes(role))
}

export function canReadSchedules(roles: readonly string[]) {
  return hasAnyRole(roles, scheduleReadRoles)
}

export function canReviewForms(roles: readonly string[]) {
  return hasAnyRole(roles, formReviewRoles)
}
