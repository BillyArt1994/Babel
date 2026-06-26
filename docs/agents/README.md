# Agent Roles

Babel uses task-driven role routing instead of many permanent agent profiles.

Every non-trivial task should declare one primary role:

- `designer` -> `docs/agents/roles/designer.md`
- `artist` -> `docs/agents/roles/artist.md`
- `programmer` -> `docs/agents/roles/programmer.md`

The task brief under `production/tasks/` decides which role applies, what must be
read, what paths are allowed, and how completion is validated.

If work spans multiple roles, split it into multiple tasks and connect them with
handoffs under `production/handoffs/`.
