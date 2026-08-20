using System.Text;
using Office.Data;
using Office.Gameplay;
using TMPro;
using UnityEngine;

namespace Office.UI
{
    public sealed class InventoryDetailPanel : MonoBehaviour
    {
        [Tooltip("Everything except the frame. Hidden while the cursor is on an empty slot.")]
        [SerializeField] private GameObject content;

        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text classLabel;
        [SerializeField] private TMP_Text bodyLabel;

        [Tooltip("Shown instead of the content when there is nothing to describe.")]
        [SerializeField] private TMP_Text emptyLabel;

        private readonly StringBuilder builder = new(160);

        private void Awake() => Clear();

        public void Clear()
        {
            if (content != null) content.SetActive(false);
            if (emptyLabel != null) emptyLabel.enabled = true;
        }

        public void Show(ItemDefinition definition, in ItemStack stack)
        {
            if (definition == null)
            {
                Clear();
                return;
            }

            if (content != null) content.SetActive(true);
            if (emptyLabel != null) emptyLabel.enabled = false;

            if (nameLabel != null) nameLabel.text = definition.DisplayName;
            if (classLabel != null) classLabel.text = ResolveClass(definition);
            if (bodyLabel != null) bodyLabel.text = BuildBody(definition, stack);
        }

        private static string ResolveClass(ItemDefinition definition)
        {
            if (definition.HasModule<RangedModule>()) return "RANGED WEAPON";
            if (definition.HasModule<MeleeModule>()) return "MELEE WEAPON";
            if (definition.HasModule<LightSourceModule>()) return "LIGHT SOURCE";

            return "ITEM";
        }

        private string BuildBody(ItemDefinition definition, in ItemStack stack)
        {
            builder.Clear();

            if (WeaponResolver.TryResolve(definition, out var profile))
            {
                Line($"DAMAGE   {profile.Damage:0}");
                Line($"REACH    {profile.Range:0.0} M");
                Line($"RECOVERY {profile.Cooldown:0.00} S");

                if (profile.StaminaCost > 0f) Line($"STAMINA  {profile.StaminaCost:0}");

                if (profile.Wears)
                {
                    var left = ItemWear.RemainingUses(stack, profile.MaxUses);
                    Line($"USES     {left} / {profile.MaxUses}");
                }
            }

            var optics = definition.GetModule<LightSourceModule>();
            if (optics != null) Line($"BEAM     {optics.Range:0} M");

            if (definition.MaxStack > 1) Line($"STACK    {stack.Count} / {definition.MaxStack}");

            if (builder.Length == 0) builder.Append("NO RECORDED PROPERTIES.");

            return builder.ToString();
        }

        private void Line(string text)
        {
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(text);
        }
    }
}
