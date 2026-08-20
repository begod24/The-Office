using System;
using UnityEngine;

namespace Office.Data
{
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

        public const float NeutralMultiplier = 1f;

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

        public float Resolve(float amount, DamageType type) =>
            amount <= 0f ? 0f : Mathf.Max(0f, amount * MultiplierFor(type));
    }
}
