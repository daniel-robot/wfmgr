# Local Hermes Safety Checklist

Hermes local setup is safe when:

- [ ] Hermes API is available at http://127.0.0.1:8642/v1.
- [ ] Hermes Dashboard is available at http://127.0.0.1:9119.
- [ ] Hermes API is not exposed on 0.0.0.0.
- [ ] Hermes Dashboard is not exposed on 0.0.0.0.
- [ ] Docker maps /Users/daniel/Projects to /workspace.
- [ ] Project work happens under /workspace/wfmgr inside Docker.
- [ ] Project work happens under /Users/daniel/Projects/wfmgr on macOS.
- [ ] Agent identity files are not created directly under /Users/daniel/Projects.
- [ ] Secrets are not written to project memory files.
