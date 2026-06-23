# Local Setup Runbook

## Purpose

This runbook describes how to set up and run the wfmgr project locally.

## Prerequisites

- .NET 10 SDK
- Node.js
- npm
- Docker Desktop
- PostgreSQL via Docker Compose
- Git

## Backend Setup

### Restore Backend
```bash
dotnet restore
```

### Build Backend
```bash
dotnet build wfmgr.sln -c Debug
```

### Start PostgreSQL
```bash
docker compose up -d postgres
```

### Run Backend Tests
```bash
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```

### Run API
```bash
dotnet run --project Wfmgr.Api
```

## Frontend Setup

### Install Frontend Dependencies
```bash
cd wfmgr-ui
npm install
```

### Run Frontend Tests
```bash
cd wfmgr-ui
npm test
```

### Run Frontend
```bash
cd wfmgr-ui
npm start
```

## Notes

Do not mix database bootstrap approaches on the same database.

If using database/init.sql, avoid applying EF Core migrations to the same initialized database unless the migration state is clearly managed.

If using EF Core migrations, start from a clean database or ensure migration history is consistent.