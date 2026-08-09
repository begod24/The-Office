using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// The arithmetic behind taking damage, bleeding out and being revived, kept free of NGO
    /// so it can be tested without a running session.
    /// </summary>
    /// <remarks>
    /// Same split as <see cref="ItemStacking"/>: <see cref="Health"/> owns replication and
    /// authority, this owns the rules. Every method returns a new state instead of mutating,
    /// so a caller cannot half-apply a transition.
    /// </remarks>
    public static class Vitals
    {
        /// <summary>A fresh, undamaged state.</summary>
        public static VitalsState Spawn(float maxHealth) =>
            new(Mathf.Max(0f, maxHealth), 0f, false);

        /// <summary>
        /// Applies already-resolved damage — resistances belong to the caller, via
        /// <see cref="Office.Data.DamageResponseTable"/>.
        /// </summary>
        /// <remarks>
        /// <b>Damage to a downed player does nothing.</b> They are at zero already and on a
        /// timer, so the only thing extra hits could do is shorten a window GDD §15 defines
        /// as a flat 60 seconds. It also removes the incentive to stand over a body, which
        /// is not the tension this game is going for. GDD leaves this open (§16, question 5);
        /// change it here and the tests will tell you what else moves.
        /// </remarks>
        public static VitalsState ApplyDamage(in VitalsState current, float amount,
            float bleedOutSeconds)
        {
            if (!current.IsStanding || amount <= 0f) return current;

            var health = current.Health - amount;

            if (health > 0f) return new VitalsState(health, 0f, false);

            // Overkill is discarded rather than carried into the bleed-out timer: a downed
            // player is downed, however hard they were hit.
            return new VitalsState(0f, Mathf.Max(0f, bleedOutSeconds), false);
        }

        /// <summary>
        /// Advances the bleed-out clock. Server only in practice, and a no-op for anyone
        /// standing or already dead — so it is safe to call every frame for every player.
        /// </summary>
        public static VitalsState Tick(in VitalsState current, float deltaSeconds)
        {
            if (!current.IsDowned || deltaSeconds <= 0f) return current;

            var remaining = current.BleedOutRemaining - deltaSeconds;

            return remaining > 0f
                ? new VitalsState(0f, remaining, false)
                : new VitalsState(0f, 0f, true);
        }

        /// <summary>
        /// Brings a downed player back up with a fraction of their health.
        /// </summary>
        /// <remarks>
        /// Only the downed can be revived: the dead are spectators by GDD §15, and reviving
        /// someone who never went down would be a silent heal.
        /// </remarks>
        public static VitalsState Revive(in VitalsState current, float health)
        {
            if (!current.IsDowned) return current;

            return new VitalsState(Mathf.Max(1f, health), 0f, false);
        }

        /// <summary>
        /// Heals someone standing. GDD §7.1 has no regeneration, so nothing calls this on a
        /// timer — it is the first aid kit, and it cannot substitute for a revive.
        /// </summary>
        public static VitalsState Heal(in VitalsState current, float amount, float maxHealth)
        {
            if (!current.IsStanding || amount <= 0f) return current;

            return new VitalsState(Mathf.Min(current.Health + amount, maxHealth), 0f, false);
        }

        /// <summary>Kills outright, skipping the downed state. Falls, crushes, story beats.</summary>
        public static VitalsState Kill(in VitalsState current) =>
            current.IsDead ? current : new VitalsState(0f, 0f, true);
    }
}
