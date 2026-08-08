using NUnit.Framework;
using Office.Network;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// The handshake gates every join, so its hash has to be boring and predictable.
    /// </summary>
    /// <remarks>
    /// A hash that varies for any reason other than content would reject two machines running
    /// the identical build — a worse failure than the desync it exists to prevent. That is why
    /// the implementation writes FNV-1a out by hand instead of calling
    /// <c>string.GetHashCode</c>, which is not stable across runtimes and, on some, not even
    /// across processes. These cases pin that down.
    /// </remarks>
    public sealed class ConnectionHandshakeTests
    {
        [Test]
        public void SameInput_GivesTheSameHash()
        {
            Assert.AreEqual(
                ConnectionHandshake.Fnv1a("1:ITM_Stapler;2:ITM_Keycard;"),
                ConnectionHandshake.Fnv1a("1:ITM_Stapler;2:ITM_Keycard;"));
        }

        [Test]
        public void KnownVectors_MatchTheFnv1aSpecification()
        {
            // From the reference test vectors. If these drift, the algorithm changed and every
            // client on an older build stops being able to join.
            Assert.AreEqual(2166136261u, ConnectionHandshake.Fnv1a(string.Empty));
            Assert.AreEqual(0xE40C292Cu, ConnectionHandshake.Fnv1a("a"));
            Assert.AreEqual(0xBF9CF968u, ConnectionHandshake.Fnv1a("foobar"));
        }

        [Test]
        public void RenamedDefinition_ChangesTheHash()
        {
            Assert.AreNotEqual(
                ConnectionHandshake.Fnv1a("1:ITM_Stapler;"),
                ConnectionHandshake.Fnv1a("1:ITM_Staplr;"));
        }

        [Test]
        public void ReassignedId_ChangesTheHash()
        {
            Assert.AreNotEqual(
                ConnectionHandshake.Fnv1a("1:ITM_Stapler;2:ITM_Keycard;"),
                ConnectionHandshake.Fnv1a("2:ITM_Stapler;1:ITM_Keycard;"));
        }

        [Test]
        public void AddedDefinition_ChangesTheHash()
        {
            Assert.AreNotEqual(
                ConnectionHandshake.Fnv1a("1:ITM_Stapler;"),
                ConnectionHandshake.Fnv1a("1:ITM_Stapler;2:ITM_Keycard;"));
        }

        [Test]
        public void NullContent_IsStableRatherThanThrowing()
        {
            Assert.AreEqual(
                ConnectionHandshake.ContentFingerprint(null),
                ConnectionHandshake.ContentFingerprint(null));
        }

        [Test]
        public void Build_CarriesBothTheVersionAndTheFingerprint()
        {
            var handshake = ConnectionHandshake.Build("1.2.3", null);

            StringAssert.StartsWith("1.2.3|", handshake);
            Assert.AreEqual($"1.2.3|{ConnectionHandshake.ContentFingerprint(null):X8}", handshake);
        }

        [Test]
        public void Build_DiffersBetweenVersions()
        {
            Assert.AreNotEqual(
                ConnectionHandshake.Build("1.2.3", null),
                ConnectionHandshake.Build("1.2.4", null));
        }
    }
}
