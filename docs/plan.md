# AI Meeting Assistant — Initial Build Plan

## Context

The user wants a brand-new project (working directory currently empty): an app where they paste/upload a meeting **text transcript**, AI analyzes it to produce a summary, key decisions, and action items, and for action items that represent real follow-up work, the app creates real Jira tickets via the Jira Cloud REST API.

Confirmed choices from the user:
- Input: **text transcript only** (audio/video upload was considered and explicitly dropped — text-only for v1)
- Frontend: React (TypeScript + Vite)
- Backend: C#, **.NET 10** (confirmed installed locally: SDK 10.0.100)
- Database: **PostgreSQL** (relational data — Users, Meetings, ActionItems, JiraTickets — fits EF Core well)
- Auth: **Login required**, using ASP.NET Core Identity (built-in, security-reviewed password/user management) issuing **JWT** bearer tokens consumed by the React frontend
- Jira: **real** Jira Cloud API integration (not mocked)
- AI: **Claude API** (Anthropic), pay-as-you-go
- UI: reviewed and approved via an interactive mockup (sidebar nav: New meeting / Review / History / Settings, plus a Login screen) — see §3 for the approved layout

## 1. Repository layout

```
AI Meeting Assistant/
├── backend/
│   ├── AiMeetingAssistant.sln
│   └── src/
│       ├── AiMeetingAssistant.Api/              # ASP.NET Core Web API (.NET 10), minimal API endpoints, Program.cs, JWT auth middleware
│       ├── AiMeetingAssistant.Core/             # Entities, DTOs, service interfaces
│       └── AiMeetingAssistant.Infrastructure/   # EF Core + PostgreSQL (Npgsql), Identity, AnthropicClient, JiraClient
├── frontend/
│   ├── package.json, vite.config.ts, tsconfig.json, .env.example
│   └── src/
│       ├── api/            # typed fetch client, attaches JWT to requests
│       ├── auth/            AuthContext, ProtectedRoute
│       ├── pages/          LoginPage, RegisterPage, NewMeetingPage, AnalysisReviewPage, MeetingHistoryPage, SettingsPage
│       ├── components/      SummaryCard, ActionItemRow, StatusBadge, AppShell (sidebar nav)
│       ├── hooks/            useAuth, useMeeting, useMeetings, useSettings (TanStack Query)
│       └── types/            mirrors backend DTOs
├── docs/
├── .gitignore
└── README.md
```

3-project backend split (Api / Core / Infrastructure) — enough separation to keep Claude/Jira clients swappable and testable, without CQRS/mediator ceremony a solo tool doesn't need.

## 2. Backend

**Auth**: ASP.NET Core Identity (`IdentityUser`-based `AppUser`) backed by PostgreSQL, with `AddAuthentication().AddJwtBearer(...)` for token validation. `POST /api/auth/register`, `POST /api/auth/login` (returns JWT + expiry). All meeting/settings endpoints require `[Authorize]`; data is scoped per-user (a `UserId` FK on `Meeting` and `AppSettings`) so each login only sees their own meetings.

**Core interfaces**: `IMeetingAnalysisService` (orchestrates Claude call), `IAnthropicClient` (hand-rolled typed HTTP wrapper around Anthropic's Messages API — no official C# SDK exists), `IJiraClient` (create issue + connection test), `ISettingsService` (CRUD over the current user's settings).

**Data model (EF Core + PostgreSQL/Npgsql)**:
- `AppUser` (Identity-managed: Id, Email, PasswordHash, etc.)
- `Meeting` (Id, UserId FK, Title, CreatedAt, OriginalTranscriptText, Status [`Analyzing`|`Analyzed`|`Failed`], SummaryText)
- `KeyDecision` (Id, MeetingId FK, Description)
- `ActionItem` (Id, MeetingId FK, Description, AssigneeHint, Priority, SuggestedForJira, UserConfirmed, SuggestedTicketTitle/Description)
- `JiraTicket` (Id, ActionItemId FK, JiraIssueKey, JiraIssueUrl, Status, ErrorMessage, CreatedAt)
- `AppSettings` (Id, UserId FK — one row per user: ClaudeApiKey, JiraBaseUrl, JiraEmail, JiraApiToken, JiraDefaultProjectKey, JiraDefaultIssueType)

**API endpoints**:
- `POST /api/auth/register`, `POST /api/auth/login`
- `POST /api/meetings` — analyze a pasted/uploaded transcript synchronously (text is fast enough for Claude to handle inline — no background job needed now that audio is out of scope)
- `GET /api/meetings`, `GET /api/meetings/{id}`
- `POST /api/meetings/{id}/action-items/{itemId}/confirm` — edit/confirm before ticketing
- `POST /api/meetings/{id}/jira-tickets` — create Jira issues for confirmed items
- `GET/PUT /api/settings`, `POST /api/settings/test-jira-connection`, `POST /api/settings/test-claude-connection`

Minimal API endpoint groups, CORS enabled for the Vite dev origin.

## 3. Frontend (per approved mockup)

App shell: left sidebar with **New meeting / Review / History / Settings** nav, gated behind a **Login** screen (email + password, link to register) using React Router + a `ProtectedRoute` wrapper. JWT stored client-side (e.g. memory + refresh via httpOnly cookie or localStorage — finalize during Phase 1 implementation) and attached as `Authorization: Bearer` on API calls.

- **LoginPage / RegisterPage**: centered card, email + password fields.
- **NewMeetingPage**: toggle button row (kept for future extensibility but only "Paste transcript" is wired up), large textarea, "Analyze meeting" button.
- **AnalysisReviewPage**: summary card (`--surface-1` tinted block), key-decisions/action-items list with checkboxes pre-checked per Claude's suggestion, priority badges (color-coded by role token — danger/warning/neutral), "Create Jira tickets" button, per-item success/failure with a link to the created issue.
- **MeetingHistoryPage**: bordered row list — title, date, action-item count, tickets-created count, status badge.
- **SettingsPage**: Claude API key (masked), Jira base URL/email/API token (masked), default project/issue-type selects (populated from Jira via a "load" call, not hardcoded), Test Connection buttons for both Claude and Jira.

TanStack Query for server state; no separate global store needed.

## 4. Claude API integration

- Model: `claude-sonnet-5`, via the Messages API (`x-api-key` + `anthropic-version` headers).
- **Forced structured output via tool use**: a single tool `extract_meeting_analysis` with a JSON schema for `summary`, `key_decisions[]`, and `action_items[]` (`description`, `owner_hint`, `priority`, `requires_jira_ticket`, `suggested_ticket_title/description`), called with `tool_choice: { type: "tool", name: "extract_meeting_analysis" }` so the response is guaranteed well-formed JSON.
- System prompt defines clear criteria for `requires_jira_ticket` (concrete deliverable + ownership vs. FYI/discussion) to avoid over-triggering.

## 5. Jira integration

- Auth: HTTP Basic (`base64(email:apiToken)`), Atlassian's documented API-token approach, built per-request from the logged-in user's stored settings.
- Create issue: `POST {JiraBaseUrl}/rest/api/3/issue` with `fields.project.key`, `fields.summary`, `fields.issuetype.name`, `fields.description` (Atlassian Document Format, not a plain string).
- Assignee: Jira Cloud needs an `accountId`, not free text — v1 folds Claude's `owner_hint` into the description; real account lookup (`/rest/api/3/user/search`) is a later enhancement.
- Settings page offers a "load issue types from Jira" helper instead of hardcoding values like `"Task"`.
- Per-ticket errors (bad project/issuetype, bad creds) surface individually in the review UI.

## 6. Local dev / secrets

- Backend: PostgreSQL connection string + JWT signing key in `appsettings.Development.json` (gitignored) or `dotnet user-secrets`. Provider credentials (Claude/Jira) are stored per-user in the DB, edited via the Settings page — never hardcoded. Consider `IDataProtector` encryption on those secret columns as a near-term hardening step given real credentials are now tied to real user accounts.
- Frontend: `.env`/`.env.local` for `VITE_API_BASE_URL` only.
- `.gitignore`: `appsettings.Development.json`, `.env`, `.env.local`, standard `bin/obj/node_modules`.
- Local Postgres: run via Docker (`docker run postgres:16`) or a local install — document in README.

## 7. Phased build order

1. **Scaffold**: solution + projects, Postgres connection + EF Core migrations for Identity + core entities, Vite+React+TS app, health-check endpoint, CORS, git init, README.
2. **Auth**: Identity + JWT issuance/validation, Register/Login endpoints, LoginPage/RegisterPage, ProtectedRoute + AppShell nav on the frontend.
3. **Text → Claude analysis → review UI**: `POST /api/meetings`, `IAnthropicClient`/`IMeetingAnalysisService`, `NewMeetingPage` + `AnalysisReviewPage` (read-only first) + `MeetingHistoryPage`. **Delivers working value**: log in, paste a transcript, get an AI summary/decisions/action items.
4. **Settings + Jira ticket creation**: `AppSettings` CRUD + `SettingsPage`, `IJiraClient`, `POST /api/meetings/{id}/jira-tickets`, editable/checkable action items, per-item creation results, connection-test buttons.
5. **Polish**: Jira account-id lookup for real assignee mapping, dynamic issue-type loading, secret encryption at rest, richer retry UX, pagination. (Audio/transcription intentionally out of scope — can be revisited later as its own phase if wanted.)

## Verification

- After Phase 2: register a user, log in, confirm the JWT gates access to the app shell and a logged-out user is redirected to Login.
- After Phase 3: paste a sample transcript, confirm summary/decisions/action items render correctly and persist per-user in history.
- After Phase 4: configure real Jira creds in Settings, use "Test Connection", create a ticket from a sample action item, confirm it appears in the real Jira project via the returned link.
- Throughout: git init early so each phase can be committed incrementally.
