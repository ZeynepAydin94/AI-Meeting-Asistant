import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createJiraTickets, getMeeting, type ActionItem } from '../api/meetings'
import { ApiError } from '../api/client'

const priorityStyles: Record<ActionItem['priority'], { bg: string; text: string }> = {
  Urgent: { bg: 'var(--danger-bg)', text: 'var(--danger-text)' },
  High: { bg: 'var(--danger-bg)', text: 'var(--danger-text)' },
  Medium: { bg: 'var(--warning-bg)', text: 'var(--warning-text)' },
  Low: { bg: 'var(--surface-1)', text: 'var(--text-secondary)' },
}

export function AnalysisReviewPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const { data: meeting, isLoading, isError } = useQuery({
    queryKey: ['meeting', id],
    queryFn: () => getMeeting(id!),
    enabled: !!id,
  })

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [ticketError, setTicketError] = useState<string | null>(null)

  useEffect(() => {
    if (!meeting) return
    setSelectedIds(new Set(meeting.actionItems.filter((item) => item.suggestedForJira).map((item) => item.id)))
  }, [meeting?.id])

  const createTicketsMutation = useMutation({
    mutationFn: (actionItemIds: string[]) => createJiraTickets(id!, actionItemIds),
    onSuccess: () => {
      setTicketError(null)
      queryClient.invalidateQueries({ queryKey: ['meeting', id] })
    },
    onError: (err) => {
      setTicketError(err instanceof ApiError ? err.message : 'Something went wrong creating tickets.')
    },
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

  function toggleSelected(itemId: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(itemId)) {
        next.delete(itemId)
      } else {
        next.add(itemId)
      }
      return next
    })
  }

  const ticketableSelectedCount = meeting.actionItems.filter(
    (item) => selectedIds.has(item.id) && item.jiraTicketStatus !== 'Created',
  ).length

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
          const isCreated = item.jiraTicketStatus === 'Created'
          return (
            <div
              key={item.id}
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                gap: 10,
                padding: '10px 14px',
                borderBottom: i < meeting.actionItems.length - 1 ? '1px solid var(--border)' : 'none',
              }}
            >
              <input
                type="checkbox"
                checked={selectedIds.has(item.id)}
                onChange={() => toggleSelected(item.id)}
                disabled={isCreated || createTicketsMutation.isPending}
                style={{ marginTop: 3 }}
              />
              <div style={{ flex: 1 }}>
                <p style={{ fontSize: 14, margin: 0 }}>{item.description}</p>
                {item.assigneeHint && (
                  <p style={{ fontSize: 12, color: 'var(--text-secondary)', margin: '2px 0 0' }}>
                    Owner hint: {item.assigneeHint}
                  </p>
                )}
                {isCreated && item.jiraIssueUrl && (
                  <p style={{ fontSize: 12, margin: '4px 0 0' }}>
                    <a href={item.jiraIssueUrl} target="_blank" rel="noreferrer">
                      View {item.jiraIssueKey} in Jira
                    </a>
                  </p>
                )}
                {item.jiraTicketStatus === 'Failed' && (
                  <p style={{ fontSize: 12, color: 'var(--danger-text)', margin: '4px 0 0' }}>
                    Failed to create ticket: {item.jiraErrorMessage}
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
                  flexShrink: 0,
                }}
              >
                {item.priority}
              </span>
            </div>
          )
        })}
      </div>

      {ticketError && (
        <p style={{ fontSize: 13, color: 'var(--danger-text)', marginTop: 12 }}>{ticketError}</p>
      )}

      {meeting.actionItems.length > 0 && (
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '1rem' }}>
          <button
            type="button"
            className="primary"
            disabled={ticketableSelectedCount === 0 || createTicketsMutation.isPending}
            onClick={() => createTicketsMutation.mutate(Array.from(selectedIds))}
          >
            {createTicketsMutation.isPending
              ? 'Creating tickets…'
              : `Create Jira tickets (${ticketableSelectedCount})`}
          </button>
        </div>
      )}
    </div>
  )
}
