import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { listMeetings, type MeetingSummary } from '../api/meetings'

const statusStyles: Record<MeetingSummary['status'], { bg: string; text: string }> = {
  Analyzed: { bg: 'var(--success-bg)', text: 'var(--success-text)' },
  Analyzing: { bg: 'var(--warning-bg)', text: 'var(--warning-text)' },
  Failed: { bg: 'var(--danger-bg)', text: 'var(--danger-text)' },
}

export function MeetingHistoryPage() {
  const { data: meetings, isLoading } = useQuery({
    queryKey: ['meetings'],
    queryFn: listMeetings,
  })

  return (
    <div style={{ maxWidth: 640 }}>
      <h2>Meeting history</h2>

      {isLoading && (
        <p style={{ color: 'var(--text-secondary)', marginTop: 8 }}>Loading…</p>
      )}

      {!isLoading && meetings?.length === 0 && (
        <p style={{ color: 'var(--text-secondary)', marginTop: 8 }}>
          No meetings yet. Start with a new one.
        </p>
      )}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: '1rem' }}>
        {meetings?.map((meeting) => {
          const style = statusStyles[meeting.status]
          return (
            <Link
              key={meeting.id}
              to={`/app/meetings/${meeting.id}`}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 12,
                border: '1px solid var(--border)',
                borderRadius: 8,
                padding: '10px 14px',
                textDecoration: 'none',
                color: 'var(--text-primary)',
              }}
            >
              <div style={{ flex: 1 }}>
                <p style={{ fontSize: 14, margin: 0 }}>{meeting.title}</p>
                <p style={{ fontSize: 12, color: 'var(--text-secondary)', margin: '2px 0 0' }}>
                  {new Date(meeting.createdAtUtc).toLocaleString()} · {meeting.actionItemCount} action items
                </p>
              </div>
              <span
                style={{
                  background: style.bg,
                  color: style.text,
                  fontSize: 12,
                  padding: '2px 10px',
                  borderRadius: 6,
                }}
              >
                {meeting.status}
              </span>
            </Link>
          )
        })}
      </div>
    </div>
  )
}
