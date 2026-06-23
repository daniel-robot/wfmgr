# Environment Template

## Purpose

Describe the environment this file documents.

## Local Paths

macOS/OpenClaw project path:
- /Users/daniel/Projects/wfmgr

Hermes Docker project path:
- /workspace/wfmgr

Docker mount:
- /Users/daniel/Projects -> /workspace

## Required Tools

- Docker:
- Docker Compose:
- .NET SDK:
- Node.js:
- npm:
- Azure CLI:
- Other:

## Required Local Services

| Service | URL / Port | Required | Notes |
|---|---|---|---|
| Hermes API | http://127.0.0.1:8642/v1 | Yes | Must remain local-only |
| Hermes Dashboard | http://127.0.0.1:9119 | Optional | Must remain local-only |

## Environment Variables

| Name | Required | Example / Description | Secret? |
|---|---|---|---|
|  |  |  |  |

## Setup Commands

Add commands here.

## Validation Commands

Add commands here.

## Troubleshooting

Add common issues and fixes here.

## Security Notes

- Do not commit secrets.
- Keep local-only services bound to 127.0.0.1.
- Review Docker port bindings before exposing services.
