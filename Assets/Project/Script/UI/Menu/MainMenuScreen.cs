using Office.Core;
using Office.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Office.UI
{
    public sealed class MainMenuScreen : TerminalMenuScreen
    {
        [Header("Header")]
        [SerializeField] private TMP_Text buildLabel;

        [Header("Credits")]
        [SerializeField] private GameObject menuGroup;
        [SerializeField] private GameObject creditsGroup;
        [SerializeField] private Button creditsBackButton;

        private ISceneLoader sceneLoader;
        private IGameStateService gameState;
        private bool busy;

        private void Start()
        {
            ServiceLocator.TryGet(out sceneLoader);
            ServiceLocator.TryGet(out gameState);

            InitialiseItems();

            if (buildLabel != null) buildLabel.text = $"Build {Application.version}";
            if (creditsGroup != null) creditsGroup.SetActive(false);
            if (creditsBackButton != null) creditsBackButton.onClick.AddListener(CloseCredits);

            Focus(FirstItem);
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        protected override void Update()
        {
            base.Update();

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (creditsGroup != null && creditsGroup.activeSelf) CloseCredits();
        }

        protected override void OnItemClicked(MainMenuItem item)
        {
            if (busy) return;

            switch (item.Action)
            {
                case MainMenuAction.NewGame:
                case MainMenuAction.JoinFriends:
                case MainMenuAction.HostLobby:
                    GoToLobby();
                    break;
                case MainMenuAction.Credits:
                    OpenCredits();
                    break;
                case MainMenuAction.Exit:
                    Quit();
                    break;
                default:
                    ShowHint("// NOT AVAILABLE IN THIS BUILD");
                    break;
            }
        }

        private void OpenCredits()
        {
            if (menuGroup == null || creditsGroup == null) return;

            menuGroup.SetActive(false);
            creditsGroup.SetActive(true);

            var eventSystem = EventSystem.current;
            if (eventSystem != null && creditsBackButton != null)
                eventSystem.SetSelectedGameObject(creditsBackButton.gameObject);
        }

        private void CloseCredits()
        {
            if (menuGroup == null || creditsGroup == null) return;

            creditsGroup.SetActive(false);
            menuGroup.SetActive(true);

            Focus(FirstItem);
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
