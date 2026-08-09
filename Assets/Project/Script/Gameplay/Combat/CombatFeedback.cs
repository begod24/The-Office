using Office.Core;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Turns a confirmed swing into something the player can see and hear.
    /// </summary>
    /// <remarks>
    /// Sits on the player and listens to <see cref="PlayerAttacker.Swung"/>, which fires on
    /// every machine with the server's ruling already in it. Nothing here talks to the network
    /// or decides anything — that separation is what lets the feel of combat be retuned
    /// without touching a line of authority code.
    /// <para>
    /// <b>Three outcomes, three reactions.</b> GDD §9.2 asks a player to work out that
    /// physical weapons do nothing to digital things, and Gate 10 asks them to do it in five
    /// minutes without being told. That is only possible if bouncing off something
    /// (<see cref="WeaponOutcome.Absorbed"/>) neither looks nor sounds like swinging through
    /// air (<see cref="WeaponOutcome.Missed"/>). Everything else in this file exists to serve
    /// that one distinction.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerAttacker attacker;

        [Header("Effects")]
        [Tooltip("Played when the swing hurt something.")]
        [SerializeField] private ImpactEffect connectedEffect;

        [Tooltip("Played when the swing landed and the target shrugged it off entirely. Must " +
                 "read as contact without harm — this is how a player learns the rule.")]
        [SerializeField] private ImpactEffect absorbedEffect;

        [Tooltip("Played when the swing hit nothing. Optional: a whiff is often better sold " +
                 "by the animation alone.")]
        [SerializeField] private ImpactEffect missedEffect;

        [Header("Audio")]
        [SerializeField] private AudioClip connectedClip;
        [SerializeField] private AudioClip absorbedClip;
        [SerializeField] private AudioClip missedClip;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.85f;

        [Header("Prewarm")]
        [Tooltip("Instances made ready before the first swing. A fight should never be the " +
                 "thing that first allocates an effect.")]
        [Min(0)]
        [SerializeField] private int prewarmPerEffect = 4;

        private IImpactEffectPool pool;

        private void OnEnable()
        {
            if (attacker == null)
            {
                Debug.LogError($"[Combat] {name} has no PlayerAttacker assigned. Hit feedback " +
                               "disabled for this player.", this);
                enabled = false;
                return;
            }

            attacker.Swung += OnSwung;

            if (!ServiceLocator.TryGet(out pool)) return;

            pool.Prewarm(connectedEffect, prewarmPerEffect);
            pool.Prewarm(absorbedEffect, prewarmPerEffect);
            pool.Prewarm(missedEffect, prewarmPerEffect);
        }

        private void OnDisable()
        {
            if (attacker != null) attacker.Swung -= OnSwung;

            pool = null;
        }

        private void OnSwung(WeaponOutcome outcome, Vector3 point)
        {
            // Resolved late rather than cached in Awake: on a client the player object can
            // spawn before the boot installers have finished registering services.
            if (pool == null && !ServiceLocator.TryGet(out pool)) return;

            var effect = outcome switch
            {
                WeaponOutcome.Connected => connectedEffect,
                WeaponOutcome.Absorbed => absorbedEffect,
                _ => missedEffect
            };

            var clip = outcome switch
            {
                WeaponOutcome.Connected => connectedClip,
                WeaponOutcome.Absorbed => absorbedClip,
                _ => missedClip
            };

            // Facing back towards the player, so sparks come off the surface rather than
            // through it. The swing direction is the only normal available without asking the
            // server for one, and for a melee arc it is a good enough approximation.
            var normal = point - transform.position;
            normal.y = 0f;

            pool.Play(effect, point, -normal, clip, volume);
        }
    }
}
