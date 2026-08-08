using System;
using UnityEngine;

namespace Office.Data
{
    /// <summary>How one target reacts to one kind of damage.</summary>
    [Serializable]
    public struct DamageResponse
    {
        [Tooltip("Flags. One row can cover several types at once.")]
        public DamageType Type;

        [Tooltip("0 is immunity, 1 is neutral, 2.5 is a hard weakness.")]
        [Min(0f)]
        public float Multiplier;

        public DamageResponse(DamageType type, float multiplier)
        {
            Type = type;
            Multiplier = multiplier;
        }
    }

    /// <summary>
    /// A target's resistances and weaknesses, authored as data.
    /// </summary>
    /// <remarks>
    /// GDD §8.3 pairs damage types against enemy classes — water against electrical, light
    /// against digital. Expressed as code that would be an <c>if</c> per pair inside the
    /// combat system, and a 4 × 20 matrix of those is unreadable and unbalanceable. Here
    /// "digital entities are immune to physical weapons", the central lesson of GDD §9.2, is
    /// a row in an asset, and a designer changes it without a programmer.
    /// </remarks>
    [Serializable]
    public sealed class DamageResponseTable
    {
        [Tooltip("Empty means damage lands as authored. Digital class: Blunt ×0, Light ×2.5.")]
        [SerializeField] private DamageResponse[] responses = Array.Empty<DamageResponse>();

        public DamageResponseTable()
        {
        }

        public DamageResponseTable(params DamageResponse[] responses)
        {
            this.responses = responses ?? Array.Empty<DamageResponse>();
        }

        /// <summary>Neutral. What an unlisted type is worth, and what an empty table returns.</summary>
        public const float NeutralMultiplier = 1f;

        /// <summary>
        /// The multiplier for a swing carrying <paramref name="type"/>, which may be several
        /// flags at once.
        /// </summary>
        /// <remarks>
        /// The <b>strongest matching row wins</b>, and rows that do not match are not
        /// considered at all. That combination is what makes mixed-type weapons behave the
        /// way GDD §8.3 describes:
        /// <list type="bullet">
        /// <item>A laser pointer is <c>Blunt | Light</c>. Against a digital enemy listing
        /// <c>Blunt ×0, Light ×2.5</c> it deals ×2.5 — the light is what hurts, and the
        /// immunity to being hit with a stick does not cancel it.</item>
        /// <item>A wet mop is <c>Blunt | Water</c>. Against that same enemy, only the Blunt
        /// row matches, so it deals ×0 and physical immunity holds. Water is not a listed
        /// weakness, so it contributes nothing rather than dragging the result back to
        /// neutral.</item>
        /// </list>
        /// Averaging or multiplying the rows instead would make immunity leak: adding any
        /// second damage type to a weapon would let it chip a target it should not touch.
        /// </remarks>
        public float MultiplierFor(DamageType type)
        {
            if (responses == null || responses.Length == 0 || type == DamageType.None)
                return NeutralMultiplier;

            var matched = false;
            var strongest = 0f;

            foreach (var response in responses)
            {
                if ((response.Type & type) == 0) continue;

                if (!matched || response.Multiplier > strongest) strongest = response.Multiplier;
                matched = true;
            }

            return matched ? strongest : NeutralMultiplier;
        }

        /// <summary>Damage after this target's resistances, never negative.</summary>
        public float Resolve(float amount, DamageType type) =>
            amount <= 0f ? 0f : Mathf.Max(0f, amount * MultiplierFor(type));
    }
}
