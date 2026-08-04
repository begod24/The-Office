using System;
using System.Collections.Generic;
using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// The single serialisable home for all authoritative run state. Technical Plan §2.7.2.
    ///
    /// Discipline enforced from the first commit: components read from and write to this
    /// structure, they do not own authoritative data. Every new system adds its own block
    /// here as part of being written, never later. This is what turns host migration (M4)
    /// from a rewrite into a feature.
    ///
    /// Serialised with <see cref="JsonUtility"/>, so every field must be a Unity-serialisable
    /// type: public fields only, no properties, no dictionaries, no interfaces, no nulls.
    /// </summary>
    [Serializable]
    public sealed class RunState
    {
        public int FloorSeed;
        public int FloorIndex;
        public float ElapsedSeconds;
        public GameState Phase = GameState.Lobby;

        public List<PlayerState> Players = new();
        public List<EnemyState> Enemies = new();
        public List<InteractableState> Interactables = new();
        public List<PowerZoneState> PowerZones = new();
        public List<ObjectiveState> Objectives = new();

        public string ToJson() => JsonUtility.ToJson(this);

        public static RunState FromJson(string json) => JsonUtility.FromJson<RunState>(json);

        /// <summary>Wipes the structure for reuse without allocating new lists.</summary>
        public void Reset()
        {
            FloorSeed = 0;
            FloorIndex = 0;
            ElapsedSeconds = 0f;
            Phase = GameState.Lobby;
            Players.Clear();
            Enemies.Clear();
            Interactables.Clear();
            PowerZones.Clear();
            Objectives.Clear();
        }
    }

    [Serializable]
    public struct PlayerState
    {
        // NGO client ids are ulong, but Unity's serializer has no unsigned 64-bit support,
        // so it is stored as long and converted at the boundary. Do not change this to ulong.
        public long ClientId;
        public Vector3 Position;
        public float Yaw;
        public float Health;
        public float Stamina;
        public bool IsDowned;
        public bool IsDead;
    }

    [Serializable]
    public struct EnemyState
    {
        public int DefinitionId;
        public int InstanceId;
        public Vector3 Position;
        public float Yaw;
        public float Health;
        public int BehaviourStateId;
    }

    [Serializable]
    public struct InteractableState
    {
        public int InstanceId;
        public int StateId;
        public bool IsLocked;
    }

    [Serializable]
    public struct PowerZoneState
    {
        public int ZoneId;
        public bool IsPowered;
    }

    [Serializable]
    public struct ObjectiveState
    {
        public int ObjectiveId;
        public int Progress;
        public int Required;
        public bool IsComplete;
    }
}
