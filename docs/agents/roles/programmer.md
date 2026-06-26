# Programmer Role

The programmer owns Unity implementation, tests, runtime integration, and tools.
Use this role when the task changes how Babel runs.

## Owns

- C# implementation under `Babel_Client/Assets/Scripts/`
- EditMode tests under `Babel_Client/Assets/Tests/EditMode/`
- Data parsers, runtime systems, editor tools, and Unity integration
- CSV edits only when a task explicitly includes implementation data changes
- Programmer handoffs for designer or artist review

## May Edit By Default

- `Babel_Client/Assets/Scripts/**`
- `Babel_Client/Assets/Tests/**`
- `production/tasks/**`
- `production/handoffs/**`

## May Edit Only When The Task Allows

- `Babel_Client/Assets/Data/**/*.csv`
- `Babel_Client/Assets/Resources/**`
- Unity scene, prefab, or imported asset files
- `design/gdd/**` implementation notes

## Must Not Edit By Default

- `docs/references/art/**`
- `design/lore/**`
- Final design, balance, or art direction decisions
- QFramework generated `.Designer.cs` files

## Required Behavior

- Follow existing QFramework and Babel namespace conventions.
- Prefer existing local patterns over new abstractions.
- Keep gameplay values in CSV/data unless the task explicitly permits a code
  default.
- Use `BabelLogger` or the `[BABEL][SystemName]` logging style.
- Use NonAlloc physics queries and preallocated buffers in performance-sensitive
  code.
- Protect unrelated dirty worktree changes.

## Validation

- Add or update focused EditMode tests when logic changes.
- Run relevant tests when possible, or explain why they could not be run.
- Describe any manual Unity validation needed.
