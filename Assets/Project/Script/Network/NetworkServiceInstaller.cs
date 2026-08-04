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

        public override int Order => 100;

        private IEventBus bus;
        private MultiplayerSessionService sessionService;

        private NetworkManager Manager =>
            networkManager != null ? networkManager : NetworkManager.Singleton;

        public override void Install()
        {
            sessionService = new MultiplayerSessionService();
            ServiceLocator.Register<ISessionService>(sessionService);

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
        }

        public override void Uninstall()
        {
            var manager = Manager;
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= OnClientConnected;
                manager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            // Fire and forget: the application is tearing down and nothing can await this.
            // The service itself never throws out of LeaveAsync.
            _ = sessionService?.LeaveAsync();

            ServiceLocator.Unregister<ISessionService>();
        }

        private void OnClientConnected(ulong clientId) =>
            bus?.Publish(new PlayerConnectionChanged(clientId, true));

        private void OnClientDisconnected(ulong clientId) =>
            bus?.Publish(new PlayerConnectionChanged(clientId, false));
    }
}
