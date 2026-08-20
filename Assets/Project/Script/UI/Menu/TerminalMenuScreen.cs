using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Office.UI
{
    public abstract class TerminalMenuScreen : MonoBehaviour
    {
        [Header("Items")]
        [SerializeField] private MainMenuItem[] items;

        [Header("Feedback")]
        [SerializeField] private TMP_Text hintLabel;

        [Header("Look")]
        [SerializeField] private Color focusedColour = new(0.95f, 0.95f, 0.93f, 1f);
        [SerializeField] private Color normalColour = new(0.60f, 0.60f, 0.58f, 1f);
        [SerializeField] private float cursorBlinkInterval = 0.5f;
        [SerializeField] private float hintDuration = 2.5f;

        private MainMenuItem current;
        private float blinkTimer;
        private bool cursorVisible;
        private float hintTimer;

        protected MainMenuItem FirstItem =>
            items != null && items.Length > 0 ? items[0] : null;

        protected abstract void OnItemClicked(MainMenuItem item);

        protected void InitialiseItems()
        {
            foreach (var item in items)
            {
                if (item == null) continue;

                item.Focused += OnItemFocused;
                item.Clicked += HandleClick;
                item.SetColour(normalColour);
                item.SetCursorVisible(false);
            }

            if (hintLabel != null) hintLabel.text = string.Empty;
        }

        protected virtual void Update()
        {
            BlinkCursor();
            TickHint();
            KeepSelection();
        }

        protected void Focus(MainMenuItem item)
        {
            if (item == null) return;

            var eventSystem = EventSystem.current;

            if (eventSystem != null && eventSystem.currentSelectedGameObject != item.gameObject)
                eventSystem.SetSelectedGameObject(item.gameObject);

            OnItemFocused(item);
        }

        protected void ShowHint(string message)
        {
            if (hintLabel == null) return;

            hintLabel.text = message;
            hintTimer = hintDuration;
        }

        private void HandleClick(MainMenuItem item) => OnItemClicked(item);

        private void BlinkCursor()
        {
            if (current == null || !current.gameObject.activeInHierarchy) return;

            blinkTimer += Time.unscaledDeltaTime;
            if (blinkTimer < cursorBlinkInterval) return;

            blinkTimer = 0f;
            cursorVisible = !cursorVisible;
            current.SetCursorVisible(cursorVisible);
        }

        private void TickHint()
        {
            if (hintLabel == null || hintTimer <= 0f) return;

            hintTimer -= Time.unscaledDeltaTime;
            if (hintTimer <= 0f) hintLabel.text = string.Empty;
        }

        private void KeepSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || current == null) return;
            if (!current.gameObject.activeInHierarchy) return;
            if (eventSystem.currentSelectedGameObject != null) return;

            eventSystem.SetSelectedGameObject(current.gameObject);
        }

        private void OnItemFocused(MainMenuItem item)
        {
            if (current == item) return;

            if (current != null)
            {
                current.SetCursorVisible(false);
                current.SetColour(normalColour);
            }

            current = item;
            current.SetColour(focusedColour);

            blinkTimer = 0f;
            cursorVisible = true;
            current.SetCursorVisible(true);
        }
    }
}
