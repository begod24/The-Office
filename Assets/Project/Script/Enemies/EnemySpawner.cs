using System.Collections.Generic;
using Office.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace Office.Enemies
{
    public sealed class EnemySpawner : RunScopedSpawner<EnemyPlacement>
    {
        [Tooltip("The single networked carrier for every enemy. Must be registered in the " +
                 "network prefab list, or clients cannot resolve it.")]
        [SerializeField] private GameObject enemyPrefab;

        protected override GameObject Prefab => enemyPrefab;

        protected override IReadOnlyList<EnemyPlacement> Placements => EnemyPlacement.All;

        protected override string LogCategory => "Enemy";

        protected override bool TryConfigure(NetworkObject instance, EnemyPlacement placement)
        {
            if (placement.Definition == null)
            {
                Debug.LogWarning($"[Enemy] Placement '{placement.name}' has no definition. " +
                                 "Skipped.", placement);
                return false;
            }

            var enemy = instance.GetComponent<Enemy>();

            if (enemy == null)
            {
                Debug.LogError("[Enemy] PF_Enemy is missing its Enemy component.", instance);
                return false;
            }

            if (!enemy.ServerConfigureHealth(placement.Definition))
            {
                Debug.LogError("[Enemy] PF_Enemy is missing its Health component.", instance);
                return false;
            }

            enemy.ServerInitialise(placement.Definition.Id);
            return true;
        }
    }
}
