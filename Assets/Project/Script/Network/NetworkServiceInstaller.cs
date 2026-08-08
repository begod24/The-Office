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

        [Header("Pooling")]
        [Tooltip("Prefabs that are reused instead of being created and destroyed. Must also " +
                 "appear in the network prefab list — pooling replaces how an instance is " +
                 "obtained, not how it is registered.")]
        [SerializeField] private PooledPrefab[] pooledPrefabs = System.Array.Empty<PooledPrefab>();

        public override int Order => 100;

        private IEventBus bus;
        private MultiplayerSessionService sessionService;
        private LobbyService lobbyService;
        private NetworkObject sessionInstance;
        private NetworkObjectPool pool;

        [System.Serializable]
        public struct PooledPrefab
        {
            public GameObject Prefab;

            [Tooltip("Instances built up front, before anyone can see the hitch. Set it to " +
                     "the number you expect on screen at once, not the total over a run.")]
            [Min(0)]
            public int Prewarm;
        }

        private NetworkManager Manager =>
            networkManager != null ? networkManager : NetworkManager.Singleton;

        public override void Install()
        {
            sessionService = new MultiplayerSessionService();
            ServiceLocator.Register<ISessionService>(sessionService);

            lobbyService = new LobbyService();
            ServiceLocator.Register<ILobbyService>(lobbyService);

            pool = new NetworkObjectPool();
            ServiceLocator.Register<INetworkObjectPool>(pool);

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
            manager.OnClientStarted += OnClientStarted;
            manager.OnClientStopped += OnClientStopped;
        }

        // NetworkManager.PrefabHandler does not exist until the manager initialises, which is
        // why this cannot happen in Install. Both callbacks fire on a host, and registration
        // is idempotent for exactly that reason.
        private void RegisterPooledPrefabs()
        {
            var manager = Manager;
            if (manager == null || pool == null) return;

            foreach (var entry in pooledPrefabs)
                pool.Register(manager, entry.Prefab, entry.Prewarm);
        }

        private void OnClientStarted() => RegisterPooledPrefabs();

        // Not the disconnect handling the design calls for — that still has to arrive. This
        // only stops the pool outliving the session it registered against.
        private void OnClientStopped(bool wasHost) => pool?.Clear();

        private void OnServerStarted()
        {
            RegisterPooledPrefabs();

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
            pool?.Clear();

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
                manager.OnClientStarted -= OnClientStarted;
                manager.OnClientStopped -= OnClientStopped;
            }

            _ = sessionService?.LeaveAsync();

            lobbyService?.Unbind();

            pool?.Clear();
            pool = null;

            ServiceLocator.Unregister<INetworkObjectPool>();
            ServiceLocator.Unregister<ILobbyService>();
            ServiceLocator.Unregister<ISessionService>();
        }

        private void OnClientConnected(ulong clientId) =>
            bus?.Publish(new PlayerConnectionChanged(clientId, true));

        private void OnClientDisconnected(ulong clientId) =>
            bus?.Publish(new PlayerConnectionChanged(clientId, false));
    }
}
