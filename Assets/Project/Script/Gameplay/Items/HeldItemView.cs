using Office.Data;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Shows the selected item in the player's hand, on every machine that can see them.
    /// </summary>
    /// <remarks>
    /// This component sends nothing and receives nothing. The two facts it needs are already
    /// replicated by <see cref="PlayerInventory"/> — the slots as a server-written
    /// NetworkList, the selected index as an owner-written NetworkVariable — so every peer
    /// can work out what every player is holding from state it already has. Replicating the
    /// held item separately would be a second source of truth for the same fact, and the two
    /// would disagree the first time a pickup and a slot change landed in the same tick.
    ///
    /// It runs identically on the owner and on remote instances, which is the whole reason
    /// the holder and everyone else see the same thing.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HeldItemView : MonoBehaviour
    {
        [SerializeField] private PlayerInventory inventory;

        [Tooltip("Where the item sits. A child of the body rather than the camera, so it " +
                 "stays put relative to the player instead of floating with their pitch.")]
        [SerializeField] private Transform socket;

        private int shownDefinitionId = ContentDefinition.NoId;
        private GameObject view;

        /// <summary>The item currently in hand, or null. Presentation only.</summary>
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

            // Before PlayerInventory.OnNetworkSpawn, which raises Changed once the initial
            // slot data has arrived — that first raise is what builds the view on a client
            // joining a run already in progress.
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

            // The count changes as a stack is topped up; the mesh in the hand does not.
            if (stack.DefinitionId == shownDefinitionId) return;

            shownDefinitionId = stack.DefinitionId;

            Clear();

            if (stack.IsEmpty) return;

            Held = ItemViewFactory.Resolve(stack.DefinitionId, this);

            view = ItemViewFactory.Build(Held, socket, Held != null ? Held.HeldOffset : Vector3.zero,
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
