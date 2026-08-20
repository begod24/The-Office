using NUnit.Framework;
using Office.Network;

namespace Office.Tests.EditMode
{
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
