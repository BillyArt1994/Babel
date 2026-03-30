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
    private const float NORMAL_SLOT_RADIUS = 0.3f;
    private const float PASSAGE_SLOT_RADIUS = 0.4f;
    private const float SLOT_CLICK_THRESHOLD = 0.5f;
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
            "1. 在左侧选择一个层\n" +
            "2. 点击 \"添加普通槽\" 或 \"添加通道槽\" 进入放置模式\n" +
            "3. 在 Scene View 中点击空白处放置槽位\n" +
            "4. 直接拖拽槽位圆圈可调整位置\n" +
            "5. 点击已有槽位可选中并编辑属性\n" +
            "6. 按 Esc 退出放置模式\n\n" +
            "图例：\n" +
            "  白色圆圈 = 普通槽（选中时黄色高亮）\n" +
            "  蓝色填充圆圈 = 通道槽\n" +
            "  半透明 = 非当前编辑层",
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

        for (int si = 0; si < layer.slots.Count; si++)
        {
            var slot = layer.slots[si];
            Vector3 worldPos = new Vector3(slot.position.x, slot.position.y, 0);
            bool isSelectedSlot = isCurrentLayer && si == _selectedSlotIndex;

            if (slot.isPassage)
            {
                // 通道槽：蓝色圆圈 + 半透明填充
                Color fillColor = isCurrentLayer
                    ? new Color(0.3f, 0.5f, 1f, 0.3f)
                    : new Color(0.3f, 0.5f, 1f, 0.1f);
                Color outlineColor = isCurrentLayer
                    ? new Color(0.3f, 0.5f, 1f, 1f)
                    : new Color(0.3f, 0.5f, 1f, 0.3f);

                if (isSelectedSlot)
                {
                    fillColor = new Color(0.3f, 0.8f, 1f, 0.5f);
                    outlineColor = Color.cyan;
                }

                Handles.color = fillColor;
                Handles.DrawSolidDisc(worldPos, Vector3.forward, PASSAGE_SLOT_RADIUS);
                Handles.color = outlineColor;
                Handles.DrawWireDisc(worldPos, Vector3.forward, PASSAGE_SLOT_RADIUS);
            }
            else
            {
                // 普通槽：白色圆圈，选中时黄色高亮
                Color color;
                if (isSelectedSlot)
                    color = Color.yellow;
                else if (isCurrentLayer)
                    color = Color.white;
                else
                    color = new Color(1f, 1f, 1f, 0.25f);

                Handles.color = color;
                Handles.DrawWireDisc(worldPos, Vector3.forward, NORMAL_SLOT_RADIUS);

                if (isSelectedSlot)
                {
                    Handles.color = new Color(1f, 1f, 0f, 0.15f);
                    Handles.DrawSolidDisc(worldPos, Vector3.forward, NORMAL_SLOT_RADIUS);
                }
            }

            // 标签（仅当前层）
            if (isCurrentLayer)
            {
                string label = slot.isPassage ? "P" : "N";
                Handles.Label(worldPos + new Vector3(0.35f, 0.15f, 0),
                    $"{label}{si}", EditorStyles.miniLabel);
            }
        }
    }

    private void DrawLayerGuides()
    {
        const float LAYER_HEIGHT = 1.2f;
        const float GUIDE_HALF_WIDTH = 8f;

        Handles.color = new Color(1f, 1f, 1f, 0.08f);
        for (int i = 0; i < MapConfig.MAX_LAYERS; i++)
        {
            float y = i * LAYER_HEIGHT;
            Vector3 left = new Vector3(-GUIDE_HALF_WIDTH, y, 0);
            Vector3 right = new Vector3(GUIDE_HALF_WIDTH, y, 0);
            Handles.DrawLine(left, right);
            Handles.Label(new Vector3(-GUIDE_HALF_WIDTH - 1f, y, 0),
                $"L{i}", EditorStyles.miniLabel);
        }

        // 当前选中层高亮
        if (_selectedLayerIndex >= 0 && _selectedLayerIndex < MapConfig.MAX_LAYERS)
        {
            float y = _selectedLayerIndex * LAYER_HEIGHT;
            Handles.color = new Color(1f, 1f, 0f, 0.15f);
            Vector3 left = new Vector3(-GUIDE_HALF_WIDTH, y, 0);
            Vector3 right = new Vector3(GUIDE_HALF_WIDTH, y, 0);
            Handles.DrawLine(left, right);
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
                // 添加新槽位
                Undo.RecordObject(_mapConfig, "Add Slot");

                var newSlot = new SlotData
                {
                    position = mouseWorld,
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
        {
            CreateNewMapConfig();
        }

        Undo.RecordObject(_mapConfig, "Generate Default Map");

        const float BOTTOM_WIDTH = 10f;
        const float TOP_WIDTH = 2f;
        const float LAYER_HEIGHT = 1.2f;
        const int BOTTOM_SLOTS = 10;
        const int TOP_SLOTS = 1;

        _mapConfig.layers = new List<LayerData>();

        for (int i = 0; i < MapConfig.MAX_LAYERS; i++)
        {
            float t = (float)i / (MapConfig.MAX_LAYERS - 1);
            float layerWidth = Mathf.Lerp(BOTTOM_WIDTH, TOP_WIDTH, t);
            int totalSlotCount = Mathf.RoundToInt(Mathf.Lerp(BOTTOM_SLOTS, TOP_SLOTS, t));
            totalSlotCount = Mathf.Max(1, totalSlotCount);
            float y = i * LAYER_HEIGHT;

            var layerData = new LayerData
            {
                layerIndex = i,
                slots = new List<SlotData>()
            };

            // 通道槽：每层 1 个，位于中心（X=0）
            layerData.slots.Add(new SlotData
            {
                position = new Vector2(0f, y),
                isPassage = true,
                requiredCount = 1
            });

            // 普通槽：均匀分布，排除通道位置（X=0 附近）
            int normalSlotCount = totalSlotCount - 1; // 减去通道槽
            if (normalSlotCount > 0)
            {
                float halfWidth = layerWidth * 0.5f;
                float spacing = layerWidth / (normalSlotCount + 1);

                for (int s = 0; s < normalSlotCount; s++)
                {
                    float x = -halfWidth + spacing * (s + 1);
                    // 如果太靠近中心（通道位置），稍微偏移
                    if (Mathf.Abs(x) < 0.3f)
                    {
                        x = x >= 0 ? 0.35f : -0.35f;
                    }

                    layerData.slots.Add(new SlotData
                    {
                        position = new Vector2(x, y),
                        isPassage = false,
                        requiredCount = 1
                    });
                }
            }

            _mapConfig.layers.Add(layerData);
        }

        EditorUtility.SetDirty(_mapConfig);
        AssetDatabase.SaveAssets();
        RefreshSerializedObject();
        _selectedLayerIndex = 0;
        _selectedSlotIndex = -1;
        SceneView.RepaintAll();

        Debug.Log("[MapEditor] 默认地图已生成：10 层，底层宽 10，顶层宽 2，线性插值槽位数。");
    }

    // ─────────────────────── 工具方法 ───────────────────────
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
