using NUnit.Framework;
using Office.Data;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// Pins down what a mixed-type weapon does to a resistant target.
    /// </summary>
    /// <remarks>
    /// GDD §9.2 makes "digital entities are immune to physical weapons" the central lesson
    /// of the game, and GDD §8.3 hands the player weapons that are two things at once. Those
    /// two only coexist if the matching rule is exact, and it is the kind of rule that gets
    /// quietly changed by someone who thinks averaging looks fairer.
    /// </remarks>
    public sealed class DamageResponseTests
    {
        // The digital class: cannot be hit with a stick, folds to light.
        private static DamageResponseTable Digital() => new(
            new DamageResponse(DamageType.Blunt | DamageType.Cutting, 0f),
            new DamageResponse(DamageType.Light, 2.5f));

        [Test]
        public void EmptyTable_LeavesDamageAlone()
        {
            var table = new DamageResponseTable();

            Assert.AreEqual(1f, table.MultiplierFor(DamageType.Blunt));
            Assert.AreEqual(10f, table.Resolve(10f, DamageType.Blunt));
        }

        [Test]
        public void UnlistedType_IsNeutral()
        {
            Assert.AreEqual(1f, Digital().MultiplierFor(DamageType.Water));
        }

        [Test]
        public void ListedType_UsesItsMultiplier()
        {
            Assert.AreEqual(2.5f, Digital().MultiplierFor(DamageType.Light));
        }

        [Test]
        public void Immunity_ZeroesTheDamage()
        {
            Assert.AreEqual(0f, Digital().Resolve(100f, DamageType.Blunt));
        }

        [Test]
        public void OneRow_CoversEveryFlagItLists()
        {
            Assert.AreEqual(0f, Digital().MultiplierFor(DamageType.Cutting),
                "Cutting shares a row with Blunt and must be covered by it.");
        }

        [Test]
        public void MixedWeapon_UsesItsStrongestMatchingRow()
        {
            // The laser pointer: Blunt because it is a solid object, Light because of the beam.
            var multiplier = Digital().MultiplierFor(DamageType.Blunt | DamageType.Light);

            Assert.AreEqual(2.5f, multiplier,
                "Immunity to being hit with a stick cancelled the weakness to light. A laser " +
                "pointer has to work on a digital enemy.");
        }

        [Test]
        public void MixedWeapon_DoesNotLaunderItsWayPastAnImmunity()
        {
            // The wet mop: Blunt and Water. Water is not a listed weakness of this target,
            // so it must contribute nothing rather than dragging the result back to neutral.
            var multiplier = Digital().MultiplierFor(DamageType.Blunt | DamageType.Water);

            Assert.AreEqual(0f, multiplier,
                "Adding an unrelated damage type let a weapon chip a target it is immune to.");
        }

        [Test]
        public void NoneType_IsNeutral()
        {
            Assert.AreEqual(1f, Digital().MultiplierFor(DamageType.None));
        }

        [Test]
        public void Resolve_NeverReturnsNegativeDamage()
        {
            var table = new DamageResponseTable(new DamageResponse(DamageType.Blunt, 2f));

            Assert.AreEqual(0f, table.Resolve(-5f, DamageType.Blunt));
        }
    }
}
