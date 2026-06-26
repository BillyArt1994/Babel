# Tasks

Task briefs are execution contracts for AI sessions.

Start from `_template.md` when creating a new task. Keep each task focused on one
primary role:

- `designer`: design, balance, GDD, gameplay rules, CSV tuning proposals
- `artist`: visual direction, references, style guide, asset review
- `programmer`: Unity implementation, C# scripts, tests, tools, runtime data

Before editing files, the agent must read the task brief, `AGENTS.md`, and the
declared `role_file`, then restate the current role, allowed paths, forbidden
paths, and validation plan.

Use handoffs when another role needs to continue the work.
