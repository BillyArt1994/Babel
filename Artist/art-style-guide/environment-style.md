# Environment Style

This file stores durable environment-art rules for Babel.

Use it for:

- tower visuals;
- scene and background direction;
- atmosphere and lighting rules;
- terrain and prop language;
- environment composition rules;
- approved environment references;
- known environment-generation pitfalls and fixes.

## Tower Construction Grammar: Facade Module Cells

This section defines the reusable tower construction grammar. It is not a fixed
architectural skin. Future levels may use different tower styles, such as a
Japanese-inspired tower or Chinese-inspired tower, while keeping the same build
unit and floor structure.

### Core Structure

- The tower is assembled from repeatable **facade module cells**.
- A facade module cell is the smallest unit enemies can build.
- A cell represents one complete front-facing architectural module in the chosen
  level style. The motif is theme-specific: it might be an arch, a window bay, a
  wooden frame, a screen panel, a balcony unit, a gate segment, a bracketed bay,
  or another readable facade module.
- A cell is not a brick. Avoid brick-wall texture, small masonry grids, or detail
  that makes the player read the tower as a pile of blocks.
- Multiple cells connect horizontally to complete one floor.
- A floor is a horizontal row of cells. The floor's width is defined by its cell
  count.
- Floors stack vertically. Higher floors narrow and are centered by default,
  creating a stepped tower silhouette on both sides.
- The finished structure should read as one coherent monumental building, not as
  a loose stack of props or an indivisible concept painting.

### 2D Asset View and Landmark Rules

- Tower concepts and tower resources for the side-scrolling level map should use
  a strict orthographic front elevation by default.
- Avoid perspective, three-quarter views, isometric views, visible side planes,
  visible top planes, camera tilt, or vanishing lines when designing usable tower
  resources.
- Floor and tier boundaries should read as flat horizontal rows. Avoid accidental
  center bulges, raised middle sections, curved tier tops, domes, or uneven
  rooflines unless the user explicitly approves that for a specific theme.
- The primary completed-tower concept should present the tower as the level's
  dominant monumental landmark, with a complete and impressive top/crown.
- Construction-in-progress, damaged, unfinished, or scaffold-heavy variants
  should be separate state concepts. Do not mix an unfinished top into the main
  completed-tower beauty shot.

### Build States

Each facade module cell should support at least these visual states:

- unbuilt: empty/ghosted construction slot or light scaffold marker;
- in progress: partial module, scaffolded module, or unfinished facade;
- complete: readable finished facade module in the level's chosen style.

The state design can change by tower theme, but the player must still understand
which cells are empty, which are being built, and which are complete.

### Theme Layer

The construction grammar stays stable; the visual skin changes per level.
Examples:

- Ancient Babel style: warm stone or terracotta facade modules with repeated
  openings or arches. Current ancient Babel explorations may lean into Ancient
  Greek temple language: pilasters or columns, lintels, entablature/frieze bands,
  meander/key-pattern accents, and flat horizontal cornice lines.
- Japanese-inspired style: timber-frame facade modules, screens, roofs, or
  bracket details.
- Chinese-inspired style: palace-wall or pagoda-like facade modules, gates,
  lattice panels, dougong-like bracket silhouettes, or tiled roof accents.

Do not promote a theme-specific motif, such as an arch or window, into the global
cell definition unless the user explicitly approves that as a long-term rule.

### Pitfalls

- Do not define every cell globally as an arch, window, or doorway. Those are
  style choices, not the abstract construction unit.
- Do not align all floors to one side unless a specific level intentionally
  breaks the centered stepped structure.
- Do not use brick-size cells as the gameplay build unit.
- Do not rely on a full-tower concept painting that cannot be assembled from
  repeated facade module cells.
- Do not make facade ornament so dense that enemies, click targets, or build
  progress become hard to read.

Current visual reference for the ancient Babel direction:

- `gamer-ref/Snipaste_2026-06-29_22-37-46.png`

