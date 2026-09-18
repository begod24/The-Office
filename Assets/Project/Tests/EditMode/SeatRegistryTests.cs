using NUnit.Framework;
using Office.Network;

namespace Office.Tests.EditMode
{
    public sealed class SeatRegistryTests
    {
        private const ulong Host = 0;
        private const ulong Second = 1;
        private const ulong Third = 2;

        [SetUp]
        public void SetUp() => SeatRegistry.Clear();

        [TearDown]
        public void TearDown() => SeatRegistry.Clear();

        [Test]
        public void Take_GivesTheHostSeatZero()
        {
            Assert.AreEqual(SeatRegistry.HostSeat, SeatRegistry.Take(Host, true));
        }

        [Test]
        public void Take_IsIdempotentForTheSameClient()
        {
            var first = SeatRegistry.Take(Second, false);
            var again = SeatRegistry.Take(Second, false);

            Assert.AreEqual(first, again);
        }

        [Test]
        public void Take_GivesEveryClientADifferentSeat()
        {
            var host = SeatRegistry.Take(Host, true);
            var second = SeatRegistry.Take(Second, false);
            var third = SeatRegistry.Take(Third, false);

            CollectionAssert.AllItemsAreUnique(new[] { host, second, third });
        }

        [Test]
        public void Take_NeverHandsOutTheHostSeatToAClient()
        {
            SeatRegistry.Take(Host, true);

            Assert.AreNotEqual(SeatRegistry.HostSeat, SeatRegistry.Take(Second, false));
        }

        // The bug this registry exists to stop: the roster used to name from its own length, so
        // the player who joined after a middle seat emptied wore a name someone else still had.
        [Test]
        public void Release_FreesTheSeatWithoutDuplicatingIt()
        {
            SeatRegistry.Take(Host, true);
            var leaver = SeatRegistry.Take(Second, false);
            var stayer = SeatRegistry.Take(Third, false);

            SeatRegistry.Release(Second);

            var joiner = SeatRegistry.Take(4, false);

            Assert.AreEqual(leaver, joiner, "the freed seat should be reused");
            Assert.AreNotEqual(stayer, joiner, "and must not collide with the one still held");
        }

        [Test]
        public void TryGet_IsFalseForAClientThatNeverTookASeat()
        {
            Assert.IsFalse(SeatRegistry.TryGet(Second, out _));
        }

        [Test]
        public void NameForSeat_PadsTheSingleDigits()
        {
            Assert.AreEqual("EMPLOYEE 01", SeatRegistry.NameForSeat(0).ToString());
            Assert.AreEqual("EMPLOYEE 04", SeatRegistry.NameForSeat(3).ToString());
        }

        [Test]
        public void NameForSeat_DoesNotPadDoubleDigits()
        {
            Assert.AreEqual("EMPLOYEE 10", SeatRegistry.NameForSeat(9).ToString());
        }
    }
}
