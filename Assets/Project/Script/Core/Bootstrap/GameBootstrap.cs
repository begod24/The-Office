using System;
using System.Linq;
using Office.Data;
using UnityEngine;

namespace Office.Core
{
    /// <summary>
    /// The composition root. Technical Plan §4.2. Lives on exactly one object in
    /// <c>SCN_Boot</c>, which is build index 0 and never unloads.
    ///
    /// This is the only place in the project where a service is constructed. Nothing else may
    /// call <c>new</c> on a service, and nothing may reach across scenes through an inspector
    /// reference — systems find each other through <see cref="ServiceLocator"/> and
    /// <see cref="IEventBus"/>. Violating that is what causes "B edited the level and A's
    /// scene broke" (Technical Plan §3.3).
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Installers")]
        [Tooltip("One per assembly that owns services. Executed in ascending Order, " +
                 "torn down in reverse.")]
        [SerializeField] private ServiceInstaller[] installers = Array.Empty<ServiceInstaller>();

        [Header("Startup")]
        [Tooltip("Scene loaded additively once services are registered. " +
                 "Set to the sandbox while prototyping, to SCN_MainMenu once menus exist.")]
        [SerializeField] private string firstScene = SceneNames.Sandbox;

        [SerializeField] private bool loadFirstSceneOnStart = true;

        private static bool hasBooted;

        private EventBus eventBus;
        private ServiceInstaller[] ordered = Array.Empty<ServiceInstaller>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => hasBooted = false;

        private void Awake()
        {
            // Entering play mode directly from a gameplay scene that also contains a Boot object
            // would otherwise register every service twice.
            if (hasBooted)
            {
                Destroy(gameObject);
                return;
            }

            hasBooted = true;
            DontDestroyOnLoad(gameObject);

            eventBus = new EventBus();

            ServiceLocator.Register<IEventBus>(eventBus);
            ServiceLocator.Register<ISceneLoader>(new SceneLoader());
            ServiceLocator.Register<IGameStateService>(new GameStateMachine(eventBus));
            ServiceLocator.Register(new RunState());

            ordered = installers.Where(i => i != null).OrderBy(i => i.Order).ToArray();

            foreach (var installer in ordered) installer.Install();
        }

        private async void Start()
        {
            ServiceLocator.Get<IGameStateService>().TryChange(GameState.MainMenu);

            if (!loadFirstSceneOnStart || string.IsNullOrEmpty(firstScene)) return;

            await ServiceLocator.Get<ISceneLoader>().LoadAdditiveAsync(firstScene, setActive: true);
        }

        private void OnDestroy()
        {
            if (!hasBooted) return;

            for (var i = ordered.Length - 1; i >= 0; i--) ordered[i].Uninstall();

            eventBus?.Clear();
            ServiceLocator.Clear();
            hasBooted = false;
        }
    }
}
