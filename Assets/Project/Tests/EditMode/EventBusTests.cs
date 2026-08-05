using System;
using NUnit.Framework;
using Office.Core;

namespace Office.Tests.EditMode
{
    public sealed class EventBusTests
    {
        private readonly struct Ping
        {
            public readonly int Value;
            public Ping(int value) => Value = value;
        }

        private readonly struct Pong
        {
        }

        private EventBus bus;

        [SetUp]
        public void SetUp() => bus = new EventBus();

        [Test]
        public void Publish_ReachesSubscriber()
        {
            var received = 0;
            bus.Subscribe<Ping>(e => received = e.Value);

            bus.Publish(new Ping(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => bus.Publish(new Pong()));
        }

        [Test]
        public void Publish_ReachesEverySubscriber()
        {
            var count = 0;
            bus.Subscribe<Ping>(_ => count++);
            bus.Subscribe<Ping>(_ => count++);

            bus.Publish(new Ping(1));

            Assert.AreEqual(2, count);
        }

        [Test]
        public void Publish_DoesNotCrossEventTypes()
        {
            var pings = 0;
            bus.Subscribe<Ping>(_ => pings++);

            bus.Publish(new Pong());

            Assert.AreEqual(0, pings);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var count = 0;
            void Handler(Ping _) => count++;

            bus.Subscribe<Ping>(Handler);
            bus.Publish(new Ping(1));

            bus.Unsubscribe<Ping>(Handler);
            bus.Publish(new Ping(1));

            Assert.AreEqual(1, count, "Handler ran after it was unsubscribed.");
        }

        [Test]
        public void Unsubscribe_RemovesTheEntryWhenLastHandlerLeaves()
        {
            void Handler(Ping _) { }

            bus.Subscribe<Ping>(Handler);
            bus.Unsubscribe<Ping>(Handler);

            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void Unsubscribe_UnknownHandler_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => bus.Unsubscribe<Ping>(_ => { }));
        }

        [Test]
        public void ThrowingSubscriber_DoesNotStopTheOthers()
        {
            var reached = false;

            bus.Subscribe<Ping>(_ => throw new System.InvalidOperationException("expected"));
            bus.Subscribe<Ping>(_ => reached = true);

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            bus.Publish(new Ping(1));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.IsTrue(reached, "A throwing handler swallowed the rest of the invocation list.");
        }

        [Test]
        public void Subscribe_Twice_IsIgnoredAndWarns()
        {
            var count = 0;
            void Handler(Ping _) => count++;

            bus.Subscribe<Ping>(Handler);

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            bus.Subscribe<Ping>(Handler);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            bus.Publish(new Ping(1));

            Assert.AreEqual(1, count,
                "A double subscription made the handler run twice per event.");
        }

        [Test]
        public void Handler_CanUnsubscribeItselfWhileBeingNotified()
        {
            var count = 0;
            Action<Ping> handler = null;

            handler = _ =>
            {
                count++;
                bus.Unsubscribe(handler);
            };

            bus.Subscribe(handler);

            Assert.DoesNotThrow(() => bus.Publish(new Ping(1)));

            bus.Publish(new Ping(1));
            Assert.AreEqual(1, count, "The handler kept receiving events after unsubscribing itself.");
        }

        [Test]
        public void Clear_DropsEverySubscription()
        {
            bus.Subscribe<Ping>(_ => { });
            bus.Clear();

            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }
    }
}
