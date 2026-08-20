using Office.Core;
using UnityEngine;

namespace Office.Gameplay
{
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

            var normal = point - transform.position;
            normal.y = 0f;

            pool.Play(effect, point, -normal, clip, volume);
        }
    }
}
