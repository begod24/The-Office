using System;
using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// Loads and unloads gameplay scenes in response to the replicated phase. Runs on every
    /// client, including the host.
    ///
    /// NGO's own scene synchronisation is switched off
    /// (<c>NetworkConfig.EnableSceneManagement = false</c>) because each client drives its own
    /// scene flow from the phase, and letting NGO also push scenes would load the same geometry
    /// twice. This is the piece that must be revisited in Sprint 6, when the floor generator
    /// needs a seed replicated before any client builds anything.
    ///
    /// The client reports back when its scene is loaded, and the server holds the run in
    /// <see cref="GameState.Generating"/> until everyone has. Without that handshake a fast
    /// machine spawns players into a scene a slow machine has not finished loading.
    /// </summary>
    public sealed class RunSceneFlow : MonoBehaviour
    {
        [SerializeField] private SessionDirector director;
        [SerializeField] private string lobbyScene = SceneNames.Lobby;
        [SerializeField] private string runScene = SceneNames.Sandbox;

        private ISceneLoader loader;
        private bool busy;

        private void Awake()
        {
            // Subscribed before OnNetworkSpawn so the phase that arrives with the spawn message
            // is not missed.
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
                    // InRun is included for the client that joins after the run has started:
                    // it never sees Generating, so without this it would sit in the lobby scene
                    // while its player spawns somewhere it cannot see.
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
                // An async void handler swallows exceptions silently, which in scene flow means
                // a player stuck on a black screen with nothing in the console.
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
