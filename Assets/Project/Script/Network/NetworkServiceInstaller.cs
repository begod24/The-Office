using Office.Core;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// Registers the networking services with the composition root and republishes NGO's
    /// connection callbacks onto the local event bus, so gameplay never subscribes to
    /// <see cref="NetworkManager"/> directly.
    /// </summary>
    public sealed class NetworkServiceInstaller : ServiceInstaller
    {
        /// <summary>
        /// Assigned in the Boot scene. NetworkManager.Singleton is still null here: the
        /// composition root runs at execution order -10000, which is deliberately earlier than
        /// NetworkManager's own Awake. An explicit same-scene reference is deterministic, and
        /// unlike a cross-scene reference it does not break the scene-ownership rule in
        /// Technical Plan §3.3.
        /// </summary>
        [SerializeField] private NetworkManager networkManager;

        [Tooltip("Spawned by the server once it starts. Must be registered in the network " +
                 "prefab list, or clients cannot resolve it.")]
        [SerializeField] private GameObject sessionPrefab;

        public override int Order => 100;

        private IEventBus bus;
        private MultiplayerSessionService sessionService;
        private LobbyService lobbyService;
        private NetworkObject sessionInstance;

        private NetworkManager Manager =>
            networkManager != null ? networkManager : NetworkManager.Singleton;

        public override void Install()
        {
            sessionService = new MultiplayerSessionService();
            ServiceLocator.Register<ISessionService>(sessionService);

            // Registered while offline and left registered for the whole session. The networked
            // roster binds itself into it on spawn, so the UI never holds a reference to an
            // object that can be despawned out from under it.
            lobbyService = new LobbyService();
            ServiceLocator.Register<ILobbyService>(lobbyService);

            bus = ServiceLocator.Get<IEventBus>();

            var manager = Manager;
            if (manager == null)
            {
                Debug.LogError("[Network] NetworkServiceInstaller has no NetworkManager assigned " +
                               "and none exists yet. Session creation will fail.");
                return;
            }

            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            manager.OnServerStarted += OnServerStarted;
            manager.OnServerStopped += OnServerStopped;
        }

        /// <summary>
        /// The session object is spawned rather than placed in the Boot scene. In-scene placed
        /// NetworkObjects are only resolvable by clients when NGO owns scene management, and it
        /// does not here — see <see cref="SessionRoot"/>.
        /// </summary>
        private void OnServerStarted()
        {
            if (sessionPrefab == null)
            {
                Debug.LogError("[Network] No session prefab assigned. The lobby will not work.");
                return;
            }

            if (sessionInstance != null) return;

            var instance = Instantiate(sessionPrefab);
            DontDestroyOnLoad(instance);

            sessionInstance = instance.GetComponent<NetworkObject>();

            if (sessionInstance == null)
            {
                Debug.LogError("[Network] The session prefab has no NetworkObject.");
                Destroy(instance);
                return;
            }

            sessionInstance.Spawn();
        }

        private void OnServerStopped(bool wasHost)
        {
            if (sessionInstance == null) return;

            if (sessionInstance.IsSpawned) sessionInstance.Despawn();
            else Destroy(sessionInstance.gameObject);

            sessionInstance = null;
        }

        public override void Uninstall()
        {
            var manager = Manager;
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= OnClientConnected;
                manager.OnClientDisconnectCallback -= OnClientDisconnected;
                manager.OnServerStarted -= OnServerStarted;
                manager.OnServerStopped -= OnServerStopped;
            }

            // Fire and forget: the application is tearing down and nothing can await this.
            // The service itself never throws out of LeaveAsync.
            _ = sessionService?.LeaveAsync();

            lobbyService?.Unbind();

            ServiceLocator.Unregister<ILobbyService>();
            ServiceLocator.Unregister<ISessionService>();
        }

        private void OnClientConnected(ulong clientId) =>
            bus?.Publish(new PlayerConnectionChanged(clientId, true));

        private void OnClientDisconnected(ulong clientId) =>
            bus?.Publish(new PlayerConnectionChanged(clientId, false));
    }
}
