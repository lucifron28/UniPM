import { z } from 'zod'
import type { AuthUserResponse, LoginResponse } from '@/api/generated/models'

export const authUserSchema = z
  .object({
    id: z.string().uuid(),
    email: z.string().email(),
    displayName: z.string().trim().min(1),
    roles: z.array(z.string().trim().min(1)),
  })
  .strict()

export const authSessionSchema = z
  .object({
    accessToken: z.string().min(1),
    expiresAtUtc: z
      .string()
      .refine((value) => !Number.isNaN(Date.parse(value)), 'Invalid expiry.'),
    user: authUserSchema,
  })
  .strict()

export function parseAuthSession(value: unknown): LoginResponse {
  return authSessionSchema.parse(value) as LoginResponse
}

export function parseAuthUser(value: unknown): AuthUserResponse {
  return authUserSchema.parse(value) as AuthUserResponse
}
