using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Server-side owner of every breakable thing in a run, built from the level's
    /// <see cref="TargetPlacement"/> markers.
    /// </summary>
    /// <remarks>
    /// Sits on <c>PF_Session</c> next to <see cref="WorldItemSpawner"/> and shares its whole
    /// lifecycle through <see cref="RunScopedSpawner{TPlacement}"/>. What is specific to
    /// targets is only that a definition has to reach <see cref="Health"/> before the object
    /// spawns, which is the one thing <see cref="TryConfigure"/> does.
    /// </remarks>
    public sealed class TargetSpawner : RunScopedSpawner<TargetPlacement>
    {
        [Tooltip("The single networked carrier for every breakable target. Must be registered " +
                 "in the network prefab list, or clients cannot resolve it.")]
        [SerializeField] private GameObject targetPrefab;

        protected override GameObject Prefab => targetPrefab;

        protected override IReadOnlyList<TargetPlacement> Placements => TargetPlacement.All;

        protected override string LogCategory => "Target";

        protected override bool TryConfigure(NetworkObject instance, TargetPlacement placement)
        {
            if (placement.Definition == null)
            {
                Debug.LogWarning($"[Target] Placement '{placement.name}' has no definition. Skipped.",
                    placement);
                return false;
            }

            var target = instance.GetComponent<DamageableTarget>();

            if (target == null)
            {
                Debug.LogError("[Target] PF_Target is missing its DamageableTarget component.",
                    instance);
                return false;
            }

            target.ServerInitialise(placement.Definition.Id);

            // Health has to hear the definition while the object is still unspawned, or the
            // capacity and resistances would reach clients after the spawn payload.
            return target.ServerConfigureHealth(placement.Definition);
        }
    }
}
