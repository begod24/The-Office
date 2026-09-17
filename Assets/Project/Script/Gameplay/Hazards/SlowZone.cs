using System.Collections.Generic;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SlowZone : MonoBehaviour
    {
        [Tooltip("Metres, measured flat. A player standing inside is slowed.")]
        [Min(0.1f)]
        [SerializeField] private float radius = 1.4f;

        [Tooltip("Metres above and below the zone that still count as standing in it, so a " +
                 "puddle on the floor below does not slow someone on the stairs above it.")]
        [Min(0.1f)]
        [SerializeField] private float height = 1.2f;

        [Tooltip("What the player's speed is multiplied by at full strength. GDD §9.1 gives the " +
                 "water cooler puddles as its counterplay — they have to cost something to cross " +
                 "without being a stun.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float speedMultiplier = 0.55f;

        [Tooltip("Seconds before the zone disappears. Zero means it stays until the scene is " +
                 "unloaded — right for a permanent hazard, wrong for spray that should dry.")]
        [Min(0f)]
        [SerializeField] private float lifetime = 12f;

        [Tooltip("Seconds it spreads and seconds it dries. Both scale the visual and the effect " +
                 "together: what the player sees is exactly what slows them.")]
        [Min(0.01f)]
        [SerializeField] private float spreadSeconds = 0.35f;

        [Min(0.01f)]
        [SerializeField] private float drySeconds = 2f;

        [Tooltip("Scaled by the zone's strength. Optional — a zone with no visual is a trap.")]
        [SerializeField] private Transform visual;

        private static readonly List<SlowZone> Active = new(16);

        private float age;
        private Vector3 visualScale = Vector3.one;

        public float Strength { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        // Read by the owner of each player, never by the server: movement is owner-authoritative
        // (Architecture §4), and a zone exists identically on every machine because the server
        // said where it landed.
        public static float SpeedMultiplierAt(Vector3 position)
        {
            var slowest = 1f;

            for (var i = 0; i < Active.Count; i++)
            {
                var zone = Active[i];
                if (zone == null || zone.Strength <= 0.01f) continue;

                var offset = position - zone.transform.position;
                if (Mathf.Abs(offset.y) > zone.height) continue;

                offset.y = 0f;
                if (offset.sqrMagnitude > zone.radius * zone.radius) continue;

                slowest = Mathf.Min(slowest, Mathf.Lerp(1f, zone.speedMultiplier, zone.Strength));
            }

            return slowest;
        }

        private void Awake()
        {
            if (visual != null) visualScale = visual.localScale;
        }

        private void OnEnable()
        {
            age = 0f;
            Strength = 0f;
            Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            age += Time.deltaTime;

            Strength = Mathf.Clamp01(age / spreadSeconds);

            if (lifetime > 0f)
            {
                var remaining = lifetime - age;

                if (remaining <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }

                Strength = Mathf.Min(Strength, Mathf.Clamp01(remaining / drySeconds));
            }

            if (visual != null) visual.localScale = visualScale * Mathf.Max(0.01f, Strength);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
