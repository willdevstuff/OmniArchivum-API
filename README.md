
# OmniArchivum API

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

---

## Tech Stack

| Layer        | Technology                         |
|--------------|------------------------------------|
| Runtime      | .NET 10 (ASP.NET Core Web API)     |
| Database     | PostgreSQL 16 (Docker)             |
| ORM          | Entity Framework Core (Npgsql)     |
| Search       | PostgreSQL `tsvector` + GIN index  |
| API Docs     | OpenAPI + Scalar                   |
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

### 4. Open API documentation

http://localhost:5000/scalar/v1

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
- Deployment / CI configuration

---

## Configuration

Local development connection strings are excluded from source control via `.gitignore`.
