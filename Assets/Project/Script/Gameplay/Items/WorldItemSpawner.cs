using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Office.Gameplay
{
    public sealed class WorldItemSpawner : RunScopedSpawner<ItemPlacement>
    {
        [Tooltip("The single networked carrier for every item. Must be registered in the " +
                 "network prefab list, or clients cannot resolve it.")]
        [SerializeField] private GameObject worldItemPrefab;

        public static WorldItemSpawner Server { get; private set; }

        protected override GameObject Prefab => worldItemPrefab;

        protected override IReadOnlyList<ItemPlacement> Placements => ItemPlacement.All;

        protected override string LogCategory => "Item";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Server = null;

        public override void OnNetworkSpawn()
        {
            if (IsServer) Server = this;
        }

        public override void OnNetworkDespawn()
        {
            if (ReferenceEquals(Server, this)) Server = null;

            base.OnNetworkDespawn();
        }

        protected override bool TryConfigure(NetworkObject instance, ItemPlacement placement)
        {
            if (placement.Definition == null)
            {
                Debug.LogWarning($"[Item] Placement '{placement.name}' has no definition. Skipped.",
                    placement);
                return false;
            }

            return TryInitialise(instance,
                new ItemStack(placement.Definition.Id, placement.Count));
        }

        public NetworkObject ServerSpawn(ItemStack contents, Vector3 position, Quaternion rotation)
        {
            if (!IsServer || contents.IsEmpty) return null;

            var instance = ServerCreate(position, rotation);
            if (instance == null) return null;

            if (!TryInitialise(instance, contents))
            {
                ReleaseUnspawned(instance);
                return null;
            }

            instance.Spawn();
            Track(instance);
            return instance;
        }

        private static bool TryInitialise(NetworkObject instance, ItemStack contents)
        {
            var item = instance.GetComponent<WorldItem>();

            if (item == null)
            {
                Debug.LogError("[Item] PF_WorldItem is missing its WorldItem component.", instance);
                return false;
            }

            item.ServerInitialise(contents);
            return true;
        }
    }
}
