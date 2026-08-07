using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Scenery the player can look at and sometimes operate, but never carry: doors,
    /// switches, lockers, printers. Behaviour comes from the component on the prefab;
    /// this asset carries only the numbers and strings that a designer should own.
    /// </summary>
    [CreateAssetMenu(menuName = "Office/Content/Prop", fileName = "PRP_Prop")]
    public sealed class PropDefinition : ContentDefinition
    {
        [Header("Interaction")]
        [Tooltip("Off for pure decoration: the crosshair stays quiet and Interact does nothing.")]
        [SerializeField] private bool isInteractable = true;

        [Tooltip("Verb shown under the crosshair, e.g. 'OPEN', 'SWITCH ON'.")]
        [SerializeField] private string interactionVerb = "USE";

        [Tooltip("Seconds the prop ignores further requests after being operated. " +
                 "Stops a held key from firing a door every frame.")]
        [Min(0f)]
        [SerializeField] private float cooldownSeconds = 0.4f;

        [Header("Physics")]
        [Tooltip("On for props that fall over and can be shoved. Off for anything bolted down.")]
        [SerializeField] private bool isPhysical;

        [Min(0.01f)]
        [SerializeField] private float mass = 4f;

        public bool IsInteractable => isInteractable;

        public string InteractionVerb => interactionVerb;

        public float CooldownSeconds => cooldownSeconds;

        public bool IsPhysical => isPhysical;

        public float Mass => mass;
    }
}
