using System.Collections.Generic;
using Office.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace Office.Enemies
{
    /// <summary>
    /// Server-side owner of every enemy in a run: fills the run scene from the level's
    /// <see cref="EnemyPlacement"/> markers when the run starts, takes them all back when it
    /// ends.
    /// </summary>
    /// <remarks>
    /// The run-scoped lifecycle — spawn on the InRun edge, despawn at the end, recycle through
    /// the pool — is <see cref="RunScopedSpawner{TPlacement}"/>, the same base the items and
    /// the practice targets already ride. What is left here is the part that is about enemies:
    /// pushing a marker's definition into the carrier before it spawns.
    /// <para>
    /// Health is configured while the object is still unspawned — <c>Health</c> refuses a late
    /// configure rather than letting two clients disagree about how tough something is, which
    /// is why this cannot move into <c>Enemy.OnNetworkSpawn</c>.
    /// </para>
    /// </remarks>
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
