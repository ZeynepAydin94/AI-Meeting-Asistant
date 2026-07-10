import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { createMeeting } from '../api/meetings'
import { ApiError } from '../api/client'

export function NewMeetingPage() {
  const navigate = useNavigate()
  const [transcriptText, setTranscriptText] = useState('')
  const [error, setError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: createMeeting,
    onSuccess: (meeting) => {
      navigate(`/app/meetings/${meeting.id}`)
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : 'Something went wrong analyzing this meeting.')
    },
  })

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    mutation.mutate(transcriptText)
  }

  return (
    <div>
      <h2>New meeting</h2>
      <p style={{ color: 'var(--text-secondary)', marginTop: 8, marginBottom: '1rem' }}>
        Paste a meeting transcript to get an AI summary, key decisions, and action items.
      </p>
      <form onSubmit={handleSubmit}>
        <textarea
          rows={14}
          placeholder="Paste your meeting transcript here…"
          value={transcriptText}
          onChange={(e) => setTranscriptText(e.target.value)}
          required
          style={{ width: '100%', maxWidth: 640, resize: 'vertical' }}
        />
        {error && (
          <p style={{ fontSize: 13, color: 'var(--danger-text)', marginTop: 12 }}>{error}</p>
        )}
        <div style={{ display: 'flex', justifyContent: 'flex-end', maxWidth: 640, marginTop: '1rem' }}>
          <button type="submit" className="primary" disabled={mutation.isPending}>
            {mutation.isPending ? 'Analyzing…' : 'Analyze meeting'}
          </button>
        </div>
      </form>
    </div>
  )
}
