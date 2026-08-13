using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    /// <summary>
    /// One teammate in the squad readout: their seat, their name, and how alive they are.
    /// </summary>
    /// <remarks>
    /// This is also where the <em>local</em> player reads their own health. GDD §14 wants a
    /// minimal HUD with no dedicated health bar, and a co-op game already has to draw everyone
    /// — so a second readout for the player themselves would be the same number twice. Their
    /// row is marked instead.
    /// </remarks>
    public sealed class HudPlayerRow : MonoBehaviour
    {
        private static readonly Color TagLocal = new(0.86f, 0.86f, 0.84f, 1f);
        private static readonly Color TagRemote = new(0.62f, 0.62f, 0.60f, 1f);
        private static readonly Color TagBackLocal = new(0.86f, 0.86f, 0.84f, 0.22f);
        private static readonly Color TagBackRemote = new(1f, 1f, 1f, 0.07f);
        private static readonly Color NameNormal = new(0.86f, 0.86f, 0.84f, 1f);
        private static readonly Color NameDim = new(0.55f, 0.55f, 0.54f, 1f);
        private static readonly Color StatusDanger = new(0.85f, 0.25f, 0.20f, 1f);

        [SerializeField] private TMP_Text tagLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private HudSegmentBar health;

        [Tooltip("Replaces the bar when there is no one to draw one for — OFFLINE, DOWN 42.")]
        [SerializeField] private TMP_Text statusLabel;

        [SerializeField] private Image tagBackground;

        public ulong ClientId { get; private set; }

        public bool IsBound { get; private set; }

        public void Bind(ulong clientId, string tag, string displayName, bool isLocal)
        {
            gameObject.SetActive(true);

            ClientId = clientId;
            IsBound = true;

            Present(tag, displayName, isLocal);
        }

        public void ShowPlaceholder(string tag)
        {
            gameObject.SetActive(true);

            ClientId = 0;
            IsBound = false;

            Present(tag, "---", isLocal: false);
            SetOffline();
        }

        public void Hide()
        {
            IsBound = false;
            gameObject.SetActive(false);
        }

        /// <summary>Draws the bar. The normal case: someone is standing and can be hurt.</summary>
        public void SetHealth(float normalised)
        {
            ShowBar();

            if (health != null) health.SetValue(normalised);
        }

        /// <summary>
        /// Draws the seconds left instead of a bar, because a downed teammate's health is no
        /// longer the useful number — how long there is to reach them is.
        /// </summary>
        public void SetDowned(float bleedOutRemaining)
        {
            if (health != null) health.gameObject.SetActive(false);

            if (statusLabel == null) return;

            statusLabel.gameObject.SetActive(true);
            statusLabel.text = $"DOWN {Mathf.CeilToInt(Mathf.Max(0f, bleedOutRemaining))}";
            statusLabel.color = StatusDanger;

            if (nameLabel != null) nameLabel.color = StatusDanger;
        }

        public void SetDead()
        {
            if (health != null) health.gameObject.SetActive(false);

            if (statusLabel != null)
            {
                statusLabel.gameObject.SetActive(true);
                statusLabel.text = "DEAD";
                statusLabel.color = StatusDanger;
            }

            if (nameLabel != null) nameLabel.color = NameDim;
        }

        /// <summary>
        /// Connected, but with no body to read — between runs, or before this client's player
        /// object has spawned. Distinct from dead: nothing has happened to them yet.
        /// </summary>
        public void SetOffline()
        {
            if (health != null) health.gameObject.SetActive(false);

            if (statusLabel != null)
            {
                statusLabel.gameObject.SetActive(true);
                statusLabel.text = "OFFLINE";
                statusLabel.color = StatusDanger;
            }

            if (nameLabel != null) nameLabel.color = NameDim;
        }

        private void ShowBar()
        {
            if (health != null) health.gameObject.SetActive(true);
            if (statusLabel != null) statusLabel.gameObject.SetActive(false);
            if (nameLabel != null) nameLabel.color = NameNormal;
        }

        private void Present(string tag, string displayName, bool isLocal)
        {
            if (tagLabel != null)
            {
                tagLabel.text = tag;
                tagLabel.color = isLocal ? TagLocal : TagRemote;
            }

            if (tagBackground != null)
                tagBackground.color = isLocal ? TagBackLocal : TagBackRemote;

            if (nameLabel != null)
            {
                nameLabel.text = string.IsNullOrWhiteSpace(displayName) ? "---" : displayName;
                nameLabel.color = NameNormal;
            }

            SetHealth(1f);
        }
    }
}
