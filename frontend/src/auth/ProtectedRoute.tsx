import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './auth-context'

export function ProtectedRoute() {
  const { email, isLoading } = useAuth()

  if (isLoading) {
    return null
  }

  if (!email) {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}
