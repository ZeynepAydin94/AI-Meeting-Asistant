import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { getMeeting, type ActionItem } from '../api/meetings'

const priorityStyles: Record<ActionItem['priority'], { bg: string; text: string }> = {
  Urgent: { bg: 'var(--danger-bg)', text: 'var(--danger-text)' },
  High: { bg: 'var(--danger-bg)', text: 'var(--danger-text)' },
  Medium: { bg: 'var(--warning-bg)', text: 'var(--warning-text)' },
  Low: { bg: 'var(--surface-1)', text: 'var(--text-secondary)' },
}

export function AnalysisReviewPage() {
  const { id } = useParams<{ id: string }>()
  const { data: meeting, isLoading, isError } = useQuery({
    queryKey: ['meeting', id],
    queryFn: () => getMeeting(id!),
    enabled: !!id,
  })

  if (isLoading) {
    return <p style={{ color: 'var(--text-secondary)' }}>Loading…</p>
  }

  if (isError || !meeting) {
    return <p style={{ color: 'var(--danger-text)' }}>Couldn't load this meeting.</p>
  }

  if (meeting.status === 'Failed') {
    return (
      <div>
        <h2>{meeting.title}</h2>
        <p style={{ color: 'var(--danger-text)', marginTop: 8 }}>
          Analysis failed: {meeting.errorMessage}
        </p>
      </div>
    )
  }

  return (
    <div style={{ maxWidth: 640 }}>
      <h2>{meeting.title}</h2>
      <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: '1rem' }}>
        Analyzed {new Date(meeting.createdAtUtc).toLocaleString()}
      </p>

      <div className="card" style={{ marginBottom: '1rem' }}>
        <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 4 }}>Summary</p>
        <p style={{ fontSize: 14 }}>{meeting.summary}</p>
      </div>

      {meeting.keyDecisions.length > 0 && (
        <div style={{ marginBottom: '1rem' }}>
          <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 8 }}>Key decisions</p>
          <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
            {meeting.keyDecisions.map((d) => (
              <li key={d.id} style={{ fontSize: 14, marginBottom: 4 }}>
                {d.description}
              </li>
            ))}
          </ul>
        </div>
      )}

      <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 8 }}>Action items</p>
      <div style={{ border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
        {meeting.actionItems.length === 0 && (
          <p style={{ fontSize: 14, color: 'var(--text-secondary)', padding: '12px 14px', margin: 0 }}>
            No action items identified.
          </p>
        )}
        {meeting.actionItems.map((item, i) => {
          const style = priorityStyles[item.priority]
          return (
            <div
              key={item.id}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 10,
                padding: '10px 14px',
                borderBottom: i < meeting.actionItems.length - 1 ? '1px solid var(--border)' : 'none',
              }}
            >
              <input type="checkbox" checked={item.suggestedForJira} disabled />
              <div style={{ flex: 1 }}>
                <p style={{ fontSize: 14, margin: 0 }}>{item.description}</p>
                {item.assigneeHint && (
                  <p style={{ fontSize: 12, color: 'var(--text-secondary)', margin: '2px 0 0' }}>
                    Owner hint: {item.assigneeHint}
                  </p>
                )}
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
                {item.priority}
              </span>
            </div>
          )
        })}
      </div>
    </div>
  )
}
