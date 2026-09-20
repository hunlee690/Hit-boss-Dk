using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HitBoss.Multiplayer
{
    public sealed class MatchConnection : MonoBehaviour
    {
        public static MatchConnection Instance { get; private set; }
        public NetworkManager network;
        public NetworkPlayer playerPrefab;
        public string menuScene = "main menu";
        public NetworkModeRules ActiveRules { get; private set; }
        GameObject menuRig;
        bool callbacksBound, returning, quitting;
        public void BindMenu(GameObject rig) { menuRig = rig; returning = false; }

        void Awake()
        {
            Instance = this;
            SceneManager.sceneLoaded += SceneLoaded;
            network.OnClientDisconnectCallback += ClientDisconnected;
        }
        void Update()
        {
            if (network.IsListening && !callbacksBound)
            {
                network.SceneManager.OnLoadEventCompleted += LoadCompleted;
                callbacksBound = true;
            }
            if (!network.IsListening) callbacksBound = false;
            if (RoomManager.Instance != null && RoomManager.Instance.InMatch && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                var unlock = Cursor.lockState != CursorLockMode.None;
                Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = unlock;
            }
        }
        public async Task WaitForPlayersAsync(int count)
        {
            var deadline = Time.realtimeSinceStartup + 25;
            while (!network.IsHost || network.ConnectedClients.Count < count)
            {
                if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("A player could not connect. Please ready up and try again.");
                await Task.Delay(100);
            }
        }
        public void LoadMode(MultiplayerMode mode)
        {
            if (!network.IsHost) throw new InvalidOperationException("Only the host can start a match.");
            if (!Application.CanStreamedLevelBeLoaded(mode.sceneName)) throw new InvalidOperationException("This mode's scene is missing from the build.");
            var status = network.SceneManager.LoadScene(mode.sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started) throw new InvalidOperationException("Scene loading could not start: " + status);
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (RoomManager.Instance?.Room == null || !network.IsListening || scene.name == menuScene) return;
            if (menuRig != null) { menuRig.SetActive(false); Destroy(menuRig); menuRig = null; }
            // These managers run local-only AI/match loops. Online mode rules take their place.
            foreach (var manager in FindObjectsByType<ModeManagerBase>(FindObjectsSortMode.None)) manager.enabled = false;
            var definition = RoomManager.Instance.Mode;
            if (definition != null && definition.rulesPrefab != null) ActiveRules = Instantiate(definition.rulesPrefab);
        }
        void LoadCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (!network.IsHost || sceneName == menuScene || ActiveRules == null) return;
            if (timedOut.Count > 0) { RoomManager.Instance.SetStatus("Some players failed to load the scene."); }
            for (var i = 0; i < completed.Count; i++)
            {
                if (!network.ConnectedClients.TryGetValue(completed[i], out var client) || client.PlayerObject != null) continue;
                var pose = ActiveRules.SpawnPose(i);
                var player = Instantiate(playerPrefab, pose.position, pose.rotation);
                player.NetworkObject.SpawnAsPlayerObject(completed[i], true);
            }
            ActiveRules.BeginServerMatch();
        }
        async void ClientDisconnected(ulong id)
        {
            if (quitting || returning || RoomManager.Instance?.Room == null) return;
            if (!network.IsServer && id == network.LocalClientId)
            { await RoomManager.Instance.LeaveAsync(); RoomManager.Instance.SetStatus("Disconnected from the host."); }
        }
        public void ReturnToMenu()
        {
            returning = true;
            if (ActiveRules != null && network.IsServer) ActiveRules.EndServerMatch();
            if (network.IsListening) network.Shutdown();
            if (callbacksBound && network.SceneManager != null) network.SceneManager.OnLoadEventCompleted -= LoadCompleted;
            callbacksBound = false;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (SceneManager.GetActiveScene().name != menuScene) SceneManager.LoadScene(menuScene);
            else returning = false;
        }
        void OnGUI()
        {
            if (RoomManager.Instance?.InMatch != true) return;
            GUI.Box(new Rect(16, 16, 330, 60), "Online free play • " + RoomManager.Instance.Mode.displayName + "\nEsc: show cursor • combat rules coming next");
            if (Cursor.lockState == CursorLockMode.None && GUI.Button(new Rect(24, 82, 140, 36), "Leave room")) _ = RoomManager.Instance.LeaveAsync();
        }
        void OnApplicationQuit() { quitting = true; }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (network != null) network.OnClientDisconnectCallback -= ClientDisconnected;
            if (Instance == this) Instance = null;
        }
    }
}
