using System.Collections.Generic;
using UnityEngine;

namespace Office.Data
{
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

        public IReadOnlyList<ItemModule> Modules =>
            modules ?? (IReadOnlyList<ItemModule>)System.Array.Empty<ItemModule>();

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
