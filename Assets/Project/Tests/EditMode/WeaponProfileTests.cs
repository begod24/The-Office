using NUnit.Framework;
using Office.Data;
using Office.Gameplay;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// The guards a weapon's numbers carry regardless of what an asset says.
    /// </summary>
    /// <remarks>
    /// A profile is built independently on the owner and on the server, and both have to reach
    /// the same answer or an honest client loses swings it should have had. Clamping inside the
    /// constructor rather than at the call sites is what makes that true by construction.
    /// </remarks>
    public sealed class WeaponProfileTests
    {
        private static WeaponProfile Profile(float cooldown = 0.5f, int durabilityCost = 1,
            int maxUses = 20) =>
            new(damage: 10f, DamageType.Blunt, cooldown, range: 2.2f, staminaCost: 5f,
                noiseRadius: 8f, durabilityCost, maxUses, ContentDefinition.NoId);

        // A zero cooldown is a weapon that fires every frame — on the server too, where the
        // rate check is the only thing standing between a modified client and infinite damage.
        [Test]
        public void Cooldown_IsNeverZero()
        {
            Assert.Greater(Profile(cooldown: 0f).Cooldown, 0f);
            Assert.Greater(Profile(cooldown: -3f).Cooldown, 0f);
        }

        [Test]
        public void Cooldown_IsLeftAloneWhenItIsSane()
        {
            Assert.AreEqual(0.75f, Profile(cooldown: 0.75f).Cooldown);
        }

        [Test]
        public void NegativeAuthoredValues_AreFlooredAtZero()
        {
            var profile = new WeaponProfile(damage: 10f, DamageType.Blunt, cooldown: 0.5f,
                range: -4f, staminaCost: -5f, noiseRadius: -1f, durabilityCost: -2, maxUses: -10,
                ContentDefinition.NoId);

            Assert.AreEqual(0f, profile.StaminaCost);
            Assert.AreEqual(0f, profile.NoiseRadius);
            Assert.AreEqual(0, profile.DurabilityCost);
            Assert.AreEqual(0, profile.MaxUses);
            Assert.Greater(profile.Range, 0f, "A zero range weapon can never connect with anything.");
        }

        // Both halves are needed. A cost with no ceiling wears towards a limit that does not
        // exist; a ceiling with no cost never gets closer to it. Either one alone is a content
        // mistake that must not silently break or preserve a weapon.
        [Test]
        public void Wears_NeedsBothACeilingAndACost()
        {
            Assert.IsTrue(Profile(durabilityCost: 1, maxUses: 20).Wears);
            Assert.IsFalse(Profile(durabilityCost: 0, maxUses: 20).Wears);
            Assert.IsFalse(Profile(durabilityCost: 1, maxUses: 0).Wears);
        }
    }
}
