# Current Agent Handoff

## Current Goal

First safe end-to-end workflow test of the AI Agile coordination system. Refine the feature "Review the workflow configuration page and identify the smallest useful improvement" into a concrete user story.

## From Agent

Project Manager

## To Agent

Product Owner

## Current Context

The project is wfmgr, an Angular + .NET workflow manager. No backlog items exist yet — this will be the first. The workflow configuration page lives at wfmgr-ui/src/app/pages/workflow-config/. The page has two sub-components (workflow-step-edit, workflow-step-row) plus drag-and-drop sorting via reordable-table. No active locks exist. No other agents are working.

## What Product Owner Should Do

1. Read the workflow config page files to understand what the page currently offers.
2. Write one user story for the single smallest useful improvement visible on that page.
3. Add 3-5 acceptance criteria.
4. Add a definition of done (must include: app builds, page renders).
5. Update /workspace/wfmgr/.ai/backlog.md by adding the story under ## Ready.

## What Product Owner Should Not Do

- Do not edit production code.
- Do not edit project-context.md, active-sprint.md, current-handoff.md, or lock files.
- Do not expand scope beyond one smallest-improvement story.
- Do not create new files outside the .ai directory.
- Do not implement the change — only define the story.

## Expected Output

One user story added to /workspace/wfmgr/.ai/backlog.md under ## Ready, with: title, description, acceptance criteria (3-5 items), definition of done, and constraint that it must be the single smallest useful visible improvement.

## Relevant Files / Folders

- /workspace/wfmgr/wfmgr-ui/src/app/pages/workflow-config/workflow-config.page.html
- /workspace/wfmgr/wfmgr-ui/src/app/pages/workflow-config/workflow-config.page.ts
- /workspace/wfmgr/wfmgr-ui/src/app/core/services/workflow-api.service.ts
- /workspace/wfmgr/wfmgr-ui/src/app/core/models/workflow.models.ts
- /workspace/wfmgr/.ai/backlog.md (output file)

## Constraints

- Single smallest useful improvement only — no scope creep.
- Change must be reviewable in under 10 minutes of code review.
- Validation must include: app builds cleanly, page renders without errors.

## Risks / Watchouts

- No one has used these agile files before — first usage may show holes in the template structure.
- The PO has no prior backlog to reference, so this is a greenfield definition.
- Keep the story small enough that a single agent can implement in one session.

## Suggested Next Agent

Architect — after the PO completes the story, the Architect should verify the story fits the existing architecture cleanly.

## Copy-Paste Prompt For Product Owner

> **Role:** Product Owner for the wfmgr project.
>
> **Goal:** Refine the feature "Review the workflow configuration page and identify the smallest useful improvement" into a concrete user story. This is the first backlog item ever — set the quality bar.
>
> **Project context:** wfmgr is an Angular + .NET workflow manager. The workflow configuration page lives at wfmgr-ui/src/app/pages/workflow-config/.
>
> **Instructions:**
> 1. Read the files listed in "Relevant Files / Folders" above to understand the current page.
> 2. Identify the single smallest useful improvement a user would notice on that page.
> 3. Write one user story in the standard format: "As a [user], I want [goal] so that [reason]."
> 4. Write 3-5 acceptance criteria. Each should be testable.
> 5. Write a definition of done — must include validation that the app builds (ng build) and the page renders without errors.
> 6. Add the story under ## Ready in /workspace/wfmgr/.ai/backlog.md.
>
> **Constraints:**
> - Smallest possible improvement only. If the change takes more than 30 minutes to implement, it's too big.
> - Do not edit production code.
> - Do not edit handoff files, sprint files, or project context.
> - Do not create files outside .ai/.
>
> **Output file:** /workspace/wfmgr/.ai/backlog.md
>
> **Handoff back to:** PM (Project Manager) when done.
