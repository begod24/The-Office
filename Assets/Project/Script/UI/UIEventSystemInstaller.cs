using Office.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Office.UI
{
    public sealed class UIEventSystemInstaller : ServiceInstaller
    {
        private const string ObjectName = "[EventSystem]";

        public override int Order => 10;

        private GameObject owned;

        public override void Install()
        {
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

            created.AddComponent<InputSystemUIInputModule>();

            return created;
        }

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
