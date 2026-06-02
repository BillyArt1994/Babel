using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Babel.EditorTools
{
    /// <summary>
    /// 编辑器菜单栏入口：一键从主菜单场景进入 Play 模式，走完整正式流程。
    /// </summary>
    public static class BabelPlayMenu
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        private const string MenuItemPath = "Babel/▶ 开始游戏 (从主菜单) %#m"; // Ctrl+Shift+M

        [MenuItem(MenuItemPath, false, 0)]
        public static void StartFromMainMenu()
        {
            // 已在 Play 模式则先退出，避免叠加
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                Debug.LogWarning("[BABEL][PlayMenu] 已退出当前 Play 模式，请再次点击菜单启动。");
                return;
            }

            // 提示保存当前场景改动
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return; // 用户取消
            }

            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        // 已在 Play 时把菜单项变成可用（用于一键停止）；非 Play 时也可用
        [MenuItem(MenuItemPath, true)]
        public static bool StartFromMainMenuValidate() => true;
    }
}
