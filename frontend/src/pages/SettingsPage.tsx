import { useEffect, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getSettings, updateSettings, testJiraConnection, type ConnectionTestResult } from '../api/settings'

export function SettingsPage() {
  const queryClient = useQueryClient()
  const { data: settings } = useQuery({ queryKey: ['settings'], queryFn: getSettings })

  const [claudeApiKey, setClaudeApiKey] = useState('')
  const [jiraBaseUrl, setJiraBaseUrl] = useState('')
  const [jiraEmail, setJiraEmail] = useState('')
  const [jiraApiToken, setJiraApiToken] = useState('')
  const [jiraDefaultProjectKey, setJiraDefaultProjectKey] = useState('')
  const [jiraDefaultIssueType, setJiraDefaultIssueType] = useState('')
  const [testResult, setTestResult] = useState<ConnectionTestResult | null>(null)

  useEffect(() => {
    if (!settings) return
    setJiraBaseUrl(settings.jiraBaseUrl ?? '')
    setJiraEmail(settings.jiraEmail ?? '')
    setJiraDefaultProjectKey(settings.jiraDefaultProjectKey ?? '')
    setJiraDefaultIssueType(settings.jiraDefaultIssueType ?? '')
  }, [settings])

  function buildRequest() {
    return {
      claudeApiKey: claudeApiKey || undefined,
      jiraBaseUrl,
      jiraEmail,
      jiraApiToken: jiraApiToken || undefined,
      jiraDefaultProjectKey,
      jiraDefaultIssueType,
    }
  }

  const saveMutation = useMutation({
    mutationFn: updateSettings,
    onSuccess: (updated) => {
      queryClient.setQueryData(['settings'], updated)
      setClaudeApiKey('')
      setJiraApiToken('')
    },
  })

  const testMutation = useMutation({
    mutationFn: async () => {
      const updated = await updateSettings(buildRequest())
      queryClient.setQueryData(['settings'], updated)
      setClaudeApiKey('')
      setJiraApiToken('')
      return testJiraConnection()
    },
    onSuccess: (result) => setTestResult(result),
  })

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    saveMutation.mutate(buildRequest())
  }

  return (
    <div style={{ maxWidth: 480 }}>
      <h2>Settings</h2>
      <p style={{ color: 'var(--text-secondary)', marginTop: 8, marginBottom: '1.5rem' }}>
        Connect Claude and Jira so meetings can be analyzed and turned into tickets.
      </p>

      <form onSubmit={handleSubmit}>
        <label style={{ fontSize: 13, color: 'var(--text-secondary)', display: 'block', marginBottom: 4 }}>
          Claude API key
        </label>
        <input
          type="password"
          placeholder={settings?.hasClaudeApiKey ? '•••••••• (already set — leave blank to keep)' : 'sk-ant-…'}
          value={claudeApiKey}
          onChange={(e) => setClaudeApiKey(e.target.value)}
          style={{ width: '100%', marginBottom: '1.25rem' }}
        />

        <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 6 }}>Jira connection</p>
        <input
          type="text"
          placeholder="https://yourteam.atlassian.net"
          value={jiraBaseUrl}
          onChange={(e) => setJiraBaseUrl(e.target.value)}
          style={{ width: '100%', marginBottom: 8 }}
        />
        <input
          type="email"
          placeholder="you@company.com"
          value={jiraEmail}
          onChange={(e) => setJiraEmail(e.target.value)}
          style={{ width: '100%', marginBottom: 8 }}
        />
        <input
          type="password"
          placeholder={settings?.hasJiraApiToken ? '•••••••• (already set — leave blank to keep)' : 'Jira API token'}
          value={jiraApiToken}
          onChange={(e) => setJiraApiToken(e.target.value)}
          style={{ width: '100%', marginBottom: 8 }}
        />
        <div style={{ display: 'flex', gap: 8, marginBottom: '1.25rem' }}>
          <input
            type="text"
            placeholder="Default project key (e.g. ENG)"
            value={jiraDefaultProjectKey}
            onChange={(e) => setJiraDefaultProjectKey(e.target.value)}
            style={{ flex: 1 }}
          />
          <input
            type="text"
            placeholder="Default issue type (e.g. Task)"
            value={jiraDefaultIssueType}
            onChange={(e) => setJiraDefaultIssueType(e.target.value)}
            style={{ flex: 1 }}
          />
        </div>

        {testResult && (
          <p
            style={{
              fontSize: 13,
              color: testResult.success ? 'var(--success-text)' : 'var(--danger-text)',
              marginBottom: '1rem',
            }}
          >
            {testResult.message}
          </p>
        )}

        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
          <button type="button" onClick={() => testMutation.mutate()} disabled={testMutation.isPending}>
            {testMutation.isPending ? 'Testing…' : 'Test Jira connection'}
          </button>
          <button type="submit" className="primary" disabled={saveMutation.isPending}>
            {saveMutation.isPending ? 'Saving…' : 'Save settings'}
          </button>
        </div>
      </form>
    </div>
  )
}
