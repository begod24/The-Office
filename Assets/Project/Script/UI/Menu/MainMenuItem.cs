using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Office.UI
{
    public enum MainMenuAction : byte
    {
        Continue = 0,
        NewGame = 1,
        JoinFriends = 2,
        HostLobby = 3,
        Settings = 4,
        Credits = 5,
        Exit = 6
    }

    [RequireComponent(typeof(Button))]
    public sealed class MainMenuItem : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField] private MainMenuAction action;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Graphic cursor;

        public MainMenuAction Action => action;

        public event Action<MainMenuItem> Focused;
        public event Action<MainMenuItem> Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var eventSystem = EventSystem.current;

            if (eventSystem != null && eventSystem.currentSelectedGameObject != gameObject)
                eventSystem.SetSelectedGameObject(gameObject);
            else
                Focused?.Invoke(this);
        }

        public void OnSelect(BaseEventData eventData) => Focused?.Invoke(this);

        public void SetColour(Color colour)
        {
            if (label != null) label.color = colour;
            if (cursor != null) cursor.color = colour;
        }

        public void SetCursorVisible(bool visible)
        {
            if (cursor != null) cursor.enabled = visible;
        }
    }
}
