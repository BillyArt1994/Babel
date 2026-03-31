using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Babel.Map;

/// <summary>
/// Babel 地图编辑器 - 用于可视化配置通天塔每层的槽位布局。
///
/// 使用方式：
/// 1. 菜单 Babel > Map Editor 打开窗口
/// 2. 点击 "新建 MapConfig" 或拖入已有配置
/// 3. 在左侧选择层 → 添加普通槽/通道槽
/// 4. 在 Scene View 中查看可视化并拖拽槽位位置
/// 5. 点击 "生成默认地图" 可快速生成一张标准布局
///
/// Changelog:
/// 2026-03-30  v1.0  初版 - 基础编辑器窗口 + Scene View 可视化
/// </summary>
public class MapEditor : EditorWindow
{
    // ─────────────────────── 常量 ───────────────────────
    private const float PANEL_WIDTH = 280f;
    private const float SLOT_CLICK_THRESHOLD = 0.6f;
    private const float CELL_SIZE = 1.0f;   // 格子大小（世界单位），与 Tilemap cellSize 一致
    private const float LAYER_HEIGHT_EDITOR = 1.2f; // 层间距
    private const string DEFAULT_SAVE_FOLDER = "Assets/ScriptableObjects/Maps";

    // ─────────────────────── 序列化状态（跨域重载持久化） ───────────────────────
    [SerializeField] private MapConfig _mapConfig;
    [SerializeField] private int _selectedLayerIndex = 0;
    [SerializeField] private int _selectedSlotIndex = -1;
    [SerializeField] private bool _addNormalSlotMode;
    [SerializeField] private bool _addPassageSlotMode;

    // ─────────────────────── 临时状态 ───────────────────────
    private SerializedObject _serializedConfig;
    private Vector2 _layerScrollPos;
    private Vector2 _slotScrollPos;

    // ─────────────────────── 菜单入口 ───────────────────────
    [MenuItem("Babel/Map Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<MapEditor>("Map Editor");
        window.minSize = new Vector2(300, 400);
    }

    // ─────────────────────── 生命周期 ───────────────────────
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        RefreshSerializedObject();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void RefreshSerializedObject()
    {
        if (_mapConfig != null)
            _serializedConfig = new SerializedObject(_mapConfig);
        else
            _serializedConfig = null;
    }

    // ═══════════════════════════════════════════════════════════════
    //  EditorWindow GUI
    // ═══════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        // 保持 SerializedObject 同步
        if (_mapConfig != null && _serializedConfig == null)
            RefreshSerializedObject();

        if (_serializedConfig != null && _serializedConfig.targetObject == null)
        {
            _serializedConfig = null;
            _mapConfig = null;
        }

        _serializedConfig?.Update();

        EditorGUILayout.BeginHorizontal();

        // ─── 左侧面板 ───
        EditorGUILayout.BeginVertical(GUILayout.Width(PANEL_WIDTH));
        DrawLeftPanel();
        EditorGUILayout.EndVertical();

        // ─── 右侧提示 ───
        EditorGUILayout.BeginVertical();
        DrawRightPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        _serializedConfig?.ApplyModifiedProperties();
    }

    // ─────────────────────── 左侧面板 ───────────────────────
    private void DrawLeftPanel()
    {
        GUILayout.Label("Map Config", EditorStyles.boldLabel);

        // ── 新建 / 加载 ──
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新建 MapConfig"))
            CreateNewMapConfig();
        if (GUILayout.Button("生成默认地图"))
            GenerateDefaultMap();
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        _mapConfig = (MapConfig)EditorGUILayout.ObjectField(
            "加载 MapConfig", _mapConfig, typeof(MapConfig), false);
        if (EditorGUI.EndChangeCheck())
        {
            RefreshSerializedObject();
            _selectedLayerIndex = 0;
            _selectedSlotIndex = -1;
        }

        if (_mapConfig == null)
        {
            EditorGUILayout.HelpBox("请新建或加载一个 MapConfig 资产。", MessageType.Info);
            return;
        }

        // ── 应用到场景 ──
        if (GUILayout.Button("应用到场景 TowerConstructionSystem"))
            ApplyToScene();
        EditorGUILayout.Space(4);

        EditorGUILayout.Space(8);

        // ── 层列表 ──
        GUILayout.Label("层列表", EditorStyles.boldLabel);

        _layerScrollPos = EditorGUILayout.BeginScrollView(_layerScrollPos, GUILayout.Height(220));
        if (_mapConfig.layers != null)
        {
            for (int i = 0; i < _mapConfig.layers.Count; i++)
            {
                var layer = _mapConfig.layers[i];
                int normalCount = 0, passageCount = 0;
                if (layer.slots != null)
                {
                    foreach (var s in layer.slots)
                    {
                        if (s.isPassage) passageCount++;
                        else normalCount++;
                    }
                }

                bool selected = (i == _selectedLayerIndex);
                string label = $"Layer {layer.layerIndex}  (普通:{normalCount} 通道:{passageCount})";

                GUIStyle style = selected ? "selectionRect" : EditorStyles.label;
                if (selected)
                {
                    var rect = EditorGUILayout.BeginHorizontal("selectionRect");
                    GUILayout.Label(label, EditorStyles.whiteLabel);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    if (GUILayout.Button(label, EditorStyles.label))
                    {
                        _selectedLayerIndex = i;
                        _selectedSlotIndex = -1;
                        SceneView.RepaintAll();
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);

        // ── 当前层尺寸编辑 ──
        if (_mapConfig.layers != null && _selectedLayerIndex >= 0 && _selectedLayerIndex < _mapConfig.layers.Count)
        {
            var selectedLayer = _mapConfig.layers[_selectedLayerIndex];
            EditorGUILayout.LabelField($"Layer {selectedLayer.layerIndex} 尺寸", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int newW = EditorGUILayout.IntField("宽度（格）", selectedLayer.layerWidth);
            int newH = EditorGUILayout.IntField("高度（格）", selectedLayer.layerHeight);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_mapConfig, "Resize Layer");
                selectedLayer.layerWidth  = Mathf.Max(1, newW);
                selectedLayer.layerHeight = Mathf.Max(1, newH);
                EditorUtility.SetDirty(_mapConfig);
                SceneView.RepaintAll();
            }
            EditorGUILayout.Space(4);
        }

        // ── 添加槽位按钮 ──
        DrawAddSlotButtons();

        EditorGUILayout.Space(4);

        // ── 当前层的槽位列表 ──
        DrawSlotList();
    }

    private void DrawAddSlotButtons()
    {
        if (_mapConfig == null || _mapConfig.layers == null ||
            _selectedLayerIndex < 0 || _selectedLayerIndex >= _mapConfig.layers.Count)
            return;

        EditorGUILayout.BeginHorizontal();

        // 切换添加普通槽模式
        Color prevColor = GUI.backgroundColor;
        if (_addNormalSlotMode) GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button(_addNormalSlotMode ? "[ 添加普通槽 - 激活 ]" : "添加普通槽"))
        {
            _addNormalSlotMode = !_addNormalSlotMode;
            if (_addNormalSlotMode) _addPassageSlotMode = false;
        }
        GUI.backgroundColor = prevColor;

        // 切换添加通道槽模式
        if (_addPassageSlotMode) GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
        if (GUILayout.Button(_addPassageSlotMode ? "[ 添加通道槽 - 激活 ]" : "添加通道槽"))
        {
            _addPassageSlotMode = !_addPassageSlotMode;
            if (_addPassageSlotMode) _addNormalSlotMode = false;
        }
        GUI.backgroundColor = prevColor;

        EditorGUILayout.EndHorizontal();

        if (_addNormalSlotMode || _addPassageSlotMode)
        {
            EditorGUILayout.HelpBox("在 Scene View 中点击空白处放置槽位。按 Esc 退出。", MessageType.Info);
        }
    }

    private void DrawSlotList()
    {
        if (_mapConfig == null || _mapConfig.layers == null ||
            _selectedLayerIndex < 0 || _selectedLayerIndex >= _mapConfig.layers.Count)
            return;

        var layer = _mapConfig.layers[_selectedLayerIndex];
        GUILayout.Label($"Layer {layer.layerIndex} 槽位", EditorStyles.boldLabel);

        _slotScrollPos = EditorGUILayout.BeginScrollView(_slotScrollPos);

        if (layer.slots != null)
        {
            int removeIndex = -1;
            for (int i = 0; i < layer.slots.Count; i++)
            {
                var slot = layer.slots[i];
                bool selected = (i == _selectedSlotIndex);

                EditorGUILayout.BeginHorizontal(selected ? "selectionRect" : "box");

                // 选中
                if (GUILayout.Button(selected ? ">" : " ", GUILayout.Width(20)))
                {
                    _selectedSlotIndex = i;
                    // 聚焦 Scene View 到该槽位
                    if (SceneView.lastActiveSceneView != null)
                    {
                        SceneView.lastActiveSceneView.LookAt(
                            new Vector3(slot.position.x, slot.position.y, 0),
                            Quaternion.identity, 5f);
                    }
                    SceneView.RepaintAll();
                }

                string typeStr = slot.isPassage ? "[通道]" : "[普通]";
                GUILayout.Label($"{typeStr} ({slot.position.x:F1}, {slot.position.y:F1})", GUILayout.ExpandWidth(true));

                if (GUILayout.Button("X", GUILayout.Width(22)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(_mapConfig, "Delete Slot");
                layer.slots.RemoveAt(removeIndex);
                if (_selectedSlotIndex >= layer.slots.Count)
                    _selectedSlotIndex = layer.slots.Count - 1;
                EditorUtility.SetDirty(_mapConfig);
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.EndScrollView();

        // 选中槽位的详细编辑
        DrawSelectedSlotInspector(layer);
    }

    private void DrawSelectedSlotInspector(LayerData layer)
    {
        if (_selectedSlotIndex < 0 || _selectedSlotIndex >= layer.slots.Count)
            return;

        EditorGUILayout.Space(4);
        GUILayout.Label("选中槽位", EditorStyles.boldLabel);

        var slot = layer.slots[_selectedSlotIndex];

        EditorGUI.BeginChangeCheck();
        Vector2 newPos = EditorGUILayout.Vector2Field("位置", slot.position);
        bool newPassage = EditorGUILayout.Toggle("通道槽", slot.isPassage);
        int newRequired = EditorGUILayout.IntField("所需敌人数", slot.requiredCount);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_mapConfig, "Modify Slot");
            slot.position = newPos;
            slot.isPassage = newPassage;
            slot.requiredCount = Mathf.Max(1, newRequired);
            EditorUtility.SetDirty(_mapConfig);
            SceneView.RepaintAll();
        }
    }

    // ─────────────────────── 右侧面板 ───────────────────────
    private void DrawRightPanel()
    {
        GUILayout.Label("Scene View 操作指南", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "1. 在左侧选择一个层，设置层宽高\n" +
            "2. 点击 \"添加普通槽\" 或 \"添加通道槽\" 进入放置模式\n" +
            "3. 在 Scene View 中点击格子放置槽位（自动吸附到格子中心）\n" +
            "4. 拖拽已选槽位方块可调整位置\n" +
            "5. 点击已有槽位可选中并编辑属性\n" +
            "6. 按 Esc 退出放置模式\n\n" +
            "图例：\n" +
            "  灰色方块 = 普通槽（选中时黄色）\n" +
            "  蓝灰色方块 = 通道槽（选中时青色）\n" +
            "  黄色边框 = 当前选中层边界\n" +
            "  淡色网格 = 当前层可用格子",
            MessageType.None);

        if (_mapConfig != null)
        {
            EditorGUILayout.Space(16);
            GUILayout.Label("统计", EditorStyles.boldLabel);
            int totalSlots = 0, totalNormal = 0, totalPassage = 0;
            if (_mapConfig.layers != null)
            {
                foreach (var layer in _mapConfig.layers)
                {
                    if (layer.slots == null) continue;
                    foreach (var s in layer.slots)
                    {
                        totalSlots++;
                        if (s.isPassage) totalPassage++;
                        else totalNormal++;
                    }
                }
            }
            EditorGUILayout.LabelField("总层数", _mapConfig.layers?.Count.ToString() ?? "0");
            EditorGUILayout.LabelField("总槽位", totalSlots.ToString());
            EditorGUILayout.LabelField("普通槽", totalNormal.ToString());
            EditorGUILayout.LabelField("通道槽", totalPassage.ToString());
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Scene View 可视化 & 交互
    // ═══════════════════════════════════════════════════════════════
    private void OnSceneGUI(SceneView sceneView)
    {
        if (_mapConfig == null || _mapConfig.layers == null)
            return;

        // 处理 Esc 退出放置模式
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            _addNormalSlotMode = false;
            _addPassageSlotMode = false;
            Repaint();
            Event.current.Use();
        }

        // 绘制所有层
        for (int li = 0; li < _mapConfig.layers.Count; li++)
        {
            bool isCurrentLayer = (li == _selectedLayerIndex);
            DrawLayerGizmos(_mapConfig.layers[li], isCurrentLayer, li);
        }

        // 绘制层高度参考线
        DrawLayerGuides();

        // 处理当前层的交互（点击、拖拽）
        if (_selectedLayerIndex >= 0 && _selectedLayerIndex < _mapConfig.layers.Count)
        {
            HandleSceneInteraction(_mapConfig.layers[_selectedLayerIndex]);
        }
    }

    private void DrawLayerGizmos(LayerData layer, bool isCurrentLayer, int layerListIndex)
    {
        if (layer.slots == null) return;

        const float HALF = CELL_SIZE * 0.5f;

        // 绘制该层所有格子的背景网格（根据 layerWidth × layerHeight）
        if (isCurrentLayer)
        {
            float originX = -(layer.layerWidth * CELL_SIZE) * 0.5f;
            float originY = layer.layerIndex * LAYER_HEIGHT_EDITOR;

            for (int gx = 0; gx < layer.layerWidth; gx++)
            {
                for (int gy = 0; gy < layer.layerHeight; gy++)
                {
                    float cx = originX + (gx + 0.5f) * CELL_SIZE;
                    float cy = originY + (gy + 0.5f) * CELL_SIZE;
                    Vector3 center = new Vector3(cx, cy, 0);
                    Vector3[] corners = {
                        center + new Vector3(-HALF, -HALF, 0),
                        center + new Vector3( HALF, -HALF, 0),
                        center + new Vector3( HALF,  HALF, 0),
                        center + new Vector3(-HALF,  HALF, 0),
                    };
                    Handles.DrawSolidRectangleWithOutline(corners,
                        new Color(1f, 1f, 1f, 0.03f),
                        new Color(1f, 1f, 1f, 0.15f));
                }
            }
        }

        // 绘制已放置的槽位
        for (int si = 0; si < layer.slots.Count; si++)
        {
            var slot = layer.slots[si];
            Vector3 worldPos = new Vector3(slot.position.x, slot.position.y, 0);
            bool isSelectedSlot = isCurrentLayer && si == _selectedSlotIndex;

            Vector3[] corners = {
                worldPos + new Vector3(-HALF, -HALF, 0),
                worldPos + new Vector3( HALF, -HALF, 0),
                worldPos + new Vector3( HALF,  HALF, 0),
                worldPos + new Vector3(-HALF,  HALF, 0),
            };

            Color fillColor, outlineColor;
            if (slot.isPassage)
            {
                fillColor   = isCurrentLayer ? (isSelectedSlot ? new Color(0.3f, 0.8f, 1f, 0.5f) : new Color(0.4f, 0.4f, 0.5f, 0.3f)) : new Color(0.4f, 0.4f, 0.5f, 0.1f);
                outlineColor = isCurrentLayer ? (isSelectedSlot ? Color.cyan : new Color(0.4f, 0.4f, 0.8f, 1f)) : new Color(0.4f, 0.4f, 0.8f, 0.3f);
            }
            else
            {
                fillColor   = isCurrentLayer ? (isSelectedSlot ? new Color(1f, 1f, 0f, 0.3f) : new Color(0.55f, 0.55f, 0.55f, 0.3f)) : new Color(0.55f, 0.55f, 0.55f, 0.1f);
                outlineColor = isCurrentLayer ? (isSelectedSlot ? Color.yellow : new Color(0.8f, 0.8f, 0.8f, 1f)) : new Color(0.8f, 0.8f, 0.8f, 0.2f);
            }

            Handles.DrawSolidRectangleWithOutline(corners, fillColor, outlineColor);

            if (isCurrentLayer)
            {
                string label = slot.isPassage ? "P" : "N";
                Handles.Label(worldPos + new Vector3(HALF + 0.05f, 0.1f, 0), $"{label}{si}", EditorStyles.miniLabel);
            }
        }
    }

    private void DrawLayerGuides()
    {
        if (_mapConfig == null || _mapConfig.layers == null) return;

        for (int i = 0; i < _mapConfig.layers.Count; i++)
        {
            var layer = _mapConfig.layers[i];
            float w = layer.layerWidth * CELL_SIZE;
            float h = layer.layerHeight * CELL_SIZE;
            float x0 = -w * 0.5f;
            float y0 = layer.layerIndex * LAYER_HEIGHT_EDITOR;

            bool isSelected = (i == _selectedLayerIndex);
            Handles.color = isSelected
                ? new Color(1f, 1f, 0f, 0.4f)
                : new Color(1f, 1f, 1f, 0.08f);

            // 绘制层边界矩形
            Vector3 bl = new Vector3(x0,     y0,     0);
            Vector3 br = new Vector3(x0 + w, y0,     0);
            Vector3 tr = new Vector3(x0 + w, y0 + h, 0);
            Vector3 tl = new Vector3(x0,     y0 + h, 0);
            Handles.DrawLine(bl, br);
            Handles.DrawLine(br, tr);
            Handles.DrawLine(tr, tl);
            Handles.DrawLine(tl, bl);

            // 层标签
            Handles.Label(new Vector3(x0 - 1.2f, y0, 0), $"L{i}", EditorStyles.miniLabel);
        }
    }

    private void HandleSceneInteraction(LayerData layer)
    {
        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        // ── 拖拽选中的槽位 ──
        if (_selectedSlotIndex >= 0 && _selectedSlotIndex < layer.slots.Count)
        {
            var slot = layer.slots[_selectedSlotIndex];
            Vector3 worldPos = new Vector3(slot.position.x, slot.position.y, 0);

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.FreeMoveHandle(
                worldPos, 0.2f, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_mapConfig, "Move Slot");
                slot.position = new Vector2(newPos.x, newPos.y);
                EditorUtility.SetDirty(_mapConfig);
                Repaint();
            }
        }

        // ── 点击交互 ──
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector2 mouseWorld = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;

            // 尝试点击已有槽位
            int clickedSlot = FindSlotAtPosition(layer, mouseWorld);

            if (clickedSlot >= 0)
            {
                // 选中已有槽位
                _selectedSlotIndex = clickedSlot;
                Repaint();
                e.Use();
            }
            else if (_addNormalSlotMode || _addPassageSlotMode)
            {
                // 吸附到最近格子中心
                Vector2 snapped = SnapToGrid(mouseWorld, layer);

                Undo.RecordObject(_mapConfig, "Add Slot");
                var newSlot = new SlotData
                {
                    position = snapped,
                    isPassage = _addPassageSlotMode,
                    requiredCount = 1
                };
                layer.slots.Add(newSlot);
                _selectedSlotIndex = layer.slots.Count - 1;

                EditorUtility.SetDirty(_mapConfig);
                Repaint();
                e.Use();
            }
        }

        // 在添加模式下阻止 Scene View 默认交互（避免框选等）
        if ((_addNormalSlotMode || _addPassageSlotMode) && e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(controlId);
        }
    }

    /// <summary>
    /// 将世界坐标吸附到该层最近的格子中心。
    /// 格子以塔中心为原点，向左右扩展 layerWidth/2 格。
    /// </summary>
    private Vector2 SnapToGrid(Vector2 worldPos, LayerData layer)
    {
        float originX = -(layer.layerWidth * CELL_SIZE) * 0.5f;
        float originY = layer.layerIndex * LAYER_HEIGHT_EDITOR;

        // 转换到层本地坐标，四舍五入到最近整数格
        float localX = worldPos.x - originX;
        float localY = worldPos.y - originY;

        int gx = Mathf.Clamp(Mathf.FloorToInt(localX / CELL_SIZE), 0, layer.layerWidth - 1);
        int gy = Mathf.Clamp(Mathf.FloorToInt(localY / CELL_SIZE), 0, layer.layerHeight - 1);

        // 返回格子中心世界坐标
        float cx = originX + (gx + 0.5f) * CELL_SIZE;
        float cy = originY + (gy + 0.5f) * CELL_SIZE;
        return new Vector2(cx, cy);
    }

    private int FindSlotAtPosition(LayerData layer, Vector2 worldPos)
    {
        if (layer.slots == null) return -1;

        float bestDist = SLOT_CLICK_THRESHOLD;
        int bestIndex = -1;
        for (int i = 0; i < layer.slots.Count; i++)
        {
            float dist = Vector2.Distance(layer.slots[i].position, worldPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    // ═══════════════════════════════════════════════════════════════
    //  资产创建
    // ═══════════════════════════════════════════════════════════════
    private void CreateNewMapConfig()
    {
        // 确保目录存在
        EnsureFolderExists(DEFAULT_SAVE_FOLDER);

        string path = AssetDatabase.GenerateUniqueAssetPath(
            DEFAULT_SAVE_FOLDER + "/MapConfig.asset");

        var config = CreateInstance<MapConfig>();
        config.layers = new List<LayerData>();
        for (int i = 0; i < MapConfig.MAX_LAYERS; i++)
        {
            config.layers.Add(new LayerData
            {
                layerIndex = i,
                slots = new List<SlotData>()
            });
        }

        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _mapConfig = config;
        RefreshSerializedObject();
        _selectedLayerIndex = 0;
        _selectedSlotIndex = -1;

        EditorGUIUtility.PingObject(config);
        Debug.Log($"[MapEditor] 新建 MapConfig: {path}");
    }

    // ─────────────────────── 默认地图生成 ───────────────────────
    private void GenerateDefaultMap()
    {
        if (_mapConfig == null)
            CreateNewMapConfig();

        Undo.RecordObject(_mapConfig, "Generate Default Map");

        const float BASE_WIDTH_G = 10f;
        const float TOP_WIDTH_RATIO_G = 0.2f;
        const float LAYER_HEIGHT_G = 1.2f;
        const int NORMAL_SLOTS = 4;
        const int PASSAGE_SLOTS = 2;

        _mapConfig.layers = new List<LayerData>();

        for (int i = 0; i < MapConfig.MAX_LAYERS; i++)
        {
            float step = (1f - TOP_WIDTH_RATIO_G) / (MapConfig.MAX_LAYERS - 1f);
            float layerWidth = BASE_WIDTH_G * (1f - i * step);
            float halfWidth = layerWidth * 0.5f;
            float y = i * LAYER_HEIGHT_G;

            var layerData = new LayerData
            {
                layerIndex = i,
                slots      = new List<SlotData>(),
                layerWidth  = 6,
                layerHeight = 1,
            };

            // 普通槽：均匀分布在层宽内
            for (int s = 0; s < NORMAL_SLOTS; s++)
            {
                float t = NORMAL_SLOTS > 1 ? (float)s / (NORMAL_SLOTS - 1) : 0.5f;
                float x = Mathf.Lerp(-halfWidth * 0.8f, halfWidth * 0.8f, t);
                layerData.slots.Add(new SlotData
                {
                    position = new Vector2(x, y),
                    isPassage = false,
                    requiredCount = 1
                });
            }

            // 通道槽：居中分布
            for (int s = 0; s < PASSAGE_SLOTS; s++)
            {
                float t = PASSAGE_SLOTS > 1 ? (float)s / (PASSAGE_SLOTS - 1) : 0.5f;
                float x = Mathf.Lerp(-halfWidth * 0.3f, halfWidth * 0.3f, t);
                layerData.slots.Add(new SlotData
                {
                    position = new Vector2(x, y),
                    isPassage = true,
                    requiredCount = 1
                });
            }

            _mapConfig.layers.Add(layerData);
        }

        EditorUtility.SetDirty(_mapConfig);
        AssetDatabase.SaveAssets();
        RefreshSerializedObject();
        _selectedLayerIndex = 0;
        _selectedSlotIndex = -1;
        SceneView.RepaintAll();

        Debug.Log($"[MapEditor] 默认地图已生成：{MapConfig.MAX_LAYERS} 层，每层 {NORMAL_SLOTS} 普通槽 + {PASSAGE_SLOTS} 通道槽。");
    }

    // ─────────────────────── 工具方法 ───────────────────────
    private void ApplyToScene()
    {
        var tower = FindObjectOfType<TowerConstructionSystem>();
        if (tower == null)
        {
            EditorUtility.DisplayDialog("提示", "场景中未找到 TowerConstructionSystem。", "OK");
            return;
        }

        Undo.RecordObject(tower, "Apply MapConfig to Scene");

        // 通过 SerializedObject 设置私有 SerializeField
        var so = new SerializedObject(tower);
        var prop = so.FindProperty("_mapConfig");
        if (prop != null)
        {
            prop.objectReferenceValue = _mapConfig;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tower);
            Debug.Log($"[MapEditor] MapConfig '{_mapConfig.name}' 已应用到场景 TowerConstructionSystem。");
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "找不到 TowerConstructionSystem._mapConfig 字段。", "OK");
        }
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
