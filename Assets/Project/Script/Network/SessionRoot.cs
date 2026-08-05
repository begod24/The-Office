using Unity.Netcode;
using UnityEngine;

namespace Office.Network
{
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
