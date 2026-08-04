using UnityEngine;

namespace Office.Core
{
    /// <summary>
    /// Additive scene flow for the non-networked part of the game (Boot, MainMenu, Lobby).
    /// Once a session is running, scenes are driven by NGO's NetworkSceneManager instead —
    /// mixing the two on the same scene is a guaranteed desync.
    /// </summary>
    public interface ISceneLoader
    {
        bool IsLoaded(string sceneName);

        Awaitable LoadAdditiveAsync(string sceneName, bool setActive = false);

        Awaitable UnloadAsync(string sceneName);

        /// <summary>Loads the new scene before unloading the old one, so there is never an empty frame.</summary>
        Awaitable SwapAsync(string sceneToUnload, string sceneToLoad);
    }
}
