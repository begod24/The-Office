using UnityEngine;

namespace Office.Data
{
    /// <summary>
    /// Something in the world that can be hit: a breakable prop, a training target, a piece of
    /// scenery that reacts to a fire extinguisher.
    /// </summary>
    /// <remarks>
    /// Everything that makes one target different from another is here rather than on a
    /// prefab, so a designer authors "a filing cabinet" and "an anomaly that shrugs off
    /// physical weapons" as two assets sharing one registered network prefab. That is the same
    /// arrangement <see cref="ItemDefinition"/> uses, and for the same reason: with
    /// <c>ForceSamePrefabs</c> on, a forgotten registry entry fails only on the remote client.
    /// <para>
    /// This is also where the physical/digital rule of GDD §9.2 first becomes playable. The
    /// rule is a row in <see cref="Responses"/>, not an <c>if</c> anywhere in the combat code,
    /// and the same table shape moves to <c>EnemyDefinition</c> unchanged when enemies arrive.
    /// </para>
    /// </remarks>
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
