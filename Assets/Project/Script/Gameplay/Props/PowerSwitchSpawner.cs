using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
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
