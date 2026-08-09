using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Core
{
    public sealed class SceneLoader : ISceneLoader
    {
        private readonly HashSet<string> inFlight = new(4);

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

                while (!op.isDone) await Awaitable.NextFrameAsync();

                if (setActive) SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
            }
            finally
            {
                inFlight.Remove(sceneName);
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

            // Collected before unloading anything: SceneManager's indices shift as scenes go
            // away, so iterating and unloading in one pass skips half of them.
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
    }
}
