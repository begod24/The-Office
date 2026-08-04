using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Core
{
    /// <inheritdoc cref="ISceneLoader"/>
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

            // Additive loads leave orphaned assets behind; without this the memory budget
            // from Technical Plan §8.2 drifts upward across a play session.
            var unload = Resources.UnloadUnusedAssets();
            while (!unload.isDone) await Awaitable.NextFrameAsync();
        }

        public async Awaitable SwapAsync(string sceneToUnload, string sceneToLoad)
        {
            await LoadAdditiveAsync(sceneToLoad, setActive: true);
            await UnloadAsync(sceneToUnload);
        }
    }
}
