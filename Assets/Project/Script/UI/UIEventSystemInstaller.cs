using Office.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Office.UI
{
    /// <summary>
    /// Owns the one EventSystem the whole application uses.
    /// </summary>
    /// <remarks>
    /// Scene flow here is additive and overlapping — the loader brings the next scene up
    /// before it drops the previous one — so an EventSystem baked into each UI scene means
    /// two are live for the whole length of a load. Unity warns about that every frame, and
    /// the real cost is worse than the noise: two active systems both raise events, so the
    /// outgoing screen keeps taking clicks while the incoming one is already visible.
    /// The composition root never unloads, so one system installed here outlives every swap.
    /// </remarks>
    public sealed class UIEventSystemInstaller : ServiceInstaller
    {
        private const string ObjectName = "[EventSystem]";

        // Before the session installer: menus must be clickable the moment a scene appears.
        public override int Order => 10;

        private GameObject owned;

        public override void Install()
        {
            // Adopt rather than add a second one. Nothing ships an EventSystem today, but a
            // hand-built scene might, and two is the exact problem this class exists to stop.
            var existing = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);

            owned = existing != null ? existing.transform.root.gameObject : Create();

            DontDestroyOnLoad(owned);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public override void Uninstall()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (owned != null) Destroy(owned);
            owned = null;
        }

        private static GameObject Create()
        {
            var created = new GameObject(ObjectName);

            created.AddComponent<EventSystem>();

            // Default configuration reads the project-wide actions, whose UI map already
            // carries Navigate, Submit, Cancel, Point and Click.
            created.AddComponent<InputSystemUIInputModule>();

            return created;
        }

        // A stray EventSystem in a scene resurrects the duplicate the moment that scene
        // loads. Say so loudly — a silent removal would hide a scene that needs rebuilding.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var stray = root.GetComponentInChildren<EventSystem>(true);
                if (stray == null || stray.gameObject == owned) continue;

                Debug.LogWarning(
                    $"[UI] '{scene.name}' carries its own EventSystem ('{stray.name}'). " +
                    "Removing it — the boot scene owns the only one. Regenerate that scene " +
                    "from its Office/Setup menu item so it stops shipping one.", stray);

                Destroy(stray.gameObject);
            }
        }
    }
}
