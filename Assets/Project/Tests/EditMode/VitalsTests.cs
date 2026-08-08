using NUnit.Framework;
using Office.Data;
using Office.Gameplay;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// The rules behind dying, kept honest without a running session.
    /// </summary>
    /// <remarks>
    /// These are the transitions GDD §15 specifies — zero health is downed rather than dead,
    /// a teammate has sixty seconds — and getting one of them wrong shows up as a player who
    /// cannot be revived or one who never dies. Both are far cheaper to catch here than in a
    /// four-player test.
    /// </remarks>
    public sealed class VitalsTests
    {
        private const float Max = GameplayConstants.MaxPlayerHealth;
        private const float BleedOut = GameplayConstants.BleedOutSeconds;

        private static VitalsState Downed() =>
            Vitals.ApplyDamage(Vitals.Spawn(Max), Max, BleedOut);

        [Test]
        public void Spawn_IsStandingAtFullHealth()
        {
            var state = Vitals.Spawn(Max);

            Assert.AreEqual(Max, state.Health);
            Assert.IsTrue(state.IsStanding);
            Assert.IsFalse(state.IsDowned);
            Assert.IsFalse(state.IsDead);
        }

        [Test]
        public void Damage_BelowHealth_LeavesThePlayerStanding()
        {
            var state = Vitals.ApplyDamage(Vitals.Spawn(Max), 30f, BleedOut);

            Assert.AreEqual(Max - 30f, state.Health);
            Assert.IsTrue(state.IsStanding);
        }

        [Test]
        public void Damage_ToZero_DownsRatherThanKills()
        {
            var state = Downed();

            Assert.IsTrue(state.IsDowned, "Zero health killed outright. GDD §7.1 says downed.");
            Assert.IsFalse(state.IsDead);
            Assert.IsTrue(state.IsAlive, "A downed player is still savable and must read as alive.");
            Assert.AreEqual(BleedOut, state.BleedOutRemaining);
        }

        [Test]
        public void Damage_Overkill_DoesNotShortenTheReviveWindow()
        {
            var state = Vitals.ApplyDamage(Vitals.Spawn(Max), Max * 10f, BleedOut);

            Assert.IsTrue(state.IsDowned);
            Assert.AreEqual(BleedOut, state.BleedOutRemaining,
                "Overkill leaked into the bleed-out timer.");
        }

        [Test]
        public void Damage_WhileDowned_ChangesNothing()
        {
            var downed = Downed();
            var after = Vitals.ApplyDamage(downed, 50f, BleedOut);

            Assert.AreEqual(downed, after,
                "Hitting a downed player altered their state — the revive window is meant to " +
                "be a flat timer.");
        }

        [Test]
        public void Tick_CountsTheReviveWindowDown()
        {
            var state = Vitals.Tick(Downed(), 10f);

            Assert.IsTrue(state.IsDowned);
            Assert.AreEqual(BleedOut - 10f, state.BleedOutRemaining, 0.001f);
        }

        [Test]
        public void Tick_PastTheWindow_Kills()
        {
            var state = Vitals.Tick(Downed(), BleedOut + 0.1f);

            Assert.IsTrue(state.IsDead);
            Assert.IsFalse(state.IsAlive);
            Assert.IsFalse(state.IsDowned, "A dead player must not still read as revivable.");
        }

        [Test]
        public void Tick_WhileStanding_ChangesNothing()
        {
            var standing = Vitals.Spawn(Max);

            Assert.AreEqual(standing, Vitals.Tick(standing, 5f));
        }

        [Test]
        public void Revive_BringsADownedPlayerBackUp()
        {
            var state = Vitals.Revive(Downed(), GameplayConstants.ReviveHealth);

            Assert.IsTrue(state.IsStanding);
            Assert.AreEqual(GameplayConstants.ReviveHealth, state.Health);
            Assert.AreEqual(0f, state.BleedOutRemaining);
        }

        [Test]
        public void Revive_DoesNotWorkOnTheDead()
        {
            var dead = Vitals.Tick(Downed(), BleedOut);
            var after = Vitals.Revive(dead, GameplayConstants.ReviveHealth);

            Assert.AreEqual(dead, after, "The dead are spectators by GDD §15, not revivable.");
        }

        [Test]
        public void Revive_OnAStandingPlayer_IsNotASilentHeal()
        {
            var hurt = Vitals.ApplyDamage(Vitals.Spawn(Max), 40f, BleedOut);

            Assert.AreEqual(hurt, Vitals.Revive(hurt, Max));
        }

        [Test]
        public void Heal_ClampsToMaximum()
        {
            var hurt = Vitals.ApplyDamage(Vitals.Spawn(Max), 40f, BleedOut);
            var healed = Vitals.Heal(hurt, 1000f, Max);

            Assert.AreEqual(Max, healed.Health);
        }

        [Test]
        public void Heal_DoesNotSubstituteForARevive()
        {
            var downed = Downed();

            Assert.AreEqual(downed, Vitals.Heal(downed, 50f, Max),
                "Healing stood a downed player up without anyone reviving them.");
        }

        [Test]
        public void Kill_SkipsTheDownedState()
        {
            var state = Vitals.Kill(Vitals.Spawn(Max));

            Assert.IsTrue(state.IsDead);
            Assert.IsFalse(state.IsDowned);
        }
    }
}
