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
        private bool busy;

        private void Awake()
        {
            if (director != null) director.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (director != null) director.PhaseChanged -= OnPhaseChanged;
        }

        private async void OnPhaseChanged(GameState phase)
        {
            if (busy) return;

            if (loader == null && !ServiceLocator.TryGet(out loader))
            {
                Debug.LogError("[SceneFlow] No ISceneLoader registered.");
                return;
            }

            busy = true;

            try
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
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                busy = false;
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
