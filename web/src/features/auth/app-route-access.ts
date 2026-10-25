const operationalReadRoles = ['GSD', 'Inspector', 'Supervisor'] as const
const dashboardRoles = [...operationalReadRoles, 'Admin'] as const
const scheduleReadRoles = operationalReadRoles
const scheduleManageRoles = ['GSD', 'Supervisor'] as const
const assetManageRoles = ['GSD'] as const
const formReviewRoles = operationalReadRoles
const acknowledgementReviewRoles = ['GSD', 'Inspector'] as const
const pmAnalyticsRoles = ['GSD'] as const
const adminRoles = ['Admin'] as const

function isAtOrBelow(pathname: string, basePath: string) {
  return pathname === basePath || pathname.startsWith(`${basePath}/`)
}

export function getRequiredAppRoles(
  pathname: string,
): readonly string[] | null {
  if (isAtOrBelow(pathname, '/app/admin')) return adminRoles
  if (isAtOrBelow(pathname, '/app/analytics')) return pmAnalyticsRoles
  if (isAtOrBelow(pathname, '/app/dashboard')) return dashboardRoles
  if (isAtOrBelow(pathname, '/app/schedules/new')) return scheduleManageRoles
  if (isAtOrBelow(pathname, '/app/schedules')) return scheduleReadRoles
  if (isAtOrBelow(pathname, '/app/assets/new')) return assetManageRoles
  if (isAtOrBelow(pathname, '/app/assets')) return operationalReadRoles
  if (isAtOrBelow(pathname, '/app/inspections')) return operationalReadRoles
  if (isAtOrBelow(pathname, '/app/preventive-maintenance-forms')) {
    if (pathname.endsWith('/review')) return acknowledgementReviewRoles
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

export function canReadOperationalData(roles: readonly string[]) {
  return hasAnyRole(roles, operationalReadRoles)
}

export function canReviewForms(roles: readonly string[]) {
  return hasAnyRole(roles, formReviewRoles)
}
