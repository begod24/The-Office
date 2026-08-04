using Office.Core;
using Office.Data;
using Office.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Office.UI
{
    /// <summary>
    /// Temporary host/join panel for development. IMGUI on purpose: it needs no canvas, no
    /// prefab wiring and no art, and it is meant to be deleted.
    ///
    /// The real lobby (player list, ready state, start run) is Sprint 1 task A-11 and lives in
    /// SCN_Lobby. Do not grow this file into that — it is scaffolding, and scaffolding that
    /// acquires features never gets removed.
    /// </summary>
    public sealed class DevSessionPanel : MonoBehaviour
    {
        private const int MaxPlayers = 4;

        // The project runs with the new Input System only, so UnityEngine.Input is unavailable.
        [SerializeField] private Key toggleKey = Key.F1;
        [SerializeField] private bool visibleOnStart = true;

        private ISessionService session;
        private string joinCodeInput = string.Empty;
        private bool visible;
        private bool busy;
        private GUIStyle richLabel;

        private void Awake() => visible = visibleOnStart;

        private void Start()
        {
            if (!ServiceLocator.TryGet(out session))
                Debug.LogError("[UI] No ISessionService registered. Is the Boot scene loaded?");
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || session == null) return;

            const float width = 300f;
            const float height = 190f;

            richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };

            GUILayout.BeginArea(new Rect(12f, 12f, width, height), GUI.skin.box);
            GUILayout.Label($"<b>SESSION</b>  ({session.Phase})", richLabel);

            switch (session.Phase)
            {
                case SessionPhase.Offline:
                case SessionPhase.Failed:
                    DrawOfflineControls();
                    break;

                case SessionPhase.InSession:
                    DrawSessionControls();
                    break;

                default:
                    GUILayout.Label("Working...");
                    break;
            }

            if (!string.IsNullOrEmpty(session.LastError))
            {
                GUILayout.Space(4f);
                GUILayout.Label($"<color=#ff6b6b>{session.LastError}</color>", richLabel);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"<size=10>{toggleKey} toggles this panel</size>", richLabel);
            GUILayout.EndArea();
        }

        private void DrawOfflineControls()
        {
            GUI.enabled = !busy;

            if (GUILayout.Button("Host", GUILayout.Height(26f))) Host();

            GUILayout.Space(6f);
            GUILayout.Label("Join code");
            joinCodeInput = GUILayout.TextField(joinCodeInput, 8);

            if (GUILayout.Button("Join", GUILayout.Height(26f))) Join();

            GUI.enabled = true;
        }

        private void DrawSessionControls()
        {
            GUILayout.Label($"<b>Code: {session.JoinCode}</b>", richLabel);
            GUILayout.Label($"Players: {session.PlayerCount} / {session.MaxPlayers}");
            GUILayout.Label(session.IsHost ? "You are the host." : "You are a client.");

            GUILayout.Space(6f);

            if (GUILayout.Button("Copy code")) GUIUtility.systemCopyBuffer = session.JoinCode;
            if (GUILayout.Button("Leave")) Leave();
        }

        private async void Host()
        {
            busy = true;
            await session.HostAsync(MaxPlayers, "Office");
            busy = false;
        }

        private async void Join()
        {
            busy = true;
            await session.JoinAsync(joinCodeInput);
            busy = false;
        }

        private async void Leave()
        {
            busy = true;
            await session.LeaveAsync();
            busy = false;
        }
    }
}
