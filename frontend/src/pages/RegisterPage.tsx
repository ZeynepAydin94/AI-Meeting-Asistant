import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/auth-context'
import { ApiError } from '../api/client'

export function RegisterPage() {
  const { register } = useAuth()
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
      await register(email, password)
      navigate('/app/new-meeting')
    } catch (err) {
      if (err instanceof ApiError && err.status === 400) {
        setError('That email is already registered, or the password is too weak (min 8 characters).')
      } else {
        setError('Something went wrong. Try again.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main style={{ maxWidth: 320, margin: '4rem auto' }}>
      <div className="card">
        <h2>Create an account</h2>
        <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: '1.25rem' }}>
          Set up your meeting workspace.
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
            placeholder="At least 8 characters"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={8}
            style={{ width: '100%', marginBottom: 12 }}
          />
          {error && (
            <p style={{ fontSize: 13, color: 'var(--danger-text)', marginBottom: 12 }}>{error}</p>
          )}
          <button type="submit" className="primary" style={{ width: '100%' }} disabled={isSubmitting}>
            {isSubmitting ? 'Creating account…' : 'Create account'}
          </button>
        </form>
        <p style={{ fontSize: 13, color: 'var(--text-secondary)', textAlign: 'center', marginTop: '1rem' }}>
          Already have an account? <Link to="/login">Log in</Link>
        </p>
      </div>
    </main>
  )
}
