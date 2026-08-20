using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Core
{
    public sealed class SceneLoader : ISceneLoader
    {
        private readonly HashSet<string> inFlight = new(4);

        private readonly IEventBus bus;

        private const float LoadCompleteProgress = 0.9f;

        public SceneLoader(IEventBus bus = null) => this.bus = bus;

        public bool IsLoaded(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        public async Awaitable LoadAdditiveAsync(string sceneName, bool setActive = false)
        {
            if (IsLoaded(sceneName) || !inFlight.Add(sceneName)) return;

            try
            {
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    Debug.LogError($"[SceneLoader] '{sceneName}' is not in Build Settings.");
                    return;
                }

                Report(sceneName, 0f, isLoading: true);

                while (!op.isDone)
                {
                    Report(sceneName, op.progress / LoadCompleteProgress, isLoading: true);
                    await Awaitable.NextFrameAsync();
                }

                if (setActive) SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
            }
            finally
            {
                inFlight.Remove(sceneName);

                Report(sceneName, 1f, isLoading: false);
            }
        }

        public async Awaitable UnloadAsync(string sceneName)
        {
            if (!IsLoaded(sceneName)) return;

            var op = SceneManager.UnloadSceneAsync(sceneName);
            if (op == null) return;

            while (!op.isDone) await Awaitable.NextFrameAsync();

            var unload = Resources.UnloadUnusedAssets();
            while (!unload.isDone) await Awaitable.NextFrameAsync();
        }

        public async Awaitable SwapAsync(string sceneToUnload, string sceneToLoad)
        {
            await LoadAdditiveAsync(sceneToLoad, setActive: true);
            await UnloadAsync(sceneToUnload);
        }

        public async Awaitable ReturnToAsync(string sceneName, params string[] keep)
        {
            await LoadAdditiveAsync(sceneName, setActive: true);

            var doomed = new List<string>(SceneManager.sceneCount);

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded || scene.name == sceneName) continue;
                if (keep != null && System.Array.IndexOf(keep, scene.name) >= 0) continue;

                doomed.Add(scene.name);
            }

            foreach (var name in doomed) await UnloadAsync(name);
        }

        private void Report(string sceneName, float progress, bool isLoading) =>
            bus?.Publish(new SceneLoadProgressChanged(sceneName, progress, isLoading));
    }
}
