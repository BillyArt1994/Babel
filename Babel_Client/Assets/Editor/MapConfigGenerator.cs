using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Babel.Map;

/// <summary>
/// Editor utility to generate a default MapConfig asset with slot layouts.
/// Menu: Babel/Generate Default MapConfig
/// </summary>
public static class MapConfigGenerator
{
    private const int LAYER_COUNT = 10;
    // Grid-aligned layout (matches TileMap cellSize=1, layerWidth=6, layerHeight=1)
    private const int   LAYER_WIDTH         = 6;    // grid columns per layer
    private const float CELL_SIZE           = 1.0f; // world units per cell
    private const float LAYER_HEIGHT        = 1.2f; // world units between layers
    // Slots per layer
    private const int NORMAL_SLOTS_PER_LAYER  = 4;
    private const int PASSAGE_SLOTS_PER_LAYER = 2;

    [MenuItem("Babel/Generate Default MapConfig")]
    public static void GenerateDefaultMapConfig()
    {
        // Create the asset
        var config = ScriptableObject.CreateInstance<MapConfig>();
        config.layers = new List<LayerData>();

        for (int layerIdx = 0; layerIdx < LAYER_COUNT; layerIdx++)
        {
            // Grid origin: layer is centred at X=0, bottom-left cell starts at -layerWidth/2
            float originX = -(LAYER_WIDTH * CELL_SIZE) * 0.5f;
            float layerY  = layerIdx * LAYER_HEIGHT;

            var layerData = new LayerData
            {
                layerIndex  = layerIdx,
                slots       = new List<SlotData>(),
                layerWidth  = LAYER_WIDTH,
                layerHeight = 1,
            };

            // Normal slots: columns 0,1,4,5 (outer four cells)
            int[] normalCols = { 0, 1, 4, 5 };
            foreach (int col in normalCols)
            {
                float x = originX + (col + 0.5f) * CELL_SIZE; // cell centre
                layerData.slots.Add(new SlotData
                {
                    position     = new Vector2(x, layerY + 0.5f),
                    isPassage    = false,
                    requiredCount = 1
                });
            }

            // Passage slots: columns 2,3 (inner two cells — staircase)
            int[] passageCols = { 2, 3 };
            foreach (int col in passageCols)
            {
                float x = originX + (col + 0.5f) * CELL_SIZE;
                layerData.slots.Add(new SlotData
                {
                    position     = new Vector2(x, layerY + 0.5f),
                    isPassage    = true,
                    requiredCount = 1
                });
            }

            config.layers.Add(layerData);
        }

        // Save as asset
        string path = "Assets/Data/MapConfig_Default.asset";
        System.IO.Directory.CreateDirectory("Assets/Data");
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MapConfigGenerator] Created MapConfig at {path} with {LAYER_COUNT} layers, " +
                  $"{NORMAL_SLOTS_PER_LAYER} normal + {PASSAGE_SLOTS_PER_LAYER} passage slots per layer.");

        // Select it in the Project window
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }
}
