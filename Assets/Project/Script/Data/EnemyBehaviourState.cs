namespace Office.Data
{
    /// <summary>
    /// What an enemy is doing, as one replicated value.
    /// </summary>
    /// <remarks>
    /// One <c>NetworkVariable</c> rather than a flag per behaviour, for the same reason
    /// <c>VitalsState</c> travels whole: a client must never observe "chasing" and "dead" at
    /// once because two variables arrived in different frames. The server decides, everyone
    /// else reads.
    /// <para>
    /// This is also the input the view animates from. A procedural animator reads this plus the
    /// replicated transform and needs nothing else — which is what keeps enemies off
    /// <c>NetworkAnimator</c> entirely. GDD §9.1 is built on swarms, and a swarm cannot afford
    /// an animator sync per member.
    /// </para>
    /// <para>
    /// <b>Named for the behaviour, not the enemy</b>, because <see cref="EnemyState"/> is
    /// already taken by the save record inside <see cref="RunState"/> — and the two do meet:
    /// <c>EnemyState.BehaviourStateId</c> is where a value of this type is persisted across a
    /// run being saved and restored.
    /// </para>
    /// <para>
    /// Sent as a byte, so the values are fixed. <see cref="Investigating"/> is authored ahead of
    /// the hearing system that will raise it: adding it later would renumber everything after
    /// it, and a build in flight would read a chase as a death.
    /// </para>
    /// </remarks>
    public enum EnemyBehaviourState : byte
    {
        /// <summary>Nothing seen, nothing heard. Standing or wandering.</summary>
        Idle = 0,

        /// <summary>Heard something and is moving to where it came from. Nothing raises this yet.</summary>
        Investigating = 1,

        /// <summary>Has a target in sight and is closing on it.</summary>
        Chasing = 2,

        /// <summary>In reach of its target. Covers the wind-up as well as the hit.</summary>
        Attacking = 3,

        /// <summary>Killed. The body may still be on the floor.</summary>
        Dead = 4
    }
}
