using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    public struct WeaponAim : INetworkSerializable
    {
        public NetworkObjectReference Target;

        public Vector3 Point;

        public WeaponAim(NetworkObjectReference target, Vector3 point)
        {
            Target = target;
            Point = point;
        }

        public static WeaponAim AtNothing(Vector3 point) => new(default, point);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Target);
            serializer.SerializeValue(ref Point);
        }
    }
}
