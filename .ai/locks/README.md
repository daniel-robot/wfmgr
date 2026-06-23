# Agent Locks

Use this folder to prevent multiple agents from editing the same files at the same time.

Rules:
1. Before editing files, an agent must check this folder.
2. If another lock affects the same files, do not edit.
3. Create a lock before editing.
4. Remove the lock after work is complete.
5. Update .ai/handoffs/current-handoff.md after work.

Lock filename:
<agent-id>.lock.md
