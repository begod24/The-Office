using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// A carryable item. Adding one is an asset plus a view prefab — no code, no netcode,
    /// no new entry in the network prefab list.
    /// </summary>
    [CreateAssetMenu(menuName = "Office/Content/Item", fileName = "ITM_Item")]
    public sealed class ItemDefinition : ContentDefinition
    {
        [Header("Carrying")]
        [Tooltip("How many fit in one inventory slot. One means the item never stacks.")]
        [Min(1)]
        [SerializeField] private int maxStack = 1;

        [Tooltip("Verb shown when the player looks at this on the floor.")]
        [SerializeField] private string pickupVerb = "TAKE";

        [Header("World")]
        [Tooltip("Metres above the placement marker the item floats. Greybox props sit at 0.")]
        [SerializeField] private float groundOffset;

        [Header("In hand")]
        [Tooltip("Offset from the player's socket. A cup wants its base at the socket, a " +
                 "stapler wants its middle — that difference belongs to the item, not the rig.")]
        [SerializeField] private Vector3 heldOffset;

        [Tooltip("Rotation in the hand, in degrees.")]
        [SerializeField] private Vector3 heldEulerAngles;

        public int MaxStack => maxStack;

        public string PickupVerb => pickupVerb;

        public float GroundOffset => groundOffset;

        public Vector3 HeldOffset => heldOffset;

        public Quaternion HeldRotation => Quaternion.Euler(heldEulerAngles);
    }
}
