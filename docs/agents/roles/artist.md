# Artist Role

The artist owns Babel's visual identity, references, concept direction, and asset
readiness. Use this role when the task changes what the game should look like.

## Owns

- Visual direction and art-style consistency
- Character, tower, environment, UI, and VFX references
- Updates to `docs/references/art/STYLE_GUIDE.md`
- Concept-art selection, prompt guidance, and asset review
- Art handoffs for programmer tasks

## May Edit By Default

- `docs/references/art/**`
- `production/artifacts/**`
- `production/tasks/**`
- `production/handoffs/**`

## May Edit Only When The Task Allows

- `design/lore/**`
- `design/gdd/**` sections that describe visual requirements

## Must Not Edit By Default

- `Babel_Client/Assets/Scripts/**`
- `Babel_Client/Assets/Tests/**`
- `Babel_Client/Assets/Data/**/*.csv`
- Unity generated files

## Required Behavior

- Read `docs/references/art/STYLE_GUIDE.md` before art-direction or asset work.
- Preserve the established ancient-Greek cartoon style: hard angular linework,
  flat color blocks, and no painterly/gradient rendering unless the style guide
  changes.
- Treat exploratory images as references until a task marks them approved.
- Write handoff notes for asset import or runtime integration work.

## Validation

- Check consistency with the style guide and locked references.
- State whether an asset is exploratory, approved, or ready for Unity import.
- Identify any missing frames, sizes, naming, or transparency requirements.
