using UnityEngine;

namespace Office.Data
{
    [CreateAssetMenu(menuName = "Office/Content/Target", fileName = "TGT_Target")]
    public class TargetDefinition : ContentDefinition
    {
        [Header("Durability")]
        [Min(1f)]
        [SerializeField] private float maxHealth = 60f;

        [Tooltip("Empty means damage lands as authored. A digital target lists Blunt ×0 and " +
                 "Light ×2.5 — that pair is the whole lesson of GDD §9.2.")]
        [SerializeField] private DamageResponseTable responses = new();

        [Header("After it breaks")]
        [Tooltip("Seconds before it comes back whole. Zero leaves it broken until the run " +
                 "ends, which is what a real prop should do; a practice target wants a few " +
                 "seconds so a player can keep testing.")]
        [Min(0f)]
        [SerializeField] private float respawnSeconds;

        public float MaxHealth => maxHealth;

        public DamageResponseTable Responses => responses;

        public float RespawnSeconds => respawnSeconds;

        public bool Respawns => respawnSeconds > 0f;
    }
}
