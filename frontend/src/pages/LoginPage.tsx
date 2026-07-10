import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/auth-context'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await login(email, password)
      navigate('/app/new-meeting')
    } catch {
      setError('Incorrect email or password.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main style={{ maxWidth: 320, margin: '4rem auto' }}>
      <div className="card">
        <h2>Log in</h2>
        <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: '1.25rem' }}>
          Sign in to your meeting workspace.
        </p>
        <form onSubmit={handleSubmit}>
          <label style={{ fontSize: 13, color: 'var(--text-secondary)', display: 'block', marginBottom: 4 }}>
            Email
          </label>
          <input
            type="email"
            placeholder="name@company.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            style={{ width: '100%', marginBottom: 12 }}
          />
          <label style={{ fontSize: 13, color: 'var(--text-secondary)', display: 'block', marginBottom: 4 }}>
            Password
          </label>
          <input
            type="password"
            placeholder="Password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            style={{ width: '100%', marginBottom: 12 }}
          />
          {error && (
            <p style={{ fontSize: 13, color: 'var(--danger-text)', marginBottom: 12 }}>{error}</p>
          )}
          <button type="submit" className="primary" style={{ width: '100%' }} disabled={isSubmitting}>
            {isSubmitting ? 'Logging in…' : 'Log in'}
          </button>
        </form>
        <p style={{ fontSize: 13, color: 'var(--text-secondary)', textAlign: 'center', marginTop: '1rem' }}>
          No account? <Link to="/register">Create one</Link>
        </p>
      </div>
    </main>
  )
}
