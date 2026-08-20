using UnityEngine;

namespace Office.Gameplay
{
    public interface IImpactEffectPool
    {
        void Play(ImpactEffect prefab, Vector3 position, Vector3 normal, AudioClip clip,
            float volume = 1f);

        void Prewarm(ImpactEffect prefab, int count);

        void Clear();
    }
}
