# Handoffs

Handoffs capture the durable memory from a task session so another role or a new
session can continue without reading the full chat history.

Each completed task should write a handoff when follow-up work, review, or risk
remains.

## Template

```md
# <Task Title> Handoff

task:
role:
date:

## Summary

What changed or what decision was made.

## Files Touched

- `path`

## Validation

What was checked, tested, reviewed, or not possible to verify.

## Open Risks

- Risk or ambiguity that remains.

## Next Recommended Action

- Role and task suggestion for the next step.
```
