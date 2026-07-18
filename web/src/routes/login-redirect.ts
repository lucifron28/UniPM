export function isInternalAppRedirect(value: string): boolean {
  if (
    value.startsWith('//') ||
    value.includes('\\') ||
    Array.from(value).some((character) => {
      const code = character.charCodeAt(0)
      return code <= 31 || code === 127
    })
  ) {
    return false
  }

  return value === '/app' || value.startsWith('/app/')
}
