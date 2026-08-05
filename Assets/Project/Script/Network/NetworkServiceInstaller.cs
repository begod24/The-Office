using Office.Core;
using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    public sealed class NetworkServiceInstaller : ServiceInstaller
    {
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
