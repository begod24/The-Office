using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    public interface IDamageable
    {
        bool IsAlive { get; }

        float ApplyDamage(in DamageInfo info);
    }

    public readonly struct DamageInfo
    {
        public readonly float Amount;

        public readonly DamageType Type;

        public readonly ulong SourceClientId;

        public readonly Vector3 Point;

        public readonly Vector3 Direction;

        public const ulong World = ulong.MaxValue;

        public DamageInfo(float amount, DamageType type, ulong sourceClientId,
            Vector3 point, Vector3 direction)
        {
            Amount = amount;
            Type = type;
            SourceClientId = sourceClientId;
            Point = point;
            Direction = direction;
        }

        public bool IsFromWorld => SourceClientId == World;

        public override string ToString() => $"{Amount:0.#} {Type} from {SourceClientId}";
    }
}
