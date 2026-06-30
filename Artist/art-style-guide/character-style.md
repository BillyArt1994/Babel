# Character Style

This file stores durable character-art rules for Babel.

Use it for:

- character anatomy rules;
- faction and species identity;
- silhouette language;
- clothing and prop language;
- approved character anchor images;
- reusable character prompt recipes;
- known character-generation pitfalls and fixes.

For global rendering targets and cross-asset rules, see
[`../STYLE_GUIDE.md`](../STYLE_GUIDE.md).

## Character Direction

All enemy units belong to the same overreaching builder species. They share a
hard-lined, flat-color, hand-drawn cartoon rendering style with a satirical
Ancient Greek flavor.

They should read as comic, pathetic, arrogant, grotesque, and doomed. They
should not read as handsome heroes, elite fantasy warriors, or aspirational
protagonists.

## Three Rule Layers

Use three layers when defining a character:

- **Rendering style**: how the character is drawn. This is shared by all enemy
  characters.
- **Species identity**: what kind of creature this is. This is shared by all
  enemy characters.
- **Individual character variables**: who this specific unit is within the
  species. These vary by unit type.

## Rendering Style

These rules have the highest priority for every enemy character.

1. **Hard hand-inked contours**: outlines should have varying line weight,
   occasional broken or tapered strokes, and a cutout-like hand-drawn feel. Do
   not use uniform industrial linework.
2. **Flat hard color blocks**: use flat fills plus hard-edged shadow shapes.
   Each major color should stay within 2-3 clear value steps.
3. **Angularity at major edges only**: sharp corners belong on silhouettes and
   major shape boundaries. Interior surfaces should stay clean and flat.
4. **Crisp edges**: silhouettes, major separations, and shadow boundaries should
   stay clean and readable.

Avoid soft gradients, airbrushed volume, glossy highlights, painterly rendering,
internal low-poly facets, and wireframe-like surface patterns.

## Species Identity

Workers, elites, scouts, priests, zealots, and other human-side units are all
members of the same species.

These are biological anatomy rules. They should remain true regardless of
outfit, body type, expression, or social role:

1. **Humanoid but not human**: the species has a humanoid body plan, but is
   visibly non-human organic flesh. It is not robotic, armored, mechanical, or
   metallic.
2. **Large bulging goggle-like round eyes**: the eye shape is large, round, and
   protruding. Expressions can vary, but the species eye type stays consistent.
3. **Rounded organic egg-shaped skull**: the head structure is rounded and
   organic. It should not become a cube, block, flat-sided helmet, or mechanical
   head.

The species baseline is satirical and pathetic rather than noble. Costume,
rank, and body type can vary, but the species should not become heroic.

## Individual Character Variables

These elements belong to the specific unit type. Do not promote them into global
species rules unless the user explicitly approves that.

- **Body type**: hunched worker, bulky elite, thin scout, massive brute.
- **Skin color**: gray-green, sickly yellow, pale, reddish, or another unit
  discriminator.
- **Costume rank**: torn exomis tunic, priest robe, elite drape, work gear,
  ritual object.
- **Expression and gaze**: exhausted, fanatical, gloomy, smug, terrified.
- **Hair and head detail**: spikes, bald head, long beard, wrapped headcloth.
- **Memory point**: central cyclops eye, horn, carried stone, ritual tool,
  builder equipment.
- **Pose and action**: carrying, blowing a horn, praying, charging, healing,
  directing others.

In short: species identity answers "what creature is this"; individual variables
answer "who is this member of the species".

`worker_v3.png` includes worker-specific variables such as gray-green skin, torn
exomis clothing, hunched posture, spiky hair, and a weary face. Do not copy
those details automatically into every enemy.

## Side-Scroller Facing Rule

Moving character assets should have a clear right-facing read.

Judge facing direction mainly from the feet and legs: both feet should point
right, and the legs should form a side-view walking stride.

The upper body may remain partly front-facing to preserve expression, action,
and memory points. Do not over-correct into a pure side profile if that weakens
the character.

## Approved Character Anchors

Use `concept-art/worker_v3.png` as the main character style anchor.

It proves the shared rendering style and species anatomy: egg-shaped skull,
bulging round eyes, non-human organic flesh, and a comic pathetic tone.

It also includes worker-specific variables: gray-green skin, torn exomis,
hunched body, spiky hair, and a weary expression.

When using it as an anchor:

- Copy the rendering style.
- Copy the species anatomy.
- Do not copy the worker's skin color, costume, body type, hair, or expression
  unless the new character is intentionally worker-like.

`_proto_titan_replica4.png` can be used as a secondary rendering anchor. It uses
the same rendering language with a different body type.

## Reusable Prompt Recipe

When using an approved character image as a variation source, write prompts in
three explicit blocks. If any block is vague, the output tends to drift toward
soft, rounded, painterly cartoon rendering.

### Block 1: Lock The Rendering Style

```text
Replicate the EXACT art style of the reference image. HARD hand-inked ANGULAR
outlines with varying line weight and occasional broken/tapered strokes, like
brush-and-ink vector cutout art - sharp pointed corners at the silhouette and
between major shapes, NOT smooth/rounded, NOT uniform industrial linework. FLAT
cel-shaded color blocks with crisp hard-edged boundaries - absolutely NO soft
gradients, NO airbrushed volume shading, NO glossy highlights. Only 2-3 flat tone
steps per color. Angularity ONLY at silhouette + major-piece boundaries; surface
interiors stay clean and flat - NO internal polygon facets, NO low-poly wireframe.
```

### Block 2: Species Identity Plus Unit Variables

Always include the species anatomy:

```text
humanoid but NOT human, organic living flesh, NOT a robot, NO metal parts;
large round bulging goggle-like eyes; rounded organic egg-shaped skull;
goofy/dopey, NOT cool/handsome/heroic
```

Then define the specific unit: body type, skin color, costume rank, expression,
hair, memory point, action, and right-facing leg pose.

Use strong language for critical features. For a cyclops, write:

```text
exactly ONE single large round eye in the CENTER of the forehead - only ONE eye,
no second eye anywhere
```

For the head shape, write:

```text
ROUNDED ORGANIC egg-shaped skull, smooth curved dome, structure ONLY from the
side-profile contour in the classic Simpsons way; NO boxy / square / cube /
flat-sided head
```

For facing direction, write:

```text
both feet pointing RIGHT, legs in a side-view walking stride
```

### Block 3: Restate The Rendering Constraint

```text
Match the reference's exact rendering style precisely: angular, flat, hard-lined,
NOT soft or painterly.
```

## Command Example

```bash
node ~/.claude/skills/image-generator/scripts/gen-image.js \
  --mode variations \
  --source "H:/Babel/Artist/concept-art/worker_v3.png" \
  -p "<rendering block> <species plus unit block> <final style lock>" \
  --size 1024x1536 --background transparent --quality high \
  -o "H:/Babel/Artist/concept-art/<name>.png"
```

## Known Pitfalls

- Writing only `match the reference style` produces soft lines and painterly
  gradients. Spell out hard edges, flat color, and banned rendering modes.
- Phrases such as `flat forehead plane`, `faceted skull`, or `angular jaw` can
  push the head into a blocky cube. Use rounded organic egg-shaped skull
  language and explicitly ban boxy, square, cube, and flat-sided heads.
- Writing only `Cyclops` may still produce two eyes. Use `exactly ONE` and `no
  second eye anywhere`.
- If the prompt does not say non-human organic flesh, the character can drift
  into a robot or a normal human.
- If the prompt does not state the pathetic comic baseline, the enemy can drift
  into a handsome hero or elite warrior.
- Clothing details can multiply automatically. For simple clothing, say `plain
  one-piece tunic, NO patches, NO holes, NO extra straps`.
- Some word combinations around mouths, strain, and teeth can trigger image
  safety filters. Use neutral phrasing when a prompt fails.
- When facing direction is weak, fix the legs and feet first. Do not force the
  whole body into a pure side profile if the upper-body action was already
  working.
