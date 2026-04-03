# Copilot Instructions — Babel (代号：巴别塔)

## Project Overview

Babel is a **2D side-scrolling Survivor-like / action roguelite** built in **Unity 2022.3.73f1 (LTS)** with **C#**. The player is an angry god preventing humans from completing the Tower of Babel within 15 minutes. Humans stream in from both sides carrying stone blocks; the player uses click attacks and divine skills to destroy them.

- **Rendering**: URP (Universal Render Pipeline) for 2D
- **Unity project path**: `Babel_Client/` (the Unity project root)
- **Single scene**: `Babel_Client/Assets/Scenes/GameScene.unity`
- **MCP integration**: Unity MCP server at `http://127.0.0.1:8080/mcp` (see `.mcp.json`)

## Architecture

### Event-Driven Singleton Systems

All major systems are **MonoBehaviour singletons** that communicate exclusively through **static C# Action event classes** (no UnityEvents). Systems never reference each other directly — they subscribe to events.

```
Input → InputEvents → SkillSystem → ClickAttackSystem → CombatEvents → VFX/UI
                                                        ↗
EnemyEvents ← EnemyController ← WaveSpawnSystem (CSV-driven)
                    ↓
         TowerConstructionSystem → TowerProgressEvents → GameLoopManager → GameEvents
```

**Event classes** (all static, across `Scripts/Core/`, `Scripts/Enemy/`, `Scripts/Combat/`, `Scripts/Skills/`):
- `GameEvents` — game state transitions (start, pause, victory, defeat, level-up)
- `InputEvents` — mouse down/held/up, pause key
- `CombatEvents` — attack executed with `AttackResult`
- `EnemyEvents` — enemy died, enemy reached tower
- `SkillEvents` — skill added, charge started/updated
- `UpgradeEvents` — 3-option upgrade pool generated
- `TowerEvents` / `TowerProgressEvents` — tower layer progress and completion

**Pattern**: Sources call `RaiseXxx()`, listeners subscribe in `OnEnable`/`Awake` and unsubscribe in `OnDisable`/`OnDestroy`.

### Initialization Order

Controlled via `[DefaultExecutionOrder]`:
1. **SceneBootstrap** (-200) — validates singletons, registers enemy pools, sets `Application.targetFrameRate = 60`, calls `GameLoopManager.StartGame()`
2. **GameLoopManager** (-100) — state machine: `NotStarted → Playing ↔ Paused → LevelingUp → Victory/Defeat`
3. **SkillSystem** (-60) — click-form attacks + passive modifier cache
4. **ClickAttackSystem** (-50) — Physics2D-based target resolution

### Data Architecture

All game data lives in **ScriptableObjects**:
- `SkillData` / `SkillDatabase` — skill definitions (damage, cooldown, AOE, chain, passives, upgrade weights)
- `EnemyData` / `EnemyDatabase` — enemy types (Worker, Elite, Priest, Engineer, Zealot)
- `MapConfig` — tower layer/slot layout

Wave definitions are **CSV-driven** (`Resources/Data/level_waves.csv`), loaded by `WaveSpawnSystem`. Enemy stats can be overridden from CSV via `EnemyStatsLoader`.

### Object Pooling

Generic `ObjectPool<T>` (stack-based, hash-set guarded against double returns) with `IPoolable` interface (`OnGetFromPool()`, `OnReturnToPool()`). `EnemyPool` dispatches by `EnemyType` to type-specific `EnemyControllerPool` instances.

### Tower System

`TowerConstructionSystem` manages 10 layers via `LayerSlotManager` instances. Uses **Tilemap** for visuals — tiles pre-placed hidden, revealed on construction. Enemies use a **slot-based pathfinding state machine**: `FindNormalSlot → FindPassageSlot → ClimbStairs → EnterTower`. Tower completion triggers game defeat.

## Scripts Directory Structure

```
Babel_Client/Assets/Scripts/
├── Core/           # GameLoopManager, GameState, GameEvents, SceneBootstrap, BabelLogger, CameraController
├── Combat/         # ClickAttackSystem, AttackTypes (enums/structs), CombatEvents
├── Enemy/          # EnemyController, EnemyData/Database, WaveSpawnSystem, EnemyStatsLoader
├── Skills/         # SkillSystem, SkillData/Database, UpgradeSystem, UpgradeEvents
├── Tower/          # TowerConstructionSystem, LayerSlotManager, TowerLayer, TowerProgressEvents
├── UI/             # GameHUD, UpgradeSelectionUI, GameOverUI, PauseButton, GameSpeedButton, DebugHUD
├── VFX/            # AttackVFXManager, CircleVFX, ChainVFX
├── Input/          # PlayerInputHandler, InputEvents
├── Map/            # MapConfig (ScriptableObject)
├── Utilities/      # ObjectPool<T>, EnemyPool, IPoolable, CsvParser
└── Debug/          # PerformanceBenchmark
```

Editor tools: `Babel_Client/Assets/Editor/` (MapEditor, SceneSetupWizard, AIUpgradeSelector, TileCreator).

## Collaboration Protocol

This is an AI-assisted indie game project. AI agents implement code; the human developer provides direction, reviews, and approval.

**Workflow for any code change:**
1. **Read the relevant design document** in `design/gdd/` before implementing
2. **Propose architecture before writing code** — show class structure, data flow, trade-offs
3. **Flag spec ambiguities** — ask rather than assume; never silently deviate from the GDD
4. **Get approval before writing files** — list all affected files, show code or summary
5. **Offer next steps** — tests, code review, potential improvements

**Key principle**: UI must never own game state. UI displays state and sends commands through events. Gameplay values must come from ScriptableObjects or CSV, never hardcoded — designers must tune without touching code.

## Code Conventions

### Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Classes/Methods | `PascalCase` | `SkillSystem`, `ExecuteAttack()` |
| Private fields | `_camelCase` | `_remainingTime`, `_activeClickForm` |
| Constants | `UPPER_SNAKE_CASE` | `TOTAL_DURATION`, `LAYER_COUNT` |
| Enums/Values | `PascalCase` | `GameState.Playing`, `EnemyType.Worker` |
| Event raisers | `Raise<Name>()` | `GameEvents.RaiseGameStart()` |
| Event handlers | `On<Name>()` | `OnEnemyDied()`, `OnMouseDown()` |

### Class Suffixes

- `*System` — singleton gameplay systems (`SkillSystem`, `UpgradeSystem`, `WaveSpawnSystem`)
- `*Events` — static event aggregator classes (`GameEvents`, `CombatEvents`)
- `*Data` — ScriptableObject data containers (`SkillData`, `EnemyData`)
- `*Database` — ScriptableObject collection + lookup (`SkillDatabase`, `EnemyDatabase`)
- `*Manager` — subsystem managers (`LayerSlotManager`, `AttackVFXManager`)
- `*UI` — UI MonoBehaviours (`GameHUD`, `UpgradeSelectionUI`)

### Code Quality Standards

- All public methods and classes must have **XML doc comments**
- Maximum **cyclomatic complexity of 10** per method
- No method longer than **40 lines** (excluding data declarations)
- All dependencies injected or event-driven; avoid direct cross-system references
- Configuration values loaded from data files (ScriptableObjects/CSV), never hardcoded
- Use `readonly` and `const` where applicable
- Frame-rate independent logic — use `Time.deltaTime` everywhere

### Unity Practices

- Use `[SerializeField]` on private fields, never make fields public just for Inspector
- Use `[Header]` and `[Tooltip]` for Inspector readability
- Cache component references in `Awake()` — never call `GetComponent` or `Find` in `Update()`
- Never use `Find()`, `FindObjectOfType()`, or `SendMessage()` in production code
- Use `== null` not `is null` for Unity object null checks (Unity overrides `==`)
- MonoBehaviour lifecycle methods ordered: `Awake → OnEnable → Start → Update → OnDisable → OnDestroy`
- Prefer composition over inheritance (no custom base MonoBehaviour)
- Avoid `Update()` where possible — prefer events, timers, or coroutines

### Performance Rules

- **Zero-allocation hot paths**: use `Physics2D.OverlapCircleNonAlloc` with pre-allocated `Collider2D[]` buffers (size 64–256)
- **Object pooling** for all frequently spawned/destroyed objects (enemies, VFX)
- **No coroutine GC**: prefer timer-based approaches over `WaitForSeconds` in performance-critical paths
- **StringBuilder** for string concatenation in loops
- **Frame budget**: 16.67ms total at 60fps; individual systems must stay under 4ms. See `docs/PERF_BUDGET.md`
- **GC target**: ≤1KB per frame in steady state, zero GC spikes >2ms

### Architecture Rules

- **Dependency direction**: Engine/Core ← Gameplay ← UI (never reverse)
- **No circular dependencies** between modules
- **UI does not own game state** — UI reads state via events, sends commands back through events
- **Events for cross-system communication** — static C# Action classes, not direct references
- **ScriptableObjects for data, MonoBehaviours for behavior** — keep data separate from logic

### Logging

Use `BabelLogger` with structured, grep-friendly prefixes: `[BABEL][AC][S3-14]` (module + sprint task ID).

### Bug Severity Scale

| Level | Meaning | Policy |
|-------|---------|--------|
| S1 | Crash, data loss, progression blocker | Must fix before any build |
| S2 | Broken feature, severe visual glitch | Must fix before milestone |
| S3 | Cosmetic, minor inconvenience | Fix when capacity allows |
| S4 | Polish, minor text error | Lowest priority |

## Design & Production Documents

Game design documents are in `design/gdd/` (written in Chinese):
- `game-concept.md` — full game concept with MDA analysis, core loop, and mechanics
- `systems-index.md` — 20-system breakdown with dependencies and priority tiers
- Individual system GDDs named in Chinese (e.g., `技能系统.md`, `塔建造系统.md`)

Production documents in `production/`:
- `sprints/` — 7-day sprint plans with **Must Have / Should Have / Nice to Have** task tiers
- `milestones/` — milestone definitions with success criteria
- `reports/` — performance reports with frame-time budgets
- `scene-setup-guide.md` — mandatory scene hierarchy and physics layer config
- `risk-register/` — technical and design risk tracking

**Sprint workflow**: Each sprint has explicit task dependencies (e.g., S2-03 depends on S2-02), per-task acceptance criteria, and a suggested day-by-day execution order. Tasks are identified as `S{sprint}-{number}` (e.g., `S3-14`).

## Scene Setup

Single scene (`GameScene.unity`) with mandatory hierarchy:

```
Bootstrap (SceneBootstrap.cs)
Managers (GameLoopManager, PlayerInputHandler, EnemyPool)
Gameplay (Tower, EnemySpawnSystem)
SpawnPoints (LeftSpawnPoint, RightSpawnPoint)
EnemyPools (one ObjectPool<> per enemy type)
Camera (Main Camera, Orthographic size=6)
UI (Canvas with HUD/DebugHUD)
```

**Physics layers**: Layer 6 = Enemy, Layer 7 = Tower. Enemies use `CircleCollider2D` (radius 0.3) + kinematic `Rigidbody2D`.

## Key Game Constants

| Constant | Value | Location |
|----------|-------|----------|
| Game duration | 900s (15 min) | `GameLoopManager.TOTAL_DURATION` |
| Tower layers | 10 | `TowerConstructionSystem.LAYER_COUNT` |
| Upgrade options | 3 per level-up | `UpgradeSystem.OPTIONS_COUNT` |
| Faith scaling | ×1.2 per level | `UpgradeSystem.FAITH_SCALE_FACTOR` |
| Crit multiplier | 2.0× | `SkillSystem.CRIT_MULT` |
| Max crit chance | 80% | `SkillSystem.MAX_CRIT_CHANCE` |
| Target FPS | 60 | `SceneBootstrap` |

## Important Notes

- **Language**: Design docs and code comments are in **Chinese (中文)**; code identifiers are in English.
- **CSV data pipeline**: Wave spawning and enemy stat overrides use CSV files in `Resources/Data/`. `CsvParser` handles type-safe parsing.
- **No CLI build/test**: The project builds and runs through the Unity Editor. Use the Unity MCP server for Editor automation (compile, play, read console).
- **Performance verification**: Use `PerformanceBenchmark.cs` (in `Scripts/Debug/`) to spawn 100 units and measure FPS/frame times. Check logs with `[PerformanceBenchmark]` prefix.
- **Code review checklist** (from `.claude/skills/code-review/`):
  - Public API has doc comments
  - Cyclomatic complexity <10, methods <40 lines
  - No allocations in hot paths
  - Correct dependency direction (Core ← Gameplay ← UI)
  - Events for cross-system communication
  - Frame-rate independence (delta time)
- **AI agent configs**: `.claude/` contains multi-agent workflow configs for Claude Code — not relevant to Copilot, but the conventions defined in `.claude/docs/` (coding-standards, technical-preferences) are authoritative for this project.
