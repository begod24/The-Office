using UnityEngine;

namespace Office.Core
{
    public interface ISceneLoader
    {
        bool IsLoaded(string sceneName);

        Awaitable LoadAdditiveAsync(string sceneName, bool setActive = false);

        Awaitable UnloadAsync(string sceneName);

        Awaitable SwapAsync(string sceneToUnload, string sceneToLoad);

        /// <summary>
        /// Brings <paramref name="sceneName"/> up and takes every other loaded scene down,
        /// except those named in <paramref name="keep"/>.
        /// </summary>
        /// <remarks>
        /// For getting out of a session when the caller cannot know what is loaded. A dropped
        /// host can strand a client in the lobby or in any run scene, and which run scene it
        /// is depends on the level being played — naming it at the call site would put the
        /// level list in the network layer.
        /// </remarks>
        Awaitable ReturnToAsync(string sceneName, params string[] keep);
    }
}
