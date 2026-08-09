using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// Reuses networked instances instead of creating and destroying them.
    /// </summary>
    /// <remarks>
    /// The point is not memory, it is the frame this saves. GDD §9.1 is built on swarms —
    /// the stapler is a "fast melee swarm", the extension cord is "fast, low HP, swarm", the
    /// copier "spawns weak duplicates" — and a printer that "fires paper shards" is a
    /// projectile every few frames. Instantiating those at the moment they appear puts a GC
    /// spike exactly where frame time matters most.
    /// <para>
    /// Server code calls <see cref="Acquire"/>. Clients never do: NGO calls the registered
    /// <see cref="INetworkPrefabInstanceHandler"/> for them when a spawn message arrives.
    /// Both paths end in the same queue.
    /// </para>
    /// </remarks>
    public interface INetworkObjectPool
    {
        /// <summary>
        /// Server only. A ready instance of <paramref name="prefab"/>, from the pool when one
        /// is spare. Still needs <see cref="NetworkObject.Spawn"/> by the caller, so that
        /// initial state can be written before the spawn message goes out.
        /// </summary>
        NetworkObject Acquire(GameObject prefab, Vector3 position, Quaternion rotation);

        /// <summary>Whether this prefab goes through the pool at all.</summary>
        bool IsPooled(GameObject prefab);
    }
}
