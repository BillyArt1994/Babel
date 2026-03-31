using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public static class TileCreator
{
    [MenuItem("Babel/Create Block Tile")]
    public static void CreateBlockTile()
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.color = Color.white;
        tile.colliderType = Tile.ColliderType.None;

        AssetDatabase.CreateAsset(tile, "Assets/Tiles/BlockTile.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TileCreator] Created Assets/Tiles/BlockTile.asset");
        Selection.activeObject = tile;
    }
}
