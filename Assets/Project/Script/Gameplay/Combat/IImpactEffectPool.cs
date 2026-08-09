using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Plays short-lived local effects without allocating during play.
    /// </summary>
    /// <remarks>
    /// The counterpart to <c>INetworkObjectPool</c>, and deliberately a separate service: that
    /// one exists because NGO owns the lifetime of networked objects, this one exists because
    /// combat produces dozens of one-frame objects a second and <c>Instantiate</c> during a
    /// fight is what a frame-time spike looks like (Technical Plan §8.2).
    /// </remarks>
    public interface IImpactEffectPool
    {
        /// <summary>
        /// Plays <paramref name="prefab"/> at a point. A null prefab is ignored, so a caller
        /// with nothing authored for one outcome needs no special case.
        /// </summary>
        /// <param name="normal">Surface direction the effect faces along. Zero faces up.</param>
        void Play(ImpactEffect prefab, Vector3 position, Vector3 normal, AudioClip clip,
            float volume = 1f);

        /// <summary>Fills a prefab's pool up front, so the first hit of a run costs nothing.</summary>
        void Prewarm(ImpactEffect prefab, int count);

        /// <summary>Destroys everything parked. Called when the composition root goes away.</summary>
        void Clear();
    }
}
