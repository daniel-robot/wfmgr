# Security Baseline

## Purpose

This document defines the initial security baseline for the wfmgr project and future AI agent integration.

## Security Goals

- Protect secrets and credentials
- Prevent unauthorized workflow changes
- Ensure auditability of sensitive actions
- Avoid unsafe autonomous agent behavior
- Preserve data integrity
- Support future regulated enterprise deployment

## Initial Security Principles

1. No secrets in source control.
2. Configuration should be environment-specific.
3. High-risk actions require human approval.
4. Agent actions should be logged.
5. Workflow configuration changes should be auditable.
6. Authorization should be enforced on admin/configuration APIs.
7. Database changes should be reviewed.
8. Deployment operations should be explicit and controlled.

## Agent Security Principles

AI agents may assist with:

- Reading code
- Generating reports
- Suggesting tests
- Reviewing configuration
- Drafting documentation

AI agents must not automatically:

- Deploy systems
- Delete data
- Modify secrets
- Change production configuration
- Run destructive commands
- Bypass tests
- Approve their own changes

## Future Security Work

- Agent permission model
- Human-in-the-loop approval
- Agent audit logs
- Threat model
- Security architecture diagram
- Azure identity and Key Vault integration
- CI/CD security checks