using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Network
{
    /// <summary>
    /// Wrapper for Photon Fusion 2 networking. Handles room creation/joining,
    /// RPCs for alliance chat, territory updates, and war coordination.
    /// Currently runs in stub mode. To activate:
    /// 1. Install Photon Fusion 2 via Package Manager
    /// 2. Set Photon App ID in PhotonServerSettings
    /// 3. Define PHOTON_SDK in Player Settings > Scripting Define Symbols
    /// </summary>
    public class PhotonManager : MonoBehaviour
    {
        [SerializeField] private string photonAppId = "YOUR_PHOTON_APP_ID";
        [SerializeField] private int maxPlayersPerRoom = 50;
        [SerializeField] private string gameVersion = "1.0";

        public bool IsConnected { get; private set; }
        public string CurrentRoomName { get; private set; }
        public int PlayerCount { get; private set; }

        public event Action OnConnectedToServer;
        public event Action<string> OnDisconnected;
        public event Action<string> OnRoomJoined;
        public event Action<string> OnRoomLeft;
        public event Action<string, string> OnChatMessageReceived; // senderId, message
        public event Action<string, byte[]> OnDataReceived; // senderId, data

        private void Awake()
        {
            ServiceLocator.Register<PhotonManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<PhotonManager>();
        }

        /// <summary>Connect to Photon master server.</summary>
        public void Connect()
        {
#if PHOTON_SDK
            // Photon Fusion 2 connection setup would go here
            // var startGameArgs = new StartGameArgs
            // {
            //     GameMode = GameMode.Shared,
            //     SessionName = "lobby",
            //     PlayerCount = maxPlayersPerRoom
            // };
            // runner.StartGame(startGameArgs);
#endif
            Debug.LogWarning("[PhotonManager] Running in stub mode. Define PHOTON_SDK to enable.");
            IsConnected = true;
            OnConnectedToServer?.Invoke();
            EventBus.Publish(new PhotonConnectedEvent());
        }

        /// <summary>Disconnect from Photon.</summary>
        public void Disconnect()
        {
#if PHOTON_SDK
            // runner.Shutdown();
#endif
            IsConnected = false;
            CurrentRoomName = null;
            PlayerCount = 0;
            OnDisconnected?.Invoke("Client requested disconnect");
            EventBus.Publish(new PhotonDisconnectedEvent("Client requested disconnect"));
        }

        /// <summary>Create or join a room by name (e.g., alliance room).</summary>
        public void JoinOrCreateRoom(string roomName)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[PhotonManager] Not connected. Call Connect() first.");
                return;
            }

#if PHOTON_SDK
            // var startGameArgs = new StartGameArgs
            // {
            //     GameMode = GameMode.Shared,
            //     SessionName = roomName,
            //     PlayerCount = maxPlayersPerRoom
            // };
            // runner.StartGame(startGameArgs);
#endif
            CurrentRoomName = roomName;
            PlayerCount = 1;
            OnRoomJoined?.Invoke(roomName);
            EventBus.Publish(new PhotonRoomJoinedEvent(roomName));
            Debug.Log($"[PhotonManager] Stub: Joined room '{roomName}'.");
        }

        /// <summary>Leave the current room.</summary>
        public void LeaveRoom()
        {
            if (string.IsNullOrEmpty(CurrentRoomName)) return;

#if PHOTON_SDK
            // runner.Shutdown();
#endif
            string leftRoom = CurrentRoomName;
            CurrentRoomName = null;
            PlayerCount = 0;
            OnRoomLeft?.Invoke(leftRoom);
            EventBus.Publish(new PhotonRoomLeftEvent(leftRoom));
        }

        /// <summary>Send a chat message to the current room.</summary>
        public void SendChatMessage(string message)
        {
            if (string.IsNullOrEmpty(CurrentRoomName))
            {
                Debug.LogWarning("[PhotonManager] Cannot send chat: not in a room.");
                return;
            }

            if (string.IsNullOrWhiteSpace(message) || message.Length > 500)
            {
                Debug.LogWarning("[PhotonManager] Invalid chat message (empty or >500 chars).");
                return;
            }

#if PHOTON_SDK
            // RPC to all players in room
#endif
            Debug.Log($"[PhotonManager] Stub: SendChat('{message}').");
            // Echo back locally for stub
            OnChatMessageReceived?.Invoke("LOCAL_PLAYER", message);
        }

        /// <summary>Broadcast binary data to all players in the current room (e.g., territory updates).</summary>
        public void BroadcastData(byte[] data)
        {
            if (string.IsNullOrEmpty(CurrentRoomName) || data == null) return;

#if PHOTON_SDK
            // Fusion RPC broadcast
#endif
            Debug.Log($"[PhotonManager] Stub: BroadcastData({data.Length} bytes).");
        }
    }

    // --- Events ---
    public readonly struct PhotonConnectedEvent { }
    public readonly struct PhotonDisconnectedEvent { public readonly string Reason; public PhotonDisconnectedEvent(string r) { Reason = r; } }
    public readonly struct PhotonRoomJoinedEvent { public readonly string RoomName; public PhotonRoomJoinedEvent(string r) { RoomName = r; } }
    public readonly struct PhotonRoomLeftEvent { public readonly string RoomName; public PhotonRoomLeftEvent(string r) { RoomName = r; } }
}
