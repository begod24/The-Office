using System;
using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Network
{
    public sealed class RunSceneFlow : MonoBehaviour
    {
        [SerializeField] private SessionDirector director;
        [SerializeField] private string lobbyScene = SceneNames.Lobby;
        [SerializeField] private string runScene = SceneNames.Sandbox;

        private ISceneLoader loader;
        private GameState? pending;
        private bool busy;

        private void Awake()
        {
            if (director != null) director.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (director != null) director.PhaseChanged -= OnPhaseChanged;
        }

        // A scene load takes many frames, and the session does not pause for it — the host
        // can end a run while this client is still bringing the run scene up. Dropping the
        // phase that arrives mid-load would strand the client in a scene the session has
        // already left, so the latest one is remembered and applied once the load finishes.
        private async void OnPhaseChanged(GameState phase)
        {
            pending = phase;

            if (busy) return;

            if (loader == null && !ServiceLocator.TryGet(out loader))
            {
                Debug.LogError("[SceneFlow] No ISceneLoader registered.");
                pending = null;
                return;
            }

            busy = true;

            try
            {
                while (pending.HasValue)
                {
                    var next = pending.Value;
                    pending = null;

                    await ApplyAsync(next);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                pending = null;
                busy = false;
            }
        }

        private async Awaitable ApplyAsync(GameState phase)
        {
            switch (phase)
            {
                case GameState.Generating:
                case GameState.InRun:
                    await EnterRunAsync();
                    break;

                case GameState.Lobby:
                    await EnterLobbyAsync();
                    break;
            }
        }

        private async Awaitable EnterRunAsync()
        {
            await loader.LoadAdditiveAsync(runScene, setActive: true);
            await loader.UnloadAsync(lobbyScene);

            if (this == null || director == null) return;

            director.ReportRunSceneReadyRpc();
        }

        private async Awaitable EnterLobbyAsync()
        {
            await loader.LoadAdditiveAsync(lobbyScene, setActive: true);
            await loader.UnloadAsync(runScene);
        }
    }
}
