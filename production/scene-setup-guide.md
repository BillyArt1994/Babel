# Scene Setup Guide — GameScene

## Step 1: Rename SampleScene
Rename Assets/Scenes/SampleScene.unity → GameScene.unity

## Step 2: Add Physics Layers
Edit > Project Settings > Tags and Layers:
- Layer 6: Enemy
- Layer 7: Tower

## Step 3: Create GameObject Hierarchy
Create the following hierarchy in the scene:

--- Bootstrap
    └── SceneBootstrap.cs (auto-start enabled for dev)

--- Managers
    ├── GameLoopManager.cs
    ├── PlayerInputHandler.cs
    └── EnemyPool.cs

--- Gameplay
    ├── Tower (Empty GO, position 0,0,0)
    │   └── TowerConstructionSystem.cs
    └── EnemySpawnSystem.cs

--- SpawnPoints
    ├── LeftSpawnPoint  (position: -12, 0, 0)
    └── RightSpawnPoint (position: +12, 0, 0)

--- EnemyPools
    └── WorkerPool
        └── ObjectPool<EnemyController> (prefab = Worker prefab)
    (repeat for Elite, Priest, Engineer, Zealot)

--- Camera
    └── Main Camera (tag: MainCamera, Orthographic, size 6)

--- UI (Canvas, Screen Space Overlay)
    └── DebugHUD (temporary - will be replaced)

## Step 4: Wire References in Inspector
- SceneBootstrap: drag all manager references
- EnemySpawnSystem: drag EnemyDatabase SO, TowerConstructionSystem, spawn points
- TowerConstructionSystem: drag tower root transform, layer sprite

## Step 5: Configure EnemyPool
For each enemy type, register the pool in EnemyPool via RegisterPool().
Easiest: call EnemyPool.Instance.RegisterPool(EnemyType.Worker, workerPoolRef) from SceneBootstrap.Start()

## Step 6: Create Enemy Prefabs (placeholder)
For each enemy type, create a GameObject with:
- SpriteRenderer (placeholder white square)
- CircleCollider2D (radius 0.3, layer = Enemy)
- EnemyController.cs
- Rigidbody2D (kinematic)
Save as prefab in Assets/Prefabs/Enemies/

## Step 7: Press Play
With autoStartForTesting = true, the game should start immediately.
