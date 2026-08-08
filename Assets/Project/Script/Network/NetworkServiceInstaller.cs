using System.Text;
using Office.Core;
using Office.Data;
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

            ConfigureConnectionApproval(manager);

            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            manager.OnServerStarted += OnServerStarted;
            manager.OnServerStopped += OnServerStopped;
            manager.OnClientStarted += OnClientStarted;
            manager.OnClientStopped += OnClientStopped;
        }

        /// <summary>
        /// Turns on approval and fixes both sides of it, here at boot.
        /// </summary>
        /// <remarks>
        /// Both the payload a client presents and the value a server expects come from
        /// <see cref="ConnectionHandshake.Build"/>, so an identical build always agrees with
        /// itself. It has to happen before anything connects, and the Multiplayer Services SDK
        /// starts the NetworkManager from inside its own create-or-join call — by then it is
        /// too late.
        /// <para>
        /// The flag is also written into the scene by the boot builder. Setting it again here
        /// costs nothing and means a boot scene that was not regenerated fails closed rather
        /// than silently accepting anyone.
        /// </para>
        /// </remarks>
        private void ConfigureConnectionApproval(NetworkManager manager)
        {
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(ConnectionHandshake.Build());
            manager.ConnectionApprovalCallback = OnConnectionApproval;
        }

        private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var presented = request.Payload != null && request.Payload.Length > 0
                ? Encoding.UTF8.GetString(request.Payload)
                : string.Empty;

            var expected = ConnectionHandshake.Build();

            response.Approved = presented == expected;

            // Players are spawned by PlayerSpawner when the run starts, not by NGO on
            // connection — the lobby has no bodies in it.
            response.CreatePlayerObject = false;
            response.Reason = response.Approved ? string.Empty : ConnectionHandshake.MismatchReason;

            if (response.Approved) return;

            Debug.LogWarning($"[Network] Rejected a client. Expected '{expected}', got " +
                             $"'{presented}'. Both sides need the same build and the same " +
                             "REG_Definitions.");
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

        /// <summary>
        /// The local client's connection ended — cleanly, or because the host went away.
        /// </summary>
        /// <remarks>
        /// GDD §15: a host disconnect ends the session for everyone and returns them to the
        /// menu. Without this the client sits in the run scene with a dead session, no
        /// message and no way out: the session object is despawned, so nothing that lives on
        /// it can react — which is exactly why this handler is in the boot scene.
        /// <para>
        /// The host is skipped. It stopped its own server, and <see cref="OnServerStopped"/>
        /// has already run for it.
        /// </para>
        /// </remarks>
        private void OnClientStopped(bool wasHost)
        {
            pool?.Clear();

            if (wasHost) return;

            var manager = Manager;
            var reason = manager != null ? manager.DisconnectReason : string.Empty;

            Debug.Log(string.IsNullOrEmpty(reason)
                ? "[Network] Connection lost. Returning to the main menu."
                : $"[Network] Connection lost: {reason}");

            if (ServiceLocator.TryGet<IGameStateService>(out var state))
                state.SetFromAuthority(GameState.MainMenu);

            // Whatever is loaded goes, because which run scene it was depends on the level.
            // Boot survives — it is the composition root and never unloads.
            if (ServiceLocator.TryGet<ISceneLoader>(out var loader))
                _ = loader.ReturnToAsync(SceneNames.MainMenu, SceneNames.Boot);
        }

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

                // The callback holds a reference to this installer; a torn-down composition
                // root must not stay reachable from the NetworkManager.
                manager.ConnectionApprovalCallback = null;
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
