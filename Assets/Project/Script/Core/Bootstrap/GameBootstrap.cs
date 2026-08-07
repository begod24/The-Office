using System;
using System.Linq;
using Office.Data;
using UnityEngine;

namespace Office.Core
{
    [DefaultExecutionOrder(-10000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Installers")]
        [Tooltip("One per assembly that owns services. Executed in ascending Order, " +
                 "torn down in reverse.")]
        [SerializeField] private ServiceInstaller[] installers = Array.Empty<ServiceInstaller>();

        [Header("Content")]
        [Tooltip("Turns network ids back into authored definitions. Without it no item or " +
                 "prop can resolve on either machine.")]
        [SerializeField] private DefinitionRegistry definitions;

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

            if (definitions != null)
            {
                // A registry cached from a previous play session would still hold ids that
                // were reassigned in between.
                definitions.Invalidate();
                ServiceLocator.Register(definitions);
            }
            else
            {
                Debug.LogError("[Bootstrap] No DefinitionRegistry assigned. Items and props " +
                               "will not resolve — run 'Office/Content/Rebuild Definition " +
                               "Registry', then 'Office/Setup/Build Boot Scene'.");
            }

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
