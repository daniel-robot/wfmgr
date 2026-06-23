# DevOps Engineer Memory

## Environment Notes

Add stable environment and operations knowledge here.

Examples:
- Local setup commands
- Docker Compose service names
- Required environment variables
- Health check endpoints
- Common troubleshooting steps
- Known port mappings
- Known volume mappings
- CI/CD commands
- Deployment assumptions

## Known Local Services

Hermes API:
- http://127.0.0.1:8642/v1

Hermes Dashboard:
- http://127.0.0.1:9119

Project mount:
- macOS: /Users/daniel/Projects
- Hermes Docker: /workspace

Current project:
- macOS: /Users/daniel/Projects/wfmgr
- Hermes Docker: /workspace/wfmgr

## Validation Commands

Record known validation commands here.

Docker:
- docker ps
- docker compose ps
- docker compose logs

Hermes:
- curl http://127.0.0.1:8642/v1/models
- curl http://127.0.0.1:9119

Project:
- TBD

## Known Risks

- Accidentally exposing Hermes API or dashboard publicly
- Docker Desktop not sharing /Users/daniel/Projects
- Agent files created at /Users/daniel/Projects root
- Secrets committed to repo
- Multiple agents editing the same config files
- Environment-specific config drift
