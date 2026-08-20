using NUnit.Framework;
using Office.Data;
using Office.Gameplay;

namespace Office.Tests.EditMode
{
    public sealed class WeaponProfileTests
    {
        private static WeaponProfile Profile(float cooldown = 0.5f, int durabilityCost = 1,
            int maxUses = 20) =>
            new(damage: 10f, DamageType.Blunt, cooldown, range: 2.2f, staminaCost: 5f,
                noiseRadius: 8f, durabilityCost, maxUses, ContentDefinition.NoId);

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

        [Test]
        public void Wears_NeedsBothACeilingAndACost()
        {
            Assert.IsTrue(Profile(durabilityCost: 1, maxUses: 20).Wears);
            Assert.IsFalse(Profile(durabilityCost: 0, maxUses: 20).Wears);
            Assert.IsFalse(Profile(durabilityCost: 1, maxUses: 0).Wears);
        }
    }
}
