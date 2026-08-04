using System;
using NUnit.Framework;
using Office.Core;

namespace Office.Tests.EditMode
{
    public sealed class ServiceLocatorTests
    {
        private interface IThing
        {
            int Id { get; }
        }

        private sealed class Thing : IThing
        {
            public Thing(int id) => Id = id;
            public int Id { get; }
        }

        [SetUp]
        public void SetUp() => ServiceLocator.Clear();

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        [Test]
        public void Get_ReturnsTheRegisteredInstance()
        {
            var thing = new Thing(7);
            ServiceLocator.Register<IThing>(thing);

            Assert.AreSame(thing, ServiceLocator.Get<IThing>());
        }

        [Test]
        public void Get_Unregistered_ThrowsWithAUsefulMessage()
        {
            var e = Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IThing>());
            StringAssert.Contains(nameof(IThing), e.Message);
        }

        [Test]
        public void TryGet_Unregistered_ReturnsFalseAndDoesNotThrow()
        {
            Assert.IsFalse(ServiceLocator.TryGet<IThing>(out var service));
            Assert.IsNull(service);
        }

        [Test]
        public void Register_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ServiceLocator.Register<IThing>(null));
        }

        [Test]
        public void Register_RegistersAgainstTheInterfaceNotTheConcreteType()
        {
            ServiceLocator.Register<IThing>(new Thing(1));

            // A test may substitute a fake precisely because registration is by interface.
            Assert.IsTrue(ServiceLocator.IsRegistered<IThing>());
            Assert.IsFalse(ServiceLocator.IsRegistered<Thing>());
        }

        [Test]
        public void Unregister_RemovesTheService()
        {
            ServiceLocator.Register<IThing>(new Thing(1));
            ServiceLocator.Unregister<IThing>();

            Assert.IsFalse(ServiceLocator.IsRegistered<IThing>());
        }

        [Test]
        public void Register_Twice_KeepsTheLatest()
        {
            ServiceLocator.Register<IThing>(new Thing(1));

            // The locator warns about this; the warning must not fail the test.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            ServiceLocator.Register<IThing>(new Thing(2));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(2, ServiceLocator.Get<IThing>().Id);
        }
    }
}
