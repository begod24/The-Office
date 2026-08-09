using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Office.Gameplay
{
    /// <summary>
    /// Server-side owner of every item lying in the world: fills the run scene from the
    /// level's <see cref="ItemPlacement"/> markers, and spawns dropped items on request.
    /// </summary>
    /// <remarks>
    /// The run-scoped lifecycle — spawn on the InRun edge, despawn when the run ends, recycle
    /// through the pool — is <see cref="RunScopedSpawner{TPlacement}"/>. What is left here is
    /// the part that is actually about items: reading a marker's definition, and the drop path,
    /// which has no marker behind it at all.
    /// </remarks>
    public sealed class WorldItemSpawner : RunScopedSpawner<ItemPlacement>
    {
        [Tooltip("The single networked carrier for every item. Must be registered in the " +
                 "network prefab list, or clients cannot resolve it.")]
        [SerializeField] private GameObject worldItemPrefab;

        /// <summary>The live server instance, or null off the server. Set at network spawn.</summary>
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

        /// <summary>Server only. Returns the spawned carrier, or null when it could not spawn.</summary>
        /// <remarks>
        /// The drop path. It shares everything with a placement spawn except where the position
        /// comes from, which is why it goes through the same create-configure-spawn steps
        /// rather than a second implementation of them.
        /// </remarks>
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

        // Before Spawn, so the contents ride along with the spawn message rather than arriving
        // as a delta a late client could miss.
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
