using Office.Core;
using Office.Data;
using Office.Network;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Office.UI
{
    // Local pause overlay for the run scenes. The game is co-op, so pausing never
    // freezes the simulation — it only frees the cursor and mutes this player's
    // input (via LocalPauseChanged, handled by PlayerRig).
    public sealed class PauseScreen : TerminalMenuScreen
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject menuGroup;
        [SerializeField] private GameObject settingsGroup;
        [SerializeField] private Button settingsBackButton;

        private IEventBus bus;
        private ISessionService session;
        private ISceneLoader sceneLoader;
        private IGameStateService gameState;

        private bool paused;
        private bool busy;

        private void Start()
        {
            ServiceLocator.TryGet(out bus);
            ServiceLocator.TryGet(out session);
            ServiceLocator.TryGet(out sceneLoader);
            ServiceLocator.TryGet(out gameState);

            InitialiseItems();

            if (panelRoot != null) panelRoot.SetActive(false);
            if (settingsGroup != null) settingsGroup.SetActive(false);
            if (settingsBackButton != null) settingsBackButton.onClick.AddListener(CloseSettings);
        }

        private void OnDestroy()
        {
            if (paused) bus?.Publish(new LocalPauseChanged(false));
        }

        protected override void Update()
        {
            base.Update();

            if (busy) return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (paused && settingsGroup != null && settingsGroup.activeSelf) CloseSettings();
            else SetPaused(!paused);
        }

        protected override void OnItemClicked(MainMenuItem item)
        {
            if (busy) return;

            switch (item.Action)
            {
                case MainMenuAction.Resume:
                    SetPaused(false);
                    break;
                case MainMenuAction.Settings:
                    OpenSettings();
                    break;
                case MainMenuAction.MainMenu:
                    LeaveToMainMenu();
                    break;
                default:
                    ShowHint("// NOT AVAILABLE IN THIS BUILD");
                    break;
            }
        }

        private void SetPaused(bool value)
        {
            if (paused == value) return;

            paused = value;
            if (panelRoot != null) panelRoot.SetActive(value);

            if (settingsGroup != null) settingsGroup.SetActive(false);
            if (menuGroup != null) menuGroup.SetActive(true);

            bus?.Publish(new LocalPauseChanged(value));

            if (!value) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Focus(FirstItem);
        }

        private void OpenSettings()
        {
            if (menuGroup == null || settingsGroup == null)
            {
                ShowHint("// NOT AVAILABLE IN THIS BUILD");
                return;
            }

            menuGroup.SetActive(false);
            settingsGroup.SetActive(true);

            var eventSystem = EventSystem.current;
            if (eventSystem != null && settingsBackButton != null)
                eventSystem.SetSelectedGameObject(settingsBackButton.gameObject);
        }

        private void CloseSettings()
        {
            if (menuGroup == null || settingsGroup == null) return;

            settingsGroup.SetActive(false);
            menuGroup.SetActive(true);

            Focus(FirstItem);
        }

        private async void LeaveToMainMenu()
        {
            if (sceneLoader == null)
            {
                ShowHint("// NO SERVICES — ENTER PLAY MODE FROM SCN_BOOT");
                return;
            }

            busy = true;

            var runScene = SceneManager.GetActiveScene().name;

            if (session != null) await session.LeaveAsync();

            gameState?.TryChange(GameState.MainMenu);

            await sceneLoader.LoadAdditiveAsync(SceneNames.MainMenu, setActive: true);
            await sceneLoader.UnloadAsync(runScene);
        }
    }
}
