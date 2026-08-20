using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    public sealed class HudPlayerRow : MonoBehaviour
    {
        private static readonly Color Accent = new(0.45f, 0.83f, 0.40f, 1f);
        private static readonly Color TagTextLocal = new(0.05f, 0.07f, 0.05f, 1f);
        private static readonly Color TagTextRemote = new(0.72f, 0.72f, 0.70f, 1f);
        private static readonly Color TagBackRemote = new(1f, 1f, 1f, 0.07f);
        private static readonly Color RowBackLocal = new(0.45f, 0.83f, 0.40f, 0.10f);
        private static readonly Color RowBackRemote = new(0f, 0f, 0f, 0f);
        private static readonly Color NameLocal = new(0.72f, 0.95f, 0.68f, 1f);
        private static readonly Color NameNormal = new(0.86f, 0.86f, 0.84f, 1f);
        private static readonly Color NameDim = new(0.55f, 0.55f, 0.54f, 1f);
        private static readonly Color StatusDanger = new(0.85f, 0.25f, 0.20f, 1f);

        private const string LocalSuffix = "  (YOU)";

        [SerializeField] private TMP_Text tagLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private HudSegmentBar health;

        [Tooltip("Replaces the bar when there is no one to draw one for — OFFLINE, DOWN 42.")]
        [SerializeField] private TMP_Text statusLabel;

        [SerializeField] private Image tagBackground;

        [Tooltip("Stripe down the left of the row. Visible on the local player's row only.")]
        [SerializeField] private Image accentStripe;

        [Tooltip("Tint behind the whole row. Faintly lit for the local player.")]
        [SerializeField] private Image rowBackground;

        private bool isLocal;

        public ulong ClientId { get; private set; }

        public bool IsBound { get; private set; }

        public bool IsLocal => isLocal;

        public void Bind(ulong clientId, string tag, string displayName, bool local)
        {
            gameObject.SetActive(true);

            ClientId = clientId;
            IsBound = true;

            Present(tag, displayName, local);
        }

        public void ShowPlaceholder(string tag)
        {
            gameObject.SetActive(true);

            ClientId = 0;
            IsBound = false;

            Present(tag, "---", local: false);
            SetOffline();
        }

        public void Hide()
        {
            IsBound = false;
            gameObject.SetActive(false);
        }

        public void SetHealth(float normalised)
        {
            ShowBar();

            if (health != null) health.SetValue(normalised);
        }

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
            if (nameLabel != null) nameLabel.color = isLocal ? NameLocal : NameNormal;
        }

        private void Present(string tag, string displayName, bool local)
        {
            isLocal = local;

            if (tagLabel != null)
            {
                tagLabel.text = tag;
                tagLabel.color = local ? TagTextLocal : TagTextRemote;
            }

            if (tagBackground != null)
                tagBackground.color = local ? Accent : TagBackRemote;

            if (accentStripe != null)
            {
                accentStripe.color = Accent;
                accentStripe.enabled = local;
            }

            if (rowBackground != null)
                rowBackground.color = local ? RowBackLocal : RowBackRemote;

            if (nameLabel != null)
            {
                var name = string.IsNullOrWhiteSpace(displayName) ? "---" : displayName;

                nameLabel.text = local ? name + LocalSuffix : name;
                nameLabel.color = local ? NameLocal : NameNormal;
            }

            if (health != null) health.SetHighlighted(local);

            SetHealth(1f);
        }
    }
}
