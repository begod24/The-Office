using System;
using UnityEngine;

namespace Office.Gameplay
{
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

        public event Action<ImpactEffect> Finished;

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

            if (clip != null) source.PlayOneShot(clip);
        }

        public void StopImmediate()
        {
            playing = false;

            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (source != null) source.Stop();
        }

        private void Update()
        {
            if (!playing || Time.time < expiresAt) return;

            playing = false;

            Finished?.Invoke(this);
        }
    }
}
