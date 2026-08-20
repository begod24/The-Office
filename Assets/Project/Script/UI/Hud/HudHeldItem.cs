using Office.Data;
using Office.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    public sealed class HudHeldItem : MonoBehaviour
    {
        private static readonly Color CounterNormal = new(0.90f, 0.90f, 0.88f, 1f);
        private static readonly Color CounterLow = new(0.85f, 0.25f, 0.20f, 1f);

        private const float LowAt = 0.25f;

        [SerializeField] private GameObject root;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text counterLabel;

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
