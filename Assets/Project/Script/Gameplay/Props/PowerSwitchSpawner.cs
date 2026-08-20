using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Server-side owner of every power switch in a run, built from the level's
    /// <see cref="PowerSwitchPlacement"/> markers.
    /// </summary>
    /// <remarks>
    /// The fourth subclass of <see cref="RunScopedSpawner{TPlacement}"/>, and it exists for the
    /// same reason as the other three: a marker is level data every machine has, and the thing
    /// a player can actually press has to be a spawned network object. What is specific here is
    /// only that the switch needs to be handed the judge that ends the run — see
    /// <see cref="TryConfigure"/>.
    /// </remarks>
    public sealed class PowerSwitchSpawner : RunScopedSpawner<PowerSwitchPlacement>
    {
        [Tooltip("The networked carrier for every power switch. Must be registered in the " +
                 "network prefab list, or clients cannot resolve it.")]
        [SerializeField] private GameObject switchPrefab;

        [Tooltip("Who decides the run is over. Sits on this same object.")]
        [SerializeField] private RunOutcome outcome;

        protected override GameObject Prefab => switchPrefab;

        protected override IReadOnlyList<PowerSwitchPlacement> Placements => PowerSwitchPlacement.All;

        protected override string LogCategory => "Power";

        protected override bool TryConfigure(NetworkObject instance, PowerSwitchPlacement placement)
        {
            var component = instance.GetComponent<PowerSwitch>();

            if (component == null)
            {
                Debug.LogError("[Power] The switch prefab is missing its PowerSwitch component.",
                    instance);
                return false;
            }

            if (outcome == null)
                Debug.LogWarning("[Power] No RunOutcome assigned. A switch that completes the " +
                                 "run will restore power and nothing else.", this);

            component.ServerInitialise(placement, outcome);
            return true;
        }
    }
}
