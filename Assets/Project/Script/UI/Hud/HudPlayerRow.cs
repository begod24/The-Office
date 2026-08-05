using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    /// <summary>
    /// One line of the squad panel: portrait slot, short tag and a health bar.
    ///
    /// Rows are built once by the setup script and shown or hidden, never instantiated at
    /// runtime — the squad is capped at four and a HUD that allocates during a chase is a
    /// frame spike in the worst possible moment.
    ///
    /// The portrait is a flat placeholder box. Character art does not exist yet, and a slot that
    /// is already the right size means the art pass is a sprite assignment rather than a layout
    /// rewrite.
    /// </summary>
    public sealed class HudPlayerRow : MonoBehaviour
    {
        private static readonly Color LocalBackground = new(0.16f, 0.17f, 0.19f, 0.55f);
        private static readonly Color RemoteBackground = new(0.08f, 0.08f, 0.09f, 0.35f);
        private static readonly Color LabelNormal = new(0.86f, 0.86f, 0.84f, 1f);
        private static readonly Color LabelDowned = new(0.78f, 0.29f, 0.22f, 1f);

        [SerializeField] private TMP_Text label;
        [SerializeField] private HudSegmentBar health;
        [SerializeField] private Image portrait;
        [SerializeField] private Image background;

        /// <summary>NGO id of the player on this row. Meaningless while <see cref="IsBound"/> is false.</summary>
        public ulong ClientId { get; private set; }

        /// <summary>False for a placeholder row, so health updates cannot address it by client id.</summary>
        public bool IsBound { get; private set; }

        public void Bind(ulong clientId, string tag, bool isLocal)
        {
            gameObject.SetActive(true);

            ClientId = clientId;
            IsBound = true;

            Present(tag, isLocal);
        }

        /// <summary>
        /// A row with no player behind it. Entering play mode straight from SCN_Sandbox skips the
        /// lobby entirely, and a HUD that is blank in that case cannot be judged while it is
        /// being designed.
        /// </summary>
        public void ShowPlaceholder(string tag)
        {
            gameObject.SetActive(true);

            ClientId = 0;
            IsBound = false;

            Present(tag, isLocal: false);
        }

        public void Hide()
        {
            IsBound = false;
            gameObject.SetActive(false);
        }

        public void SetHealth(float normalized)
        {
            if (health != null) health.SetValue(normalized);
        }

        public void SetDowned(bool downed)
        {
            if (label != null) label.color = downed ? LabelDowned : LabelNormal;
        }

        /// <summary>Portrait art for this player, once characters exist. Null clears it.</summary>
        public void SetPortrait(Sprite sprite)
        {
            if (portrait == null) return;

            portrait.sprite = sprite;
            portrait.enabled = true;
        }

        private void Present(string tag, bool isLocal)
        {
            if (label != null)
            {
                label.text = tag;
                label.color = LabelNormal;
            }

            if (background != null)
                background.color = isLocal ? LocalBackground : RemoteBackground;

            SetHealth(1f);
        }
    }
}
