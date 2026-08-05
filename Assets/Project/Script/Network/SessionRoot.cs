using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// Keeps the session object alive across scene transitions.
    ///
    /// The session used to be an in-scene placed NetworkObject in SCN_Boot, which does not work
    /// here: with <c>NetworkConfig.EnableSceneManagement = false</c> NGO cannot resolve in-scene
    /// placed objects on a client. It sends them as ordinary spawns, the client looks for a
    /// matching prefab in its registry, finds nothing, and logs
    /// "NetworkPrefab could not be found". The host never sees this because it already has the
    /// object locally.
    ///
    /// So the session is a registered prefab the server spawns, and every instance moves itself
    /// out of the active scene — otherwise unloading the lobby to enter a run would destroy the
    /// very object driving the transition.
    /// </summary>
    public sealed class SessionRoot : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (transform.parent != null)
            {
                Debug.LogError("[Session] The session object must be a scene root for " +
                               "DontDestroyOnLoad to apply.");
                return;
            }

            DontDestroyOnLoad(gameObject);
        }
    }
}
