using NUnit.Framework;
using Office.Data;
using UnityEngine;

namespace Office.Tests.EditMode
{
    /// <summary>
    /// Technical Plan §2.7.2. This test is the reason host migration in M4 is a feature rather
    /// than a rewrite: it fails the moment someone adds run state that cannot survive a
    /// snapshot. Run it every milestone and never delete it.
    /// </summary>
    public sealed class RunStateSerializationTests
    {
        private static RunState BuildPopulatedState()
        {
            var state = new RunState
            {
                FloorSeed = 123456,
                FloorIndex = 2,
                ElapsedSeconds = 942.5f,
                Phase = GameState.InRun
            };

            state.Players.Add(new PlayerState
            {
                ClientId = 3,
                Position = new Vector3(4f, 0f, -12.5f),
                Yaw = 91.25f,
                Health = 64f,
                Stamina = 30f,
                IsDowned = true,
                IsDead = false
            });

            state.Enemies.Add(new EnemyState
            {
                DefinitionId = 13,
                InstanceId = 900,
                Position = new Vector3(-3f, 0f, 8f),
                Yaw = 12f,
                Health = 20f,
                BehaviourStateId = 4
            });

            state.Interactables.Add(new InteractableState
            {
                InstanceId = 55, StateId = 1, IsLocked = true
            });

            state.PowerZones.Add(new PowerZoneState { ZoneId = 2, IsPowered = false });

            state.Objectives.Add(new ObjectiveState
            {
                ObjectiveId = 1, Progress = 1, Required = 2, IsComplete = false
            });

            return state;
        }

        [Test]
        public void RoundTrip_PreservesScalars()
        {
            var original = BuildPopulatedState();

            var restored = RunState.FromJson(original.ToJson());

            Assert.AreEqual(original.FloorSeed, restored.FloorSeed);
            Assert.AreEqual(original.FloorIndex, restored.FloorIndex);
            Assert.AreEqual(original.ElapsedSeconds, restored.ElapsedSeconds, 0.001f);
            Assert.AreEqual(original.Phase, restored.Phase);
        }

        [Test]
        public void RoundTrip_PreservesEveryCollection()
        {
            var original = BuildPopulatedState();

            var restored = RunState.FromJson(original.ToJson());

            Assert.AreEqual(1, restored.Players.Count);
            Assert.AreEqual(1, restored.Enemies.Count);
            Assert.AreEqual(1, restored.Interactables.Count);
            Assert.AreEqual(1, restored.PowerZones.Count);
            Assert.AreEqual(1, restored.Objectives.Count);
        }

        [Test]
        public void RoundTrip_PreservesPlayerFields()
        {
            var original = BuildPopulatedState();

            var restored = RunState.FromJson(original.ToJson());
            var player = restored.Players[0];

            Assert.AreEqual(3, player.ClientId);
            Assert.AreEqual(new Vector3(4f, 0f, -12.5f), player.Position);
            Assert.AreEqual(91.25f, player.Yaw, 0.001f);
            Assert.AreEqual(64f, player.Health, 0.001f);
            Assert.IsTrue(player.IsDowned);
            Assert.IsFalse(player.IsDead);
        }

        [Test]
        public void RoundTrip_IsStable()
        {
            var original = BuildPopulatedState();

            var once = original.ToJson();
            var twice = RunState.FromJson(once).ToJson();

            Assert.AreEqual(once, twice,
                "Serialising a restored state produced different JSON, so the snapshot is lossy.");
        }

        [Test]
        public void Reset_EmptiesEveryCollection()
        {
            var state = BuildPopulatedState();

            state.Reset();

            Assert.AreEqual(0, state.Players.Count);
            Assert.AreEqual(0, state.Enemies.Count);
            Assert.AreEqual(0, state.Interactables.Count);
            Assert.AreEqual(0, state.PowerZones.Count);
            Assert.AreEqual(0, state.Objectives.Count);
            Assert.AreEqual(0, state.FloorSeed);
            Assert.AreEqual(GameState.Lobby, state.Phase);
        }
    }
}
