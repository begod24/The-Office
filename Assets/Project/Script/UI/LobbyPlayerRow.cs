using Office.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    public sealed class LobbyPlayerRow : MonoBehaviour
    {
        private static readonly Color ReadyColour = new(0.42f, 0.78f, 0.45f);
        private static readonly Color WaitingColour = new(0.55f, 0.55f, 0.53f);
        private static readonly Color LocalBackground = new(0.10f, 0.10f, 0.11f);
        private static readonly Color RemoteBackground = new(0.05f, 0.05f, 0.06f);

        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Image background;

        public void Bind(in PlayerSlot slot, bool isLocal)
        {
            gameObject.SetActive(true);

            if (nameLabel != null)
                nameLabel.text = isLocal ? $"{slot.DisplayName}  (you)" : slot.DisplayName.ToString();

            if (statusLabel != null)
            {
                statusLabel.text = slot.IsReady ? "READY" : "WAITING";
                statusLabel.color = slot.IsReady ? ReadyColour : WaitingColour;
            }

            if (background != null)
                background.color = isLocal ? LocalBackground : RemoteBackground;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
