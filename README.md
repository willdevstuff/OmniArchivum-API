
# OmniArchivum API

[![CI](https://github.com/willdevstuff/OmniArchivum-API/actions/workflows/ci.yml/badge.svg)](https://github.com/willdevstuff/OmniArchivum-API/actions/workflows/ci.yml)

**OmniArchivum** is a structured backend knowledge archive API built with **ASP.NET Core 10**, **PostgreSQL**, and **Entity Framework Core**.

It is designed to store and query technical knowledge such as game development setups, programming solutions, music production chains, and workflow notes using rich search and flexible tagging.

---

## Features

### Core Architecture

- ASP.NET Core Web API (.NET 10)
- Layered architecture (Controllers → Services → Data)
- PostgreSQL 16 (Dockerized)
- Entity Framework Core (Npgsql)
- OpenAPI + Scalar interactive documentation
- EF Core migrations for schema evolution

### Data Model

- `Note` entities with Markdown content
- `Tag` entities (many-to-many with notes)
- Soft delete via global query filter
- Full-text search using PostgreSQL `tsvector` + GIN index

### Search & Filtering

- Full-text search endpoint  
  `/api/notes/search?q=<query>`

- Multi-tag AND filtering  
  `/api/notes?tag=unity&tag=fmod`

### Frontend

- Plain HTML, CSS, and vanilla JavaScript (no framework, no build step)
- Lists, searches, and paginates notes
- Filters notes by tag, with tags grouped and color-coded by category
- Creates, edits, and deletes notes; links/unlinks tags on notes; deletes tags outright
- Talks to the API directly via `fetch`

### Testing

- Unit tests for the service layer against SQLite in-memory (fast, no Docker required)
- Integration tests against a real, disposable PostgreSQL container (Testcontainers) — covers full-text search behavior that no in-memory provider can reproduce
- HTTP-level tests via `WebApplicationFactory` exercising the real routing/DI pipeline

---

## Tech Stack

| Layer        | Technology                         |
|--------------|------------------------------------|
| Runtime      | .NET 10 (ASP.NET Core Web API)     |
| Frontend     | HTML, CSS, vanilla JavaScript      |
| Database     | PostgreSQL 16 (Docker)             |
| ORM          | Entity Framework Core (Npgsql)     |
| Search       | PostgreSQL `tsvector` + GIN index  |
| API Docs     | OpenAPI + Scalar                   |
| Testing      | xUnit, SQLite (unit), Testcontainers + Postgres (integration) |
| Tooling      | PowerShell dev helpers             |
| Versioning   | Git + GitHub                       |

---

## Getting Started

### Requirements

- .NET 10 SDK
- Docker Desktop
- PowerShell (optional, for dev tooling)

---

### 1. Clone the repository

git clone https://github.com/willdevstuff/OmniArchivum-API.git
cd OmniArchivum-API

### 2. Start PostgreSQL via Docker

docker compose up -d

### 3. Run the API

cd OmniArchivum.Api
dotnet run

In Development, the API automatically applies EF Core migrations and seeds a sample "Test Note" (with a "test-tag" tag) on first run if the database is empty, so there's something to see immediately.

### 4. Open API documentation

http://localhost:5000/scalar/v1

### 5. Run the frontend

The frontend is static HTML/CSS/JS with no build step, but it uses ES modules, which browsers won't load over `file://`. Serve it with any static file server while the API (step 3) is running:

cd frontend
python -m http.server 8080

Then open http://localhost:8080. It talks to the API at `http://localhost:5000` by default (see `API_BASE` in `frontend/js/api.js`).

### 6. Run the tests

dotnet test

Requires Docker Desktop running (the integration tests spin up a real, disposable PostgreSQL container via Testcontainers — this is deliberate, since full-text search relies on `tsvector`/GIN behavior that's specific to Postgres and can't be validated against an in-memory or SQLite substitute). Everything else in the suite runs against SQLite in-memory, so most of it is fast and doesn't touch Docker at all.

---

## Example Usage

### Create a Note

Invoke-RestMethod -Method POST `
  -Uri "http://localhost:5000/api/Notes" `
  -ContentType "application/json" `
  -Body '{"title":"Test Note","bodyMarkdown":"Test information."}'

### Search Notes

Invoke-RestMethod -Method GET `
  -Uri "http://localhost:5000/api/Notes/search?q=test"

### Filter by Tags (AND logic)

Invoke-RestMethod -Method GET `
  -Uri "http://localhost:5000/api/Notes?tag=programming&tag=csharp"

---

## Developer Tooling

Optional PowerShell helper functions are located in:

Scripts/dev-tools.ps1

Load them with:

. .\Scripts\dev-tools.ps1

| Command          | Description                        | Example                                          |
| ---------------- | ---------------------------------- | ------------------------------------------------ |
| `oa-note-new`    | Create a new note                  | `oa-note-new "Test Note" "Test info"` |
| `oa-notes`       | List all notes                     | `oa-notes`                                       |
| `oa-tag-new`     | Create a new tag (Name + Cateogry (Optional))                   | `oa-tag-new "csharp" "programminglanguage"`                      |
| `oa-tags`        | List all tags                      | `oa-tags`                                        |
| `oa-tag-link`    | Link a tag to a note               | `oa-tag-link <TAG_ID> <NOTE_ID>`                 |
| `oa-notes-bytag` | Filter notes by tag(s) (AND logic) | `oa-notes-bytag programming csharp`                      |
| `oa-search`      | Full-text search                   | `oa-search "test"`                          |


---

## Architecture Overview

HTTP Request
    ->
Controller
    ->
Service Layer
    ->
Entity Framework Core
    ->
PostgreSQL (Docker)

---

## Key Design Decisions

- Separation of concerns via layered architecture
- Soft delete implemented with EF Core global query filters
- Full-text search using PostgreSQL `tsvector` + GIN indexing
- Multi-tag AND filtering using repeated query parameters

---

## Planned Enhancements

- Note revision history
- Tag negation filtering
- Hierarchical categories
- Authentication and user support
- Deployment

---

## Configuration

Local development connection strings are excluded from source control via `.gitignore`.
