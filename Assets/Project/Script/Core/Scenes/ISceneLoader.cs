using UnityEngine;

namespace Office.Core
{
    public interface ISceneLoader
    {
        bool IsLoaded(string sceneName);

        Awaitable LoadAdditiveAsync(string sceneName, bool setActive = false);

        Awaitable UnloadAsync(string sceneName);

        Awaitable SwapAsync(string sceneToUnload, string sceneToLoad);
    }
}
