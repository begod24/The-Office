using Office.Core;
using Office.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Office.UI
{
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [Header("Items")]
        [SerializeField] private MainMenuItem[] items;

        [Header("Header")]
        [SerializeField] private TMP_Text buildLabel;

        [Header("Feedback")]
        [SerializeField] private TMP_Text hintLabel;

        [Header("Look")]
        [SerializeField] private Color focusedColour = new(0.95f, 0.95f, 0.93f, 1f);
        [SerializeField] private Color normalColour = new(0.60f, 0.60f, 0.58f, 1f);
        [SerializeField] private float cursorBlinkInterval = 0.5f;
        [SerializeField] private float hintDuration = 2.5f;

        private ISceneLoader sceneLoader;
        private IGameStateService gameState;

        private MainMenuItem current;
        private float blinkTimer;
        private bool cursorVisible;
        private float hintTimer;
        private bool busy;

        private void Start()
        {
            ServiceLocator.TryGet(out sceneLoader);
            ServiceLocator.TryGet(out gameState);

            foreach (var item in items)
            {
                if (item == null) continue;

                item.Focused += OnItemFocused;
                item.Clicked += OnItemClicked;
                item.SetColour(normalColour);
                item.SetCursorVisible(false);
            }

            if (hintLabel != null) hintLabel.text = string.Empty;
            if (buildLabel != null) buildLabel.text = $"Build {Application.version}";

            if (items.Length > 0 && items[0] != null) Focus(items[0]);
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            BlinkCursor();
            TickHint();
            KeepSelection();
        }

        private void BlinkCursor()
        {
            if (current == null) return;

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

        // Clicking empty space clears the EventSystem selection, which would kill
        // keyboard navigation until the mouse hovers an item again.
        private void KeepSelection()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || current == null) return;
            if (eventSystem.currentSelectedGameObject != null) return;

            eventSystem.SetSelectedGameObject(current.gameObject);
        }

        private void Focus(MainMenuItem item)
        {
            var eventSystem = EventSystem.current;

            if (eventSystem != null && eventSystem.currentSelectedGameObject != item.gameObject)
                eventSystem.SetSelectedGameObject(item.gameObject);

            OnItemFocused(item);
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

        private void OnItemClicked(MainMenuItem item)
        {
            if (busy) return;

            switch (item.Action)
            {
                case MainMenuAction.NewGame:
                case MainMenuAction.JoinFriends:
                case MainMenuAction.HostLobby:
                    GoToLobby();
                    break;
                case MainMenuAction.Exit:
                    Quit();
                    break;
                default:
                    ShowHint("// NOT AVAILABLE IN THIS BUILD");
                    break;
            }
        }

        private async void GoToLobby()
        {
            if (sceneLoader == null)
            {
                ShowHint("// NO SERVICES — ENTER PLAY MODE FROM SCN_BOOT");
                return;
            }

            busy = true;
            gameState?.TryChange(GameState.Lobby);
            await sceneLoader.SwapAsync(SceneNames.MainMenu, SceneNames.Lobby);
        }

        private void ShowHint(string message)
        {
            if (hintLabel == null) return;

            hintLabel.text = message;
            hintTimer = hintDuration;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
