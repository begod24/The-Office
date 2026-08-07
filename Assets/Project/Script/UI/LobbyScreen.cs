using System.Collections.Generic;
using Office.Core;
using Office.Data;
using Office.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    public sealed class LobbyScreen : MonoBehaviour
    {
        private const int MaxPlayers = 4;

        [Header("Groups")]
        [SerializeField] private GameObject offlineGroup;
        [SerializeField] private GameObject sessionGroup;

        [Header("Offline")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button backButton;

        [Header("Session")]
        [SerializeField] private TMP_Text codeLabel;
        [SerializeField] private Button copyButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private Button leaveButton;

        [Header("Roster")]
        [SerializeField] private Transform rowRoot;
        [SerializeField] private LobbyPlayerRow rowPrefab;

        [Header("Feedback")]
        [SerializeField] private TMP_Text statusLabel;

        private readonly List<LobbyPlayerRow> rows = new(MaxPlayers);

        private ISessionService session;
        private ILobbyService lobby;
        private ISceneLoader sceneLoader;
        private IGameStateService gameState;
        private bool busy;

        private void Start()
        {
            if (!ServiceLocator.TryGet(out session) || !ServiceLocator.TryGet(out lobby))
            {
                Debug.LogError("[Lobby] Session services are not registered. " +
                               "Enter play mode from SCN_Boot, not from this scene.");
                enabled = false;
                return;
            }

            ServiceLocator.TryGet(out sceneLoader);
            ServiceLocator.TryGet(out gameState);

            BuildRows();

            hostButton.onClick.AddListener(OnHostClicked);
            joinButton.onClick.AddListener(OnJoinClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            copyButton.onClick.AddListener(OnCopyClicked);
            readyButton.onClick.AddListener(OnReadyClicked);
            startButton.onClick.AddListener(OnStartClicked);
            leaveButton.onClick.AddListener(OnLeaveClicked);

            session.PhaseChanged += OnSessionPhaseChanged;
            lobby.Changed += Refresh;

            Refresh();
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (session != null) session.PhaseChanged -= OnSessionPhaseChanged;
            if (lobby != null) lobby.Changed -= Refresh;
        }

        private void BuildRows()
        {
            if (rowPrefab == null || rowRoot == null) return;

            for (var i = 0; i < MaxPlayers; i++)
            {
                var row = Instantiate(rowPrefab, rowRoot);
                row.Hide();
                rows.Add(row);
            }
        }

        private void Refresh()
        {
            if (session == null) return;

            var inSession = session.Phase == SessionPhase.InSession;

            offlineGroup.SetActive(!inSession);
            sessionGroup.SetActive(inSession);

            hostButton.interactable = !busy;
            joinButton.interactable = !busy;
            if (backButton != null) backButton.interactable = !busy;

            if (inSession) RefreshSession();

            RefreshStatus();
        }

        private void RefreshSession()
        {
            codeLabel.text = session.JoinCode;

            RefreshRows();

            readyLabel.text = lobby.LocalIsReady ? "CANCEL READY" : "READY";
            readyButton.interactable = lobby.IsAvailable && !busy;

            var canStart = lobby.IsHost && lobby.AllReady && lobby.PlayerCount > 0;

            startButton.gameObject.SetActive(lobby.IsHost);
            startButton.interactable = canStart && !busy;

            startLabel.text = lobby.PlayerCount == 0
                ? "WAITING FOR PLAYERS"
                : lobby.AllReady ? "START THE SHIFT" : "WAITING FOR READY";

            leaveButton.interactable = !busy;
        }

        private void RefreshRows()
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (lobby.TryGetSlot(i, out var slot))
                    rows[i].Bind(slot, slot.ClientId == lobby.LocalClientId);
                else
                    rows[i].Hide();
            }
        }

        private void RefreshStatus()
        {
            if (!string.IsNullOrEmpty(session.LastError) &&
                session.Phase is SessionPhase.Failed or SessionPhase.Offline)
            {
                statusLabel.text = $"<color=#ff6b6b>{session.LastError}</color>";
                return;
            }

            statusLabel.text = session.Phase switch
            {
                SessionPhase.Offline => "Host a shift, or join one with a code.",
                SessionPhase.Initialising => "Signing in...",
                SessionPhase.Creating => "Opening the office...",
                SessionPhase.Joining => "Finding the office...",
                SessionPhase.Leaving => "Clocking out...",
                SessionPhase.InSession => lobby.IsHost
                    ? "Share the code. Everyone must be ready before the shift starts."
                    : "Waiting for the host to start the shift.",
                _ => string.Empty
            };
        }

        private void OnSessionPhaseChanged(SessionPhase phase) => Refresh();

        private async void OnHostClicked()
        {
            busy = true;
            Refresh();

            await session.HostAsync(MaxPlayers, "Office");

            busy = false;
            Refresh();
        }

        private async void OnJoinClicked()
        {
            busy = true;
            Refresh();

            await session.JoinAsync(codeInput.text);

            busy = false;
            Refresh();
        }

        private async void OnLeaveClicked()
        {
            busy = true;
            Refresh();

            await session.LeaveAsync();

            busy = false;
            Refresh();
        }

        private async void OnBackClicked()
        {
            if (sceneLoader == null) return;

            busy = true;
            Refresh();

            try
            {
                gameState?.TryChange(GameState.MainMenu);
                await sceneLoader.SwapAsync(SceneNames.Lobby, SceneNames.MainMenu);
            }
            finally
            {
                // This scene is usually gone by now, but a failed swap would otherwise leave
                // every button in the lobby permanently disabled.
                busy = false;
                if (this != null) Refresh();
            }
        }

        private void OnCopyClicked() => GUIUtility.systemCopyBuffer = session.JoinCode;

        private void OnReadyClicked()
        {
            lobby.SetReady(!lobby.LocalIsReady);
            Refresh();
        }

        private void OnStartClicked() => lobby.RequestStartRun();
    }
}
