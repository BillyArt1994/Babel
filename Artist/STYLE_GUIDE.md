# Babel Global Art Style Guide

This document defines Babel's project-wide art direction and macro visual
targets. It is the routing entry point for durable art rules, not a catch-all
bucket for character or environment details.

## Related Documents

- Character rules, enemy species identity, character anchors, character prompt
  recipes, and character generation pitfalls:
  [`art-style-guide/character-style.md`](art-style-guide/character-style.md)
- Tower, scene, background, ground, environment composition, and construction
  grammar:
  [`art-style-guide/environment-style.md`](art-style-guide/environment-style.md)

## Where To Record New Art Decisions

When iterating on art assets, classify the decision by scope before writing it
down:

- **Global art direction** belongs in this file. Use it for rendering direction,
  macro style targets, cross-asset readability, asset-path conventions, and
  rules that apply across characters, environments, UI, and VFX.
- **Character-specific decisions** belong in
  `art-style-guide/character-style.md`. Use it for anatomy, species identity,
  silhouettes, clothing, props, anchors, prompt recipes, and character-specific
  generation pitfalls.
- **Environment-specific decisions** belong in
  `art-style-guide/environment-style.md`. Use it for tower visuals, backgrounds,
  ground planes, scene composition, theme language, and construction grammar.

Temporary explorations can stay in chat or under `concept-art/`. Promote a rule
into these documents only when it should guide future sessions.

## Global Style Targets

### Shape Language

- Prefer strong, readable silhouettes over dense surface detail.
- Use crisp, hand-drawn contours with controlled angularity.
- Let large shapes carry identity before texture or ornament does.
- Avoid noisy detail that competes with gameplay targets.

### Rendering

- Favor flat color blocks and hard-edged cel shading.
- Keep each major color to a small number of clear value steps.
- Use hard edges for silhouettes, cast forms, and major material boundaries.
- Avoid soft gradients, airbrushed volume, glossy highlights, painterly
  over-rendering, and realism-first lighting.

### Tone Boundaries

- Aim for exaggerated, satirical, grotesque, hand-drawn cartoon energy.
- Avoid realistic war art, generic dark fantasy painting, cute chibi language,
  and modern sci-fi mechanical styling.
- Enemy art should not become handsome, heroic, or aspirational.

## 2D Asset Presentation Rules

- Moving characters in side-scrolling gameplay should have a clear right-facing
  read by default.
- Judge facing direction primarily from the feet and legs: toes point right, and
  the legs form a side-view walking stride.
- The upper body may stay partly front-facing to preserve expression, action,
  and character memory points.
- Tower and level-map assets should default to clear orthographic front views
  unless a specific concept exploration needs another camera.

## Asset Paths

- `concept-art/`: generated or hand-authored concept art and visual
  explorations.
- `concept-art/<subject>/`: grouped exploration sets for a named subject, such
  as a tower facade study or enemy species pass.
- `gamer-ref/`: external visual references.
- `art-style-guide/`: durable art rules. Do not use it for throwaway notes.

## Asset Naming

For newly organized assets, prefer:

`[category]_[name]_[variant]_[size].[ext]`

Examples:

- `char_worker_idle_01.png`
- `env_tower_base_large.png`
- `ui_btn_primary_hover.png`
- `vfx_lightning_loop_small.png`

Do not rename historical files only to satisfy this convention.

## Iteration Flow

1. Clarify the asset's purpose, mood, gameplay function, and constraints.
2. Check this file and the relevant topic guide before generating or revising.
3. Use approved reference images as style anchors when possible.
4. Save exploration sets under the relevant `concept-art/<subject>/` folder.
5. After the user approves a reusable direction, record the durable rule in the
   correct style-guide file.

Do not treat one exploratory image as an approved long-term style rule unless
the user explicitly approves it or asks to preserve it.
