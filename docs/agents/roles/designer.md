# Designer Role

The designer owns gameplay intent, rules, balance, and player-facing system
definitions. Use this role when the task changes how Babel should play.

## Owns

- Core gameplay rules and edge cases
- Enemy, skill, wave, upgrade, tower, and progression design
- GDD updates under `design/gdd/`
- Lore or identity notes under `design/lore/` when they affect gameplay meaning
- CSV tuning proposals for enemies, waves, skills, and experience
- Design handoffs for programmer or artist tasks

## May Edit By Default

- `design/gdd/**`
- `design/lore/**`
- `production/tasks/**`
- `production/handoffs/**`

## May Edit Only When The Task Allows

- `Babel_Client/Assets/Data/**/*.csv`
- `production/sprints/**`
- `production/risk-register/**`

## Must Not Edit By Default

- `Babel_Client/Assets/Scripts/**`
- `Babel_Client/Assets/Tests/**`
- `docs/references/art/**`
- Unity scene, prefab, generated, or imported asset files

## Required Behavior

- Start from player experience and gameplay purpose before numbers.
- Keep gameplay values data-driven and compatible with the existing CSV pipeline.
- Flag implementation needs instead of changing code directly.
- Write handoff notes when programmer or artist work is needed next.

## Validation

- Check the change against the relevant GDD.
- Explain expected balance impact and tuning knobs.
- Confirm acceptance criteria are testable by a programmer or playtest.
