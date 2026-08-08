using System.Collections.Generic;
using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// A carryable item. Adding one is an asset plus a view prefab — no code, no netcode,
    /// no new entry in the network prefab list.
    /// </summary>
    /// <remarks>
    /// What an item <em>is</em> lives in these fields; what it <em>does</em> lives in
    /// <see cref="Modules"/>. The class is unsealed so a genuinely new kind of content can
    /// subclass it, but reach for a module first — behaviour that arrives as a subclass
    /// cannot be combined with another subclass, and GDD §8.3 is full of items that need
    /// exactly that combination.
    /// </remarks>
    [CreateAssetMenu(menuName = "Office/Content/Item", fileName = "ITM_Item")]
    public class ItemDefinition : ContentDefinition
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

        [Header("Behaviour")]
        [Tooltip("What this item can do. A laser pointer is Melee(Light) + LightSource + " +
                 "Durability — three assets in this list and no new code. Order does not " +
                 "matter; a second module of the same type is ignored.")]
        [SerializeField] private ItemModule[] modules = System.Array.Empty<ItemModule>();

        public int MaxStack => maxStack;

        public string PickupVerb => pickupVerb;

        public float GroundOffset => groundOffset;

        public Vector3 HeldOffset => heldOffset;

        public Quaternion HeldRotation => Quaternion.Euler(heldEulerAngles);

        /// <summary>Every capability on this item. Never null.</summary>
        public IReadOnlyList<ItemModule> Modules =>
            modules ?? (IReadOnlyList<ItemModule>)System.Array.Empty<ItemModule>();

        /// <summary>
        /// The first module of type <typeparamref name="T"/>, or null when the item has none.
        /// </summary>
        /// <remarks>
        /// A linear scan over a list that is realistically two or three entries long beats a
        /// per-definition dictionary: it allocates nothing, and the attack path calls this
        /// once per swing, not once per frame.
        /// </remarks>
        public T GetModule<T>() where T : ItemModule
        {
            if (modules == null) return null;

            foreach (var module in modules)
                if (module is T typed)
                    return typed;

            return null;
        }

        public bool HasModule<T>() where T : ItemModule => GetModule<T>() != null;
    }
}
