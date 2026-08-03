using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Babel.EditorTools
{
    /// <summary>
    /// Editor shortcut that always starts the complete production flow from BootScene.
    /// </summary>
    public static class BabelPlayMenu
    {
        private const string BootScenePath = "Assets/Babel/Scenes/Boot/BootScene.unity";
        private const string MenuItemPath = "Babel/▶ 开始游戏 (从 Boot) %#m";

        [MenuItem(MenuItemPath, false, 0)]
        public static void StartFromBoot()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                Debug.LogWarning("[Babel][PlayMenu] 已退出当前 Play 模式，请再次点击菜单启动。");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem(MenuItemPath, true)]
        public static bool StartFromBootValidate() => true;
    }
}
