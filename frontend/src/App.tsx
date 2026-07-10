import { useEffect, useState } from 'react'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5130'

function App() {
  const [apiStatus, setApiStatus] = useState<'checking' | 'ok' | 'error'>('checking')

  useEffect(() => {
    fetch(`${API_BASE_URL}/api/health`)
      .then((res) => (res.ok ? setApiStatus('ok') : setApiStatus('error')))
      .catch(() => setApiStatus('error'))
  }, [])

  return (
    <main style={{ maxWidth: 480, margin: '4rem auto', fontFamily: 'sans-serif' }}>
      <h1>AI meeting assistant</h1>
      <p>API connection: {apiStatus}</p>
    </main>
  )
}

export default App
