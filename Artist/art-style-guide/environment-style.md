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

For global rendering targets and cross-asset rules, see
[`../STYLE_GUIDE.md`](../STYLE_GUIDE.md).

## Level Scene Layering

Each level scene is composed from three primary visual layers:

- **Background**: the far scene, sky, atmosphere, distant landmarks, weather, and
  mood. It establishes the world and emotional tone, but should stay visually
  subordinate to the tower and gameplay foreground.
- **Tower of Babel**: the central landmark, the enemies' primary construction
  target, and the core visual subject of the level. The tower is not just a
  background building; it is the visible state carrier for enemy progress and
  the player's main strategic focus.
- **Foreground Ground**: the playable ground plane in front of the tower. It
  supports enemy movement, combat readability, click/interaction clarity, and
  immediate material contact with the scene.

Composition should make this hierarchy clear: the background supports the mood,
the foreground supports play readability, and the Tower of Babel owns the main
silhouette, progress readability, and level identity.

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
- Tower designs must stay decomposable into individual buildable cells. A
  concept should not depend on one continuous painted facade that cannot be
  broken into enemy-built units. Large features such as entrances, routes,
  bands, and landmarks may span several cells, but their cell boundaries and
  build order must remain plausible.
- Floors stack vertically. Higher floors narrow and are centered by default,
  creating a stepped tower silhouette on both sides.
- Every floor must include a readable route that allows builders to reach the
  next floor above. The tower should not read as isolated horizontal slabs.
- The route requirement starts at the first floor: the base/first completed
  floor must show or imply how builders reach the second floor. A bottom level
  with only a single entrance door and no upward connection is not acceptable.
- Circulation should be part of the tower's own architecture. Avoid using
  scaffolding as the main route between floors. Enemies are not simply climbing a
  temporary work frame; they are completing the tower's built-in upward routes as
  part of construction.
- Do not lock the exact circulation form too early. Specific route designs should
  be tested in concept art first, then recorded here only after they are
  approved as durable rules.
- The finished structure should read as one coherent monumental building, not as
  a loose stack of props or an indivisible concept painting.

### 2D Asset View and Landmark Rules

- Tower concepts and tower resources for the side-scrolling level map must use a
  strict orthographic front elevation when they are intended as usable gameplay
  resources.
- Avoid perspective, depth-based scale changes, three-quarter views, isometric
  views, visible side planes, visible top planes, camera tilt, or vanishing lines
  when designing usable tower resources.
- Floor and tier boundaries should read as flat horizontal rows. Avoid accidental
  center bulges, raised middle sections, curved tier tops, domes, or uneven
  rooflines unless the user explicitly approves that for a specific theme.
- The primary completed-tower concept should present the tower as the level's
  dominant monumental landmark, with a complete and impressive top/crown.
- Construction-in-progress, damaged, or unfinished variants should be separate
  state concepts. Do not mix an unfinished top into the main completed-tower
  beauty shot.

### Build States

Each facade module cell should support at least these visual states:

- unbuilt: empty/ghosted construction slot or readable architectural placeholder;
- in progress: partial module, unfinished facade, or incomplete built-in passage;
- complete: readable finished facade module in the level's chosen style.

The state design can change by tower theme, but the player must still understand
which cells are empty, which are being built, and which are complete.

### Theme Layer

The construction grammar stays stable; the visual skin changes per level.
Examples:

- Ancient Babel style: the current tower iteration should explore a Hellenistic
  mixed megastructure rather than a simple repeated arch facade. Specific motifs,
  vertical sections, circulation solutions, and ornament rules are exploratory
  until concept art proves they work and the user approves them as durable rules.
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
