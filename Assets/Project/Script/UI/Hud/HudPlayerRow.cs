using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
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

        public ulong ClientId { get; private set; }

        public bool IsBound { get; private set; }

        public void Bind(ulong clientId, string tag, bool isLocal)
        {
            gameObject.SetActive(true);

            ClientId = clientId;
            IsBound = true;

            Present(tag, isLocal);
        }

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
