using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Babel.Unity.Infrastructure.SceneFlow
{
    public static class SceneNames
    {
        public const string Boot = "BootScene";
        public const string Menu = "MainMenuScene";
        public const string Game = "GameScene";
    }

    public sealed class SceneFlowService
    {
        public bool IsLoading { get; private set; }

        public AsyncOperation LoadBoot() => Load(SceneNames.Boot);
        public AsyncOperation LoadMenu() => Load(SceneNames.Menu);
        public AsyncOperation LoadGame() => Load(SceneNames.Game);
        public AsyncOperation RestartGame() => Load(SceneNames.Game);

        public AsyncOperation Load(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) throw new ArgumentException("Scene name is required.", nameof(sceneName));
            if (IsLoading) return null;

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("Unity could not start loading scene: " + sceneName);

            IsLoading = true;
            operation.completed += HandleLoadCompleted;
            return operation;
        }

        private void HandleLoadCompleted(AsyncOperation operation)
        {
            operation.completed -= HandleLoadCompleted;
            IsLoading = false;
        }
    }
}
