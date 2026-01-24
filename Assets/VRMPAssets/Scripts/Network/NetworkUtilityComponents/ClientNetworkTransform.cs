using UnityEngine;
using Unity.Netcode.Components;

namespace XRMultiplayer
{
    /// <summary>
    /// ClientNetworkTransform class is responsible for updating the
    /// <see cref="NetworkTransform"/> from the local owner perspective.
    /// Works in both traditional client-server and Distributed Authority modes.
    /// 
    /// In Distributed Authority mode, the key is that OnIsServerAuthoritative() returns false,
    /// which tells Netcode that the OWNER has authority over the transform, not the server.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        /// <summary>
        /// If true, only the Server can update the transform of the object.
        /// For client-authoritative movement (like player avatars), this should be FALSE.
        /// </summary>
        [SerializeField, Tooltip("If false, the owner can update the transform (client-authoritative). If true, only server can update.")] 
        private bool isServerAuthoritative = false;

        private bool _hasLoggedSpawn = false;

        /// <summary>
        /// Returns false to indicate this is NOT server authoritative.
        /// When this returns false, the OWNER of the NetworkObject has authority to update the transform.
        /// This is the key method for client-authoritative movement in both client-server and DA modes.
        /// </summary>
        protected override bool OnIsServerAuthoritative()
        {
            return isServerAuthoritative;
        }

        /// <summary>
        /// Called when spawned. Add debug logging to verify ownership.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            if (IsOwner && !_hasLoggedSpawn)
            {
                _hasLoggedSpawn = true;
                Debug.Log($"ClientNetworkTransform: Spawned on {gameObject.name} - IsOwner=true, IsServerAuthoritative={OnIsServerAuthoritative()}");
            }
        }
    }
}
