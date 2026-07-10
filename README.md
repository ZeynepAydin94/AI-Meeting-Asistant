# AI Meeting Assistant

Paste a meeting transcript, get an AI-generated summary, key decisions, and action items, and turn the ones that need follow-up into real Jira tickets.

## Stack

- **Backend**: ASP.NET Core Web API (.NET 10), EF Core + PostgreSQL, ASP.NET Core Identity + JWT auth
- **Frontend**: React + TypeScript (Vite), React Router, TanStack Query
- **AI**: Claude API (Anthropic)
- **Tickets**: Jira Cloud REST API

## Repository layout

```
backend/    ASP.NET Core solution (Api / Core / Infrastructure)
frontend/   Vite + React + TypeScript app
docs/       plan/notes
```

## Prerequisites

- .NET 10 SDK
- Node.js 20+
- PostgreSQL (local install, or via Docker: `docker run --name meeting-assistant-db -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16`)
- An Anthropic API key (for Claude analysis)
- A Jira Cloud site, account email, and API token (for ticket creation) — configured later, from the app's Settings page

## Running locally

### Backend

```
cd backend
dotnet user-secrets init --project src/AiMeetingAssistant.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=ai_meeting_assistant;Username=postgres;Password=postgres" --project src/AiMeetingAssistant.Api
dotnet run --project src/AiMeetingAssistant.Api
```

API listens on `http://localhost:5130` by default (see `Properties/launchSettings.json`). Health check: `GET /api/health`.

### Frontend

```
cd frontend
cp .env.example .env.local
npm install
npm run dev
```

App runs on `http://localhost:5173` and talks to the API via `VITE_API_BASE_URL`.

## Status

Actively being built in phases — see [docs/plan.md](docs/plan.md) for the full build plan. Current phase: auth (ASP.NET Core Identity + JWT).
