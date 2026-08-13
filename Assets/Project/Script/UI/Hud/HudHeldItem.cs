using Office.Data;
using Office.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    /// <summary>
    /// What is in the player's hand, and how much of it is left. GDD §14: held item and
    /// durability, bottom right.
    /// </summary>
    /// <remarks>
    /// The counter is uses remaining out of the item's maximum, not a stack count — the hotbar
    /// already draws the stack. Durability is the number that decides whether to swing at
    /// something or walk past it (GDD §6.2, §8.1), so it is the one worth a permanent readout.
    /// <para>
    /// Numbers come from <see cref="WeaponResolver.TryResolve"/> rather than from the item's
    /// fields, so this panel and the swing it describes cannot disagree. <c>TryResolve</c>
    /// rather than <c>Resolve</c> for the reason the inventory card uses it too: <c>Resolve</c>
    /// answers "what happens when this player attacks", so it reports an empty hand as armed
    /// with the unarmed profile — true for the attack path, and a lie printed under a coffee cup.
    /// </para>
    /// </remarks>
    public sealed class HudHeldItem : MonoBehaviour
    {
        private static readonly Color CounterNormal = new(0.90f, 0.90f, 0.88f, 1f);
        private static readonly Color CounterLow = new(0.85f, 0.25f, 0.20f, 1f);

        private const float LowAt = 0.25f;

        [SerializeField] private GameObject root;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text counterLabel;

        /// <summary>Hides the panel. An empty hand has nothing to say about durability.</summary>
        public void Clear()
        {
            if (root != null) root.SetActive(false);
        }

        public void Show(ItemDefinition definition, in ItemStack stack)
        {
            if (definition == null)
            {
                Clear();
                return;
            }

            if (root != null) root.SetActive(true);

            if (icon != null)
            {
                icon.sprite = definition.Icon;
                icon.enabled = definition.Icon != null;
            }

            if (nameLabel != null) nameLabel.text = definition.DisplayName.ToUpperInvariant();

            if (counterLabel == null) return;

            // An item that never wears out has no counter to draw. Showing a full one would
            // promise a resource the player does not actually have to manage.
            if (!WeaponResolver.TryResolve(definition, out var profile) || !profile.Wears)
            {
                counterLabel.text = string.Empty;
                counterLabel.enabled = false;
                return;
            }

            var remaining = ItemWear.RemainingUses(stack, profile.MaxUses);

            counterLabel.enabled = true;
            counterLabel.text = $"{remaining} / {profile.MaxUses}";
            counterLabel.color = remaining <= profile.MaxUses * LowAt ? CounterLow : CounterNormal;
        }
    }
}
