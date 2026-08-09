using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// What the owner claims it was aiming at. Travels in the swing request and is never
    /// trusted — the server re-resolves every field before anything happens.
    /// </summary>
    /// <remarks>
    /// It exists as a type rather than as two RPC parameters so that a weapon behaviour can
    /// decide what "aim" means for it — a melee swing reports the object under the crosshair,
    /// a projectile will report an origin and a direction — without changing the shape of
    /// <see cref="PlayerAttacker"/>'s request path.
    /// </remarks>
    public struct WeaponAim : INetworkSerializable
    {
        /// <summary>
        /// The candidate the owner's probe found, or <c>default</c> for a swing at nothing.
        /// A reference the server cannot resolve is treated as a miss, not as an error.
        /// </summary>
        public NetworkObjectReference Target;

        /// <summary>
        /// Where the owner saw the impact. Effects only, and only when nothing was actually
        /// hit — it never decides damage or reach.
        /// </summary>
        public Vector3 Point;

        public WeaponAim(NetworkObjectReference target, Vector3 point)
        {
            Target = target;
            Point = point;
        }

        /// <summary>A swing that connected with nothing, ending at <paramref name="point"/>.</summary>
        public static WeaponAim AtNothing(Vector3 point) => new(default, point);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Target);
            serializer.SerializeValue(ref Point);
        }
    }
}
