import { apiFetch } from './client'

export interface SettingsResponse {
  hasClaudeApiKey: boolean
  jiraBaseUrl: string | null
  jiraEmail: string | null
  hasJiraApiToken: boolean
  jiraDefaultProjectKey: string | null
  jiraDefaultIssueType: string | null
}

export interface UpdateSettingsRequest {
  claudeApiKey?: string
  jiraBaseUrl: string
  jiraEmail: string
  jiraApiToken?: string
  jiraDefaultProjectKey: string
  jiraDefaultIssueType: string
}

export interface ConnectionTestResult {
  success: boolean
  message: string | null
}

export function getSettings(): Promise<SettingsResponse> {
  return apiFetch<SettingsResponse>('/api/settings')
}

export function updateSettings(request: UpdateSettingsRequest): Promise<SettingsResponse> {
  return apiFetch<SettingsResponse>('/api/settings', {
    method: 'PUT',
    body: JSON.stringify(request),
  })
}

export function testJiraConnection(): Promise<ConnectionTestResult> {
  return apiFetch<ConnectionTestResult>('/api/settings/test-jira-connection', {
    method: 'POST',
  })
}
