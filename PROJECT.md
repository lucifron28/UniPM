# UniPM Project Memory

## Commands
- **Build**: `dotnet build .\UniPM.slnx`
- **Test**: `dotnet test .\UniPM.slnx --no-build` (or `dotnet test`)
- **Docker Up**: `docker compose up --build -d`
- **Start Database**: `docker compose up -d unipm-db`
- **Start API**: `docker compose up -d unipm-api`
- **Docker Down**: `docker compose down`
- **Migration Add**: `dotnet ef migrations add <Name> --project server`
- **Migration Update**: `$env:ConnectionStrings__DefaultConnection="<String>"; dotnet ef database update --project server`

## Active Context
- **Architecture**: ASP.NET Core API + SQL Server 2025 (Docker local / IIS prod).
- **Core Entities**: `Asset`, `PreventiveMaintenanceSchedule`, `InspectionRecord` scaffolded.
- **Completed**:
  - Docker environment with SQL Server 2025 + Full-Text Search.
  - DB Migration `InitialDomainSchema` applied to container.
  - Minimal API endpoints:
    - POST `/api/v1/assets`, GET `/api/v1/assets/{id}`
    - POST `/api/v1/schedules`
    - POST `/api/v1/inspections`, GET `/api/v1/inspections/history/{assetId}`
  - Unit tests and health checks passing (resolved DBContextFactory injection).

## Upcoming Plans & Tasks
1. **GSD Clarifications**:
   - Resolve form details (verify "Page 2" content for the four forms).
   - Get official list of buildings and departments.
   - Confirm schedule adjustment authority & rules.
2. **Backend & Database**:
   - Add specific detail tables for asset categories (e.g., `FireExtinguisherDetail`).
   - Implement user role management (Admin, GSD, Inspector, Department Head, Supervisor).
   - Setup IIS deployment configuration (remove Docker assumptions for production).
3. **Mobile Sync**:
   - Design offline-first sync APIs (inspectors need local SQLite/Hive/Isar sync).
4. **AI & RAG Support**:
   - Establish vector embeddings pipeline (Gemini Free Tier, DeepSeek V4, Qwen3:4b-instruct with Ollama).
   - Implement Reciprocal Rank Fusion (RRF) combining SQL FTS & Vector search.
