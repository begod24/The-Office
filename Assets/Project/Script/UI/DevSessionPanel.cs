using Office.Core;
using Office.Data;
using Office.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Office.UI
{
    public sealed class DevSessionPanel : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.F1;
        [SerializeField] private bool visibleOnStart;

        private ISessionService session;
        private ILobbyService lobby;
        private bool visible;
        private GUIStyle richLabel;

        private void Awake() => visible = visibleOnStart;

        private void Start()
        {
            ServiceLocator.TryGet(out session);
            ServiceLocator.TryGet(out lobby);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || session == null) return;

            richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };

            GUILayout.BeginArea(new Rect(12f, 12f, 280f, 190f), GUI.skin.box);

            GUILayout.Label("<b>DEBUG — SESSION</b>", richLabel);
            GUILayout.Label($"connection: {session.Phase}");

            if (lobby != null && lobby.IsAvailable)
            {
                GUILayout.Label($"phase: {lobby.Phase}");
                GUILayout.Label($"players: {lobby.PlayerCount}  ready: {lobby.AllReady}");
                GUILayout.Label($"role: {(lobby.IsHost ? "host" : "client")}  id: {lobby.LocalClientId}");
            }
            else
            {
                GUILayout.Label("phase: offline");
            }

            GUILayout.FlexibleSpace();

            if (lobby != null && lobby.IsHost && lobby.Phase is GameState.InRun or GameState.Generating)
            {
                if (GUILayout.Button("End run")) lobby.RequestEndRun();
            }

            if (session.Phase == SessionPhase.InSession && GUILayout.Button("Leave session"))
                _ = session.LeaveAsync();

            GUILayout.Label($"<size=10>{toggleKey} toggles this panel</size>", richLabel);
            GUILayout.EndArea();
        }
    }
}
