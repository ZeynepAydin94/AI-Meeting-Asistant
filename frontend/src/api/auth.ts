import { apiFetch } from './client'

export interface AuthResponse {
  token: string
  expiresAtUtc: string
  email: string
}

export function register(email: string, password: string): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
}

export function login(email: string, password: string): Promise<AuthResponse> {
  return apiFetch<AuthResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
}

export function me(): Promise<{ email: string }> {
  return apiFetch<{ email: string }>('/api/auth/me')
}
