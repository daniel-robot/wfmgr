# DevOps Engineer Checklist

## Docker Review

- [ ] Docker Compose file exists and is readable.
- [ ] Service names are clear.
- [ ] Volume mappings are correct.
- [ ] Port bindings are local-only when required.
- [ ] Environment variables are explicit.
- [ ] Secrets are not hardcoded.
- [ ] Health checks exist where useful.
- [ ] Logs are accessible.
- [ ] Container user and permissions are appropriate.
- [ ] Restart policy is understood.

## Hermes / OpenClaw Safety

- [ ] Hermes API uses 127.0.0.1:8642.
- [ ] Hermes Dashboard uses 127.0.0.1:9119.
- [ ] Hermes is not exposed on 0.0.0.0 unless explicitly approved.
- [ ] /Users/daniel/Projects maps to /workspace.
- [ ] Agents use private OpenClaw workspaces under ~/.openclaw/workspace/<agent>.
- [ ] Project files live under /Users/daniel/Projects/wfmgr.
- [ ] Docker-side project path is /workspace/wfmgr.
- [ ] No agent identity files are created directly under /Users/daniel/Projects.

## Environment Config

- [ ] Required environment variables are documented.
- [ ] .env files are not committed if they contain secrets.
- [ ] .env.example or template exists if needed.
- [ ] Local vs Docker paths are documented.
- [ ] macOS-specific path requirements are documented.
- [ ] Required SDK/runtime versions are documented.

## CI/CD Review

- [ ] Build command is known.
- [ ] Test command is known.
- [ ] Lint command is known.
- [ ] Artifact output is known.
- [ ] Secrets are referenced securely.
- [ ] Deployment target is clear.
- [ ] Rollback or recovery approach is documented.

## Observability

- [ ] Health endpoint exists or is planned.
- [ ] Readiness endpoint exists or is planned.
- [ ] Logs are structured or searchable.
- [ ] Errors are visible.
- [ ] Configuration problems are diagnosable.
- [ ] Startup failures are easy to inspect.

## Safety Before Editing

- [ ] .ai/locks checked.
- [ ] Intended files listed.
- [ ] Change is small.
- [ ] Validation command identified.
- [ ] Rollback plan identified.
