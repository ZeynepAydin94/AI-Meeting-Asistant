import { apiFetch } from './client'

export interface KeyDecision {
  id: string
  description: string
}

export interface ActionItem {
  id: string
  description: string
  assigneeHint: string | null
  priority: 'Low' | 'Medium' | 'High' | 'Urgent'
  suggestedForJira: boolean
  userConfirmed: boolean | null
  suggestedTicketTitle: string | null
  suggestedTicketDescription: string | null
  dueDate: string | null
  jiraTicketStatus: 'Created' | 'Failed' | null
  jiraIssueKey: string | null
  jiraIssueUrl: string | null
  jiraErrorMessage: string | null
}

export interface JiraTicketResult {
  actionItemId: string
  success: boolean
  jiraIssueKey: string | null
  jiraIssueUrl: string | null
  errorMessage: string | null
}

export interface MeetingDetail {
  id: string
  title: string
  createdAtUtc: string
  status: 'Analyzing' | 'Analyzed' | 'Failed'
  summary: string | null
  errorMessage: string | null
  keyDecisions: KeyDecision[]
  actionItems: ActionItem[]
}

export interface MeetingSummary {
  id: string
  title: string
  createdAtUtc: string
  status: 'Analyzing' | 'Analyzed' | 'Failed'
  actionItemCount: number
}

export function createMeeting(transcriptText: string): Promise<MeetingDetail> {
  return apiFetch<MeetingDetail>('/api/meetings', {
    method: 'POST',
    body: JSON.stringify({ transcriptText }),
  })
}

export function listMeetings(): Promise<MeetingSummary[]> {
  return apiFetch<MeetingSummary[]>('/api/meetings')
}

export function getMeeting(id: string): Promise<MeetingDetail> {
  return apiFetch<MeetingDetail>(`/api/meetings/${id}`)
}

export function createJiraTickets(meetingId: string, actionItemIds: string[]): Promise<JiraTicketResult[]> {
  return apiFetch<JiraTicketResult[]>(`/api/meetings/${meetingId}/jira-tickets`, {
    method: 'POST',
    body: JSON.stringify({ actionItemIds }),
  })
}
