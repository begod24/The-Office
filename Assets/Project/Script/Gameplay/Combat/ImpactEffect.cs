using System;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// One short-lived burst of particles and sound at a point in the world, designed to be
    /// reused rather than instantiated.
    /// </summary>
    /// <remarks>
    /// Purely local: an impact is a consequence of something the server already confirmed, so
    /// every machine plays its own from the same confirmation. Replicating the effect itself
    /// would send a message per hit to say what every client can already work out.
    /// <para>
    /// Lifetime is counted down rather than awaited. An <c>Awaitable</c> would keep running
    /// across a release and a re-acquire and return an instance that is already back in use —
    /// the classic pooling bug, and one that only shows up under load, which is exactly when
    /// combat is happening.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ImpactEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem particles;
        [SerializeField] private AudioSource source;

        [Tooltip("Seconds before this returns to the pool. Must outlast the particle system " +
                 "and the longest clip, or effects are cut off mid-play under fire.")]
        [Min(0.05f)]
        [SerializeField] private float lifetime = 1.25f;

        [Tooltip("Randomised each play so repeated hits do not phase into one flat tone.")]
        [SerializeField] private Vector2 pitchRange = new(0.92f, 1.08f);

        private float expiresAt;
        private bool playing;

        /// <summary>Raised once when this is finished and safe to reuse.</summary>
        public event Action<ImpactEffect> Finished;

        /// <summary>Plays at a point, oriented along the surface it hit.</summary>
        public void Play(Vector3 position, Quaternion rotation, AudioClip clip, float volume)
        {
            transform.SetPositionAndRotation(position, rotation);

            expiresAt = Time.time + lifetime;
            playing = true;

            if (particles != null)
            {
                particles.Clear(true);
                particles.Play(true);
            }

            if (source == null) return;

            source.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
            source.volume = volume;

            // A null clip is authored silence, not a bug: the greybox effects are visual only
            // until B delivers audio, and PlayOneShot(null) warns on every hit.
            if (clip != null) source.PlayOneShot(clip);
        }

        /// <summary>Stops immediately without raising <see cref="Finished"/>. For pool teardown.</summary>
        public void StopImmediate()
        {
            playing = false;

            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (source != null) source.Stop();
        }

        private void Update()
        {
            if (!playing || Time.time < expiresAt) return;

            // Cleared before the callback: the handler releases this to the pool, which may
            // hand it straight back out, and a second expiry would then release it twice.
            playing = false;

            Finished?.Invoke(this);
        }
    }
}
