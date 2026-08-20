using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HeldItemView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;

        [Tooltip("Where the item sits. A child of the body rather than the camera, so it " +
                 "stays put relative to the player instead of floating with their pitch.")]
        [SerializeField] private Transform socket;

        private int shownDefinitionId = ContentDefinition.NoId;
        private GameObject view;

        public ItemDefinition Held { get; private set; }

        private void Awake()
        {
            if (inventory == null || socket == null)
            {
                Debug.LogError($"[Item] {name} has no inventory or socket assigned. " +
                               "Held items will not show.", this);
                enabled = false;
                return;
            }

            inventory.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= Refresh;
        }

        private void Refresh()
        {
            var index = inventory.SelectedIndex;

            var stack = index >= 0 && index < inventory.Capacity
                ? inventory[index]
                : ItemStack.Empty;

            if (stack.DefinitionId == shownDefinitionId) return;

            shownDefinitionId = stack.DefinitionId;

            Clear();

            if (stack.IsEmpty) return;

            Held = ContentViewFactory.Resolve<ItemDefinition>(stack.DefinitionId, this);

            view = ContentViewFactory.Build(Held, socket, Held != null ? Held.HeldOffset : Vector3.zero,
                Held != null ? Held.HeldRotation : Quaternion.identity,
                PhysicsLayers.ViewModel, solid: false);
        }

        private void Clear()
        {
            Held = null;

            if (view == null) return;

            Destroy(view);
            view = null;
        }
    }
}
