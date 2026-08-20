using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    public interface INetworkObjectPool
    {
        NetworkObject Acquire(GameObject prefab, Vector3 position, Quaternion rotation);

        bool IsPooled(GameObject prefab);
    }
}
