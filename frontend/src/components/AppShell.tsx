import type { CSSProperties } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/auth-context'

const navLinkStyle = ({ isActive }: { isActive: boolean }): CSSProperties => ({
  display: 'flex',
  alignItems: 'center',
  gap: 8,
  textDecoration: 'none',
  color: 'var(--text-primary)',
  padding: '8px 10px',
  borderRadius: 6,
  background: isActive ? 'var(--surface-0)' : 'transparent',
  fontSize: 14,
})

export function AppShell() {
  const { email, logout } = useAuth()

  return (
    <div style={{ display: 'flex', minHeight: '100svh' }}>
      <aside
        style={{
          width: 200,
          flexShrink: 0,
          background: 'var(--surface-1)',
          borderRight: '1px solid var(--border)',
          padding: '1rem 0.75rem',
          display: 'flex',
          flexDirection: 'column',
          gap: 4,
        }}
      >
        <div style={{ fontSize: 14, fontWeight: 500, padding: '0 0.5rem 1rem' }}>Meeting AI</div>
        <NavLink to="/app/new-meeting" style={navLinkStyle}>
          New meeting
        </NavLink>
        <NavLink to="/app/history" style={navLinkStyle}>
          History
        </NavLink>
        <NavLink to="/app/settings" style={navLinkStyle}>
          Settings
        </NavLink>
        <div style={{ flex: 1 }} />
        <div style={{ fontSize: 12, color: 'var(--text-secondary)', padding: '0 0.5rem' }}>{email}</div>
        <button onClick={logout} style={{ marginTop: 4 }}>
          Log out
        </button>
      </aside>
      <div style={{ flex: 1, padding: '1.5rem' }}>
        <Outlet />
      </div>
    </div>
  )
}
