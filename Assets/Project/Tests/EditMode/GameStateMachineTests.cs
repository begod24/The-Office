using NUnit.Framework;
using Office.Core;
using Office.Data;

namespace Office.Tests.EditMode
{
    public sealed class GameStateMachineTests
    {
        private EventBus bus;
        private GameStateMachine machine;

        [SetUp]
        public void SetUp()
        {
            bus = new EventBus();
            machine = new GameStateMachine(bus);
        }

        [Test]
        public void StartsInBoot()
        {
            Assert.AreEqual(GameState.Boot, machine.Current);
        }

        [Test]
        public void LegalTransition_Applies()
        {
            Assert.IsTrue(machine.TryChange(GameState.MainMenu));
            Assert.AreEqual(GameState.MainMenu, machine.Current);
        }

        [Test]
        public void IllegalTransition_IsRejectedAndStateIsUnchanged()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var result = machine.TryChange(GameState.InRun);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(result);
            Assert.AreEqual(GameState.Boot, machine.Current,
                "An illegal transition corrupted the phase instead of being rejected.");
        }

        [Test]
        public void TransitionToSameState_IsANoOpAndSucceeds()
        {
            var raised = 0;
            machine.Changed += (_, _) => raised++;

            Assert.IsTrue(machine.TryChange(GameState.Boot));
            Assert.AreEqual(0, raised, "A no-op transition raised a change event.");
        }

        [Test]
        public void Transition_RaisesTheEventWithPreviousAndCurrent()
        {
            GameState from = default, to = default;
            machine.Changed += (previous, current) =>
            {
                from = previous;
                to = current;
            };

            machine.TryChange(GameState.MainMenu);

            Assert.AreEqual(GameState.Boot, from);
            Assert.AreEqual(GameState.MainMenu, to);
        }

        [Test]
        public void Transition_PublishesOnTheEventBus()
        {
            var published = false;
            bus.Subscribe<GameStateChanged>(e =>
                published = e.Previous == GameState.Boot && e.Current == GameState.MainMenu);

            machine.TryChange(GameState.MainMenu);

            Assert.IsTrue(published);
        }

        [Test]
        public void SetFromAuthority_AppliesATransitionThatWouldBeIllegalToRequest()
        {
            machine.TryChange(GameState.MainMenu);

            machine.SetFromAuthority(GameState.InRun);

            Assert.AreEqual(GameState.InRun, machine.Current);
        }

        [Test]
        public void SetFromAuthority_RaisesTheSameEventsAsALocalTransition()
        {
            var raised = 0;
            var published = 0;

            machine.Changed += (_, _) => raised++;
            bus.Subscribe<GameStateChanged>(_ => published++);

            machine.SetFromAuthority(GameState.InRun);

            Assert.AreEqual(1, raised, "Listeners must not care where the transition came from.");
            Assert.AreEqual(1, published);
        }

        [Test]
        public void SetFromAuthority_ToTheSameState_IsANoOp()
        {
            var raised = 0;
            machine.Changed += (_, _) => raised++;

            machine.SetFromAuthority(GameState.Boot);

            Assert.AreEqual(0, raised);
        }

        [Test]
        public void FullHappyPath_IsLegalEndToEnd()
        {
            var path = new[]
            {
                GameState.MainMenu, GameState.Lobby, GameState.Generating, GameState.InRun,
                GameState.RunComplete, GameState.Lobby
            };

            foreach (var state in path)
                Assert.IsTrue(machine.TryChange(state), $"Transition into {state} was rejected.");
        }

        [Test]
        public void RunCannotStartWithoutGenerating()
        {
            machine.TryChange(GameState.MainMenu);
            machine.TryChange(GameState.Lobby);

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var result = machine.TryChange(GameState.InRun);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(result,
                "Lobby -> InRun must be illegal, otherwise a run can start with no floor.");
        }
    }
}
