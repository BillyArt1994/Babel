/// <summary>
/// SceneSetupWizard.cs
/// Unity Editor menu: Babel > 🏗 Setup Game Scene
///
/// Automates all scene setup steps from production/scene-setup-guide.md:
///   - Creates/opens GameScene
///   - Builds the full GameObject hierarchy
///   - Adds every component
///   - Wires all cross-component Inspector references
///   - Creates placeholder enemy prefabs (5 types)
///   - Creates starter SkillData SO + empty SkillDatabase + EnemyDatabase SOs
///   - Generates a 32×32 white placeholder sprite for the tower layers
///   - Sets physics layers (Enemy = 6, Tower = 7) via TagManager.asset
/// </summary>
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

public static class SceneSetupWizard
{
    // ─── Paths ────────────────────────────────────────────────────────────────
    private const string ScenePath      = "Assets/Scenes/GameScene.unity";
    private const string PrefabsPath    = "Assets/Prefabs/Enemies";
    private const string SOPath         = "Assets/ScriptableObjects";
    private const string SkillSOPath    = "Assets/ScriptableObjects/Skills";
    private const string EnemySOPath    = "Assets/ScriptableObjects/Enemies";
    private const string SpritesPath    = "Assets/Sprites";

    // Enemy layer index expected by the game code
    private const int EnemyLayerIndex = 6;
    private const int TowerLayerIndex = 7;

    // ─── Entry Points ────────────────────────────────────────────────────────

    /// <summary>
    /// Step 1: Creates all ScriptableObject and texture assets on disk.
    /// Run this first, then wait for Unity to finish importing, then run Step 2.
    /// </summary>
    [MenuItem("Babel/🏗 Step 1 - Create Assets", priority = 1)]
    public static void Step1_CreateAssets()
    {
        SetupPhysicsLayers();
        EnsureEnemyDatabase();
        EnsureSkillDatabase();
        EnsureStarterSkill(null);
        EnsureLayerSprite();
        CreateEnemyPrefabs();
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneSetupWizard] ✅ Step 1 done! Wait for Unity to finish importing, then run 'Step 2 - Build Scene'.");
    }

    /// <summary>
    /// Step 2: Builds the scene hierarchy and wires all references.
    /// Run after Step 1 has finished and Unity has imported all assets.
    /// </summary>
    [MenuItem("Babel/🏗 Step 2 - Build Scene", priority = 2)]
    public static void Step2_BuildScene()
    {
        // Load all external assets from disk (no imports happen here)
        var enemyDb      = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(EnemySOPath + "/EnemyDatabase.asset");
        var skillDb      = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillSOPath + "/SkillDatabase.asset");
        var starterSkill = AssetDatabase.LoadAssetAtPath<SkillData>(SkillSOPath + "/BasicSmite.asset");
        var layerSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesPath + "/TowerLayer_White.png");

        // Create a fresh scene – no renames, no Refresh(), no domain-reload risk
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

        // Save immediately to give it a path on disk
        bool initialSave = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!initialSave)
        {
            Debug.LogError("[SceneSetupWizard] Could not save scene to " + ScenePath + ". Aborting.");
            return;
        }

        // Build hierarchy
        var h = BuildHierarchy(scene);

        // Wire all references
        WireReferences(h, enemyDb, skillDb, starterSkill, layerSprite);

        // Final save
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log("[SceneSetupWizard] ✅ Scene saved: " + saved + "  Press Play to test!");
    }

    // Keep the old combined entry point as a convenience wrapper
    [MenuItem("Babel/🏗 Setup Game Scene (Full)", priority = 3)]
    public static void SetupGameScene()
    {
        Step1_CreateAssets();
        // Assets must be imported before building the scene.
        // Use EditorApplication.delayCall to run Step 2 after the current editor frame.
        EditorApplication.delayCall += () =>
        {
            AssetDatabase.Refresh();
            EditorApplication.delayCall += Step2_BuildScene;
        };
    }

    // ─── 1. Physics Layers ───────────────────────────────────────────────────
    private static void SetupPhysicsLayers()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));

        var layersProp = tagManager.FindProperty("layers");
        bool changed = false;

        changed |= EnsureLayer(layersProp, EnemyLayerIndex, "Enemy");
        changed |= EnsureLayer(layersProp, TowerLayerIndex, "Tower");

        if (changed)
        {
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[SceneSetupWizard] Physics layers set: Enemy=6, Tower=7");
        }
    }

    private static bool EnsureLayer(SerializedProperty layersProp, int index, string name)
    {
        var element = layersProp.GetArrayElementAtIndex(index);
        if (element.stringValue == name) return false;
        element.stringValue = name;
        return true;
    }

    // ─── 2. Scene (no longer used directly — kept for reference) ──────────────
    private static EnemyDatabase EnsureEnemyDatabase()
    {
        EnsureDirectory(EnemySOPath);
        const string dbPath = EnemySOPath + "/EnemyDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(dbPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<EnemyDatabase>();
            AssetDatabase.CreateAsset(db, dbPath);
            Debug.Log("[SceneSetupWizard] Created EnemyDatabase.asset");
        }

        // Define all 5 enemy types with their stats
        var enemyDefs = new[]
        {
            // name, id, type, hp, speed, faith, buildContrib, weight, startTime
            new { name="Worker",   id="worker",   type=EnemyType.Worker,   hp=40f,  speed=1.5f, faith=5f,  build=1f,   weight=1.0f, start=0f   },
            new { name="Elite",    id="elite",    type=EnemyType.Elite,    hp=160f, speed=2.0f, faith=15f, build=1f,   weight=0.5f, start=120f },
            new { name="Priest",   id="priest",   type=EnemyType.Priest,   hp=80f,  speed=1.2f, faith=10f, build=0.5f, weight=0.3f, start=300f },
            new { name="Engineer", id="engineer", type=EnemyType.Engineer, hp=100f, speed=1.8f, faith=10f, build=3f,   weight=0.3f, start=300f },
            new { name="Zealot",   id="zealot",   type=EnemyType.Zealot,   hp=250f, speed=2.5f, faith=25f, build=1f,   weight=0.4f, start=480f },
        };

        // Create EnemyData assets and rebuild the _allEnemies array on the database
        var soDb = new SerializedObject(db);
        var allEnemiesProp = soDb.FindProperty("_allEnemies");
        allEnemiesProp.arraySize = enemyDefs.Length;

        for (int i = 0; i < enemyDefs.Length; i++)
        {
            var def = enemyDefs[i];
            string assetPath = EnemySOPath + "/" + def.name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, assetPath);
                Debug.Log($"[SceneSetupWizard] Created {def.name}.asset");
            }

            // Always (re)write the stats so they stay in sync with the wizard
            var soData = new SerializedObject(data);
            soData.FindProperty("_enemyId").stringValue = def.id;
            soData.FindProperty("_enemyName").stringValue = def.name;
            soData.FindProperty("_enemyType").enumValueIndex = (int)def.type;
            soData.FindProperty("_maxHealth").floatValue = def.hp;
            soData.FindProperty("_moveSpeed").floatValue = def.speed;
            soData.FindProperty("_faithValue").floatValue = def.faith;
            soData.FindProperty("_buildContribution").floatValue = def.build;
            soData.FindProperty("_spawnWeight").floatValue = def.weight;
            soData.FindProperty("_spawnStartTime").floatValue = def.start;
            soData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            allEnemiesProp.GetArrayElementAtIndex(i).objectReferenceValue = data;
        }

        soDb.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(db);

        return db;
    }

    private static SkillDatabase EnsureSkillDatabase()
    {
        EnsureDirectory(SkillSOPath);
        const string path = SkillSOPath + "/SkillDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<SkillDatabase>(path);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<SkillDatabase>();
            AssetDatabase.CreateAsset(db, path);
            Debug.Log("[SceneSetupWizard] Created SkillDatabase.asset");
        }
        return db;
    }

    private static SkillData EnsureStarterSkill(SkillDatabase skillDb)
    {
        EnsureDirectory(SkillSOPath);
        const string path = SkillSOPath + "/BasicSmite.asset";
        var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            // Set fields via reflection-free SerializedObject approach
            var so = new SerializedObject(skill);
            so.FindProperty("_skillId").stringValue       = "basic_smite";
            so.FindProperty("_skillName").stringValue     = "Basic Smite";
            so.FindProperty("_description").stringValue   = "Starter click attack.";
            so.FindProperty("_skillType").enumValueIndex  = (int)SkillType.ClickForm;
            so.FindProperty("_damage").floatValue         = 10f;
            so.FindProperty("_cooldown").floatValue       = 0.5f;
            so.FindProperty("_chargeTime").floatValue     = 1.0f;
            so.FindProperty("_weight").floatValue         = 0f;   // not selectable in upgrades
            so.FindProperty("_isStarterSkill").boolValue  = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(skill, path);
            Debug.Log("[SceneSetupWizard] Created BasicSmite.asset starter skill");
        }
        return skill;
    }

    private static Sprite EnsureLayerSprite()
    {
        EnsureDirectory(SpritesPath);
        const string path = SpritesPath + "/TowerLayer_White.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            // Generate a 128×32 white texture for sliced rendering
            var tex = new Texture2D(128, 32);
            var pixels = new Color[128 * 32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(Application.dataPath + "/../" + path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            // Configure as Sprite, sliced border
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType  = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder   = new Vector4(4, 4, 4, 4);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Debug.Log("[SceneSetupWizard] Created TowerLayer_White.png sprite");
        }
        return sprite;
    }

    // ─── 4. Hierarchy ─────────────────────────────────────────────────────────
    private struct SceneHandles
    {
        public GameObject bootstrap;
        public GameObject gameLoopManagerGO;
        public GameObject inputHandlerGO;
        public GameObject enemyPoolGO;
        public GameObject towerGO;
        public GameObject spawnSystemGO;
        public GameObject leftSpawnPoint;
        public GameObject rightSpawnPoint;
        public GameObject cameraGO;
        public GameObject canvasGO;
        public GameObject debugHudGO;
        public GameObject combatGO;
        public GameObject skillSystemGO;
        public GameObject upgradeGO;

        public SceneBootstrap        sceneBootstrap;
        public GameLoopManager       gameLoopManager;
        public PlayerInputHandler    inputHandler;
        public EnemyPool             enemyPool;
        public TowerConstructionSystem towerSystem;
        public WaveSpawnSystem       spawnSystem;
        public ClickAttackSystem     clickAttack;
        public SkillSystem           skillSystem;
        public UpgradeSystemPlaceholder upgrade;
        public DebugHUD              debugHud;

        // Per-type enemy pools
        public EnemyControllerPool[] enemyPoolComponents;
        public EnemyType[]           enemyPoolTypes;
    }

    private static SceneHandles BuildHierarchy(Scene scene)
    {
        // Remove previous wizard-generated roots to allow re-running
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name is "Bootstrap" or "Managers" or "Gameplay"
                          or "SpawnPoints" or "Combat" or "UI" or "EnemyPools")
                GameObject.DestroyImmediate(root);
        }

        SceneHandles h = new SceneHandles();

        // ── Bootstrap ──
        h.bootstrap     = new GameObject("Bootstrap");
        h.sceneBootstrap = h.bootstrap.AddComponent<SceneBootstrap>();

        // ── Managers ──
        var managers = new GameObject("Managers");
        var gmGO = CreateChild(managers, "GameLoopManager");
        h.gameLoopManagerGO = gmGO;
        h.gameLoopManager   = gmGO.AddComponent<GameLoopManager>();

        var ihGO = CreateChild(managers, "PlayerInputHandler");
        h.inputHandlerGO = ihGO;
        h.inputHandler   = ihGO.AddComponent<PlayerInputHandler>();

        var epGO = CreateChild(managers, "EnemyPool");
        h.enemyPoolGO = epGO;
        h.enemyPool   = epGO.AddComponent<EnemyPool>();

        // ── Gameplay ──
        var gameplay = new GameObject("Gameplay");

        h.towerGO = CreateChild(gameplay, "Tower");
        h.towerGO.transform.position = Vector3.zero;
        h.towerSystem = h.towerGO.AddComponent<TowerConstructionSystem>();

        var spawnGO = CreateChild(gameplay, "WaveSpawnSystem");
        h.spawnSystemGO = spawnGO;
        h.spawnSystem   = spawnGO.AddComponent<WaveSpawnSystem>();

        h.skillSystemGO = CreateChild(gameplay, "SkillSystem");
        h.skillSystem   = h.skillSystemGO.AddComponent<SkillSystem>();

        h.upgradeGO = CreateChild(gameplay, "UpgradeSystemPlaceholder");
        h.upgrade   = h.upgradeGO.AddComponent<UpgradeSystemPlaceholder>();

        // ── SpawnPoints ──
        var spawnPoints = new GameObject("SpawnPoints");
        h.leftSpawnPoint  = CreateChild(spawnPoints, "LeftSpawnPoint");
        h.rightSpawnPoint = CreateChild(spawnPoints, "RightSpawnPoint");
        h.leftSpawnPoint.transform.position  = new Vector3(-12f, 0f, 0f);
        h.rightSpawnPoint.transform.position = new Vector3( 12f, 0f, 0f);

        // ── EnemyPools ──
        var poolsRoot = new GameObject("EnemyPools");
        string[] poolNames = { "Worker", "Elite", "Priest", "Engineer", "Zealot" };
        EnemyType[] poolTypes = { EnemyType.Worker, EnemyType.Elite, EnemyType.Priest, EnemyType.Engineer, EnemyType.Zealot };

        h.enemyPoolComponents = new EnemyControllerPool[poolNames.Length];
        h.enemyPoolTypes      = poolTypes;

        for (int i = 0; i < poolNames.Length; i++)
        {
            var poolGO = CreateChild(poolsRoot, poolNames[i] + "Pool");
            var pool   = poolGO.AddComponent<EnemyControllerPool>();
            var poolParentGO = CreateChild(poolGO, poolNames[i] + "Instances");

            string prefabPath = PrefabsPath + "/" + poolNames[i] + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<EnemyController>(prefabPath);

            var so = new SerializedObject(pool);
            if (prefab != null)
                so.FindProperty("_prefab").objectReferenceValue = prefab;
            so.FindProperty("_initialSize").intValue = 20;
            so.FindProperty("_poolParent").objectReferenceValue = poolParentGO.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            h.enemyPoolComponents[i] = pool;
        }

        // ── Combat ──
        var combat = new GameObject("Combat");
        h.combatGO  = combat;
        h.clickAttack = combat.AddComponent<ClickAttackSystem>();

        // ── Camera ──
        // Re-use the existing Main Camera if present, otherwise create one
        h.cameraGO = GameObject.FindGameObjectWithTag("MainCamera");
        if (h.cameraGO == null)
        {
            h.cameraGO = new GameObject("Main Camera");
            h.cameraGO.tag = "MainCamera";
            h.cameraGO.AddComponent<Camera>();
            h.cameraGO.AddComponent<AudioListener>();
        }
        var cam = h.cameraGO.GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic     = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0, 0, -10);
        }

        // ── UI Canvas ──
        var ui     = new GameObject("UI");
        h.canvasGO = ui;
        var canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ui.AddComponent<CanvasScaler>();
        ui.AddComponent<GraphicRaycaster>();

        h.debugHudGO = CreateUIChild(ui, "DebugHUD");
        h.debugHud   = h.debugHudGO.AddComponent<DebugHUD>();
        BuildDebugHUDTexts(h.debugHudGO, canvas);

        return h;
    }

    // ─── 5. Wire References ───────────────────────────────────────────────────
    private static void WireReferences(
        SceneHandles h,
        EnemyDatabase enemyDb,
        SkillDatabase skillDb,
        SkillData starterSkill,
        Sprite layerSprite)
    {
        int enemyLayerMask = 1 << EnemyLayerIndex;
        var mainCam = h.cameraGO.GetComponent<Camera>();

        // SceneBootstrap — core references
        Wire(h.sceneBootstrap, "_gameLoopManager", h.gameLoopManager);
        Wire(h.sceneBootstrap, "_inputHandler",    h.inputHandler);
        Wire(h.sceneBootstrap, "_spawnSystem",     h.spawnSystem);
        Wire(h.sceneBootstrap, "_towerSystem",     h.towerSystem);
        Wire(h.sceneBootstrap, "_enemyPool",       h.enemyPool);
        SetBool(h.sceneBootstrap, "_autoStartForTesting", true);

        // SceneBootstrap — enemy pool registration arrays
        var bso = new SerializedObject(h.sceneBootstrap);

        var poolCompsProp = bso.FindProperty("_enemyPoolComponents");
        poolCompsProp.arraySize = h.enemyPoolComponents.Length;
        for (int i = 0; i < h.enemyPoolComponents.Length; i++)
            poolCompsProp.GetArrayElementAtIndex(i).objectReferenceValue = h.enemyPoolComponents[i];

        var poolTypesProp = bso.FindProperty("_enemyPoolTypes");
        poolTypesProp.arraySize = h.enemyPoolTypes.Length;
        for (int i = 0; i < h.enemyPoolTypes.Length; i++)
            poolTypesProp.GetArrayElementAtIndex(i).enumValueIndex = (int)h.enemyPoolTypes[i];

        bso.ApplyModifiedPropertiesWithoutUndo();

        // PlayerInputHandler
        Wire(h.inputHandler, "_camera", mainCam);

        // TowerConstructionSystem
        Wire(h.towerSystem, "_towerRoot",   h.towerGO.transform);
        Wire(h.towerSystem, "_layerSprite", layerSprite);

        // WaveSpawnSystem
        Wire(h.spawnSystem, "_enemyDatabase",    enemyDb);
        Wire(h.spawnSystem, "_towerSystem",      h.towerSystem);
        Wire(h.spawnSystem, "_leftSpawnPoint",   h.leftSpawnPoint.transform);
        Wire(h.spawnSystem, "_rightSpawnPoint",  h.rightSpawnPoint.transform);

        // ClickAttackSystem
        SetLayerMask(h.clickAttack, "_enemyLayer", enemyLayerMask);

        // SkillSystem
        Wire(h.skillSystem, "_defaultStarterSkill", starterSkill);
        SetLayerMask(h.skillSystem, "_enemyLayer", enemyLayerMask);

        // UpgradeSystemPlaceholder
        Wire(h.upgrade, "_skillDatabase", skillDb);
        Wire(h.upgrade, "_skillSystem",   h.skillSystem);

        // DebugHUD
        Wire(h.debugHud, "_towerSystem", h.towerSystem);
        // Text references are wired inside BuildDebugHUDTexts
    }

    // ─── 6. Enemy Prefabs ─────────────────────────────────────────────────────
    private static void CreateEnemyPrefabs()
    {
        EnsureDirectory(PrefabsPath);

        string[] enemyNames = { "Worker", "Elite", "Priest", "Engineer", "Zealot" };
        foreach (var name in enemyNames)
        {
            string prefabPath = PrefabsPath + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue;

            var go = new GameObject(name);
            go.layer = EnemyLayerIndex;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = Color.white;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            go.AddComponent<EnemyController>();

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            GameObject.DestroyImmediate(go);
            Debug.Log($"[SceneSetupWizard] Created prefab: {prefabPath}");
        }
    }

    // ─── Debug HUD Layout ─────────────────────────────────────────────────────
    private static void BuildDebugHUDTexts(GameObject hudRoot, Canvas canvas)
    {
        var hud = hudRoot.GetComponent<DebugHUD>();
        if (hud == null) return;

        string[] labels = { "Timer", "TowerProgress", "CurrentLayer", "GameState", "KillCount" };
        string[] fields = { "_timerText", "_towerProgressText", "_currentLayerText", "_gameStateText", "_killCountText" };

        // ── DebugHUD root RectTransform ──
        var rect = hudRoot.GetComponent<RectTransform>();
        if (rect == null) rect = hudRoot.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot     = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(10, -10);
        rect.sizeDelta = new Vector2(300, 200);

        for (int i = 0; i < labels.Length; i++)
        {
            var textGO   = new GameObject(labels[i], typeof(RectTransform));
            textGO.transform.SetParent(hudRoot.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.pivot     = new Vector2(0, 1);
            textRect.anchoredPosition = new Vector2(0, -i * 30f);
            textRect.sizeDelta = new Vector2(0, 28f);

            var t = textGO.AddComponent<Text>();
            t.font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                         ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 18;
            t.color    = Color.white;
            t.text     = labels[i] + ": --";

            Wire(hud, fields[i], t);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private static GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static GameObject CreateUIChild(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    private static void Wire(Object target, string fieldName, Object value)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning($"[SceneSetupWizard] Field '{fieldName}' not found on {target.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string fieldName, bool value)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null) return;
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerMask(Object target, string fieldName, int mask)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null) return;
        prop.intValue = mask;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
