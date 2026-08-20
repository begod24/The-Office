using UnityEngine;

namespace Office.Gameplay
{
    public static class Vitals
    {
        public static VitalsState Spawn(float maxHealth) =>
            new(Mathf.Max(0f, maxHealth), 0f, false);

        public static VitalsState ApplyDamage(in VitalsState current, float amount,
            float bleedOutSeconds)
        {
            if (!current.IsStanding || amount <= 0f) return current;

            var health = current.Health - amount;

            if (health > 0f) return new VitalsState(health, 0f, false);

            return new VitalsState(0f, Mathf.Max(0f, bleedOutSeconds), false);
        }

        public static VitalsState Tick(in VitalsState current, float deltaSeconds)
        {
            if (!current.IsDowned || deltaSeconds <= 0f) return current;

            var remaining = current.BleedOutRemaining - deltaSeconds;

            return remaining > 0f
                ? new VitalsState(0f, remaining, false)
                : new VitalsState(0f, 0f, true);
        }

        public static VitalsState Revive(in VitalsState current, float health)
        {
            if (!current.IsDowned) return current;

            return new VitalsState(Mathf.Max(1f, health), 0f, false);
        }

        public static VitalsState Heal(in VitalsState current, float amount, float maxHealth)
        {
            if (!current.IsStanding || amount <= 0f) return current;

            return new VitalsState(Mathf.Min(current.Health + amount, maxHealth), 0f, false);
        }

        public static VitalsState Kill(in VitalsState current) =>
            current.IsDead ? current : new VitalsState(0f, 0f, true);
    }
}
