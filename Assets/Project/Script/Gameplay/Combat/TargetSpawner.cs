using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
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

            return target.ServerConfigureHealth(placement.Definition);
        }
    }
}
