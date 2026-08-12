using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Something in the office that hunts: a stapler swarm, a printer, a shredder blocking a
    /// corridor.
    /// </summary>
    /// <remarks>
    /// Everything that makes one enemy different from another is here rather than on a prefab,
    /// so a designer authors "the stapler" and "the shredder" as two assets sharing one
    /// registered network prefab. Same arrangement as <see cref="ItemDefinition"/> and
    /// <see cref="TargetDefinition"/>, and for the same reason: with <c>ForceSamePrefabs</c> on,
    /// a forgotten registry entry fails only on the remote client.
    /// <para>
    /// <b>Not a subclass of <see cref="TargetDefinition"/>.</b> The two share a health value and
    /// a response table and nothing else — a target respawns and never moves, an enemy moves and
    /// never respawns. Inheriting would put <c>RespawnSeconds</c> on every enemy asset as a field
    /// that must stay zero, which is the kind of number nobody remembers to check.
    /// </para>
    /// <para>
    /// The response table is the same shape the targets already use, so GDD §9.2's rule —
    /// digital entities shrug off physical weapons — arrives with the first enemy for free and
    /// is still not an <c>if</c> anywhere in the combat code.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Office/Content/Enemy", fileName = "ENM_Enemy")]
    public class EnemyDefinition : ContentDefinition
    {
        [Header("Durability")]
        [Min(1f)]
        [SerializeField] private float maxHealth = 40f;

        [Tooltip("Empty means damage lands as authored. A Glitch lists Blunt ×0 and Light ×2.5 — " +
                 "that pair is the whole lesson of GDD §9.2.")]
        [SerializeField] private DamageResponseTable responses = new();

        [Header("Body")]
        [Tooltip("Metres. Sizes both the capsule that a swing connects with and the navigation " +
                 "agent, because one carrier prefab serves every enemy and neither can be " +
                 "authored on it. A stapler is not a shredder.")]
        [Min(0.05f)]
        [SerializeField] private float bodyRadius = 0.25f;

        [Min(0.1f)]
        [SerializeField] private float bodyHeight = 0.9f;

        [Header("Movement")]
        [Tooltip("Metres per second while nothing is in sight.")]
        [Min(0f)]
        [SerializeField] private float patrolSpeed = 1.2f;

        [Tooltip("Metres per second while chasing. GDD §9.1 #13 makes the stapler fast and " +
                 "fragile; the player's walk is 3.2 and sprint is 5.6, so anything above 5.6 " +
                 "cannot be outrun and should be a deliberate decision, not a typo.")]
        [Min(0f)]
        [SerializeField] private float chaseSpeed = 3.6f;

        [Min(0f)]
        [SerializeField] private float acceleration = 12f;

        [Tooltip("Degrees per second. High values read as a thing with no neck, which most of " +
                 "GDD §9.1 is.")]
        [Min(0f)]
        [SerializeField] private float turnSpeed = 720f;

        [Header("Sight")]
        [Tooltip("Metres. Sight is blocked by level geometry — see CombatGeometry.IsOccluded.")]
        [Min(0f)]
        [SerializeField] private float sightRadius = 14f;

        [Tooltip("Full cone width in degrees. 360 is an enemy with no blind spot, which should " +
                 "be rare: breaking line of sight is the counter GDD §9.1 hands the player.")]
        [Range(10f, 360f)]
        [SerializeField] private float sightAngle = 110f;

        [Tooltip("Seconds an enemy keeps chasing after losing sight of its target. Zero makes " +
                 "stepping behind a pillar an instant escape, which reads as a bug.")]
        [Min(0f)]
        [SerializeField] private float memorySeconds = 4f;

        [Header("Hearing")]
        [Tooltip("Metres. Paired against the NoiseRadius a weapon already resolves, so GDD §8.1 " +
                 "— fighting is loud and pulls enemies in — becomes playable the moment " +
                 "something publishes a noise. Authored now, consumed by EnemySenses.")]
        [Min(0f)]
        [SerializeField] private float hearingRadius = 18f;

        [Header("Attack")]
        [Min(0f)]
        [SerializeField] private float attackDamage = 10f;

        [Tooltip("Flags, the same as a weapon's. The player's own resistances read it.")]
        [SerializeField] private DamageType attackDamageType = DamageType.Blunt;

        [Tooltip("Metres, measured centre to centre. Should sit a little beyond BodyRadius plus " +
                 "the player's, or the enemy has to overlap the player to reach them.")]
        [Min(0.1f)]
        [SerializeField] private float attackRange = 1.4f;

        [Tooltip("Seconds between the decision to attack and the damage landing. This is the " +
                 "tell: without one the player is hit by something they had no chance to read, " +
                 "which is unfair rather than frightening.")]
        [Min(0f)]
        [SerializeField] private float attackWindup = 0.35f;

        [Min(0.05f)]
        [SerializeField] private float attackCooldown = 1.2f;

        [Header("After it dies")]
        [Tooltip("Seconds the body stays before it is despawned. Zero leaves it until the run " +
                 "ends — right for one shredder, wrong for a swarm that would pile up.")]
        [Min(0f)]
        [SerializeField] private float corpseSeconds = 8f;

        public float MaxHealth => maxHealth;

        public DamageResponseTable Responses => responses;

        public float BodyRadius => bodyRadius;

        public float BodyHeight => bodyHeight;

        public float PatrolSpeed => patrolSpeed;

        public float ChaseSpeed => chaseSpeed;

        public float Acceleration => acceleration;

        public float TurnSpeed => turnSpeed;

        public float SightRadius => sightRadius;

        public float SightAngle => sightAngle;

        public float MemorySeconds => memorySeconds;

        public float HearingRadius => hearingRadius;

        public float AttackDamage => attackDamage;

        public DamageType AttackDamageType => attackDamageType;

        public float AttackRange => attackRange;

        public float AttackWindup => attackWindup;

        public float AttackCooldown => attackCooldown;

        public float CorpseSeconds => corpseSeconds;

        /// <summary>True when the body is cleaned up on a timer rather than at the end of the run.</summary>
        public bool CorpseExpires => corpseSeconds > 0f;
    }
}
