import { useEffect, useState, type ReactNode } from 'react'
import * as authApi from '../api/auth'
import { getToken, setToken } from '../api/client'
import { AuthContext } from './auth-context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [email, setEmail] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    if (!getToken()) {
      setIsLoading(false)
      return
    }

    authApi
      .me()
      .then((res) => setEmail(res.email))
      .catch(() => setToken(null))
      .finally(() => setIsLoading(false))
  }, [])

  async function login(loginEmail: string, password: string) {
    const res = await authApi.login(loginEmail, password)
    setToken(res.token)
    setEmail(res.email)
  }

  async function register(registerEmail: string, password: string) {
    const res = await authApi.register(registerEmail, password)
    setToken(res.token)
    setEmail(res.email)
  }

  function logout() {
    setToken(null)
    setEmail(null)
  }

  return (
    <AuthContext.Provider value={{ email, isLoading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}
